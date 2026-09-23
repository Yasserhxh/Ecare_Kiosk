using System;
using System.Collections.Generic;
using System.Linq;

namespace Ecare.Application.Services.Alerts;

/// <summary>
/// Règles pures (testables sans DB) de l'alerte « camion trop longtemps dans l'usine ».
/// « Dans l'usine » = de la 1ère pesée (PabEntryAt renseigné) jusqu'à la 2ème pesée
/// (DeuxiemePoid renseigné). PabEntryAt est stocké en heure Maroc par le kiosk : la
/// comparaison doit donc se faire avec un « maintenant » lui aussi en heure Maroc.
/// </summary>
public static class OverstayAlertRules
{
    /// <summary>
    /// Vrai si le camion est entré (1ère pesée faite), pas encore sorti (2ème pesée absente),
    /// et présent depuis strictement plus de <paramref name="thresholdMinutes"/> minutes.
    /// </summary>
    public static bool IsOverstaying(DateTime? pabEntryAt, int? deuxiemePoid, DateTime nowLocal, int thresholdMinutes)
    {
        if (pabEntryAt is null) return false;   // pas encore de 1ère pesée → pas dans l'usine
        if (deuxiemePoid is not null) return false; // 2ème pesée faite → déjà sorti
        return (nowLocal - pabEntryAt.Value).TotalMinutes > thresholdMinutes;
    }

    /// <summary>Minutes écoulées depuis la 1ère pesée, arrondies à la minute.</summary>
    public static int ElapsedMinutes(DateTime pabEntryAt, DateTime nowLocal)
        => (int)Math.Round((nowLocal - pabEntryAt).TotalMinutes);

    /// <summary>
    /// Dédoublonne (insensible à la casse, en préservant la 1ère occurrence),
    /// ignore les entrées vides, puis renvoie le 1er destinataire en TO et les
    /// suivants en CC (joints par des virgules, ou null si aucun).
    /// </summary>
    public static (string To, string? Cc) SplitRecipients(IEnumerable<string> recipients)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clean = new List<string>();
        foreach (var raw in recipients ?? Enumerable.Empty<string>())
        {
            var e = raw?.Trim();
            if (string.IsNullOrEmpty(e)) continue;
            if (seen.Add(e)) clean.Add(e);
        }

        if (clean.Count == 0) return (string.Empty, null);
        var to = clean[0];
        var cc = clean.Count > 1 ? string.Join(",", clean.Skip(1)) : null;
        return (to, cc);
    }
}
