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

        var thresholds = (_opt.Thresholds ?? new List<int>()).Where(t => t > 0).Distinct().OrderBy(t => t).ToList();
        if (thresholds.Count == 0)
        {
            _logger.LogWarning("Aucun palier configuré (OverstayAlert:Thresholds) — scan ignoré.");
            return;
        }
        var minThreshold = thresholds[0];

        var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _tz);

        await using var conn = new SqlConnection(connStr);

        // Camions entrés (1ère pesée), pas encore sortis (2ème pesée), non annulés, au-delà
        // du plus petit palier. SentStagesCsv = paliers déjà notifiés pour ce camion.
        const string selectSql = @"
            SELECT l.Id, l.ClientName, l.Chantier, l.Matricule, l.RFIDCard,
                   l.TransporteurName, l.ChauffeurName,
                   l.Produit1, l.Quantite1, l.Produit2, l.Quantite2, l.PabEntryAt,
                   (SELECT STRING_AGG(CAST(a.ThresholdMinutes AS varchar(10)), ',')
                      FROM dbo.Ecare_Overstay_Alert_Log a WHERE a.LegendId = l.Id) AS SentStagesCsv
            FROM dbo.Ecare_Order_Legend l
            WHERE l.PabEntryAt IS NOT NULL
              AND l.DeuxiemePoid IS NULL
              AND (l.AnnulationCommercial IS NULL OR l.AnnulationCommercial = 0)
              AND (l.Status IS NULL OR l.Status NOT IN ('Completed', 'Canceled'))
              AND DATEDIFF(MINUTE, l.PabEntryAt, @NowLocal) > @MinThreshold";

        var rows = (await conn.QueryAsync<OverstayRow>(
            new CommandDefinition(selectSql,
                new { NowLocal = nowLocal, MinThreshold = minThreshold },
                cancellationToken: ct))).ToList();

        if (rows.Count == 0) return;

        foreach (var r in rows)
        {
            var alreadySent = ParseStages(r.SentStagesCsv);
            var due = OverstayAlertRules.StagesToSend(r.PabEntryAt, null, nowLocal, thresholds, alreadySent);
            if (due.Count == 0) continue;

            var elapsed = OverstayAlertRules.ElapsedMinutes(r.PabEntryAt, nowLocal);

            foreach (var stage in due)
            {
                // Verrou = claim par insertion (PK LegendId+ThresholdMinutes) : dédoublonne
                // même en multi-instances / multi-ticks.
                try
                {
                    var claimed = await conn.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO dbo.Ecare_Overstay_Alert_Log (LegendId, ThresholdMinutes, SentAt) VALUES (@Id, @Stage, @SentAt)",
                        new { r.Id, Stage = stage, SentAt = nowLocal },
                        cancellationToken: ct));
                    if (claimed == 0) continue;
                }
                catch (SqlException ex) when (ex.Number is 2627 or 2601)
                {
                    continue; // palier déjà alerté ailleurs
                }

                var subject = $"⚠ Camion en dépassement (> {stage} min) — {r.Matricule ?? "?"} — {elapsed} min dans l'usine";
                var body = BuildHtmlBody(r, stage, elapsed, nowLocal);

                var ok = await _email.SendHtmlAsync(_opt.Sender, to, cc, subject, body, highPriority: true, ct);
                if (!ok)
                    _logger.LogWarning("Alerte {Stage} min non délivrée pour la legend {LegendId} ({Matricule}).",
                        stage, r.Id, r.Matricule);
            }
        }
    }

    private static ISet<int> ParseStages(string? csv)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(csv)) return set;
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var v)) set.Add(v);
        return set;
    }

    private string BuildHtmlBody(OverstayRow r, int stageMinutes, int elapsedMinutes, DateTime nowLocal)
    {
        string E(string? s) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(s) ? "—" : s);
        string Article(string? produit, decimal? qte)
        {
            if (string.IsNullOrWhiteSpace(produit)) return "—";
            var q = qte.HasValue ? qte.Value.ToString("0.##") : "—";
            return $"{E(produit)} — {q}";
        }

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><body style=\"font-family:Segoe UI,Arial,sans-serif;\">");
        sb.Append("<h2 style=\"color:#b00020;\">⚠ Camion en dépassement dans l'usine</h2>");
        sb.Append($"<p>Le camion <b>{E(r.Matricule)}</b> est dans l'usine depuis <b>{elapsedMinutes} minutes</b> " +
                  $"(palier <b>{stageMinutes} min</b> franchi), sans 2ème pesée enregistrée.</p>");
        sb.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;\">");
        Row(sb, "Matricule", E(r.Matricule));
        Row(sb, "Carte RFID", E(r.RFIDCard));
        Row(sb, "Transporteur", E(r.TransporteurName));
        Row(sb, "Chauffeur", E(r.ChauffeurName));
        Row(sb, "Client", E(r.ClientName));
        Row(sb, "Chantier", E(r.Chantier));
        Row(sb, "Article 1", Article(r.Produit1, r.Quantite1));
        if (!string.IsNullOrWhiteSpace(r.Produit2))
            Row(sb, "Article 2", Article(r.Produit2, r.Quantite2));
        Row(sb, "Entrée pont bascule (scan)", r.PabEntryAt.ToString("dd/MM/yyyy HH:mm"));
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
        public string? RFIDCard { get; set; }
        public string? TransporteurName { get; set; }
        public string? ChauffeurName { get; set; }
        public string? Produit1 { get; set; }
        public decimal? Quantite1 { get; set; }
        public string? Produit2 { get; set; }
        public decimal? Quantite2 { get; set; }
        public DateTime PabEntryAt { get; set; }
        public string? SentStagesCsv { get; set; }
    }
}
