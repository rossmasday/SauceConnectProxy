namespace SauceConnectProxy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Model;
    using static System.FormattableString;
    using UriBuilder = System.UriBuilder;

    public sealed class SauceConnectProxy : IDisposable
    {
        private readonly int port;
        private const string FailedToInitiateConnectionText = "Goodbye.";
        private const string SuccessfullConnectionText = "you may start your tests.";
        private const string ExecutableName = "sc.exe";
        private const string UserArgumentName = "--user";
        private const string AccessKeyArgumentName = "--api-key";
        private const string TunnelIdArgumentName = "--tunnel-identifier";
        private readonly string proxyDirectory;
        private readonly SauceConnectRestClient sauceConnectRestClient;
        private readonly IDictionary<string, string> arguments;
        private readonly TimeSpan timeout;
        private Process process;
        private TunnelInformation information;

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectProxy"/> class with default Sauce labs environment variable setting for username and access key.
        /// </summary>
        /// <param name="port">The port to use.</param>
        public SauceConnectProxy(int port)
            : this(EnvironmentConfig.SauceUsername, EnvironmentConfig.SauceAccessKey, port)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectProxy"/> class
        /// </summary>
        /// <param name="port">The port to use.</param>
        /// <param name="timeout"></param>
        public SauceConnectProxy(int port, TimeSpan timeout)
            : this(EnvironmentConfig.SauceUsername, EnvironmentConfig.SauceAccessKey, port, timeout)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectProxy"/> class
        /// </summary>
        /// <param name="username">The usename to use to connect to the Sauce Labs.</param>
        /// <param name="accessKey">The access key to use to connect to Sauce Labs.</param>
        /// <param name="port">The port to use.</param>
        public SauceConnectProxy(string username, string accessKey, int port)
            : this(username, accessKey, port, default(TimeSpan))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectProxy"/> class
        /// </summary>
        /// <param name="username">The usename to use to connect to the Sauce Labs.</param>
        /// <param name="accessKey">The access key to use to connect to Sauce Labs.</param>
        /// <param name="port">The port to use.</param>
        /// <param name="timeout">Timeout for start up, if none is specified it will be 5 minutes.</param>
        public SauceConnectProxy(string username, string accessKey, int port, TimeSpan timeout)
            : this(Directory.GetCurrentDirectory(), new Dictionary<string, string>
            {
                { UserArgumentName, username },
                { AccessKeyArgumentName, accessKey },
                { "--se-port", port.ToString() }
            }, timeout)
        {
            this.port = port;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectProxy"/> class
        /// </summary>
        /// <param name="proxyDirectory">The directory containing the proxy exe '<c>sc.exe</c>'</param>
        /// <param name="launchArguments"></param>
        /// <param name="timeout">Timeout for start up, if none is specified it will be 5 minutes.</param>
        public SauceConnectProxy(string proxyDirectory, IDictionary<string, string> launchArguments, TimeSpan timeout)
        {
            this.arguments = launchArguments ?? throw new ArgumentNullException(nameof(launchArguments));
            this.timeout = timeout == default(TimeSpan) ? TimeSpan.FromMinutes(5) : timeout;

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

            this.Username = arguments[UserArgumentName];
            this.AccessKey = arguments[AccessKeyArgumentName];

            if (!launchArguments.ContainsKey(TunnelIdArgumentName))
            {
                arguments[TunnelIdArgumentName] = arguments[UserArgumentName];
            }

            this.sauceConnectRestClient = new SauceConnectRestClient(arguments[UserArgumentName], arguments[AccessKeyArgumentName]);
        }

        /// <summary>
        /// Gets the access key used.
        /// </summary>
        public string AccessKey { get; }

        /// <summary>
        /// Gets the username used.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Reads the process output.
        /// </summary>
        public string Output => ReadOutput();

        /// <summary>
        /// Gets teh proxy address.
        /// </summary>
        public Uri ProxyAddress { get; private set; }

        /// <summary>
        /// Starts the process and waits for the tunnel to establish.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StartAsync()
        {
            var hasStarted = StartProcess();
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(timeout);

            this.information = await ProcessOutput(tokenSource);
            this.information.Process = process;
            this.ProxyAddress = new UriBuilder("http", "localHost", port, "wd/hub").Uri;
            return hasStarted;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Task.WhenAll(ReleaseResources());
            GC.SuppressFinalize(this);
        }

        private async Task<TunnelInformation> ProcessOutput(CancellationTokenSource tokenSource)
        {
            var token = tokenSource.Token;

            TunnelInformation tunnelInformation;
            do
            {
                var tunnelIds = await sauceConnectRestClient.GetTunnelIdsAsync(token).ConfigureAwait(false);
                var tunnels = await Task.WhenAll(tunnelIds.Select(async t => await sauceConnectRestClient.GetTunnelnformationAsync(t, token).ConfigureAwait(false))).ConfigureAwait(false);
                tunnelInformation = tunnels.FirstOrDefault(t => t.Status == TunnelStatus.Running &&
                                                                t.TunnelIdentifier != null &&
                                                                t.TunnelIdentifier == this.Username);
            } while (tunnelInformation == null && !token.IsCancellationRequested);

            return tunnelInformation;
        }

        private string ReadOutput()
        {
            var output = new StringBuilder();
            var sw = Stopwatch.StartNew();
            string newLine;
            do
            {
                newLine = process.StandardOutput.ReadLine()?.Trim();
                output.AppendLine(newLine);

            } while (sw.ElapsedMilliseconds < 500 &&
                     !string.IsNullOrWhiteSpace(newLine) &&
                     !newLine.EndsWith(FailedToInitiateConnectionText, StringComparison.OrdinalIgnoreCase) &&
                     !newLine.EndsWith(SuccessfullConnectionText, StringComparison.OrdinalIgnoreCase));
            sw.Stop();
            return output.ToString().Trim();
        }

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

        private async Task ReleaseResources()
        {
            if ((await this.sauceConnectRestClient.GetTunnelIdsAsync(default(CancellationToken)).ConfigureAwait(false)).Any(i => i.Equals(information?.Id)))
            {
                await this.sauceConnectRestClient.DeleteTunnelAsync(information?.Id, default(CancellationToken)).ConfigureAwait(false);
            }

            if (!process.HasExited)
            {
                process?.Kill();
                process?.WaitForExit();
            }
        }

        ~SauceConnectProxy()
        {
            Task.WhenAll(ReleaseResources());
        }
    }
}