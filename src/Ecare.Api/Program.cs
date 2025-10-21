using System;
using System.Linq;
using System.Text;
using Ecare.Api.Options;
using Ecare.Api.Services;
using Ecare.Application;
using Ecare.Application.Commands;
using Ecare.Application.Pipelines;
using Ecare.Application.Queries;
using Ecare.Domain.Interfaces;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure;
using Ecare.Infrastructure.Persistence;
using Ecare.Infrastructure.Printing;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Capture startup errors ----------
builder.WebHost.CaptureStartupErrors(true).UseSetting("detailedErrors", "true");

// ---------- Logging ----------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ---------- Diagnostic mode toggle ----------
var diagEnabled = cfg.GetValue("Diagnostic:Enabled", false);

// ---------- CORS ----------
const string ViteDev = "ViteDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(ViteDev, policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetPreflightMaxAge(TimeSpan.FromHours(1)));
});

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- MediatR + Validation ----------
builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<IAssemblyMarker>());
builder.Services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// ---------- Persistence (guarded) ----------
var sqlCs = cfg.GetConnectionString("SqlServer");
builder.Services.AddDbContext<EcareDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(sqlCs))
    {
        Console.WriteLine("[Startup] WARNING: SqlServer connection string missing. Using InMemory so app can boot.");
        //options.UseInMemoryDatabase("BootOnly_InMemory");
    }
    else
    {
        options.UseSqlServer(sqlCs, b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName));
    }
});
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(sqlCs ?? string.Empty));
builder.Services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

// ---------- Repositories + Printing ----------
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

// ---------- SignalR Infra + Listener (gated) ----------
builder.Services.Configure<RfidListenerOptions>(cfg.GetSection("RfidListener"));
var listenerEnabled = cfg.GetValue("RfidListener:Enabled", false);
var signalRConn = cfg["SignalR:ConnectionString"];
var canWireSignalR = !string.IsNullOrWhiteSpace(signalRConn);

if (!diagEnabled && canWireSignalR)
{
    try
    {
        builder.Services.AddSignalRNegotiation(builder.Configuration);
        if (listenerEnabled)
        {
            builder.Services.AddHostedService<RfidSignalRListener>();
            Console.WriteLine("[Startup] RfidSignalRListener ENABLED.");
        }
        else
        {
            Console.WriteLine("[Startup] RfidSignalRListener DISABLED via config.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Startup] ERROR wiring SignalR (continuing): {ex}");
    }
}
else if (!canWireSignalR)
{
    Console.WriteLine("[Startup] SignalR:ConnectionString missing. Skipping negotiator/listener.");
    builder.Services.AddSingleton<ISignalRNegotiator>(_ => new NoopNegotiator());
}

var app = builder.Build();

// ---------- Exception -> JSON (don’t leak HTML) ----------
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { error = "Unhandled exception", path = ctx.Request.Path });
    });
});

// ---------- Forwarded headers (prevent HTTPS redirect loops) ----------
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost |
                       ForwardedHeaders.XForwardedFor,
    RequireHeaderSymmetry = false
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

// Optional PathBase
var pathBase = cfg["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
    if (!pathBase.StartsWith("/")) pathBase = "/" + pathBase;
    app.UsePathBase(pathBase);
    Console.WriteLine($"[Startup] PathBase = '{pathBase}'");
}

