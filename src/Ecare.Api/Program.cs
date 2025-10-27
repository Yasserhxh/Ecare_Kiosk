using Ecare.Api.Extensions; // <-- for AddSignalRListeners
using Ecare.Application;
using Ecare.Application.Commands;
using Ecare.Application.Commands.Flux;
using Ecare.Application.Commands.Flux.Create;
using Ecare.Application.Commands.Orders;
using Ecare.Application.Commands.Queue.CreateQueue;
using Ecare.Application.Commands.Queue.PinTruck;
using Ecare.Application.Commands.Queue.UpdateQueue;
using Ecare.Application.Pipelines;
using Ecare.Application.Queries;
using Ecare.Application.Services;
using Ecare.Infrastructure;
using Ecare.Infrastructure.Persistence;
using Ecare.Infrastructure.Printing;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------------------------------------------------------
// General host config: show all DI problems during startup
// ---------------------------------------------------------
builder.Host.UseDefaultServiceProvider(opt =>
{
    opt.ValidateScopes = true;
    opt.ValidateOnBuild = true;
});

// ---------------------------------------------------------
// CORS + Swagger
// ---------------------------------------------------------
const string ViteDev = "ViteDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(ViteDev, policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetPreflightMaxAge(TimeSpan.FromHours(1)));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------------------
// MediatR + Validation Pipelines
// ---------------------------------------------------------
builder.Services.AddMediatR(m => m.RegisterServicesFromAssemblyContaining<IAssemblyMarker>());
builder.Services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// ---------------------------------------------------------
// Persistence (EF Core + Dapper UnitOfWork)
// ---------------------------------------------------------
builder.Services.AddDbContext<EcareDbContext>(options =>
    options.UseSqlServer(
        cfg.GetConnectionString("SqlServer"),
        b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName)));

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(cfg.GetConnectionString("SqlServer")!));

builder.Services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

// ---------------------------------------------------------
// Repositories + Infrastructure services
// ---------------------------------------------------------
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IWeighRepository, WeighRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<IEcareCimentRepository, EcareCimentRepository>();
builder.Services.AddScoped<IClientEquipementRepository, ClientEquipementRepository>();
builder.Services.AddScoped<IKioskDriverRepository, KioskDriverRepository>();
builder.Services.AddScoped<IKioskOrderRepository, KioskOrderRepository>();
builder.Services.AddScoped<ILegacyOrderWriter, LegacyOrderWriter>();
builder.Services.AddSingleton<IBlPrinter, MockBlPrinter>();

// ---------------------------------------------------------
// Azure SignalR setup
// ---------------------------------------------------------
builder.Services.AddSingleton<ServiceManager>(sp =>
{
    var cs = cfg.GetConnectionString("AzureSignalR")
        ?? throw new InvalidOperationException("ConnectionStrings:AzureSignalR missing");
    return new ServiceManagerBuilder()
        .WithOptions(o => o.ConnectionString = cs)
        .BuildServiceManager();
});

// Optional: shared publisher used elsewhere
builder.Services.AddSingleton<OrderDataPublisher>();

// ---------------------------------------------------------
// Custom Extension: Registers both ENTRY and EXIT listeners
// ---------------------------------------------------------
builder.Services.AddSignalRListeners(cfg);

// ---------------------------------------------------------
// Build + middleware
// ---------------------------------------------------------
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(ViteDev);

// ---------------------------------------------------------
// Negotiate endpoint (for all hubs)
// ---------------------------------------------------------
app.MapGet("/signalr/negotiate", async (string hub, ServiceManager manager, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(hub))
        return Results.BadRequest("hub is required");

    await using var hubContext = await manager.CreateHubContextAsync(hub, ct);
    var negotiation = await hubContext.NegotiateAsync(new NegotiationOptions(), ct);
    return Results.Ok(new { url = negotiation.Url, accessToken = negotiation.AccessToken });
});

// ---------------------------------------------------------
// Minimal API endpoints
// ---------------------------------------------------------
app.MapPost("/kiosk/scan", async (ScanBySlvQuery q, IMediator m) => await m.Send(q));
app.MapPost("/orders/confirm", async (ConfirmOrderCommand c, IMediator m) => await m.Send(c));
app.MapPost("/orders/cancel", async (CancelOrderCommand c, IMediator m) => await m.Send(c));
app.MapPost("/pab1/weigh", async (RecordPab1WeighCommand c, IMediator m) => await m.Send(c));
app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) => await m.Send(c));
app.MapPost("/pab2/weigh-bl", async (RecordPab2AndIssueBlCommand c, IMediator m) => await m.Send(c));
app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) => await m.Send(new GetCimentsQuery(), ct));
app.MapPost("/orders", async (CreateOrderAtKioskCommand c, IMediator m, CancellationToken ct) => await m.Send(c, ct));
app.MapPost("/orders/legacy", async (CreateLegacyOrderCommand c, IMediator m, CancellationToken ct) => await m.Send(c, ct));
app.MapGet("/flux/qualite", async (IMediator m, CancellationToken ct) => await m.Send(new GetFluxQualiteQuery(), ct));

