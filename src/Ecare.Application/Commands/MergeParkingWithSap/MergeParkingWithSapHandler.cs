using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.MergeParkingWithSap
{
    public sealed class MergeParkingWithSapHandler
    : IRequestHandler<MergeParkingWithSapCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;

        public MergeParkingWithSapHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<int>> Handle(MergeParkingWithSapCommand request, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);

            // 1) Load Parking Row (Matricule & empty product)
            const string sqlParking = @"
            SELECT TOP 1 *
            FROM Ecare_Order_Legend
            WHERE Matricule = @Matricule
              AND (Produit1 IS NULL OR Produit1 = '');
        ";

            var parking = await _uow.Connection.QueryFirstOrDefaultAsync<dynamic>(
                sqlParking,
                new { request.Matricule },
                _uow.Transaction
            );

            if (parking == null)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail("PARKING_ROW_NOT_FOUND");
            }

            // 2) Load SAP Row (CodeSapCommande & product exists)
            const string sqlSap = @"
            SELECT TOP 1 *
            FROM Ecare_Order_Legend
            WHERE CodeSapCommande = @CodeSapCommande
              AND Produit1 IS NOT NULL;
        ";

            var sap = await _uow.Connection.QueryFirstOrDefaultAsync<dynamic>(
                sqlSap,
                new { request.CodeSapCommande },
                _uow.Transaction
            );

            if (sap == null)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail("SAP_ROW_NOT_FOUND");
            }

            // 3) Create NEW merged row
            const string sqlInsert = @"
            INSERT INTO Ecare_Order_Legend
            (
                ClientName, Chantier, Matricule, RFIDCard, TypeCamion, NombrePlombs,
                Produit1, Quantite1, Produit2, Quantite2, TypeProduit,
                BonDeCommande, SacNumber,
                CodeSapProduit1, CodeSapProduit2,
                CodeSapChantier, CodeSapClient, CodeSapCommande,
                ParkingAt, Step, AddedToQueueAt
            )
            VALUES
            (
                @ClientName,
                @Chantier,
                @Matricule,
                @RFIDCard,
                @TypeCamion,
                @NombrePlombs,
                @Produit1,
                @Quantite1,
                @Produit2,
                @Quantite2,
                @TypeProduit,
                @BonDeCommande,
                @SacNumber,
                @CodeSapProduit1,
                @CodeSapProduit2,
                @CodeSapChantier,
                @CodeSapClient,
                @CodeSapCommande,
                @ParkingAt,
                1,
                @AddedToQueueAt
            );

            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

            var now = DateTime.Now;

            var newId = await _uow.Connection.ExecuteScalarAsync<int>(
                sqlInsert,
                new
                {
                    // From SAP row
                    sap.ClientName,
                    sap.Chantier,
                    Matricule = parking.Matricule,
                    RFIDCard = parking.RFIDCard,
                    TypeCamion = parking.TypeCamion,
                    NombrePlombs = parking.NombrePlombs,
                    Produit1 = sap.Produit1,
                    Quantite1 = sap.Quantite1,
                    Produit2 = sap.Produit2,
                    Quantite2 = sap.Quantite2,
                    TypeProduit = sap.TypeProduit,
                    BonDeCommande = sap.BonDeCommande,
                    SacNumber = sap.SacNumber,
                    CodeSapProduit1 = sap.CodeSapProduit1,
                    CodeSapProduit2 = sap.CodeSapProduit2,
                    CodeSapChantier = sap.CodeSapChantier,
                    CodeSapClient = sap.CodeSapClient,
                    CodeSapCommande = sap.CodeSapCommande,

                    // From parking row
                    ParkingAt = parking.ParkingAt ?? now,
                    AddedToQueueAt = parking.AddedToQueueAt ?? now
                },
                _uow.Transaction
            );

            // 4) DELETE BOTH OLD ROWS
            const string sqlDelete = @"
            DELETE FROM Ecare_Order_Legend
            WHERE Id = @ParkingId OR Id = @SapId;
        ";

            await _uow.Connection.ExecuteAsync(
                sqlDelete,
                new { ParkingId = (int)parking.Id, SapId = (int)sap.Id },
                _uow.Transaction
            );

            await _uow.CommitAsync(ct);
            return Result<int>.Ok(newId);
        }
    }
}
