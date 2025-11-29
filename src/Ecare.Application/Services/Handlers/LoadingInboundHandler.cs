using Dapper;
using Ecare.Application.Queries.LegendSnapshot;
using Ecare.Application.Services.Ecare.Application.Services;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ecare.Application.Services;

public sealed class LoadingOutboundOptions
{
    public string Hub { get; set; } = "loading_data_hub";
    public string Method { get; set; } = "LoadingDataEvent";
}

public sealed class LoadingInboundHandler : ISignalRInboundHandler
{
    private readonly ILogger<LoadingInboundHandler> _log;
    private readonly IServiceProvider _sp;
    private readonly ServiceManager _signalR;
    private readonly LoadingOutboundOptions _opt;
    private readonly IHttpClientFactory _http;


    public LoadingInboundHandler(
        ILogger<LoadingInboundHandler> log,
        IServiceProvider sp,
        ServiceManager signalR,
        IOptions<LoadingOutboundOptions> outOpt,
        IHttpClientFactory http)
    {
        _log = log;
        _sp = sp;
        _signalR = signalR;
        _opt = outOpt.Value;
        _http = http;
    }

    public async Task HandleAsync(object payload, CancellationToken ct)
    {
        // 1) Extract SLV
        string? slv = TryExtractCarteSlv(payload);
        if (string.IsNullOrWhiteSpace(slv))
        {
            _log.LogWarning("LoadingInboundHandler: missing SLV.");
            return;
        }

        // 2) Extract DeviceId
        string? deviceId = TryExtractDeviceId(payload);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _log.LogWarning("LoadingInboundHandler: missing deviceId for SLV={slv}", slv);
            return;
        }

        _log.LogInformation("LOADING SNAPSHOT: SLV={slv} device={dev}", slv, deviceId);

        using var scope = _sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // 🚀 NEW: Use your new LegendSnapshot handler
        var result = await mediator.Send(new LegendSnapshotQuery(slv), ct);

        if (!result.Success || result.Value is null)
        {
            _log.LogWarning("LoadingInboundHandler: no legend snapshot found for SLV={slv}", slv);
            return;
        }

        var vm = result.Value;

     
        // BUILD FINAL PAYLOAD 
        

        var payloadOut = new
        {
            @event = "LoadingDataEvent",
            site = "Asment-Temara-01",
            kiosk = deviceId,
            step = vm.Step,
            slv = vm.RFIDCard.ToString(),
            ts = DateTime.UtcNow,

            driver = new
            {
                name = vm.DriverFullName,
                plate = vm.Matricule
            },

            client = new
            {
                name = vm.ClientName,
                sapOk = true 
            },

            firstWeight = vm.PremierePoid,
            pabEntryAt = vm.PabEntryAt,
            sacnumber = vm.SacNumber,
            numberSacs_Charged = vm.NumberSacs_Charged,
            weight_Charged = vm.Weight_Charged,
            order = new
            {
                number = vm.BonDeCommande,
                destination = vm.Chantier,
                ligne = vm.Ligne,
                ligneImage = vm.LigneImage,
                typeCamion = vm.TypeCamion,

                produits = new[]
                {
                    new {
                        name = vm.Produit1,
                        quantity = vm.Quantite1,
                        imageUrl = vm.Produit1Image
                    },
                    new {
                        name = vm.Produit2,
                        quantity = vm.Quantite2,
                        imageUrl = vm.Produit2Image
                    }
                }
            }
        };

        // Broadcast to device
        await SignalRHelper.BroadcastToDeviceAsync(
            _signalR,
            _opt.Hub,
            _opt.Method,
            deviceId,
            payloadOut,
            _log,
            ct);
        //TODO: CONNECT TO LOCALHOST 5005 WITH DEVICE ID deviceId AND SEND IN A POST REQUEST /api/start-loading IN IT TO send premiere Poids premierePoid and convert vm.Quantite2 
        //public sealed record StartLoadingRequest(Guid DeviceId, int PremierePoid, double Quantite2, int QualityCode); 

        try
        {
            var client = _http.CreateClient("kiosk");

            var kioskUrl = "http://localhost:5005/api/start-loading";

            double quantite2Kg = (vm.Quantite2 ?? 0) * 1000;
            int QualityCode = ResolveQualityCode(vm.Produit1);

            // Build request payload
            var kioskRequest = new
            {
                deviceId = deviceId,
                premierePoid = vm.PremierePoid,
                quantite1 = quantite2Kg,
                qualityCode = QualityCode
            };

            _log.LogInformation(
                "Sending StartLoadingRequest to kiosk {url}: {@req}",
                kioskUrl, kioskRequest);

            var json = JsonSerializer.Serialize(kioskRequest);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(kioskUrl, content, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Kiosk StartLoadingRequest failed: {status} {reason}",
                    resp.StatusCode,
                    resp.ReasonPhrase);
            }
            else
            {
                _log.LogInformation("Kiosk StartLoadingRequest OK for device {dev}", deviceId);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Error while sending StartLoadingRequest to kiosk for device {dev}",
                deviceId);
        }

        _log.LogInformation("LOADING SNAPSHOT SENT: SLV={slv} device={dev}", slv, deviceId);
    }

    // Helpers
    private static string? TryExtractCarteSlv(object payload)
    {
        if (payload is JsonElement el)
        {
            if (el.TryGetProperty("carteSlv", out var v1)) return v1.GetString();
            if (el.TryGetProperty("slv", out var v2)) return v2.GetString();
        }

        return payload.GetType().GetProperty("carteSlv")?.GetValue(payload)?.ToString()
            ?? payload.GetType().GetProperty("slv")?.GetValue(payload)?.ToString();
    }

    private static string? TryExtractDeviceId(object payload)
    {
        if (payload is JsonElement el &&
            el.TryGetProperty("deviceId", out var v) &&
            v.ValueKind == JsonValueKind.String)
            return v.GetString();

        return payload.GetType().GetProperty("deviceId")
            ?.GetValue(payload)?.ToString();
    }

    private static int ResolveQualityCode(string? produit1)
    {
        if (string.IsNullOrWhiteSpace(produit1))
            return 0;

        string p = produit1.ToLowerInvariant();

        if (p.Contains("55")) return 55;
        if (p.Contains("65")) return 65;
        if (p.Contains("45")) return 45;

        return 0; // default
    }

}
