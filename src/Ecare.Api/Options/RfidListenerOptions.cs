namespace Ecare.Api.Options
{
    public sealed class RfidListenerOptions
    {
        public string HubName { get; set; } = "slv_hub";
        public string IncomingMethod { get; set; } = "ReceiveRfid";
        public int ReconnectBaseDelayMs { get; set; } = 2000;
    }
}