// HTTPS redirect (can be disabled via config if needed)
if (cfg.GetValue("HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}

// ---------- Swagger ----------
app.UseSwagger(c => c.RouteTemplate = "swagger/{documentName}/swagger.json");
app.UseSwaggerUI(c =>
{
    var bp = pathBase ?? string.Empty;
    c.SwaggerEndpoint($"{bp}/swagger/v1/swagger.json", "Ecare API v1");
    c.RoutePrefix = "swagger";
});

// Redirect root to Swagger (unless in diag mode)
if (!diagEnabled)
    app.MapGet("/", () => Results.Redirect($"{(string.IsNullOrEmpty(pathBase) ? "" : pathBase)}/swagger"));

// ---------- CORS ----------
app.UseCors(ViteDev);

// ---------- DIAGNOSTIC MODE ----------
if (diagEnabled)
{
    // Minimal endpoints to prove the app serves responses
    app.MapGet("/diag", (HttpContext ctx) =>
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ecare API — Diagnostic Mode (set Diagnostic:Enabled=false to disable)");
        sb.AppendLine($"Time UTC: {DateTime.UtcNow:o}");
        sb.AppendLine($"Environment: {app.Environment.EnvironmentName}");
        sb.AppendLine($"Scheme: {ctx.Request.Scheme}");
        sb.AppendLine($"Host: {ctx.Request.Host}");
        sb.AppendLine($"PathBase: '{ctx.Request.PathBase}'  Path: '{ctx.Request.Path}'");
        sb.AppendLine($"Forwarded: proto='{ctx.Request.Headers["X-Forwarded-Proto"]}', host='{ctx.Request.Headers["X-Forwarded-Host"]}', for='{ctx.Request.Headers["X-Forwarded-For"]}'");
        sb.AppendLine($"hasSql: {!string.IsNullOrWhiteSpace(sqlCs)}");
        sb.AppendLine($"hasSignalRConn: {!string.IsNullOrWhiteSpace(signalRConn)}");
        sb.AppendLine($"listenerEnabled: {listenerEnabled}");
        sb.AppendLine($"User: {ctx.User?.Identity?.AuthenticationType ?? "(none)"}  Authenticated: {ctx.User?.Identity?.IsAuthenticated ?? false}");
        sb.AppendLine("Headers:");
        foreach (var h in ctx.Request.Headers.OrderBy(h => h.Key))
            sb.AppendLine($"  {h.Key}: {h.Value}");
        return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
    });

    app.MapGet("/", () => Results.Text("Diagnostic mode is ON. See /diag", "text/plain"));
}
else
{
    // ---------- Normal endpoints ----------
    app.MapPost("/kiosk/scan", async (ScanBySlvQuery q, IMediator m) => await m.Send(q));
    app.MapPost("/orders/confirm", async (ConfirmOrderCommand c, IMediator m) => await m.Send(c));
    app.MapPost("/orders/cancel", async (CancelOrderCommand c, IMediator m) => await m.Send(c));
    app.MapPost("/pab1/weigh", async (RecordPab1WeighCommand c, IMediator m) => await m.Send(c));
    app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) => await m.Send(c));
    app.MapPost("/pab2/weigh-bl", async (RecordPab2AndIssueBlCommand c, IMediator m) => await m.Send(c));
    app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) => await m.Send(new GetCimentsQuery(), ct));
    app.MapPost("/orders", async (CreateOrderAtKioskCommand c, IMediator m, CancellationToken ct) => await m.Send(c, ct));
    app.MapPost("/orders/legacy", async (CreateLegacyOrderCommand c, IMediator m, CancellationToken ct) => await m.Send(c, ct));

    // /negotiate
    app.MapMethods("/negotiate", new[] { "GET", "POST", "OPTIONS" },
        async (HttpContext ctx, ISignalRNegotiator negotiator, CancellationToken ct) =>
        {
            if (HttpMethods.IsOptions(ctx.Request.Method))
                return Results.StatusCode(StatusCodes.Status204NoContent);

            try
            {
                var hub = ctx.Request.Query["hub"].ToString();
                var res = await negotiator.NegotiateAsync(new NegotiateRequest
                {
                    HubName = string.IsNullOrWhiteSpace(hub) ? null : hub
                }, ct);

                return Results.Json(new { url = res.Url, accessToken = res.AccessToken, hub = res.Hub });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 500);
            }
        });
}

// ---------- Extra tiny probes ----------
app.MapGet("/ping", () => Results.Text("pong", "text/plain")).ExcludeFromDescription();
app.MapGet("/scheme", (HttpContext ctx) => Results.Ok(new
{
    scheme = ctx.Request.Scheme,
    host = ctx.Request.Host.ToString(),
    xfp = ctx.Request.Headers["X-Forwarded-Proto"].ToString(),
    xfh = ctx.Request.Headers["X-Forwarded-Host"].ToString()
})).ExcludeFromDescription();

app.Run();

// ---------- Fallback negotiator ----------
sealed class NoopNegotiator : ISignalRNegotiator
{
    public Task<NegotiateResult> NegotiateAsync(NegotiateRequest request, CancellationToken ct = default)
        => Task.FromResult(new NegotiateResult
        {
            Url = string.Empty,
            AccessToken = string.Empty,
            Hub = request.HubName ?? "slv_hub"
        });
}
