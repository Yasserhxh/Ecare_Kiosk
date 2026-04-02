namespace Ecare.Domain.Inerface.Sync;
public sealed class PendingOrderSyncModel
{
    public int Id { get; set; }
    public string? CodeSapClient { get; set; }
    public string? CodeSapCommande { get; set; }
    public DateTime? CreatedAt { get; set; }
}