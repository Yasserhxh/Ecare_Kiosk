using Ecare.Api.Endpoints; // <-- for endpoint extensions
using Ecare.Api.Extensions; // <-- for AddSignalRListeners and AddRepositories
using Ecare.Application;
using Ecare.Application.Pipelines;
using Ecare.Application.Services;
using Ecare.Infrastructure;
using Ecare.Infrastructure.Persistence;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------------------------------------------------------
// General host config
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
// MediatR + Validation
// ---------------------------------------------------------
builder.Services.AddMediatR(m => m.RegisterServicesFromAssemblyContaining<IAssemblyMarker>());
builder.Services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// ---------------------------------------------------------
// Persistence
// ---------------------------------------------------------
builder.Services.AddDbContext<EcareDbContext>(options =>
    options.UseSqlServer(
        cfg.GetConnectionString("SqlServer"),
        b => b.MigrationsAssembly(typeof(EcareDbContext).Assembly.FullName)));

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(cfg.GetConnectionString("SqlServer")!));

builder.Services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

// ---------------------------------------------------------
// Auto-register Repositories + Infrastructure services
// ---------------------------------------------------------
builder.Services.AddRepositories();
builder.Services.AddInfrastructureServices();

// ---------------------------------------------------------
// Azure SignalR setup (concrete ServiceManager)
// ---------------------------------------------------------
builder.Services.AddSingleton<ServiceManager>(sp =>
{
    var cs = cfg.GetConnectionString("AzureSignalR")
        ?? throw new InvalidOperationException("ConnectionStrings:AzureSignalR missing");
    return new ServiceManagerBuilder()
        .WithOptions(o => o.ConnectionString = cs)
        .BuildServiceManager();
});

// If your listeners or negotiate paths use HttpClient, make sure this is present:
builder.Services.AddHttpClient();

// ---------------------------------------------------------
// Device registry + Nonce store (your own impls)
// ---------------------------------------------------------
builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
builder.Services.AddSingleton<INonceStore>(new NonceStore(TimeSpan.FromMinutes(10)));

// Admin key for /api/device/register
builder.Services.AddSingleton(new DeviceAdminKey(cfg["DeviceRegistry:AdminApiKey"] ?? "dev-admin-key"));

// ---------------------------------------------------------
// Custom Extension: Registers listeners etc.
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
// Map all endpoints
// ---------------------------------------------------------
app.MapKioskEndpoints();
app.MapOrderEndpoints();
app.MapPabEndpoints();
app.MapQueueEndpoints();
app.MapFluxEndpoints();
app.MapOtherEndpoints();

app.Run();
