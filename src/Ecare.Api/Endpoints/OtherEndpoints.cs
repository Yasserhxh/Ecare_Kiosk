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
                where.Append(" AND CarteSLV = @CarteSlv ");
                param.Add("@CarteSlv", carteSlv.Trim());
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
                where.Append(" AND CarteSLV = @CarteSlv ");
                param.Add("@CarteSlv", carteSlv.Trim());
            }

            if (!string.IsNullOrWhiteSpace(rfidHex))
            {
                where.Append(" AND RfidHex = @RfidHex ");
                param.Add("@RfidHex", rfidHex.Trim());
            }

            if (availableOnly == true)
            {
                where.Append("""
                    AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.Ecare_ClientEquipements ce
                        WHERE (ISNULL(ce.IsClient, 0) = 1 OR ISNULL(ce.IsTransporteur, 0) = 1)
                          AND (
                              (NULLIF(LTRIM(RTRIM(t.CarteSLV)), '') IS NOT NULL
                               AND LTRIM(RTRIM(ce.CarteSLV)) = LTRIM(RTRIM(t.CarteSLV)))
                              OR
                              (NULLIF(LTRIM(RTRIM(t.RfidHex)), '') IS NOT NULL
                               AND LTRIM(RTRIM(ce.RfidHex)) = LTRIM(RTRIM(t.RfidHex)))
                          )
                    )
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
                WHERE LTRIM(RTRIM(CarteSLV)) = @CarteSLV
                   OR LTRIM(RTRIM(RfidHex)) = @RfidHex;
                """;

            var existingId = await conn.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(duplicateSql, new { CarteSLV = carteSlv, RfidHex = rfidHex }, cancellationToken: ct));

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
}
