using System.Net;
using System.Text;
using Dapper;
using Ecare.Application.Services.Alerts;
using Ecare.Infrastructure.Email;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Ecare.Api.HostedServices;

/// <summary>
/// Surveille les camions restés trop longtemps dans l'usine (de la 1ère pesée
/// jusqu'à la 2ème pesée) et envoie une alerte e-mail (Azure AD / Graph) au-delà
/// du seuil configuré. Une seule alerte par legend (journal Ecare_Overstay_Alert_Log).
/// </summary>
public sealed class OverstayAlertBackgroundService : BackgroundService
{
    private readonly IConfiguration _cfg;
    private readonly IEmailSender _email;
    private readonly OverstayAlertOptions _opt;
    private readonly ILogger<OverstayAlertBackgroundService> _logger;
    private readonly TimeZoneInfo _tz;

    public OverstayAlertBackgroundService(
        IConfiguration cfg,
        IEmailSender email,
        IOptions<OverstayAlertOptions> opt,
        ILogger<OverstayAlertBackgroundService> logger)
    {
        _cfg = cfg;
        _email = email;
        _opt = opt.Value;
        _logger = logger;
        _tz = ResolveMoroccoTimeZone(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_opt.Enabled)
        {
            _logger.LogInformation("OverstayAlert désactivé (OverstayAlert:Enabled = false).");
            return;
        }

        // Laisser l'application finir de démarrer avant le 1er scan.
        try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
        catch (OperationCanceledException) { return; }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _opt.PollIntervalMinutes));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur pendant le scan des camions en dépassement.");
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            _logger.LogWarning("Chaîne de connexion 'SqlServer' absente — scan dépassement ignoré.");
            return;
        }

        var (to, cc) = OverstayAlertRules.SplitRecipients(_opt.Recipients);
        if (string.IsNullOrEmpty(to))
        {
            _logger.LogWarning("Aucun destinataire configuré (OverstayAlert:Recipients) — scan ignoré.");
            return;
        }

        var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _tz);

        await using var conn = new SqlConnection(connStr);

        // Camions entrés (1ère pesée), pas encore sortis (2ème pesée), non annulés,
        // au-delà du seuil, et pas déjà alertés.
        const string selectSql = @"
            SELECT Id, ClientName, Chantier, Matricule, TransporteurName, ChauffeurName,
                   Produit1, Quantite1, PabEntryAt
            FROM dbo.Ecare_Order_Legend
            WHERE PabEntryAt IS NOT NULL
              AND DeuxiemePoid IS NULL
              AND (AnnulationCommercial IS NULL OR AnnulationCommercial = 0)
              AND (Status IS NULL OR Status NOT IN ('Completed', 'Canceled'))
              AND DATEDIFF(MINUTE, PabEntryAt, @NowLocal) > @Threshold
              AND NOT EXISTS (SELECT 1 FROM dbo.Ecare_Overstay_Alert_Log l WHERE l.LegendId = dbo.Ecare_Order_Legend.Id)";

        var rows = (await conn.QueryAsync<OverstayRow>(
            new CommandDefinition(selectSql,
                new { NowLocal = nowLocal, Threshold = _opt.ThresholdMinutes },
                cancellationToken: ct))).ToList();

        if (rows.Count == 0) return;

        _logger.LogInformation("Dépassement : {Count} camion(s) au-delà de {Threshold} min à alerter.",
            rows.Count, _opt.ThresholdMinutes);

        foreach (var r in rows)
        {
            // Verrou = claim par insertion (PK LegendId) : dédoublonne même multi-instances.
            try
            {
                var claimed = await conn.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO dbo.Ecare_Overstay_Alert_Log (LegendId, SentAt) VALUES (@Id, @SentAt)",
                    new { r.Id, SentAt = nowLocal },
                    cancellationToken: ct));
                if (claimed == 0) continue;
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                // Déjà alerté par un autre tick/instance → on passe.
                continue;
            }

            var elapsed = OverstayAlertRules.ElapsedMinutes(r.PabEntryAt, nowLocal);
            var subject = $"⚠ Camion en dépassement (> {_opt.ThresholdMinutes} min) — {r.Matricule ?? "?"} — {elapsed} min dans l'usine";
            var body = BuildHtmlBody(r, elapsed, nowLocal);

            var ok = await _email.SendHtmlAsync(_opt.Sender, to, cc, subject, body, highPriority: true, ct);
            if (!ok)
            {
                // L'envoi a échoué ; le claim reste en place (une seule alerte, pas de spam).
                _logger.LogWarning("Alerte non délivrée pour la legend {LegendId} ({Matricule}).", r.Id, r.Matricule);
            }
        }
    }

    private string BuildHtmlBody(OverstayRow r, int elapsedMinutes, DateTime nowLocal)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? "—");
        var qty = r.Quantite1.HasValue ? r.Quantite1.Value.ToString("0.##") : "—";

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><body style=\"font-family:Segoe UI,Arial,sans-serif;\">");
        sb.Append("<h2 style=\"color:#b00020;\">⚠ Camion en dépassement dans l'usine</h2>");
        sb.Append($"<p>Le camion <b>{E(r.Matricule)}</b> est présent dans l'usine depuis <b>{elapsedMinutes} minutes</b> " +
                  $"(seuil : {_opt.ThresholdMinutes} min), sans 2ème pesée enregistrée.</p>");
        sb.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;\">");
        Row(sb, "Matricule", E(r.Matricule));
        Row(sb, "Transporteur", E(r.TransporteurName));
        Row(sb, "Chauffeur", E(r.ChauffeurName));
        Row(sb, "Client", E(r.ClientName));
        Row(sb, "Chantier", E(r.Chantier));
        Row(sb, "Produit", E(r.Produit1));
        Row(sb, "Quantité", E(qty));
        Row(sb, "1ère pesée", r.PabEntryAt.ToString("dd/MM/yyyy HH:mm"));
        Row(sb, "Durée dans l'usine", $"{elapsedMinutes} min");
        sb.Append("</table>");
        sb.Append($"<p><small>Alerte générée le {nowLocal:dd/MM/yyyy HH:mm} (heure Maroc) — ID legend {r.Id}.</small></p>");
        sb.Append("</body></html>");
        return sb.ToString();

        static void Row(StringBuilder b, string k, string v)
            => b.Append($"<tr><td style=\"background:#f2f2f2;\"><b>{k}</b></td><td>{v}</td></tr>");
    }

    private static TimeZoneInfo ResolveMoroccoTimeZone(ILogger logger)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca"); }
            catch (TimeZoneNotFoundException ex)
            {
                logger.LogWarning(ex, "Fuseau Maroc introuvable, UTC utilisé comme repli.");
                return TimeZoneInfo.Utc;
            }
        }
    }

    private sealed class OverstayRow
    {
        public int Id { get; set; }
        public string? ClientName { get; set; }
        public string? Chantier { get; set; }
        public string? Matricule { get; set; }
        public string? TransporteurName { get; set; }
        public string? ChauffeurName { get; set; }
        public string? Produit1 { get; set; }
        public decimal? Quantite1 { get; set; }
        public DateTime PabEntryAt { get; set; }
    }
}
