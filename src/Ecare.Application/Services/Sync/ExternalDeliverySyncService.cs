using Ecare.Domain.Inerface.Sync;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Services.Sync;

public sealed class ExternalDeliverySyncService : IExternalDeliverySyncService
{
    private const string LockName = "Ecare_Order_Legend_ExternalSync";
    private const int BatchSize = 200;

    private readonly IOrderLegendSyncRepository _repository;
    private readonly IClientLivraisonApi _clientLivraisonApi;
    private readonly ISyncExecutionLockProvider _lockProvider;
    private readonly ILogger<ExternalDeliverySyncService> _logger;

    public ExternalDeliverySyncService(
        IOrderLegendSyncRepository repository,
        IClientLivraisonApi clientLivraisonApi,
        ISyncExecutionLockProvider lockProvider,
        ILogger<ExternalDeliverySyncService> logger)
    {
        _repository = repository;
        _clientLivraisonApi = clientLivraisonApi;
        _lockProvider = lockProvider;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("External delivery sync started at {Now}", DateTime.Now);

        await using var syncLock = await _lockProvider.TryAcquireAsync(LockName, cancellationToken);

        if (!syncLock.Acquired)
        {
            _logger.LogInformation("Sync skipped because another instance is already running.");
            return;
        }

        var pendingOrders = await _repository.GetPendingOrdersAsync(BatchSize, cancellationToken);

        _logger.LogInformation("Found {Count} pending rows to process.", pendingOrders.Count);

        if (pendingOrders.Count == 0)
        {
            _logger.LogInformation("External delivery sync finished at {Now}", DateTime.Now);
            return;
        }

        var grouped = pendingOrders
            .Where(x => !string.IsNullOrWhiteSpace(x.CodeSapClient))
            .GroupBy(x => x.CodeSapClient!.Trim());

        foreach (var clientGroup in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clientCode = clientGroup.Key;
            var orders = clientGroup.ToList();

            _logger.LogInformation("Processing client {ClientCode} with {Count} orders.", clientCode, orders.Count);

            var startDate = orders
                .Where(x => x.CreatedAt.HasValue)
                .Select(x => x.CreatedAt!.Value.Date)
                .DefaultIfEmpty(DateTime.Now.Date.AddMonths(-3))
                .Min();

            var endDate = DateTime.Now.Date.AddDays(1);

            IReadOnlyList<ExternalLivraisonModel> apiResult;

            try
            {
                apiResult = await _clientLivraisonApi.GetLivraisonsAsync(
                    clientCode,
                    startDate,
                    endDate,
                    cancellationToken);

                _logger.LogInformation(
                    "API returned {Count} livraisons for client {ClientCode}.",
                    apiResult.Count,
                    clientCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API call failed for client {ClientCode}", clientCode);
                continue;
            }

            if (apiResult.Count == 0)
                continue;

            var byCommande = apiResult
                .Where(x => !string.IsNullOrWhiteSpace(x.CodeCommandeSap))
                .GroupBy(x => x.CodeCommandeSap!)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var order in orders)
            {
                if (string.IsNullOrWhiteSpace(order.CodeSapCommande))
                    continue;

                var commande = order.CodeSapCommande.Trim();

                if (!byCommande.TryGetValue(commande, out var livraison))
                    continue;

                var updated = await _repository.MarkAsSyncedAsync(
                    order.Id,
                    livraison.NumeroLivraison!,
                    cancellationToken);

                if (updated)
                {
                    _logger.LogInformation(
                        "Order {Id} synced. Commande={Commande}, Livraison={Livraison}",
                        order.Id,
                        commande,
                        livraison.NumeroLivraison);
                }
            }
        }

        _logger.LogInformation("External delivery sync finished at {Now}", DateTime.Now);
    }
}
