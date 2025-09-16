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
    options.UseSqlServer(cfg.GetConnectionString("SqlServer")));
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(cfg.GetConnectionString("SqlServer")!));
builder.Services.AddScoped<IUnitOfWork, DapperUnitOfWork>();

// Repos + Printing
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IWeighRepository, WeighRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddSingleton<IBlPrinter, MockBlPrinter>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// Endpoints
app.MapPost("/kiosk/scan", async (ScanBySlvQuery q, IMediator m) => await m.Send(q));
app.MapPost("/orders/confirm", async (ConfirmOrderCommand c, IMediator m) => await m.Send(c));
app.MapPost("/orders/cancel", async (CancelOrderCommand c, IMediator m) => await m.Send(c));
app.MapPost("/pab1/weigh", async (RecordPab1WeighCommand c, IMediator m) => await m.Send(c));
app.MapPost("/line/start", async (StartLoadingCommand c, IMediator m) => await m.Send(c));
app.MapPost("/pab2/weigh-bl", async (RecordPab2AndIssueBlCommand c, IMediator m) => await m.Send(c));

app.Run();
