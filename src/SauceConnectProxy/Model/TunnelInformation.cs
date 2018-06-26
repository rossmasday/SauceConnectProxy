namespace SauceConnectProxy.Model
{
    using System.Diagnostics;
    using Newtonsoft.Json;

    public sealed class TunnelInformation
    {
        [JsonProperty("status")]
        public TunnelStatus Status { get; set; }

        [JsonProperty("user_shutdown")]
        public bool? UserShutdown { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tunnel_identifier")]
        public string TunnelIdentifier { get; set; }

        public Process Process { get; internal set; }
    }
}