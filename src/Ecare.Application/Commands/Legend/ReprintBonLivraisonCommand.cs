using Ecare.Application.Services;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace Ecare.Application.Commands.Legend;

public sealed record ReprintBonLivraisonCommand(int Id) : IRequest<Result<BlJson>>;

public sealed class ReprintBonLivraisonHandler
    : IRequestHandler<ReprintBonLivraisonCommand, Result<BlJson>>
{
    private readonly ILogger<ReprintBonLivraisonHandler> _log;
    private readonly ServiceManager _signalR;
    private readonly PrintOutboundOptions _opt;
    private readonly HttpClient _http;

    public ReprintBonLivraisonHandler(
        ILogger<ReprintBonLivraisonHandler> log,
        ServiceManager signalR,
        IOptions<PrintOutboundOptions> opt,
        IHttpClientFactory httpFactory)
    {
        _log = log;
        _signalR = signalR;
        _opt = opt.Value;
        _http = httpFactory.CreateClient("SapShipment");
    }

    public async Task<Result<BlJson>> Handle(ReprintBonLivraisonCommand request, CancellationToken ct)
    {
        if (request.Id <= 0)
            return Result<BlJson>.Fail("INVALID_LEGEND_ID");

        try
        {
            var response = await _http.GetAsync(
                $"https://app-emea-we-dssprod-dss-001.azurewebsites.net/api/SapShipment/livraison/{request.Id}",
                ct);

            if (response.StatusCode == HttpStatusCode.Accepted ||
                response.StatusCode == HttpStatusCode.NotFound)
            {
                _log.LogWarning(
                    "Cannot reprint BL because no existing SAP livraison was found for LegendId={Id}. Status={Status}",
                    request.Id,
                    response.StatusCode);

                return Result<BlJson>.Fail("SAP_LIVRAISON_NOT_FOUND");
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError(
                    "SAP livraison reprint lookup failed for LegendId={Id}, Status={Status}",
                    request.Id,
                    response.StatusCode);

                return Result<BlJson>.Fail("SAP_API_ERROR");
            }

            var shipment = await response.Content.ReadFromJsonAsync<BlJson>(cancellationToken: ct);
            if (shipment is null)
                return Result<BlJson>.Fail("SAP_EMPTY_RESPONSE");

            await SignalRHelper.BroadcastAsync(
                _signalR,
                _opt.Hub,
                _opt.Method,
                shipment,
                _log,
                ct);

            return Result<BlJson>.Ok(shipment);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error reprinting BL for LegendId={Id}", request.Id);
            return Result<BlJson>.Fail("UNEXPECTED_ERROR");
        }
    }
}
