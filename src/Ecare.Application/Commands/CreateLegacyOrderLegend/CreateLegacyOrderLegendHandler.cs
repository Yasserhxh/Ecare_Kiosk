using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

                var parameters = new DynamicParameters();
                parameters.Add("@BonDeCommande", request.BonDeCommande);
                parameters.Add("@OrderId", request.OrderId);
                parameters.Add("@ClientName", request.ClientName);
                parameters.Add("@Chantier", request.Chantier);
                parameters.Add("@Matricule", request.Matricule);
                parameters.Add("@RFIDCard", request.RFIDCard);
                parameters.Add("@TypeCamion", request.TypeCamion);
                parameters.Add("@NombrePlombs", request.NombrePlombs);
                parameters.Add("@Produit1", request.Produit1);
                parameters.Add("@Quantite1", request.Quantite1);
                parameters.Add("@Produit2", request.Produit2);
                parameters.Add("@Quantite2", request.Quantite2);
                parameters.Add("@TypeProduit", request.TypeProduit);
                parameters.Add("@AddedToQueueAt", request.AddedToQueueAt);

                // Stored procedure returns InsertedId
                int insertedId = await conn.ExecuteScalarAsync<int>(
                    "sp_InitLegacyOrder",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return Result<int>.Ok(insertedId);
            }
            catch (Exception ex)
            {
                return Result<int>.Fail($"Error while inserting legend order: {ex.Message}");
            }
        }
    }
}
