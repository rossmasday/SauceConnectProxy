namespace SauceConnectProxy
{
    using System;

    internal static class EnvironmentConfig
    {
        public static string SauceUsername { get; } = Environment.GetEnvironmentVariable("SAUCE_USERNAME");

        public static string SauceAccessKey { get; } = Environment.GetEnvironmentVariable("SAUCE_ACCESS_KEY");
    }
}