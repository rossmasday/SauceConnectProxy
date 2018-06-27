namespace SauceConnectProxy.Model
{
    using Newtonsoft.Json;

    public class Metadata
    {
        public string Hostname { get; set; }

        [JsonProperty("git_version")]
        public string GitVersion { get; set; }

        public string Platform { get; set; }

        public string Command { get; set; }

        public string Build { get; set; }

        public string Release { get; set; }

        [JsonProperty("nofile_limit")]
        public int? NofileLimit { get; set; }
    }
}