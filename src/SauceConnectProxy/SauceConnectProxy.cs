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
        private int port;
        private const string FailedToInitiateConnectionText = "Goodbye.";
        private const string SuccessfullConnectionText = "you may start your tests.";
        private const string ExecutableName = "sc.exe";
        private const string UserFlagName = "--user";
        private const string AccessKeyFlagName = "--api-key";
        private const string TunnelIdFlagName = "--tunnel-identifier";
        private const string PortFlagName = "--se-port";
        private readonly string proxyDirectory;
        private readonly SauceConnectRestClient sauceConnectRestClient;
        private readonly IDictionary<string, string> arguments;
        private readonly TimeSpan timeout;
        private Process process;
        private TunnelInformation information;
        private bool previouslyRunning;

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
        /// <param name="username">The usename to use to connect to the Sauce Labs.</param>
        /// <param name="accessKey">The access key to use to connect to Sauce Labs.</param>
        /// <param name="port">The port to use.</param>
        public SauceConnectProxy(string username, string accessKey, int port)
            : this(Directory.GetCurrentDirectory(), new Dictionary<string, string>
            {
                { UserFlagName, username },
                { AccessKeyFlagName, accessKey },
                { PortFlagName, port.ToString() }
            })
        {
            this.port = port;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectProxy"/> class
        /// </summary>
        /// <param name="proxyDirectory">The directory containing the proxy exe '<c>sc.exe</c>'</param>
        /// <param name="launchArguments"></param>
        public SauceConnectProxy(string proxyDirectory, IDictionary<string, string> launchArguments)
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

            this.Username = arguments[UserFlagName];
            this.AccessKey = arguments[AccessKeyFlagName];

            if (!launchArguments.ContainsKey(TunnelIdFlagName))
            {
                arguments[TunnelIdFlagName] = this.Username;
            }

            this.sauceConnectRestClient = new SauceConnectRestClient(arguments[UserFlagName], arguments[AccessKeyFlagName]);
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
        /// Gets the process output.
        /// </summary>
        public string Output { get; private set; }

        /// <summary>
        /// Gets teh proxy address.
        /// </summary>
        public Uri ProxyAddress { get; private set; }

        /// <summary>
        /// Starts the process and waits for the tunnel to establish.
        /// </summary>
        /// <returns></returns>
        public Task<bool> StartAsync()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(timeout);
            return StartAsync(tokenSource.Token);
        }


        /// <summary>
        /// Starts the process and waits for the tunnel to establish.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<bool> StartAsync(CancellationToken cancellationToken)
        {
            var info = await GetTunnelInformation(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var hasStarted = info != null && info.Metadata.Hostname == Environment.MachineName
                ? await Task.Run(() => QueryExistingProxyAndSetValues(info), cancellationToken)
                : await StartUpNewProxy(cancellationToken);

            this.ProxyAddress = new UriBuilder("http", "localHost", port, "wd/hub").Uri;
            return hasStarted;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ReleaseResources().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }


        private async Task<bool> StartUpNewProxy(CancellationToken token)
        {
            var hasStarted = StartProcess();

            this.Output = ReadOutput(token);
            token.ThrowIfCancellationRequested();
            if (!Output.EndsWith(SuccessfullConnectionText))
            {
                throw new SauceConnectProxyException(Output);
            }

            this.information = await WaitForTunnelInformation(token);
            this.information.Process = process;
            return hasStarted;
        }

        private bool QueryExistingProxyAndSetValues(TunnelInformation info)
        {
            this.information = info;
            var start = this.information.Metadata.Command.IndexOf('-');
            var parameters = this.information.Metadata.Command.Trim().Substring(start).Split(' ');

            if (parameters.Length % 2 != 0)
            {
                throw new SauceConnectProxyException($"Could not parse metadata parameters from tunnel information, because there is not an even number or arguments: '{info.Metadata.Command}'");
            }

            IDictionary<string, string> commandLineParameters = new Dictionary<string, string>();
            for (var i = 0; i < parameters.Length; i += 2)
            {
                commandLineParameters.Add(parameters[i], parameters[i + 1]);
            }

            this.port = int.Parse(SearchForParameter(commandLineParameters, info, PortFlagName, "-P"));
            this.previouslyRunning = true;
            return true;
        }

        private static string SearchForParameter(IDictionary<string, string> commandLineParameters, TunnelInformation info, string fullFlag, string shortFlag, bool throwIfMissing = true)
        {
            if (!commandLineParameters.TryGetValue(fullFlag, out var parameter) && !commandLineParameters.TryGetValue(shortFlag, out parameter) && throwIfMissing)
            {
                throw new SauceConnectProxyException($"Proxy seems to be open but could not locate either flag [{fullFlag}, {shortFlag}] in the launch command '{info.Metadata?.Command}");
            }
            return parameter;
        }

        private async Task<TunnelInformation> WaitForTunnelInformation(CancellationToken token)
        {
            TunnelInformation tunnelInformation;
            do
            {
                tunnelInformation = await GetTunnelInformation(token);
            } while ((tunnelInformation == null || tunnelInformation.Status != TunnelStatus.Running) && !token.IsCancellationRequested);
            token.ThrowIfCancellationRequested();
            return tunnelInformation;
        }

        private async Task<TunnelInformation> GetTunnelInformation(CancellationToken token)
        {
            var tunnelIds = await sauceConnectRestClient.GetTunnelIdsAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var tunnels = await Task.WhenAll(tunnelIds.Select(async id => await sauceConnectRestClient.GetTunnelnformationAsync(id, token).ConfigureAwait(false))).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return tunnels.FirstOrDefault(t => t.Metadata.Hostname == Environment.MachineName);
        }

        private string ReadOutput(CancellationToken token)
        {
            var output = new StringBuilder();
            var sw = Stopwatch.StartNew();
            string newLine;
            do
            {
                token.ThrowIfCancellationRequested();
                newLine = process.StandardOutput.ReadLine()?.Trim();
                output.AppendLine(newLine);
            } while (sw.Elapsed < timeout &&
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
            if (previouslyRunning)
            {
                return;
            }

            information = await GetTunnelInformation(default(CancellationToken));
            if (information != null)
            {
                await this.sauceConnectRestClient.DeleteTunnelAsync(information?.Id, default(CancellationToken)).ConfigureAwait(false);
            }

            if ((!process?.HasExited).GetValueOrDefault())
            {
                process?.Kill();
                process?.WaitForExit();
            }
        }

        ~SauceConnectProxy()
        {
            ReleaseResources().GetAwaiter().GetResult();
        }
    }
}