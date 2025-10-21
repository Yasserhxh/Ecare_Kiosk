using Ecare.Application;
using Ecare.Application.Commands;
using Ecare.Application.Queries;
using Ecare.Domain.Interfaces;     // ISignalRNegotiator
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure; // NegotiateRequest
using MediatR;

var builder = WebApplication.CreateBuilder(args);
 
//Register everything from Infrastructure (DB, Repos, SignalR, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

//Register MediatR (so your handlers work)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IAssemblyMarker>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string AllowAll = "AllowAll";
builder.Services.AddCors(opts =>
{
    opts.AddPolicy(AllowAll, p =>
        p.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(AllowAll);



app.MapGet("/", () => Results.Redirect("/swagger"));




// ------------------- Minimal API endpoints -------------------

app.MapPost("/kiosk/scan", async (ScanBySlvQuery q, IMediator m) =>
    await m.Send(q));

app.MapPost("/orders/confirm", async (ConfirmOrderCommand c, IMediator m) =>
    await m.Send(c));

app.MapPost("/orders/cancel", async (CancelOrderCommand c, IMediator m) =>
    await m.Send(c));

app.MapPost("/pab1/weigh", async (RecordPab1WeighCommand c, IMediator m) =>
    await m.Send(c));

app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) =>
    await m.Send(c));

app.MapPost("/pab2/weigh-bl", async (RecordPab2AndIssueBlCommand c, IMediator m) =>
    await m.Send(c));

// catalog (GET)
app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) =>
    await m.Send(new GetCimentsQuery(), ct));

// orders (POST)
app.MapPost("/orders", async (CreateOrderAtKioskCommand c, IMediator m, CancellationToken ct) =>
    await m.Send(c, ct));

// legacy orders (POST)
app.MapPost("/orders/legacy", async (CreateLegacyOrderCommand c, IMediator m, CancellationToken ct) =>
    await m.Send(c, ct));

// SignalR negotiate (GET/POST/OPTIONS)
app.MapMethods("/negotiate", new[] { "GET", "POST", "OPTIONS" },
    async (HttpContext ctx, ISignalRNegotiator negotiator, CancellationToken ct) =>
    {
        if (HttpMethods.IsOptions(ctx.Request.Method))
            return Results.StatusCode(StatusCodes.Status204NoContent);

        var hub = ctx.Request.Query["hub"].ToString();
        var res = await negotiator.NegotiateAsync(new NegotiateRequest
        {
            HubName = string.IsNullOrWhiteSpace(hub) ? null : hub
        }, ct);

        return Results.Json(new { url = res.Url, accessToken = res.AccessToken, hub = res.Hub });
    });

// ------------------------------------------------------------

app.Run();
