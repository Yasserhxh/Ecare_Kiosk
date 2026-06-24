using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetLegendsDocument
{
    public sealed class GetLegendsHandler : IRequestHandler<GetLegendsQuery, PagedLegendResult>
    {
        private readonly IUnitOfWork _uow;

        public GetLegendsHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<PagedLegendResult> Handle(GetLegendsQuery q, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);

            try
            {
                var where = new StringBuilder(" WHERE 1=1 ");
                var param = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(q.ClientName))
                {
                    where.Append(" AND ClientName LIKE @ClientName ");
                    param.Add("ClientName", $"%{q.ClientName}%");
                }

                if (!string.IsNullOrWhiteSpace(q.Matricule))
                {
                    where.Append(" AND Matricule LIKE @Matricule ");
                    param.Add("Matricule", $"%{q.Matricule}%");
                }

                if (!string.IsNullOrWhiteSpace(q.Produit1))
                {
                    where.Append(" AND Produit1 LIKE @Produit1 ");
                    param.Add("Produit1", $"%{q.Produit1}%");
                }

                int skip = (q.Page - 1) * q.PageSize;

                string sqlCount = $@"
                SELECT COUNT(*) 
                FROM Ecare_Order_Legend
                {where}
            ";

                string sqlData = $@"
                SELECT 
                      Id, ClientName, Chantier, Matricule, RFIDCard,
                      Produit1, Quantite1, Produit2, Quantite2,
                      TypeProduit, CodeClientSAP, CodeProduitSAP,
                      PremierePoid, DeuxiemePoid,
                      ParkingAt, PabEntryAt, StartChargingAt, FinishedChargingAt, PabExitAt,
                      BonDeLivraison, CreatedAt, DateAffectation, BonDeCommande, Ligne,
                      ExtraSac, PlusBags, MinusBags, StartExtraSac, EndExtraSac,
                      SacNumber, NumberSacs_Charged, Weight_Charged, Status
                FROM Ecare_Order_Legend
                {where}
                ORDER BY CreatedAt DESC
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            ";

                param.Add("Skip", skip);
                param.Add("Take", q.PageSize);

                int total = await _uow.Connection.ExecuteScalarAsync<int>(
                    sqlCount,
                    param,
                    _uow.Transaction
                );

                var items = await _uow.Connection.QueryAsync<LegendDto>(
                    sqlData,
                    param,
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);

                return new PagedLegendResult
                {
                    Items = items,
                    TotalCount = total,
                    Page = q.Page,
                    PageSize = q.PageSize
                };
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }
    }
}
