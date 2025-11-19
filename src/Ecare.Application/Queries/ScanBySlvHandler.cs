using Dapper;
using Ecare.Application.Dtos;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Ecare.Application.Services;

namespace Ecare.Application.Queries;

public sealed class ScanBySlvHandler : IRequestHandler<ScanBySlvQuery, Result<ScanBySlvVm>>
{
    private readonly IDriverRepository _drivers;
    private readonly IOrderRepository _orders;
    private readonly IKioskDriverRepository _kioskDrivers;
    private readonly IKioskOrderRepository _kioskOrders;
    private readonly IOrderItemRepository _orderItems;
    private readonly IEcareCimentRepository _ciments;
    private readonly IUnitOfWork _uow;
    private readonly ServiceManager _signalR;
    private readonly ILogger<ScanBySlvHandler> _log;

    public ScanBySlvHandler(
        IDriverRepository drivers,
        IOrderRepository orders,
        IKioskDriverRepository kioskDrivers,
        IKioskOrderRepository kioskOrders,
        IOrderItemRepository orderItems,
        IEcareCimentRepository ciments,
        IUnitOfWork uow,
        ServiceManager signalR,
        ILogger<ScanBySlvHandler> log)
    {
        _drivers = drivers;
        _orders = orders;
        _kioskDrivers = kioskDrivers;
        _kioskOrders = kioskOrders;
        _orderItems = orderItems;
        _ciments = ciments;
        _uow = uow;
        _signalR = signalR;
        _log = log;
    }

    public async Task<Result<ScanBySlvVm>> Handle(ScanBySlvQuery request, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);
        try
        {
            var equipement = await _drivers.GetBySlvAsync(SlvId.From(request.Slv), _uow);
            if (equipement is null)
                return Result<ScanBySlvVm>.Fail("Carte SLV inconnue/inactive");

            //var client = await _uow.Connection.QuerySingleOrDefaultAsync<Client>(
            //    $@"SELECT TOP(1) * FROM {DbTableNames.Clients} WHERE RaisonSociale = @clientName",
            //    new { clientName = equipement.ClientName },
            //    _uow.Transaction);

            var order = await _orders.GetBySlvAsync(equipement.CarteSLV, _uow);

            OrderDto? dto = null;
            if (order is not null)
            {
                var orderItemsList = await _orderItems.GetByOrderIdAsync(order.Id, _uow);
                var productIds = orderItemsList.Select(i => i.ProductId).Distinct().ToArray();
                var images = await GetImageUrlsAsync(productIds, _uow, ct);

                var items = new List<OrderItemDto>();
                foreach (var item in orderItemsList)
                {
                    var product = await _ciments.GetByIdAsync(item.ProductId, _uow);
                    images.TryGetValue(item.ProductId, out var imageUrl);
                    items.Add(new OrderItemDto(
                        item.ProductId,
                        product?.Name,
                        item.Quantity,
                        item.Unite,
                        imageUrl
                    ));
                }

                dto = new OrderDto(
                    order.Id,
                    order.Number,
                    order.Destination,
                    order.DeliveryMode,
                    order.TruckPlate,
                    order.Status,
                    items);
            }

            await _uow.CommitAsync(ct);

            var vm = new ScanBySlvVm(
                DriverId: equipement.Id,
                DriverName: equipement.ChauffeurName,    // NEW
                Plate: equipement.Matricule,
                CarteSLV: equipement.CarteSLV,
                ClientName: equipement.ClientName,
                SapOk: null,
                Order: dto
            );

            return Result<ScanBySlvVm>.Ok(vm);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error in ScanBySlvHandler for SLV={slv}", request.Slv);
            try { await _uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }

    private async Task<Dictionary<int, string?>> GetImageUrlsAsync(IEnumerable<int> productIds, IUnitOfWork uow, CancellationToken ct)
    {
        if (!productIds.Any()) return new();
        const string sql = @"SELECT Id, ImageUrl FROM [dbo].[EcareCiments] WHERE Id IN @ids";
        var cmd = new CommandDefinition(sql, new { ids = productIds }, transaction: uow.Transaction, cancellationToken: ct);
        var results = await uow.Connection.QueryAsync<(int Id, string? ImageUrl)>(cmd);
        return results.ToDictionary(x => x.Id, x => x.ImageUrl);
    }
}
