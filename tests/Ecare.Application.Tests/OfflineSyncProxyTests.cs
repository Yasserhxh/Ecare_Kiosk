using System.Net;
using System.Text;
using Ecare.Application.OfflineOrders;

namespace Ecare.Application.Tests;

public class OfflineSyncProxyTests
{
    [Fact]
    public async Task Sync_succes_relaie_le_corps_du_web_api()
    {
        const string body = """{"status":"Synchronisee","codeSapCommande":"0118113600","bonDeLivraison":"0050776500","error":null}""";
        var handler = new StubHandler(HttpStatusCode.OK, body);

        var result = await CreateProxy(handler).SyncAsync(29327);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/SapOffline/sync/29327", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(body, result.JsonBody);
    }

    [Fact]
    public async Task SyncAll_appelle_sync_all()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"total":0,"synchronisees":0,"erreurs":[]}""");

        var result = await CreateProxy(handler).SyncAllAsync();

        Assert.Equal("/api/SapOffline/sync-all", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(200, result.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Sync_erreurs_metier_4xx_relayees_telles_quelles(HttpStatusCode code)
    {
        const string body = """{"message":"Synchronisation déjà en cours."}""";

        var result = await CreateProxy(new StubHandler(code, body)).SyncAsync(1);

        Assert.Equal((int)code, result.StatusCode);
        Assert.Equal(body, result.JsonBody);
    }

    [Fact]
    public async Task Sync_erreur_5xx_masque_les_details_internes()
    {
        var result = await CreateProxy(new StubHandler(HttpStatusCode.InternalServerError, "System.Exception at C:\\secret")).SyncAsync(1);

        Assert.Equal(502, result.StatusCode);
        Assert.DoesNotContain("secret", result.JsonBody);
        Assert.Contains(OfflineSyncProxy.UpstreamErrorMessage, result.JsonBody);
    }

    [Fact]
    public async Task Sync_api_injoignable_retourne_503()
    {
        var result = await CreateProxy(new ThrowingHandler(new HttpRequestException("refused"))).SyncAsync(1);

        Assert.Equal(503, result.StatusCode);
        Assert.Contains(OfflineSyncProxy.UnreachableMessage, result.JsonBody);
    }

    [Fact]
    public async Task Sync_delai_depasse_retourne_504()
    {
        var result = await CreateProxy(new ThrowingHandler(new TaskCanceledException("timeout", new TimeoutException()))).SyncAsync(1);

        Assert.Equal(504, result.StatusCode);
        Assert.Contains(OfflineSyncProxy.TimeoutMessage, result.JsonBody);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Sync_id_invalide_leve(int id)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateProxy(new StubHandler(HttpStatusCode.OK, "{}")).SyncAsync(id));
    }

    [Fact]
    public void Statuts_en_attente_alignes_sur_le_web_api()
    {
        Assert.Equal(new[] { "EnAttente", "Erreur", "EnCours" }, OfflineOrderStatuses.Pending);
    }

    private static OfflineSyncProxy CreateProxy(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://webapi.test/") });

    private sealed class StubHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw ex;
    }
}
