using Ecare.Application;
using Ecare.Application.Commands;
using Ecare.Application.Pipelines;
using Ecare.Application.Queries;
using Ecare.Infrastructure;
using Ecare.Infrastructure.Printing;
using Ecare.Infrastructure.Persistence;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// --- CORS
const string ViteDev = "ViteDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(ViteDev, policy =>
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromHours(1))
    // .AllowCredentials() // enable only if we truly use cookies
    );
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MediatR + FluentValidation
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<IAssemblyMarker>();
});
builder.Services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Persistence
builder.Services.AddDbContext<EcareDbContext>(options =>
    options.UseSqlServer
    (
        cfg.GetConnectionString("SqlServer"),
        b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName)
    ));
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(cfg.GetConnectionString("SqlServer")!));

builder.Services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

// Repos + Printing
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // <<< BEFORE auth and endpoints
}

app.UseCors(ViteDev);

// Ensure preflight never blocked (safe in dev)


// Endpoints
app.MapPost("/kiosk/scan", async (ScanBySlvQuery q, IMediator m) => await m.Send(q));
app.MapPost("/orders/confirm", async (ConfirmOrderCommand c, IMediator m) => await m.Send(c));
app.MapPost("/orders/cancel", async (CancelOrderCommand c, IMediator m) => await m.Send(c));
app.MapPost("/pab1/weigh", async (RecordPab1WeighCommand c, IMediator m) => await m.Send(c));
app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) => await m.Send(c));
app.MapPost("/pab2/weigh-bl", async (RecordPab2AndIssueBlCommand c, IMediator m) => await m.Send(c));
app.MapGet("/catalog/items", async (IMediator m, CancellationToken ct) => await m.Send(new GetCimentsQuery(), ct));
app.MapPost("/orders", async (CreateOrderAtKioskCommand c, IMediator m, CancellationToken ct) => await m.Send(c, ct));
app.MapPost("/orders/legacy", async (CreateLegacyOrderCommand c, IMediator m, CancellationToken ct) => await m.Send(c, ct));

app.Run();
