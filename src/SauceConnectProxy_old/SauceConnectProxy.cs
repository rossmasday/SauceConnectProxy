using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace SauceConnectProxy
{
    public sealed class SauceConnectProxy : IDisposable
    {
        private readonly string proxyDirectory;
        private readonly IDictionary<string, string> arguments;
        private Process process;

        public SauceConnectProxy(string user, string accessToken, int port)
            : this(Directory.GetCurrentDirectory(), new Dictionary<string, string>
            {
                { "--user", user },
                { "--api-key", accessToken },
                { "--se-port", port.ToString() }
            })
        {
        }

        public SauceConnectProxy(string proxyDirectory, IDictionary<string, string> launchArguments)
        {
            this.arguments = launchArguments ?? throw new ArgumentNullException(nameof(launchArguments));
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
        }

        public string Output { get; private set; }

        public bool Start()
        {
            var processInfo = new ProcessStartInfo(Path.Combine(proxyDirectory, "sc.exe"))
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

            string line;
            var output = new StringBuilder();
            do
            {
                line = process.StandardOutput.ReadLine().Trim();
                output.AppendLine(line);
            } while (!line.EndsWith("Goodbye.", StringComparison.OrdinalIgnoreCase) && !line.EndsWith("you may start your tests.", StringComparison.OrdinalIgnoreCase));

            Output = output.ToString().Trim();
            if (line.EndsWith("Goodbye.", StringComparison.OrdinalIgnoreCase))
                throw new SauceConnectProxyException(Output);

            return hasStarted;
        }

        private void ReleaseUnmanagedResources()
        {
            if (!process.HasExited)
            {
                process?.Kill();
                process?.WaitForExit();
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~SauceConnectProxy()
        {
            ReleaseUnmanagedResources();
        }
    }
}
