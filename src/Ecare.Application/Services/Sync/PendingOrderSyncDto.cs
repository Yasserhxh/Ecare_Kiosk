namespace Ecare.Application.Services.Sync;

public sealed class PendingOrderSyncDto
{
    public long Id { get; set; }
    public string? CodeSapClient { get; set; }
    public string? CodeSapCommande { get; set; }
    public DateTime? CreatedAt { get; set; }
}