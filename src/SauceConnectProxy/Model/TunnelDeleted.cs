namespace SauceConnectProxy.Model
{
    public sealed class TunnelDeleted
    {
        public bool Result { get; set; }

        public string Id { get; set; }

        public int JobsRunning { get; set; }
    }
}
