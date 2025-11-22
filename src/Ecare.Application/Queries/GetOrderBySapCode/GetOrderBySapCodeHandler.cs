using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetOrderBySapCode
{
    public sealed class GetOrderBySapCodeHandler
    : IRequestHandler<GetOrderBySapCodeQuery, Result<IReadOnlyList<OrderBySapDto>>>
    {
        private readonly IUnitOfWork _uow;

        public GetOrderBySapCodeHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<IReadOnlyList<OrderBySapDto>>> Handle(
            GetOrderBySapCodeQuery request,
            CancellationToken ct)
        {
            await _uow.BeginAsync(ct);

            const string sql = @"
            SELECT 
                Id,
                ClientName,
                Chantier,
                Matricule,
                Produit1,
                Quantite1,
                Produit2,
                Quantite2,
                BonDeCommande
            FROM Ecare_Order_Legend
            WHERE CodeSapCommande = @CodeSapCommande;
        ";

            var rows = (await _uow.Connection.QueryAsync<OrderBySapDto>(
                sql,
                new { CodeSapCommande = request.CodeSapCommande },
                _uow.Transaction
            )).ToList();

            await _uow.CommitAsync(ct);

            if (!rows.Any())
                return Result<IReadOnlyList<OrderBySapDto>>.Fail("NOT_FOUND");

            return Result<IReadOnlyList<OrderBySapDto>>.Ok(rows);
        }
    }
}
