using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Ecare.Application.Commands.CreateLegacyOrderLegend
{
    public sealed class CreateLegacyOrderLegendHandler
        : IRequestHandler<CreateLegacyOrderLegendCommand, Result<int>>
    {
        private readonly string _connString;

        public CreateLegacyOrderLegendHandler(IConfiguration cfg)
        {
            _connString = cfg.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("Missing SqlServer connection string");
        }

        public async Task<Result<int>> Handle(
            CreateLegacyOrderLegendCommand request,
            CancellationToken ct)
        {
            try
            {
                await using var conn = new SqlConnection(_connString);
                await conn.OpenAsync(ct);

                var p = new DynamicParameters();

                p.Add("@BonDeCommande", request.BonDeCommande);
                p.Add("@ClientName", request.ClientName);
                p.Add("@Chantier", request.Chantier);
                p.Add("@Matricule", request.Matricule);
                p.Add("@RFIDCard", request.RFIDCard);
                p.Add("@TypeCamion", request.TypeCamion);
                p.Add("@NombrePlombs", request.NombrePlombs);

                p.Add("@Produit1", request.Produit1);
                p.Add("@Quantite1", request.Quantite1);
                p.Add("@Produit2", request.Produit2);
                p.Add("@Quantite2", request.Quantite2);

                p.Add("@TypeProduit", request.TypeProduit);
                p.Add("@AddedToQueueAt", request.AddedToQueueAt);

                // SP returns: InsertedId
                int legendId = await conn.ExecuteScalarAsync<int>(
                    "sp_InitLegacyOrder",
                    p,
                    commandType: CommandType.StoredProcedure
                );

                return Result<int>.Ok(legendId);
            }
            catch (Exception ex)
            {
                return Result<int>.Fail("LEGEND_INIT_ERROR: " + ex.Message);
            }
        }
    }
}
