using Azure.Storage.Blobs;
using Ecare.Api.Endpoints;
using Ecare.Api.Extensions;
using Ecare.Application;
using Ecare.Application.Auth.Services;
using Ecare.Application.Pipelines;
using Ecare.Application.Queries;
using Ecare.Application.Queries.PartnerTruck;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure;
using Ecare.Infrastructure.Persistence;
using Ecare.Infrastructure.Storage;
using Ecare.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.SignalR.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

try
{
    var builder = WebApplication.CreateBuilder(args);
    var cfg = builder.Configuration;

    Console.WriteLine("=== Starting Ecare API ===");

    // ---------------------------------------------------------
    // CORS + Swagger
    // ---------------------------------------------------------
    const string ViteDev = "ViteDev";  // <-- Define this BEFORE using it
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(ViteDev, policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetPreflightMaxAge(TimeSpan.FromHours(1)));
    });

    Console.WriteLine("✓ CORS configured");

    // ---------------------------------------------------------
    // General host config: show all DI problems during startup
    // ---------------------------------------------------------
    builder.Host.UseDefaultServiceProvider(opt =>
    {
        opt.ValidateScopes = true;
        opt.ValidateOnBuild = true;
    });

    Console.WriteLine("✓ Service provider configured");

    builder.Services.Configure<BlobStorageOptions>(
        builder.Configuration.GetSection("BlobStorage"));

    Console.WriteLine("✓ BlobStorageOptions configured");

    // FIX: Proper multi-line lambda syntax
    builder.Services.AddSingleton<BlobServiceClient>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
        Console.WriteLine($"BlobStorage ConnectionString: {(options.ConnectionString?.Length > 0 ? "Present" : "Missing")}");
        return new BlobServiceClient(options.ConnectionString);
    });

    Console.WriteLine("✓ BlobServiceClient registered");

    builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

    Console.WriteLine("✓ IBlobStorageService registered");

    // ---------------------------------------------------------
    // Swagger
    // ---------------------------------------------------------
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Ecare API", Version = "v1" });

        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token. Example: Bearer {token}"
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

    Console.WriteLine("✓ Swagger configured");

    // ---------------------------------------------------------
    // MediatR + Validation Pipelines
    // ---------------------------------------------------------
    builder.Services.AddMediatR(m => m.RegisterServicesFromAssemblyContaining<IAssemblyMarker>());
    builder.Services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

    Console.WriteLine("✓ MediatR configured");

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

    Console.WriteLine("✓ Database configured");

    // ---------------------------------------------------------
    // Auto-register Repositories + Infrastructure services
    // ---------------------------------------------------------
    builder.Services.AddRepositories();
    builder.Services.AddInfrastructureServices();

    Console.WriteLine("✓ Repositories registered");

    // ---------------------------------------------------------
    // Identity & Authentication
    // ---------------------------------------------------------
    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
        .AddEntityFrameworkStores<EcareDbContext>()
        .AddDefaultTokenProviders();

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
                ValidIssuer = cfg["Jwt:Issuer"],
                ValidAudience = cfg["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)
                ),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddScoped<JwtTokenService>();

    Console.WriteLine("✓ Authentication configured");

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

    builder.Services.AddSignalRListeners(cfg);

    Console.WriteLine("✓ SignalR configured");

    // ---------------------------------------------------------
    // Build + middleware
    // ---------------------------------------------------------
    Console.WriteLine("✓ Building application...");
    var app = builder.Build();

    Console.WriteLine("✓ Application built successfully");

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(ViteDev);

    app.UseAuthentication();
    app.UseAuthorization();

    // ---------------------------------------------------------
    // Map all endpoints
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
    app.MapBlobEndpoints();
    app.MapDriversEndpoints();
    app.MapTruckEndpoints();
    app.MapPartnerEndpoints();
    app.MapMobileAppEndpoints();
    app.MapCementMatrixEndpoints();
    app.MapLegendOrderEndpoints();
    app.MapPartnerTruckEndpoints();
    app.MapChantierEndpoints();
    app.MapProduitEndpoints();

    app.MapGet("/time", () =>
    {
        return new
        {
            ServerLocalTime = DateTime.Now,
            ServerUtc = DateTime.UtcNow,
            Timezone = TimeZoneInfo.Local.StandardName
        };
    });


    app.MapGet("mobile/charging/details/{fluxid:int}", async (
            int fluxid,
            ISender sender,
            CancellationToken ct) =>
    {
        var query = new GetChargingDetailsQuery(fluxid);
        var result = await sender.Send(query, ct);

        if (!result.Success)
        {
            return Results.BadRequest(new
            {
                success = false,
                message = result.Error
            });
        }

        return Results.Ok(new
        {
            success = true,
            data = result.Value
        });
    })
        .WithName("GetMobileChargingDetails");

   




    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"!!! STARTUP FAILED !!!");
    Console.WriteLine($"Exception: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }

    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
    throw;
}