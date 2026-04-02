using Ecare.Domain.Inerface.Sync;
using System.Text;
using System.Text.Json;

namespace Ecare.Infrastructure.Services.Sync;

public sealed class ClientLivraisonApi : IClientLivraisonApi
{
    private readonly HttpClient _httpClient;

    public ClientLivraisonApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ExternalLivraisonModel>> GetLivraisonsAsync(
        string customerNumber,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            customerNumber,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd")
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/Client/Livraisons?pageNumber=1&pageSize=1000");

        request.Headers.Accept.ParseAdd("text/plain");
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json-patch+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(content);

        if (!doc.RootElement.TryGetProperty("allData", out var allData))
            return Array.Empty<ExternalLivraisonModel>();

        var result = new List<ExternalLivraisonModel>();

        foreach (var item in allData.EnumerateArray())
        {
            var codeCommande = item.TryGetProperty("code_Commande_Sap", out var codeProp)
                ? codeProp.GetString()
                : null;

            var numeroLivraison = item.TryGetProperty("numéro_Livraison", out var livraisonProp)
                ? livraisonProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(codeCommande) || string.IsNullOrWhiteSpace(numeroLivraison))
                continue;

            result.Add(new ExternalLivraisonModel
            {
                CodeCommandeSap = codeCommande!.Trim(),
                NumeroLivraison = numeroLivraison!.Trim()
            });
        }

        return result;
    }
}