using Ecare.Api.Endpoints; // <-- for endpoint extensions
using Ecare.Api.Extensions; // <-- for AddSignalRListeners and AddRepositories
using Ecare.Application;
using Ecare.Application.Auth.Commands;
using Ecare.Application.Auth.Queries;
using Ecare.Application.Auth.Services;
using Ecare.Application.Pipelines;
using Ecare.Application.Services;
using Ecare.Domain.Entities;
using Ecare.Infrastructure;
using Ecare.Infrastructure.Persistence;
using Ecare.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.SignalR.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Ecare API", Version = "v1" });

    // JWT Bearer auth for Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer", // 👈 must be lowercase
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token. Example: Bearer {token}"
        // You can also say: "Enter only the token value" if you want them NOT to type 'Bearer '
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


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
// Auto-register Repositories + Infrastructure services
// ---------------------------------------------------------
builder.Services.AddRepositories();
builder.Services.AddInfrastructureServices();

// Identity first
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // optional basic rules
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<EcareDbContext>()
    .AddDefaultTokenProviders();

// Then Authentication with explicit defaults
// Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<EcareDbContext>()
    .AddDefaultTokenProviders();


// Authentication - FORCE JwtBearer as default
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; 
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg["Jwt:Issuer"],       // must be "EcareApi"
            ValidAudience = cfg["Jwt:Audience"],   // must be "EcareClients"
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)
            ),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<JwtTokenService>();




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

app.UseAuthentication();   // enables [Authorize]
app.UseAuthorization();    // allows policy/role checks

// ---------------------------------------------------------
// Map all endpoints via extension methods
// ---------------------------------------------------------
app.MapKioskEndpoints();
app.MapOrderEndpoints();
app.MapPabEndpoints();
app.MapQueueEndpoints();
app.MapFluxEndpoints();
app.MapOtherEndpoints();
app.MapLigneEndpoints();
app.MapDeviceEndpoints();
app.MapEcareEngineEndpoints();
app.MapAuthEndpoints();







app.Run();