namespace Ecare.Application.Commands.Legend;

/// <summary>
/// Pure static rules for the offline second-weight path.
/// No I/O — trivially unit-testable.
/// </summary>
public static class OfflineSecondWeightRules
{
    /// <summary>
    /// Returns "sync" when the legend is already in offline mode (uses Task-2 sync endpoint),
    /// "shipment" for the normal SAP shipmentNotification path.
    /// </summary>
    public static string ResolveSapTarget(bool isOffline) =>
        isOffline ? "sync" : "shipment";

    /// <summary>
    /// Maps any SAP failure to the offline provisional state.
    /// Always returns isOffline=true.
    /// Connectivity errors (HttpRequestException, TaskCanceledException, socket, 5xx/timeout)
    /// → status "EnAttente".
    /// Business/non-2xx rejections → status "Erreur" with the original message.
    /// </summary>
    public static (bool isOffline, string status, string error) OnSapFailure(
        bool wasOffline,
        bool isConnectivity,
        string message) =>
        (true, isConnectivity ? "EnAttente" : "Erreur", message);
}
