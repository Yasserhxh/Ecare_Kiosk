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
    /// Paliers d'alerte (ex. 75 puis 90 min) à envoyer maintenant pour ce camion :
    /// franchis (durée écoulée &gt; palier) mais pas encore notifiés. Vide si le camion
    /// n'est pas entré (pas de 1ère pesée) ou déjà sorti (2ème pesée faite). Trié croissant.
    /// Renvoyer plusieurs paliers d'un coup couvre le cas d'un worker resté indisponible.
    /// </summary>
    public static IReadOnlyList<int> StagesToSend(
        DateTime? pabEntryAt,
        int? deuxiemePoid,
        DateTime nowLocal,
        IEnumerable<int> thresholds,
        ISet<int> alreadySent)
    {
        if (pabEntryAt is null || deuxiemePoid is not null)
            return Array.Empty<int>();

        var elapsed = ElapsedMinutes(pabEntryAt.Value, nowLocal);
        return thresholds
            .Where(t => elapsed > t && !alreadySent.Contains(t))
            .OrderBy(t => t)
            .ToList();
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
