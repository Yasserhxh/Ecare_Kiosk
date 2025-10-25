using Ecare.Application;
using Ecare.Application.Commands;
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
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// --- CORS
const string ViteDev = "ViteDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(ViteDev, policy =>
        policy
            .AllowAnyOrigin()   // allow file:// and remote LAN frontend
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromHours(1)));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- MediatR + FluentValidation
builder.Services.AddMediatR(cfgM => cfgM.RegisterServicesFromAssemblyContaining<IAssemblyMarker>());
builder.Services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// --- Persistence
builder.Services.AddDbContext<EcareDbContext>(options =>
    options.UseSqlServer(
        cfg.GetConnectionString("SqlServer"),
        b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName)
    ));
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(cfg.GetConnectionString("SqlServer")!));
builder.Services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

// --- Repos + Printing
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

// ------------------------------
// Azure SignalR: NEGOTIATE + BACKGROUND LISTENER + ORDER BROADCASTER
// ------------------------------

// 1️⃣ Register Azure SignalR ServiceManager (used for negotiate + hub contexts)
builder.Services.AddSingleton<ServiceManager>(sp =>
{
    var cs = cfg.GetConnectionString("AzureSignalR")
             ?? throw new InvalidOperationException("ConnectionStrings:AzureSignalR missing");
    return new ServiceManagerBuilder()
        .WithOptions(o => o.ConnectionString = cs)
        .BuildServiceManager();
});

// 2️⃣ Configure client listener options
builder.Services.Configure<SignalRClientOptions>(cfg.GetSection("SignalRClient"));

builder.Services.Configure<RfidListenerOptions>(
    builder.Configuration.GetSection("RfidListener"));

// 3️⃣ Register publisher (order_data_hub broadcaster)
builder.Services.AddSingleton<OrderDataPublisher>();

// 4️⃣ Register RFID → backend listener (listens on slv_hub)
builder.Services.AddSingleton<DeviceSignalRClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeviceSignalRClient>());

builder.Services.AddHttpClient(nameof(RfidPabEntryListner));

// Background listener
builder.Services.AddHostedService<RfidPabEntryListner>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(ViteDev);

// ------------------------------
// ✅ /signalr/negotiate endpoint for both hubs
// ------------------------------
app.MapGet("/signalr/negotiate", async (string hub, ServiceManager manager, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(hub))
        return Results.BadRequest("hub is required");

    await using var hubContext = await manager.CreateHubContextAsync(hub, ct);
    var negotiation = await hubContext.NegotiateAsync(new NegotiationOptions(), ct);

    return Results.Ok(new { url = negotiation.Url, accessToken = negotiation.AccessToken });
});




// ------------------------------
// 🧩 API Endpoints
// ------------------------------
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

app.MapPost("queue", async (
        [FromBody] CreateQueueEntryCommand cmd,
        IMediator mediator,
        CancellationToken ct) =>
{
    var result = await mediator.Send(cmd, ct);

    // Adapt these two property names to your Result<T> type if they differ
    if (!result.Success)
        return Results.BadRequest(new { error = result.Error });

    // 201 Created + Location header + body { id }
    return Results.Created($"/queue/{result.Value}", new { id = result.Value });
})
    .WithName("CreateQueueEntry")
    .Produces(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest);

app.MapPost("/queue/toggle-pin/{matricule}", async (string matricule, IMediator mediator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(matricule))
        return Results.BadRequest("matricule is required");

    var res = await mediator.Send(new TogglePinByMatriculeCommand(matricule), ct);
    return res.Success
        ? Results.Ok(new { affected = res.Value })
        : Results.BadRequest(res.Error);
})
.WithName("Queue_TogglePin");

app.MapPost("/queue/rebroadcast", async (
    ServiceManager signalR,
    IUnitOfWork uow,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    var log = loggerFactory.CreateLogger("QueueRebroadcast");
    await QueueSnapshot.BuildAndBroadcastAsync(signalR, uow, log, ct);
    return Results.Ok(new { ok = true, sent = "QueueDataEvent", hub = "queue_data_hub" });
})
.WithName("Queue_Rebroadcast");

app.MapPost("/flux", async (CreateFluxEntryCommand c, IMediator m, CancellationToken ct)
    => await m.Send(c, ct))
   .WithName("Flux_Create");

// Program.cs (or your endpoints file)
app.MapPost("/orders/from-form", async (CreateOrderFromFormCommand c, IMediator m, CancellationToken ct)
    => await m.Send(c, ct))
   .WithName("CreateOrderFromForm")
   .WithSummary("Create order using only form fields (matricule, chauffeur, client, bon, product, quantity).");

app.MapPost("/queue/update-details", async (UpdateQueueDetailsCommand c, IMediator m)
    => await m.Send(c))
   .WithName("Queue_UpdateDetails");

// ------------------------------
//Initialize the publisher (connect to Azure SignalR once)
// ------------------------------
using (var scope = app.Services.CreateScope())
{
    var publisher = scope.ServiceProvider.GetRequiredService<OrderDataPublisher>();
    await publisher.InitializeAsync();
}

app.Run();
