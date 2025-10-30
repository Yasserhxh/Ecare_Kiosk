using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;

namespace Ecare.Api.Controllers;

[ApiController]
[Route("signalr")]
public class SignalRController : ControllerBase
{
    private readonly string _connString;

    public SignalRController(IConfiguration cfg)
    {
        _connString = cfg.GetConnectionString("AzureSignalR")
                      ?? throw new InvalidOperationException("ConnectionStrings:AzureSignalR missing");
    }

    /// <summary>
    /// Serverless negotiate endpoint.
    /// Example: GET /signalr/negotiate?hub=slv_hub
    /// Returns { url, accessToken }
    /// </summary>
    [HttpGet("negotiate")]
    public async Task<IActionResult> Negotiate([FromQuery] string hub, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hub)) return BadRequest("hub is required");

        // Build ServiceManager once per request (or DI/singleton it if you prefer)
        var manager = new ServiceManagerBuilder()
            .WithOptions(o => o.ConnectionString = _connString)
            .BuildServiceManager();

        // These two helpers are provided by the Management SDK
        
        var accessToken = await manager.CreateHubContextAsync(hub, ct);

        return Ok(new { accessToken });
    }
}
