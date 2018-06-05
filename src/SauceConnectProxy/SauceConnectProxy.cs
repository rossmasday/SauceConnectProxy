using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SauceConnectProxy.Model;
using static System.FormattableString;

namespace SauceConnectProxy
{
    public sealed class SauceConnectProxy : IDisposable
    {
        private const string FailedToInitiateConnectionText = "Goodbye.";
        private const string SuccessfullConnectionText = "you may start your tests.";
        private const string ExecutableName = "sc.exe";
        private const string UserArgumentName = "--user";
        private const string AccessKeyArgumentName = "--api-key";
        private const string TunnelIdArguementName = "--tunnel-identifier";
        private readonly string proxyDirectory;
        private readonly SauceConnectRestClient sauceConnectRestClient;
        private readonly IDictionary<string, string> arguments;
        private readonly TimeSpan startTimeout;
        private Process process;
        private TunnelInformation information;
        private readonly string username;

        public SauceConnectProxy(string user, string accessToken, int port)
            : this(user, accessToken, port, default(TimeSpan))
        {
        }

        public SauceConnectProxy(string user, string accessToken, int port, TimeSpan startTimeout)
            : this(Directory.GetCurrentDirectory(), new Dictionary<string, string>
            {
                { UserArgumentName, user },
                { AccessKeyArgumentName, accessToken },
                { "--se-port", port.ToString() }
            }, startTimeout)
        {
        }

        public SauceConnectProxy(string proxyDirectory, IDictionary<string, string> launchArguments, TimeSpan startTimeout)
        {
            this.arguments = launchArguments ?? throw new ArgumentNullException(nameof(launchArguments));
            this.startTimeout = startTimeout == default(TimeSpan) ? TimeSpan.FromMinutes(5) : startTimeout;
            if (string.IsNullOrWhiteSpace(proxyDirectory))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(proxyDirectory));

            var directoryInfo = new DirectoryInfo(proxyDirectory);
            if (!directoryInfo.Exists)
            {
                throw new ArgumentException(Invariant($"The directory '{proxyDirectory}' does not exist."));
            }

            if ((directoryInfo.Attributes & FileAttributes.Directory) == 0)
            {
                throw new ArgumentException(Invariant($"The directory '{proxyDirectory}' provided should be a folder not a file."));
            }

            this.proxyDirectory = proxyDirectory;

            this.username = arguments[UserArgumentName];

            if (!launchArguments.ContainsKey(TunnelIdArguementName))
            {
                arguments[TunnelIdArguementName] = arguments[UserArgumentName];
            }

            this.sauceConnectRestClient = new SauceConnectRestClient(arguments[UserArgumentName], arguments[AccessKeyArgumentName]);
        }

        public string Output { get; private set; }

        public bool Start()
        {
            var hasStarted = StartProcess();
            var tokenSource = new CancellationTokenSource();
            var processOutput = ProcessOutput(tokenSource);

            if(!processOutput.Wait(startTimeout))
                tokenSource.Cancel();

            //Output = processOutput.Result;

            if (tokenSource.IsCancellationRequested)
                throw new OperationCanceledException(Output, tokenSource.Token);
            
            //if (Output != null && Output.EndsWith(FailedToInitiateConnectionText, StringComparison.OrdinalIgnoreCase))
            //    throw new SauceConnectProxyException(Output);

            //this.information = ProcessOutput().Result;
            //this.information = GetTunnelInformation();
            this.information = processOutput.Result;
            this.information.Process = process;
            return hasStarted;
        }

        private Task<TunnelInformation> ProcessOutput(CancellationTokenSource tokenSource)
        {
            var token = tokenSource.Token;
            var processOutput = Task.Factory.StartNew(() =>
            {
                TunnelInformation tunnelInformation;
                do
                {
                    var tunnelIds = sauceConnectRestClient.GetTunnelIdsAsync().Result;
                    var tunnels = tunnelIds.Select(t => sauceConnectRestClient.GetTunnelnformation(t).Result);
                    tunnelInformation = tunnels.FirstOrDefault(t => t.Status == TunnelStatus.Running &&
                                                                    t.TunnelIdentifier != null &&
                                                                    t.TunnelIdentifier == this.username);
                } while (tunnelInformation == null && !token.IsCancellationRequested);
                return tunnelInformation;
               // TunnelInformation tunnelInformation = null;

                //foreach (var tunnelId in tunnelIds)
                //{
                //    tunnelInformation = sauceConnectRestClient.GetTunnelnformation(tunnelId).Result;

                //    if (tunnelInformation.Status == TunnelStatus.Running &&
                //        tunnelInformation.TunnelIdentifier != null &&
                //        tunnelInformation.TunnelIdentifier == this.username || token.IsCancellationRequested)
                //    {
                //        break;
                //    }

                //    Task.Delay(100, token);
                //}
                //return tunnelInformation;
            }, tokenSource.Token);
            //Output = process.StandardOutput.ReadToEnd();
            return processOutput;
        }


        //string line;
        //var output = new StringBuilder();
        //do
        //{
        //    line = process.StandardOutput.ReadLine()?.Trim();
        //    output.AppendLine(line);
        //} while (!token.IsCancellationRequested &&
        //         line != null &&
        //         !line.EndsWith(FailedToInitiateConnectionText, StringComparison.OrdinalIgnoreCase) &&
        //         !line.EndsWith(SuccessfullConnectionText, StringComparison.OrdinalIgnoreCase));

        //return output.ToString().Trim();
        private bool StartProcess()
        {
            var processInfo = new ProcessStartInfo(Path.Combine(proxyDirectory, ExecutableName))
            {
                LoadUserProfile = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = proxyDirectory,
                Arguments = string.Join(" ", arguments.Select(a => $"{a.Key} \"{a.Value}\""))
            };
            this.process = new Process { StartInfo = processInfo };

            bool hasStarted;
            try
            {
                hasStarted = process.Start();
            }
            catch (Exception)
            {
                process = null;
                throw;
            }

            return hasStarted;
        }

        private TunnelInformation GetTunnelInformation()
        {
            var tunnelIds = sauceConnectRestClient.GetTunnelIdsAsync().Result.ToList();
            foreach (var tunnelId in tunnelIds)
            {
                var tunnelInformation = sauceConnectRestClient.GetTunnelnformation(tunnelId).Result;

                if (tunnelInformation.Status == TunnelStatus.Running &&
                    tunnelInformation.TunnelIdentifier != null &&
                    tunnelInformation.TunnelIdentifier == this.username)
                {
                    return tunnelInformation;
                }
            }
            return new TunnelInformation();
        }

        private void ReleaseResources()
        {
            if (this.sauceConnectRestClient.GetTunnelIdsAsync().Result.Any(i => i.Equals(information?.Id)))
            {
                this.sauceConnectRestClient.DeleteTunnel(information?.Id).Wait();
            }

            if (!process.HasExited)
            {
                process?.Kill();
                process?.WaitForExit();
            }
        }

        public void Dispose()
        {
            ReleaseResources();
            GC.SuppressFinalize(this);
        }

        ~SauceConnectProxy()
        {
            ReleaseResources();
        }
    }
}
