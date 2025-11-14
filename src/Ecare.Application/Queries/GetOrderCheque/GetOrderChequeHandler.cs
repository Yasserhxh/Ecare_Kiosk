using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetOrderCheque
{
    public sealed class GetOrderChequesPagedHandler(
        IUnitOfWork uow,
        IBlobStorageService blobService,
        ILogger<GetOrderChequesPagedHandler> log)
        : IRequestHandler<GetOrderChequesPagedQuery, Result<OrderChequesPageVm>>
    {
        public async Task<Result<OrderChequesPageVm>> Handle(
            GetOrderChequesPagedQuery request,
            CancellationToken ct)
        {
            try
            {
                var page = request.Page <= 0 ? 1 : request.Page;
                var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
                var offset = (page - 1) * pageSize;

                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in GetOrderChequesPagedHandler.");

                const string sql = """
                -- Data page
                SELECT
                    O.Id          AS OrderId,
                    O.ChequeImage AS ChequeImage,
                    E.ClientName,
                    E.ParkedAt,
                    E.DriverName,
                    E.CarteSlv
                FROM Orders O
                JOIN EcareFlux E ON O.Id = E.OrderId
                ORDER BY E.ParkedAt DESC, O.Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                -- Total count
                SELECT COUNT(*)
                FROM Orders O
                JOIN EcareFlux E ON O.Id = E.OrderId;
                """;

                using var multi = await conn.QueryMultipleAsync(
                    new CommandDefinition(
                        sql,
                        new { Offset = offset, PageSize = pageSize },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                var rows = (await multi.ReadAsync<Row>()).ToList();
                var totalCount = await multi.ReadSingleAsync<int>();

                // Resolve cheque image URLs
                var items = new List<OrderChequeItemVm>(rows.Count);

                foreach (var r in rows)
                {
                    string? chequeUrl = null;

                    if (!string.IsNullOrWhiteSpace(r.ChequeImage))
                    {
                        chequeUrl = await blobService.GetReadSasUrlAsync(r.ChequeImage, ct);
                    }

                    items.Add(new OrderChequeItemVm(
                        r.OrderId,
                        chequeUrl,
                        r.ClientName ?? string.Empty,
                        r.ParkedAt,
                        r.DriverName ?? string.Empty,
                        r.CarteSlv));
                }

                var pageVm = new OrderChequesPageVm(items, page, pageSize, totalCount);

                await uow.CommitAsync(ct);

                return Result<OrderChequesPageVm>.Ok(pageVm);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Erreur lors du chargement paginé des commandes avec chèques.");

                await uow.RollbackAsync(ct);
                return Result<OrderChequesPageVm>.Fail(
                    "Erreur lors du chargement de la liste des commandes.");
            }
        }

        private sealed class Row
        {
            public int OrderId { get; init; }
            public string? ChequeImage { get; init; }
            public string? ClientName { get; init; }
            public DateTime ParkedAt { get; init; }
            public string? DriverName { get; init; }
            public int CarteSlv { get; init; }
        }
    }
}
