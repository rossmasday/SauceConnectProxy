using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SauceConnectProxy.Model
{
    public sealed class TunnelInformation
    {
        [JsonProperty("status")]
        public TunnelStatus Status { get; set; }

        //[JsonProperty("last_connected", ItemConverterType = typeof(UnixDateTimeConverter))]
        //public DateTime LastConnected { get; set; }

        //[JsonProperty("shutdown_time", ItemConverterType = typeof(UnixDateTimeConverter))]
        //public DateTime? ShutdownTime { get; set; }

        //[JsonProperty("launch_time", ItemConverterType = typeof(UnixDateTimeConverter))]
        //public DateTime LaunchTime { get; set; }

        [JsonProperty("user_shutdown")]
        public bool? UserShutdown { get; set; }

        //[JsonProperty("creation_time", ItemConverterType = typeof(UnixDateTimeConverter))]
        //public DateTime CreationTime { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tunnel_identifier")]
        public string TunnelIdentifier { get; set; }

        public Process Process { get; internal set; }
    }
}
