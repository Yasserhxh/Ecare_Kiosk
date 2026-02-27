using Ecare.Application.Commands;
using Ecare.Application.Commands.CreateClientEquipement;
//using Ecare.Application.Commands.Logs;
using Ecare.Application.Commands.UpdateCommercialAnnulation;
using Ecare.Application.Queries;
using Ecare.Application.Queries.GetLegendById;
using Ecare.Application.Queries.Legend;
using MediatR;
using Microsoft.Azure.SignalR.Management;

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

            //if (string.IsNullOrWhiteSpace(deviceId))
            //    return Results.BadRequest("deviceId is required");

            await using var hubContext = await manager.CreateHubContextAsync(hub, ct);

            var negotiation = await hubContext.NegotiateAsync(new NegotiationOptions
            {
                UserId = deviceId,
                TokenLifetime = TimeSpan.FromDays(365)
            }, ct);

            //Get the connection ID and add it to the device group
            // Note: This won't work directly because we don't have the connectionId yet
            // The client needs to join the group after connecting

            return Results.Ok(new
            {
                url = negotiation.Url,
                accessToken = negotiation.AccessToken,
                deviceId = deviceId
            });
        });


        app.MapPost("/api/client-equipements",
        async (CreateClientEquipementDto dto, IMediator mediator) =>
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(dto.Matricule))
                return Results.BadRequest(new { error = "Matricule is required" });

            if (string.IsNullOrWhiteSpace(dto.CarteSLV))
                return Results.BadRequest(new { error = "CarteSLV is required" });

            var id = await mediator.Send(new CreateClientEquipementCommand(dto));

            return Results.Ok(new
            {
                Id = id,
                Message = "Client equipement created successfully."
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
            // ------------------------------------------------------------
            // Build dynamic DB filters from known fields
            // ------------------------------------------------------------
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

            // ------------------------------------------------------------
            // Send CQRS query
            // ------------------------------------------------------------
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

            // Adjust property names if your Result<T> differs (Success/IsSuccess, Value/Data, Message, Errors, etc.)
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

            // If you prefer always returning 200 with Result<T>, replace with: return Results.Ok(result);
            if (!result.Success)
                return Results.NotFound(new { message = result.Value });

            return Results.Ok(result.Value);
        })
        .WithName("GetLegendById")
        .WithTags("Legend");

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
    // Dates
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }

    // Hours
    public int? HourFrom { get; init; }
    public int? HourTo { get; init; }

    // Default Step < 5
    public int MaxStep { get; init; } = 5;

    // Common filters (explicit for Swagger)
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


}