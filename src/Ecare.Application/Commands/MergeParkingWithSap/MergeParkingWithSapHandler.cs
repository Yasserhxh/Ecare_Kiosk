using Dapper;
using Ecare.Shared;
using MediatR;

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
            FROM dbo.Ecare_Order_Legend
            WHERE Matricule = @Matricule
              AND (Produit1 IS NULL OR Produit1 = '')
            ORDER BY Id DESC;
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
            FROM dbo.Ecare_Order_Legend
            WHERE CodeSapCommande = @CodeSapCommande
              AND Produit1 IS NOT NULL
            ORDER BY Id DESC;
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

            // 3) Load latest PTAC/TARE from ClientEquipements (by Matricule)
            const string sqlEquip = @"
            SELECT TOP (1) PTAC, TARE
            FROM dbo.Ecare_ClientEquipements
            WHERE Matricule = @Matricule
            ORDER BY Id DESC;
            ";
            var equip = await _uow.Connection.QueryFirstOrDefaultAsync<dynamic>(
                sqlEquip,
                new { Matricule = (string)parking.Matricule },
                _uow.Transaction
            );

            // Prefer values already present in parking row; fallback to equip; else null
            int? ptac = parking.PTAC ?? equip?.PTAC;
            int? tare = parking.TARE ?? equip?.TARE;

            // 4) UPSERT merged row (key = Matricule + CodeSapCommande)
            const string sqlUpsert = @"
            DECLARE @MergedId INT;

            UPDATE dbo.Ecare_Order_Legend
            SET
                ClientName          = @ClientName,
                Chantier            = @Chantier,
                RFIDCard            = @RFIDCard,
                TypeCamion          = @TypeCamion,
                NombrePlombs        = @NombrePlombs,

                Produit1            = @Produit1,
                Quantite1           = @Quantite1,
                Produit2            = @Produit2,
                Quantite2           = @Quantite2,
                TypeProduit         = @TypeProduit,

                BonDeCommande       = @BonDeCommande,
                SacNumber           = @SacNumber,

                CodeSapProduit1     = @CodeSapProduit1,
                CodeSapProduit2     = @CodeSapProduit2,
                CodeSapChantier     = @CodeSapChantier,
                CodeSapClient       = @CodeSapClient,
                CodeSapCommande     = @CodeSapCommande,

                -- NEW FIELDS
                ChauffeurName       = COALESCE(@ChauffeurName, ChauffeurName),
                CodeTransporteurSap = COALESCE(@CodeTransporteurSap, CodeTransporteurSap),
                TransporteurName    = COALESCE(@TransporteurName, TransporteurName),
                PermisDeConduite    = COALESCE(@PermisDeConduite, PermisDeConduite),

                -- PTAC / TARE
                PTAC                = COALESCE(@PTAC, PTAC),
                TARE                = COALESCE(@TARE, TARE),

                ParkingAt           = COALESCE(@ParkingAt, ParkingAt),
                AddedToQueueAt      = COALESCE(@AddedToQueueAt, AddedToQueueAt),
                Step                = 1
            WHERE Matricule = @Matricule
              AND CodeSapCommande = @CodeSapCommande;

            IF (@@ROWCOUNT = 0)
            BEGIN
                INSERT INTO dbo.Ecare_Order_Legend
                (
                    ClientName, Chantier, Matricule, RFIDCard, TypeCamion, NombrePlombs,
                    Produit1, Quantite1, Produit2, Quantite2, TypeProduit,
                    BonDeCommande, SacNumber,
                    CodeSapProduit1, CodeSapProduit2,
                    CodeSapChantier, CodeSapClient, CodeSapCommande,
                    ParkingAt, Step, AddedToQueueAt,

                    -- PTAC / TARE
                    PTAC, TARE,

                    -- NEW FIELDS
                    ChauffeurName, CodeTransporteurSap, TransporteurName, PermisDeConduite
                )
                VALUES
                (
                    @ClientName, @Chantier, @Matricule, @RFIDCard, @TypeCamion, @NombrePlombs,
                    @Produit1, @Quantite1, @Produit2, @Quantite2, @TypeProduit,
                    @BonDeCommande, @SacNumber,
                    @CodeSapProduit1, @CodeSapProduit2,
                    @CodeSapChantier, @CodeSapClient, @CodeSapCommande,
                    @ParkingAt, 1, @AddedToQueueAt,

                    -- PTAC / TARE
                    @PTAC, @TARE,

                    -- NEW FIELDS
                    @ChauffeurName, @CodeTransporteurSap, @TransporteurName, @PermisDeConduite
                );

                SET @MergedId = CAST(SCOPE_IDENTITY() AS INT);
            END
            ELSE
            BEGIN
                SELECT TOP 1 @MergedId = Id
                FROM dbo.Ecare_Order_Legend
                WHERE Matricule = @Matricule
                  AND CodeSapCommande = @CodeSapCommande
                ORDER BY Id DESC;
            END

            SELECT @MergedId;
            ";

            var now = DateTime.Now;

            // Prefer from parking (usually filled there); fallback to sap
            string? chauffeurName = parking.ChauffeurName ?? sap.ChauffeurName;
            string? codeTransporteurSap = parking.CodeTransporteurSap ?? sap.CodeTransporteurSap;
            string? transporteurName = parking.TransporteurName ?? sap.TransporteurName;

            // Your legend column is PermisDeConduite, equip column is PermisConducteur
            string? permis = parking.PermisDeConduite ?? sap.PermisDeConduite;

            var mergedId = await _uow.Connection.ExecuteScalarAsync<int>(
                sqlUpsert,
                new
                {
                    // Key
                    Matricule = (string)parking.Matricule,
                    CodeSapCommande = (string)sap.CodeSapCommande,

                    // From SAP row
                    sap.ClientName,
                    sap.Chantier,
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

                    // From Parking row
                    RFIDCard = parking.RFIDCard,
                    TypeCamion = parking.TypeCamion,
                    NombrePlombs = parking.NombrePlombs,
                    ParkingAt = parking.ParkingAt ?? now,
                    AddedToQueueAt = parking.AddedToQueueAt ?? now,

                    // NEW FIELDS
                    ChauffeurName = chauffeurName,
                    CodeTransporteurSap = codeTransporteurSap,
                    TransporteurName = transporteurName,
                    PermisDeConduite = permis,

                    // PTAC / TARE
                    PTAC = ptac,
                    TARE = tare
                },
                _uow.Transaction
            );

            // 5) DELETE BOTH OLD ROWS (parking + sap)
            const string sqlDelete = @"
            DELETE FROM dbo.Ecare_Order_Legend
            WHERE Id = @ParkingId OR Id = @SapId;
            ";
            await _uow.Connection.ExecuteAsync(
                sqlDelete,
                new { ParkingId = (int)parking.Id, SapId = (int)sap.Id },
                _uow.Transaction
            );

            await _uow.CommitAsync(ct);
            return Result<int>.Ok(mergedId);
        }
    }
}
