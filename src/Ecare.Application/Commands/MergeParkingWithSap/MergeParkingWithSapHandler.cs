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

            try
            {
                static object? GetRawValue(dynamic row, string key)
                {
                    if (row is IDictionary<string, object> dict && dict.TryGetValue(key, out var value))
                        return value;

                    return null;
                }

                static T? GetTypedValue<T>(dynamic row, string key)
                {
                    var value = GetRawValue(row, key);
                    if (value is null || value is DBNull)
                        return default;

                    return (T)Convert.ChangeType(value, typeof(T));
                }

                const string sqlParking = @"
                SELECT TOP 1 *
                FROM dbo.Ecare_Order_Legend
                WHERE Matricule = @Matricule
                  AND (Produit1 IS NULL OR Produit1 = '')
                  AND AnnulationCommercial IS NULL
                ORDER BY Id DESC;";

                var parking = await _uow.Connection.QueryFirstOrDefaultAsync<dynamic>(
                    sqlParking,
                    new { request.Matricule },
                    _uow.Transaction);

                if (parking is null)
                    return Result<int>.Fail("PARKING_ROW_NOT_FOUND");

                const string sqlSap = @"
                SELECT TOP 1 *
                FROM dbo.Ecare_Order_Legend
                WHERE CodeSapCommande = @CodeSapCommande
                  AND Produit1 IS NOT NULL
                  AND AnnulationCommercial IS NULL
                ORDER BY Id DESC;";

                var sap = await _uow.Connection.QueryFirstOrDefaultAsync<dynamic>(
                    sqlSap,
                    new { request.CodeSapCommande },
                    _uow.Transaction);

                if (sap is null)
                    return Result<int>.Fail("SAP_ROW_NOT_FOUND");

                var parkingId = GetTypedValue<int?>(parking, "Id");
                var sapId = GetTypedValue<int?>(sap, "Id");

                if (!parkingId.HasValue || !sapId.HasValue)
                    return Result<int>.Fail("MERGE_ROW_ID_MISSING");

                if (parkingId.Value == sapId.Value)
                {
                    await _uow.CommitAsync(ct);
                    return Result<int>.Ok(sapId.Value);
                }

                const string sqlEquip = @"
                SELECT TOP (1) PTAC, TARE
                FROM dbo.Ecare_ClientEquipements
                WHERE Matricule = @Matricule
                ORDER BY Id DESC;";

                var equip = await _uow.Connection.QueryFirstOrDefaultAsync<dynamic>(
                    sqlEquip,
                    new { Matricule = GetTypedValue<string>(parking, "Matricule") },
                    _uow.Transaction);

                int? ptac = GetTypedValue<int?>(parking, "PTAC") ?? GetTypedValue<int?>(equip, "PTAC");
                int? tare = GetTypedValue<int?>(parking, "TARE") ?? GetTypedValue<int?>(equip, "TARE");

                var chauffeurName = GetTypedValue<string>(parking, "ChauffeurName") ?? GetTypedValue<string>(sap, "ChauffeurName");
                var codeTransporteurSap = GetTypedValue<string>(parking, "CodeTransporteurSap") ?? GetTypedValue<string>(sap, "CodeTransporteurSap");
                var transporteurName = GetTypedValue<string>(parking, "TransporteurName") ?? GetTypedValue<string>(sap, "TransporteurName");
                var permis = GetTypedValue<string>(parking, "PermisDeConduite") ?? GetTypedValue<string>(sap, "PermisDeConduite");

                var sapHasProgress =
                    (GetTypedValue<int?>(sap, "Step") ?? 0) > 1
                    || GetRawValue(sap, "PremierePoid") is not null
                    || GetRawValue(sap, "DeuxiemePoid") is not null
                    || GetRawValue(sap, "PabEntryAt") is not null
                    || GetRawValue(sap, "StartChargingAt") is not null
                    || GetRawValue(sap, "FinishedChargingAt") is not null
                    || GetRawValue(sap, "PabExitAt") is not null
                    || !string.IsNullOrWhiteSpace(GetTypedValue<string>(sap, "BonDeLivraison"));

                var targetId = sapHasProgress ? sapId.Value : parkingId.Value;
                var deleteId = sapHasProgress ? parkingId.Value : sapId.Value;
                var now = DateTime.Now;

                const string sqlMergeIntoTarget = @"
                UPDATE dbo.Ecare_Order_Legend
                SET
                    OrderId             = COALESCE(@OrderId, OrderId),
                    CommercialOrderId   = COALESCE(@CommercialOrderId, CommercialOrderId),
                    UserId              = COALESCE(@UserId, UserId),
                    Status              = COALESCE(@Status, Status),
                    ClientName          = @ClientName,
                    Chantier            = @Chantier,
                    Matricule           = COALESCE(@Matricule, Matricule),
                    RFIDCard            = COALESCE(@RFIDCard, RFIDCard),
                    TypeCamion          = COALESCE(@TypeCamion, TypeCamion),
                    NombrePlombs        = COALESCE(@NombrePlombs, NombrePlombs),
                    Produit1            = @Produit1,
                    Quantite1           = @Quantite1,
                    Produit2            = @Produit2,
                    Quantite2           = @Quantite2,
                    TypeProduit         = COALESCE(@TypeProduit, TypeProduit),
                    BonDeCommande       = @BonDeCommande,
                    SacNumber           = COALESCE(@SacNumber, SacNumber),
                    CodeSapProduit1     = @CodeSapProduit1,
                    CodeSapProduit2     = @CodeSapProduit2,
                    CodeSapChantier     = @CodeSapChantier,
                    CodeClientSAP       = COALESCE(@CodeSapClient, CodeClientSAP),
                    CodeSapClient       = @CodeSapClient,
                    CodeSapCommande     = @CodeSapCommande,
                    ChauffeurName       = COALESCE(@ChauffeurName, ChauffeurName),
                    CodeTransporteurSap = COALESCE(@CodeTransporteurSap, CodeTransporteurSap),
                    TransporteurName    = COALESCE(@TransporteurName, TransporteurName),
                    PermisDeConduite    = COALESCE(@PermisDeConduite, PermisDeConduite),
                    PTAC                = COALESCE(@PTAC, PTAC),
                    TARE                = COALESCE(@TARE, TARE),
                    ParkingAt           = COALESCE(ParkingAt, @ParkingAt),
                    AddedToQueueAt      = COALESCE(AddedToQueueAt, @AddedToQueueAt),
                    DateAffectation     = COALESCE(DateAffectation, @DateAffectation),
                    Step                = CASE
                                              WHEN @KeepExistingStep = 1 THEN Step
                                              WHEN Step < 1 THEN 1
                                              ELSE Step
                                          END
                WHERE Id = @TargetId;

                SELECT @TargetId;";

                var mergedId = await _uow.Connection.ExecuteScalarAsync<int>(
                    sqlMergeIntoTarget,
                    new
                    {
                        TargetId = targetId,
                        KeepExistingStep = sapHasProgress ? 1 : 0,
                        OrderId = GetTypedValue<int?>(sap, "OrderId"),
                        CommercialOrderId = GetTypedValue<int?>(sap, "CommercialOrderId"),
                        UserId = GetTypedValue<string>(sap, "UserId"),
                        Status = GetTypedValue<string>(sap, "Status"),
                        ClientName = GetTypedValue<string>(sap, "ClientName"),
                        Chantier = GetTypedValue<string>(sap, "Chantier"),
                        Matricule = GetTypedValue<string>(parking, "Matricule"),
                        RFIDCard = GetTypedValue<string>(parking, "RFIDCard"),
                        TypeCamion = GetTypedValue<string>(parking, "TypeCamion"),
                        NombrePlombs = GetTypedValue<int?>(parking, "NombrePlombs"),
                        Produit1 = GetTypedValue<string>(sap, "Produit1"),
                        Quantite1 = GetTypedValue<decimal?>(sap, "Quantite1"),
                        Produit2 = GetTypedValue<string>(sap, "Produit2"),
                        Quantite2 = GetTypedValue<decimal?>(sap, "Quantite2"),
                        TypeProduit = GetTypedValue<string>(sap, "TypeProduit"),
                        BonDeCommande = GetTypedValue<string>(sap, "BonDeCommande"),
                        SacNumber = GetTypedValue<int?>(sap, "SacNumber"),
                        CodeSapProduit1 = GetTypedValue<string>(sap, "CodeSapProduit1"),
                        CodeSapProduit2 = GetTypedValue<string>(sap, "CodeSapProduit2"),
                        CodeSapChantier = GetTypedValue<string>(sap, "CodeSapChantier"),
                        CodeSapClient = GetTypedValue<string>(sap, "CodeSapClient"),
                        CodeSapCommande = GetTypedValue<string>(sap, "CodeSapCommande"),
                        ChauffeurName = chauffeurName,
                        CodeTransporteurSap = codeTransporteurSap,
                        TransporteurName = transporteurName,
                        PermisDeConduite = permis,
                        PTAC = ptac,
                        TARE = tare,
                        ParkingAt = GetTypedValue<DateTime?>(parking, "ParkingAt") ?? now,
                        AddedToQueueAt = GetTypedValue<DateTime?>(parking, "AddedToQueueAt") ?? now,
                        // Affectation = the moment a commande is affected to this matricule (this merge).
                        // Write-once: COALESCE keeps the first value if the row is ever re-merged.
                        DateAffectation = now
                    },
                    _uow.Transaction);

                await _uow.Connection.ExecuteAsync(
                    """
                    DELETE FROM dbo.Ecare_Order_Legend
                    WHERE Id = @DeleteId;
                    """,
                    new { DeleteId = deleteId },
                    _uow.Transaction);

                await _uow.CommitAsync(ct);
                return Result<int>.Ok(mergedId);
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }
    }
}
