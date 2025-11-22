using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateOrderLegend
{
    public sealed class UpdateOrderLegendHandler
    : IRequestHandler<UpdateOrderLegendCommand, Result<string>>
    {
        private readonly IUnitOfWork _uow;

        public UpdateOrderLegendHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<string>> Handle(UpdateOrderLegendCommand request, CancellationToken ct)
        {
            const string sqlGetType = @"
            SELECT TOP 1 Type
            FROM EcareCiments
            WHERE Name = @Name;
        ";

            const string sqlUpdate = @"
            UPDATE Ecare_Order_Legend
            SET 
                ClientName = @ClientName,
                Chantier = @Chantier,
                Produit1 = @Produit1,
                Quantite1 = @Quantite1,
                Produit2 = @Produit2,
                Quantite2 = @Quantite2,
                TypeProduit = @TypeProduit
            WHERE Id = @OrderId;
        ";

            try
            {
                await _uow.BeginAsync(ct);

                // 1) Resolve TypeProduit
                var typeProduit = await _uow.Connection.ExecuteScalarAsync<string?>(
                    sqlGetType,
                    new { Name = request.Produit1 },
                    _uow.Transaction
                );

                if (string.IsNullOrWhiteSpace(typeProduit))
                {
                    await _uow.RollbackAsync(ct);
                    return Result<string>.Fail("Produit1 not found in EcareCiments.");
                }

                // 2) Update Order Legend
                await _uow.Connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        request.OrderId,
                        request.ClientName,
                        request.Chantier,
                        request.Produit1,
                        request.Quantite1,
                        request.Produit2,
                        request.Quantite2,
                        TypeProduit = typeProduit
                    },
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);
                return Result<string>.Ok("Order updated successfully.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
