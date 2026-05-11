using Dapper;
using Ecare.Application.Commands;
using Ecare.Application.Commands.CreateClientEquipement;
using Ecare.Application.Commands.NewCard.NewClientEquipment;
//using Ecare.Application.Commands.Logs;
using Ecare.Application.Commands.UpdateCommercialAnnulation;
using Ecare.Application.Queries;
using Ecare.Application.Queries.GetLegendById;
using Ecare.Application.Queries.Legend;
using Ecare.Application.Services;
using Ecare.Application.Services.Handlers;
using Ecare.Shared;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;
using System.Data;
using System.Text;
using System.Text.Json;

namespace Ecare.Api.Endpoints;

public static class OtherEndpoints
{
    public static IEndpointRouteBuilder MapOtherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) =>
            await m.Send(c));

        app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) =>
            await m.Send(new GetCimentsQuery(), ct));

        // SignalR negotiate endpoint
        app.MapGet("/signalr/negotiate", async (
            string hub,
            string? deviceId,
            ServiceManager manager,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(hub))
                return Results.BadRequest("hub is required");

            await using var hubContext = await manager.CreateHubContextAsync(hub, ct);

            var negotiation = await hubContext.NegotiateAsync(new NegotiationOptions
            {
                UserId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId,
                TokenLifetime = TimeSpan.FromHours(1)
            }, ct);

            return Results.Ok(new
            {
                url = negotiation.Url,
                accessToken = negotiation.AccessToken,
                deviceId
            });
        });

        app.MapPost("/api/client-equipements",
            async (CreateClientEquipementDto dto, IMediator mediator) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Matricule))
                    return Results.BadRequest(new { error = "Matricule is required" });

                if (string.IsNullOrWhiteSpace(dto.CarteSLV))
                    return Results.BadRequest(new { error = "CarteSLV is required" });

                try
                {
                    var id = await mediator.Send(new CreateClientEquipementCommand(dto));

                    return Results.Ok(new
                    {
                        Id = id,
                        Message = "Client equipement created successfully."
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
            });

        app.MapGet("/api/client-equipements", async (
            int? isClient,
            int? isTransporteur,
            int? isDriver,
            string? status,
            string? carteSlv,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            if (isClient.HasValue)
            {
                where.Append(" AND ISNULL(IsClient, 0) = @IsClient ");
                param.Add("@IsClient", isClient.Value);
            }

            if (isTransporteur.HasValue)
            {
                where.Append(" AND ISNULL(IsTransporteur, 0) = @IsTransporteur ");
                param.Add("@IsTransporteur", isTransporteur.Value);
            }

            if (isDriver.HasValue)
            {
                where.Append(" AND ISNULL(IsDriver, 0) = @IsDriver ");
                param.Add("@IsDriver", isDriver.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                where.Append(" AND ISNULL(Status, '') = @Status ");
                param.Add("@Status", status.Trim());
            }

            if (!string.IsNullOrWhiteSpace(carteSlv))
            {
                where.Append($" AND {NormalizeSql("CarteSLV")} = @CarteSlv ");
                param.Add("@CarteSlv", NormalizeCardNumber(carteSlv));
            }

            var sql = $@"
SELECT
    Id,
    ClientName,
    CarteSLV,
    Matricule,
    ChauffeurName,
    RfidHex,
    CodeClientSAP,
    Status,
    Type,
    PlombsNumber,
    PTAC,
    TARE,
    IsClient,
    IsTransporteur,
    IsDriver,
    TransporteurName,
    CodeTransporteurSap,
    CodeTruckSap,
    CodeTransporteurSapCimar,
    TruckType,
    PermisConducteur
FROM dbo.Ecare_ClientEquipements
{where}
ORDER BY Id DESC;";

            var data = await conn.QueryAsync(sql, param);
            return Results.Ok(data);
        });

        app.MapPut("/api/client-equipements/{id:int}", async (
            int id,
            ClientEquipementUpdateRequest request,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            const string existsSql = """
                SELECT COUNT(1)
                FROM dbo.Ecare_ClientEquipements
                WHERE Id = @Id;
                """;

            var exists = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(existsSql, new { Id = id }, cancellationToken: ct));

            if (exists == 0)
                return Results.NotFound(new { message = $"Client equipement {id} introuvable." });

            const string updateSql = """
                UPDATE dbo.Ecare_ClientEquipements
                SET
                    ClientName = @ClientName,
                    Matricule = @Matricule,
                    ChauffeurName = @ChauffeurName,
                    CodeClientSAP = @CodeClientSAP,
                    Status = @Status,
                    Type = @Type,
                    PlombsNumber = @PlombsNumber,
                    PTAC = @PTAC,
                    TARE = @TARE,
                    IsClient = @IsClient,
                    IsTransporteur = @IsTransporteur,
                    IsDriver = @IsDriver,
                    TransporteurName = @TransporteurName,
                    CodeTransporteurSap = @CodeTransporteurSap,
                    CodeTruckSap = @CodeTruckSap,
                    CodeTransporteurSapCimar = @CodeTransporteurSapCimar,
                    TruckType = @TruckType,
                    PermisConducteur = @PermisConducteur
                WHERE Id = @Id;
                """;

            await conn.ExecuteAsync(new CommandDefinition(updateSql, new
            {
                Id = id,
                request.ClientName,
                request.Matricule,
                request.ChauffeurName,
                request.CodeClientSAP,
                request.Status,
                request.Type,
                request.PlombsNumber,
                request.PTAC,
                request.TARE,
                request.IsClient,
                request.IsTransporteur,
                request.IsDriver,
                request.TransporteurName,
                request.CodeTransporteurSap,
                request.CodeTruckSap,
                request.CodeTransporteurSapCimar,
                request.TruckType,
                request.PermisConducteur
            }, cancellationToken: ct));

            return Results.Ok(new { id, message = "Client equipement updated successfully." });
        });

        app.MapGet("/api/ecare-tags", async (
            string? carteSlv,
            string? rfidHex,
            bool? availableOnly,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(carteSlv))
            {
                where.Append($" AND {NormalizeSql("t.CarteSLV")} = @CarteSlv ");
                param.Add("@CarteSlv", NormalizeCardNumber(carteSlv));
            }

            if (!string.IsNullOrWhiteSpace(rfidHex))
            {
                where.Append(" AND RfidHex = @RfidHex ");
                param.Add("@RfidHex", rfidHex.Trim());
            }

            if (availableOnly == true)
            {
                where.Append($"""
                    AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.Ecare_ClientEquipements ce
                        WHERE (ISNULL(ce.IsClient, 0) = 1 OR ISNULL(ce.IsTransporteur, 0) = 1)
                          AND (
                              (NULLIF({NormalizeSql("t.CarteSLV")}, '') IS NOT NULL
                               AND {NormalizeSql("ce.CarteSLV")} = {NormalizeSql("t.CarteSLV")})
                              OR
                              (NULLIF(LTRIM(RTRIM(t.RfidHex)), '') IS NOT NULL
                               AND LTRIM(RTRIM(ISNULL(ce.RfidHex, ''))) = LTRIM(RTRIM(t.RfidHex)))
                          )
                    )
                    AND RIGHT(LTRIM(RTRIM(ISNULL(t.CarteSLV, ''))), 1) <> '*'
                    """);
            }

            var sql = $@"
SELECT
    Id,
    CarteSLV,
    RfidHex
FROM dbo.Ecare_Tags t
{where}
ORDER BY Id DESC;";

            var data = await conn.QueryAsync(sql, param);
            return Results.Ok(data);
        });

        app.MapPost("/api/ecare-tags", async (
            EcareTagRequest request,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            var carteSlv = request.CarteSLV?.Trim();
            var rfidHex = request.RfidHex?.Trim();
            var normalizedCarte = NormalizeCardNumber(carteSlv);

            if (string.IsNullOrWhiteSpace(carteSlv))
                return Results.BadRequest(new { message = "Le numero de carte est obligatoire." });

            if (string.IsNullOrWhiteSpace(rfidHex))
                return Results.BadRequest(new { message = "Le code HEX de la carte est obligatoire." });

            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            const string duplicateSql = """
                SELECT TOP(1) Id
                FROM dbo.Ecare_Tags
                WHERE CASE
                         WHEN RIGHT(LTRIM(RTRIM(CarteSLV)), 1) = '*'
                            THEN LEFT(LTRIM(RTRIM(CarteSLV)), LEN(LTRIM(RTRIM(CarteSLV))) - 1)
                         ELSE LTRIM(RTRIM(CarteSLV))
                      END = @CarteSLV
                   OR LTRIM(RTRIM(RfidHex)) = @RfidHex;
                """;

            var existingId = await conn.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(duplicateSql, new { CarteSLV = normalizedCarte, RfidHex = rfidHex }, cancellationToken: ct));

            if (existingId.HasValue)
                return Results.Conflict(new { message = $"La carte SLV {carteSlv} ou son code HEX existe deja." });

            const string insertSql = """
                INSERT INTO dbo.Ecare_Tags (CarteSLV, RfidHex)
                VALUES (@CarteSLV, @RfidHex);

                SELECT CAST(SCOPE_IDENTITY() AS int);
                """;

            var id = await conn.QuerySingleAsync<int>(
                new CommandDefinition(insertSql, new { CarteSLV = carteSlv, RfidHex = rfidHex }, cancellationToken: ct));

            return Results.Created($"/api/ecare-tags/{id}", new
            {
                Id = id,
                CarteSLV = carteSlv,
                RfidHex = rfidHex,
                Message = "Carte provisoire creee avec succes."
            });
        });

        app.MapGet("/api/cards/all", async (
            string? cardNumber,
            string? rfidHex,
            string? cardType,
            bool? includeDisabled,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            var where = new StringBuilder(" WHERE 1=1 ");
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(cardNumber))
            {
                where.Append(" AND t.RawCardNumber LIKE @CardNumber ");
                param.Add("@CardNumber", $"%{NormalizeCardNumber(cardNumber)}%");
            }

            if (!string.IsNullOrWhiteSpace(rfidHex))
            {
                where.Append(" AND ISNULL(t.RfidHex, '') LIKE @RfidHex ");
                param.Add("@RfidHex", $"%{rfidHex.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(cardType))
            {
                where.Append(" AND t.CardType = @CardType ");
                param.Add("@CardType", cardType.Trim().ToLowerInvariant());
            }

            if (includeDisabled != true)
            {
                where.Append(" AND ISNULL(t.IsDisabled, 0) = 0 ");
            }

            var sql = $@"
WITH TagsBase AS
(
    SELECT
        t.Id,
        t.CarteSLV,
        t.RfidHex,
        RawCardNumber = {NormalizeSql("t.CarteSLV")},
        IsDisabled = CASE WHEN RIGHT(LTRIM(RTRIM(ISNULL(t.CarteSLV, ''))), 1) = '*' THEN 1 ELSE 0 END
    FROM dbo.Ecare_Tags t
),
TagsWithEquipment AS
(
    SELECT
        t.Id AS TagId,
        t.CarteSLV AS CardNumber,
        t.RawCardNumber,
        t.RfidHex,
        t.IsDisabled,
        ce.Id AS ClientEquipementId,
        ce.ClientName,
        ce.Matricule,
        ce.ChauffeurName,
        ce.CodeClientSAP,
        ce.Status,
        ce.Type,
        ce.PlombsNumber,
        ce.PTAC,
        ce.TARE,
        ce.IsClient,
        ce.IsTransporteur,
        ce.TransporteurName,
        ce.CodeTransporteurSap,
        ce.CodeTruckSap,
        ce.CodeTransporteurSapCimar,
        ce.TruckType,
        ce.PermisConducteur,
        CardType = CASE
            WHEN ce.Id IS NOT NULL AND (ISNULL(ce.IsClient, 0) = 1 OR ISNULL(ce.IsTransporteur, 0) = 1)
                THEN 'permanente'
            ELSE 'provisoire'
        END
    FROM TagsBase t
    OUTER APPLY
    (
        SELECT TOP (1) ce.*
        FROM dbo.Ecare_ClientEquipements ce
        WHERE (
                NULLIF(t.RawCardNumber, '') IS NOT NULL
                AND {NormalizeSql("ce.CarteSLV")} = t.RawCardNumber
              )
           OR (
                NULLIF(LTRIM(RTRIM(t.RfidHex)), '') IS NOT NULL
                AND LTRIM(RTRIM(ISNULL(ce.RfidHex, ''))) = LTRIM(RTRIM(t.RfidHex))
              )
        ORDER BY ce.Id DESC
    ) ce
)
SELECT
    TagId,
    ClientEquipementId,
    CardNumber,
    RawCardNumber,
    RfidHex,
    CardType,
    IsDisabled,
    Status = CASE
        WHEN IsDisabled = 1 OR UPPER(ISNULL(Status, '')) = 'INACTIVE' THEN 'INACTIVE'
        ELSE 'ACTIVE'
    END,
    ClientName,
    Matricule,
    ChauffeurName,
    CodeClientSAP,
    TransporteurName,
    CodeTransporteurSap,
    CodeTruckSap,
    CodeTransporteurSapCimar,
    TruckType,
    PTAC,
    TARE,
    PlombsNumber,
    PermisConducteur
FROM TagsWithEquipment t
{where}
ORDER BY t.RawCardNumber ASC, t.TagId DESC;";

            var data = await conn.QueryAsync(sql, param);
            return Results.Ok(data);
        });

        app.MapPost("/api/cards/{cardId:int}/disable", async (
            int cardId,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            const string selectSql = """
                SELECT TOP (1) Id, CarteSLV, RfidHex
                FROM dbo.Ecare_Tags
                WHERE Id = @Id;
                """;

            var tag = await conn.QuerySingleOrDefaultAsync<CardTagRow>(
                new CommandDefinition(selectSql, new { Id = cardId }, cancellationToken: ct));

            if (tag is null)
                return Results.NotFound(new { message = "Carte introuvable." });

            var rawCard = NormalizeCardNumber(tag.CarteSLV);
            if (string.IsNullOrWhiteSpace(rawCard))
                return Results.BadRequest(new { message = "Le numero de carte est invalide." });

            var disabledCard = AppendDisabledSuffix(rawCard);

            await using var tx = await ((dynamic)conn).BeginTransactionAsync(ct);
            try
            {
                const string disableTagSql = """
                    UPDATE dbo.Ecare_Tags
                    SET CarteSLV = @DisabledCard
                    WHERE Id = @Id;
                    """;

                await conn.ExecuteAsync(new CommandDefinition(
                    disableTagSql,
                    new { Id = tag.Id, DisabledCard = disabledCard },
                    transaction: tx,
                    cancellationToken: ct));

                var disableEquipmentSql = $"""
                    UPDATE dbo.Ecare_ClientEquipements
                    SET
                        CarteSLV = @DisabledCard,
                        Status = 'INACTIVE'
                    WHERE {NormalizeSql("CarteSLV")} = @RawCard
                       OR (NULLIF(LTRIM(RTRIM(@RfidHex)), '') IS NOT NULL AND LTRIM(RTRIM(ISNULL(RfidHex, ''))) = LTRIM(RTRIM(@RfidHex)));
                    """;

                await conn.ExecuteAsync(new CommandDefinition(
                    disableEquipmentSql,
                    new { DisabledCard = disabledCard, RawCard = rawCard, tag.RfidHex },
                    transaction: tx,
                    cancellationToken: ct));

                await tx.CommitAsync(ct);
                return Results.Ok(new
                {
                    tag.Id,
                    CardNumber = disabledCard,
                    Message = "Carte desactivee avec succes."
                });
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        app.MapPost("/api/cards/{cardId:int}/assign", async (
            int cardId,
            CardAssignRequest request,
            IDbConnectionFactory factory,
            CancellationToken ct) =>
        {
            using var conn = factory.Create();
            if (conn.State != ConnectionState.Open)
                await ((dynamic)conn).OpenAsync(ct);

            var currentTag = await conn.QuerySingleOrDefaultAsync<CardTagRow>(
                new CommandDefinition(
                    "SELECT TOP (1) Id, CarteSLV, RfidHex FROM dbo.Ecare_Tags WHERE Id = @Id;",
                    new { Id = cardId },
                    cancellationToken: ct));

            if (currentTag is null)
                return Results.NotFound(new { message = "Carte source introuvable." });

            if (request.ClientEquipementId <= 0 || request.NewTagId <= 0)
                return Results.BadRequest(new { message = "Parametres d'affectation invalides." });

            var equipement = await conn.QuerySingleOrDefaultAsync<CardEquipmentRow>(
                new CommandDefinition(
                    "SELECT TOP (1) Id, CarteSLV, RfidHex, Status FROM dbo.Ecare_ClientEquipements WHERE Id = @Id;",
                    new { Id = request.ClientEquipementId },
                    cancellationToken: ct));

            if (equipement is null)
                return Results.NotFound(new { message = "Equipement introuvable." });

            var newTag = await conn.QuerySingleOrDefaultAsync<CardTagRow>(
                new CommandDefinition(
                    "SELECT TOP (1) Id, CarteSLV, RfidHex FROM dbo.Ecare_Tags WHERE Id = @Id;",
                    new { Id = request.NewTagId },
                    cancellationToken: ct));

            if (newTag is null)
                return Results.NotFound(new { message = "Nouvelle carte introuvable." });

            var oldRawCard = NormalizeCardNumber(equipement.CarteSLV ?? currentTag.CarteSLV);
            var newRawCard = NormalizeCardNumber(newTag.CarteSLV);

            if (string.IsNullOrWhiteSpace(newRawCard))
                return Results.BadRequest(new { message = "La nouvelle carte est invalide." });

            var duplicateSql = $"""
                SELECT TOP (1) ce.Id
                FROM dbo.Ecare_ClientEquipements ce
                WHERE ce.Id <> @ClientEquipementId
                  AND (
                        {NormalizeSql("ce.CarteSLV")} = @NewRawCard
                        OR (NULLIF(LTRIM(RTRIM(@NewRfidHex)), '') IS NOT NULL AND LTRIM(RTRIM(ISNULL(ce.RfidHex, ''))) = LTRIM(RTRIM(@NewRfidHex)))
                      );
                """;

            var duplicateEquipmentId = await conn.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(
                    duplicateSql,
                    new
                    {
                        request.ClientEquipementId,
                        NewRawCard = newRawCard,
                        NewRfidHex = newTag.RfidHex
                    },
                    cancellationToken: ct));

            if (duplicateEquipmentId.HasValue)
                return Results.Conflict(new { message = "La nouvelle carte est deja rattachee a un autre equipement." });

            await using var tx = await ((dynamic)conn).BeginTransactionAsync(ct);
            try
            {
                if (request.ReplacePermanentCard && !string.IsNullOrWhiteSpace(oldRawCard))
                {
                    var disabledCard = AppendDisabledSuffix(oldRawCard);

                    var disableOldTagSql = $"""
                        UPDATE dbo.Ecare_Tags
                        SET CarteSLV = @DisabledCard
                        WHERE {NormalizeSql("CarteSLV")} = @OldRawCard
                           OR (NULLIF(LTRIM(RTRIM(@OldRfidHex)), '') IS NOT NULL AND LTRIM(RTRIM(ISNULL(RfidHex, ''))) = LTRIM(RTRIM(@OldRfidHex)));
                        """;

                    await conn.ExecuteAsync(new CommandDefinition(
                        disableOldTagSql,
                        new { DisabledCard = disabledCard, OldRawCard = oldRawCard, OldRfidHex = equipement.RfidHex ?? currentTag.RfidHex },
                        transaction: tx,
                        cancellationToken: ct));
                }

                const string updateEquipmentSql = """
                    UPDATE dbo.Ecare_ClientEquipements
                    SET
                        CarteSLV = @NewCard,
                        RfidHex = @NewRfidHex,
                        Status = 'ACTIVE'
                    WHERE Id = @ClientEquipementId;
                    """;

                await conn.ExecuteAsync(new CommandDefinition(
                    updateEquipmentSql,
                    new
                    {
                        request.ClientEquipementId,
                        NewCard = newRawCard,
                        NewRfidHex = newTag.RfidHex
                    },
                    transaction: tx,
                    cancellationToken: ct));

                if (request.ReplacePermanentCard && !string.IsNullOrWhiteSpace(oldRawCard))
                {
                    const string activeLegendIdsSql = """
                        SELECT Id, OrderId, CommercialOrderId
                        FROM dbo.Ecare_Order_Legend
                        WHERE RFIDCard = @OldRawCard
                          AND ISNULL(BonDeLivraison, '') = ''
                          AND ISNULL(Step, 0) < 5;
                        """;

                    var activeLegends = (await conn.QueryAsync<ActiveLegendReference>(
                        new CommandDefinition(
                            activeLegendIdsSql,
                            new { OldRawCard = oldRawCard },
                            transaction: tx,
                            cancellationToken: ct))).ToList();

                    if (activeLegends.Count > 0)
                    {
                        const string updateLegendSql = """
                            UPDATE dbo.Ecare_Order_Legend
                            SET RFIDCard = @NewCard
                            WHERE RFIDCard = @OldRawCard
                              AND ISNULL(BonDeLivraison, '') = ''
                              AND ISNULL(Step, 0) < 5;
                            """;

                        await conn.ExecuteAsync(new CommandDefinition(
                            updateLegendSql,
                            new { NewCard = newRawCard, OldRawCard = oldRawCard },
                            transaction: tx,
                            cancellationToken: ct));

                        var orderIds = activeLegends.Where(x => x.OrderId.HasValue).Select(x => x.OrderId!.Value).Distinct().ToArray();
                        var commercialOrderIds = activeLegends.Where(x => x.CommercialOrderId.HasValue).Select(x => x.CommercialOrderId!.Value).Distinct().ToArray();

                        if (orderIds.Length > 0)
                        {
                            const string updateOrdersSql = """
                                UPDATE dbo.Orders
                                SET CarteSLV = @NewCard
                                WHERE Id IN @OrderIds;
                                """;

                            await conn.ExecuteAsync(new CommandDefinition(
                                updateOrdersSql,
                                new { NewCard = newRawCard, OrderIds = orderIds },
                                transaction: tx,
                                cancellationToken: ct));
                        }

                        if (commercialOrderIds.Length > 0)
                        {
                            const string updateCommercialOrdersSql = """
                                UPDATE dbo.Ecare_CommercialOrders
                                SET
                                    CarteSLV = @NewCard,
                                    RfidHex = @NewRfidHex
                                WHERE Id IN @CommercialOrderIds;
                                """;

                            await conn.ExecuteAsync(new CommandDefinition(
                                updateCommercialOrdersSql,
                                new
                                {
                                    NewCard = newRawCard,
                                    NewRfidHex = newTag.RfidHex,
                                    CommercialOrderIds = commercialOrderIds
                                },
                                transaction: tx,
                                cancellationToken: ct));
                        }
                    }
                }

                await tx.CommitAsync(ct);
                return Results.Ok(new
                {
                    ClientEquipementId = request.ClientEquipementId,
                    OldCardNumber = oldRawCard,
                    NewCardNumber = newRawCard,
                    NewTagId = newTag.Id,
                    request.ReplacePermanentCard,
                    Message = request.ReplacePermanentCard
                        ? "Carte permanente remplacee avec succes."
                        : "Carte affectee avec succes."
                });
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
      

        /*app.MapGet("/api/app-logs",
        async ([AsParameters] AppLogsQueryParams q, IMediator med, CancellationToken ct) =>
        {
            var res = await med.Send(new GetAppLogsPagedQuery(
                PageNumber: q.PageNumber ?? 1,
                PageSize: q.PageSize ?? 50,
                Event: q.Event,
                Stage: q.Stage,
                StatusCode: q.StatusCode,
                IsSuccess: q.IsSuccess,
                TraceId: q.TraceId,
                Slv: q.Slv,
                Matricule: q.Matricule,
                FromUtc: q.FromUtc,
                ToUtc: q.ToUtc,
                Search: q.Search,
                IncludeBodies: q.IncludeBodies ?? false
            ), ct);

                return Results.Ok(res);
        });*/

        app.MapGet("/api/legend-documents", async (
            [AsParameters] LegendQueryParams q,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var filters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(q.Matricule))
                filters["Matricule"] = q.Matricule;

            if (!string.IsNullOrWhiteSpace(q.RFIDCard))
                filters["RFIDCard"] = q.RFIDCard;

            if (!string.IsNullOrWhiteSpace(q.Chantier))
                filters["Chantier"] = q.Chantier;

            if (!string.IsNullOrWhiteSpace(q.Produit1))
                filters["Produit1"] = q.Produit1;

            if (!string.IsNullOrWhiteSpace(q.TypeProduit))
                filters["TypeProduit"] = q.TypeProduit;

            if (q.Step.HasValue)
                filters["Step"] = q.Step.Value;

            var query = new GetLegendBusinessQuery
            {
                DateFrom = q.DateFrom,
                DateTo = q.DateTo,
                HourFrom = q.HourFrom,
                HourTo = q.HourTo,
                MaxStep = q.MaxStep,
                Filters = filters.Count > 0 ? filters : null
            };

            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetLegend")
        .WithTags("Legend")
        .WithSummary("Get legend data with filters")
        .WithDescription("""
        Default behavior:
        - Date = today
        - Step < 5

        Supports:
        - Date range
        - Hour range
        - Matricule / Chantier / Produit / Step filters
        - Business mapping (Net, TypeCommande, CFR/EXW)
        """);

        app.MapPut("/api/legend/{id:int}/annulation-commercial", async (
            int id,
            UpdateCommercialAnnulationRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new UpdateCommercialAnnulationCommand(
                Id: id,
                AnnulationCommercial: body.AnnulationCommercial,
                MotifAnnulationCommercial: body.MotifAnnulationCommercial,
                UserIdAnnulationCommercial: body.UserIdAnnulationCommercial
            );

            var result = await mediator.Send(cmd, ct);

            if (!result.Success)
                return Results.BadRequest(result);

            return Results.Ok(result.Value);
        })
        .WithName("UpdateCommercialAnnulation")
        .WithTags("Legend");

        app.MapGet("/api/legend/{id:int}", async (
            int id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetLegendByIdQuery(id), ct);

            if (!result.Success)
                return Results.NotFound(new { message = result.Value });

            return Results.Ok(result.Value);
        })
        .WithName("GetLegendById")
        .WithTags("Legend");


        app.MapPost("/ecare/client-equipements", async (
            SaveClientEquipementRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var result = await mediator.Send(new SaveClientEquipementCommand(request), ct);
                return Results.Created($"/ecare/client-equipements/{result.ClientEquipementId}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("SaveEcareClientEquipement")
        .WithTags("Ecare");


        app.MapPost("/api/pab/trigger/{type}", async (
    string type,
    [FromBody] JsonElement payload,
    PabUnifiedInboundHandler handler,
    CancellationToken ct) =>
        {
            bool isExit = type.Equals("exit", StringComparison.OrdinalIgnoreCase);
            await handler.HandleAsync(payload, ct);
            return Results.Ok();
        }).WithName("Test Commande")
        .WithTags("Test Endpoint");

        return app;
    }

    private static string NormalizeCardNumber(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.EndsWith('*'))
            trimmed = trimmed[..^1];

        return trimmed.Trim();
    }

    private static string AppendDisabledSuffix(string value)
    {
        var normalized = NormalizeCardNumber(value);
        return string.IsNullOrWhiteSpace(normalized) ? normalized : $"{normalized}*";
    }

    private static string NormalizeSql(string columnExpression) => $"""
        CASE
            WHEN RIGHT(LTRIM(RTRIM(ISNULL({columnExpression}, ''))), 1) = '*'
                THEN LEFT(LTRIM(RTRIM(ISNULL({columnExpression}, ''))), LEN(LTRIM(RTRIM(ISNULL({columnExpression}, '')))) - 1)
            ELSE LTRIM(RTRIM(ISNULL({columnExpression}, '')))
        END
        """;

    public sealed record AppLogsQueryParams(
        int? PageNumber,
        int? PageSize,
        string? Event,
        string? Stage,
        int? StatusCode,
        bool? IsSuccess,
        Guid? TraceId,
        string? Slv,
        string? Matricule,
        DateTime? FromUtc,
        DateTime? ToUtc,
        string? Search,
        bool? IncludeBodies
    );

    public sealed class LegendQueryParams
    {
        public DateTime? DateFrom { get; init; }
        public DateTime? DateTo { get; init; }
        public int? HourFrom { get; init; }
        public int? HourTo { get; init; }
        public int MaxStep { get; init; } = 5;
        public string? Matricule { get; init; }
        public string? RFIDCard { get; init; }
        public string? Chantier { get; init; }
        public string? Produit1 { get; init; }
        public string? TypeProduit { get; init; }
        public int? Step { get; init; }
    }

    public sealed class UpdateCommercialAnnulationRequest
    {
        public int? AnnulationCommercial { get; set; }
        public string? MotifAnnulationCommercial { get; set; }
        public string? UserIdAnnulationCommercial { get; set; }
    }

    public sealed class EcareTagRequest
    {
        public string? CarteSLV { get; set; }
        public string? RfidHex { get; set; }
    }

    public sealed class CardAssignRequest
    {
        public int ClientEquipementId { get; set; }
        public int NewTagId { get; set; }
        public bool ReplacePermanentCard { get; set; }
    }

    public sealed class ClientEquipementUpdateRequest
    {
        public string? ClientName { get; set; }
        public string? Matricule { get; set; }
        public string? ChauffeurName { get; set; }
        public string? CodeClientSAP { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public int? PlombsNumber { get; set; }
        public decimal? PTAC { get; set; }
        public decimal? TARE { get; set; }
        public int? IsClient { get; set; }
        public int? IsTransporteur { get; set; }
        public int? IsDriver { get; set; }
        public string? TransporteurName { get; set; }
        public string? CodeTransporteurSap { get; set; }
        public string? CodeTruckSap { get; set; }
        public string? CodeTransporteurSapCimar { get; set; }
        public string? TruckType { get; set; }
        public string? PermisConducteur { get; set; }
    }

    private sealed class CardTagRow
    {
        public int Id { get; set; }
        public string? CarteSLV { get; set; }
        public string? RfidHex { get; set; }
    }

    private sealed class CardEquipmentRow
    {
        public int Id { get; set; }
        public string? CarteSLV { get; set; }
        public string? RfidHex { get; set; }
        public string? Status { get; set; }
    }

    private sealed class ActiveLegendReference
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public int? CommercialOrderId { get; set; }
    }
}
