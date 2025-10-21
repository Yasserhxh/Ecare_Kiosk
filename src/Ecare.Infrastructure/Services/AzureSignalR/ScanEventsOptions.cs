namespace Ecare.Api.Options
{
    public sealed class ScanEventsOptions
    {
        public string HubName { get; set; } = "slv_scan_hub";
        public string MethodName { get; set; } = "ReceiveScan";
        public string Site { get; set; } = "Asment-Temara-01";
        public string Kiosk { get; set; } = "Parking";
    }

}