app.MapPost("queue", async ([FromBody] CreateQueueEntryCommand cmd, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(cmd, ct);
    if (!result.Success)
        return Results.BadRequest(new { error = result.Error });

    return Results.Created($"/queue/{result.Value}", new { id = result.Value });
})
.WithName("CreateQueueEntry");

app.MapPost("/queue/toggle-pin/{matricule}", async (string matricule, IMediator mediator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(matricule))
        return Results.BadRequest("matricule is required");

    var res = await mediator.Send(new TogglePinByMatriculeCommand(matricule), ct);
    return res.Success ? Results.Ok(new { affected = res.Value }) : Results.BadRequest(res.Error);
});

app.MapPost("/queue/rebroadcast", async (ServiceManager signalR, IUnitOfWork uow, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var log = loggerFactory.CreateLogger("QueueRebroadcast");
    await QueueSnapshot.BuildAndBroadcastAsync(signalR, uow, log, ct);
    return Results.Ok(new { ok = true, sent = "QueueDataEvent", hub = "queue_data_hub" });
});

app.MapPost("/flux", async (CreateFluxEntryCommand c, IMediator m, CancellationToken ct)
    => await m.Send(c, ct));

app.MapPost("/orders/from-form", async (CreateOrderFromFormCommand c, IMediator m, CancellationToken ct)
    => await m.Send(c, ct));

app.MapPost("/queue/update-details", async (UpdateQueueDetailsCommand c, IMediator m)
    => await m.Send(c));

app.MapPost("/flux/first-weight/by-bon", async (UpdateFirstWeightByBonCommand cmd, IMediator mediator, CancellationToken ct) =>
{
    var res = await mediator.Send(cmd, ct);
    return res.Success
        ? Results.Ok(new { updated = res.Value })
        : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/flux/second-weight/by-bon", async (UpdateSecondWeightByBonCommand cmd, IMediator mediator, CancellationToken ct) =>
{
    var res = await mediator.Send(cmd, ct);
    return res.Success
        ? Results.Ok(new { updated = res.Value })
        : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/pab/exit", async Task<IResult> (string slv, ISender mediator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(slv))
        return Results.BadRequest("Missing query parameter 'slv'.");

    var result = await mediator.Send(new GetPabExitDataQuery(slv), ct);
    if (!result.Success)
    {
        var msg = result.Error ?? "Unknown error";
        if (msg.Contains("inconnue", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("inactive", StringComparison.OrdinalIgnoreCase))
            return Results.NotFound(msg);

        return Results.BadRequest(msg);
    }

    return Results.Ok(new { message = "Exit data retrieved and broadcast.", data = result.Value });
});

app.MapPost("/flux/start-charging", async (
    UpdateStartChargingCommand cmd,
    IMediator mediator,
    CancellationToken ct) =>
{
    var res = await mediator.Send(cmd, ct);
    return res.Success
        ? Results.Ok(new { updated = res.Value })
        : Results.BadRequest(new { error = res.Error });
})
.WithName("Flux_UpdateStartCharging")
.WithSummary("Met à jour EcareFlux.StartChargingAt=DateTime.Now pour un Matricule + BonDeCommande.");

app.MapPost("/flux/finish-charging", async (
    UpdateFinishChargingCommand cmd,
    IMediator mediator,
    CancellationToken ct) =>
{
    var res = await mediator.Send(cmd, ct);
    return res.Success
        ? Results.Ok(new { updated = res.Value })
        : Results.BadRequest(new { error = res.Error });
})
.WithName("Flux_UpdateFinishCharging")
.WithSummary("Met à jour EcareFlux.FinishChargingAt=DateTime.Now pour un Matricule + BonDeCommande.");


// ---------------------------------------------------------
// Initialize shared SignalR publisher once
// ---------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var publisher = scope.ServiceProvider.GetRequiredService<OrderDataPublisher>();
    await publisher.InitializeAsync();
}

app.Run();
