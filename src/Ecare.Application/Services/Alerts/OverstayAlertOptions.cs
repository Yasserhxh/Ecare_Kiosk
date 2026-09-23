using System.Collections.Generic;

namespace Ecare.Application.Services.Alerts;

/// <summary>
/// Configuration de l'alerte « camion trop longtemps dans l'usine »
/// (section "OverstayAlert" de appsettings).
/// </summary>
public sealed class OverstayAlertOptions
{
    /// <summary>Active/désactive le worker d'alerte.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Paliers d'alerte, en minutes (une alerte par palier et par camion).
    /// Défaut : 75 min puis 90 min.
    /// </summary>
    public List<int> Thresholds { get; set; } = new() { 75, 90 };

    /// <summary>Fréquence de scan, en minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 5;

    /// <summary>Boîte expéditrice (Azure AD / Graph).</summary>
    public string Sender { get; set; } = "cimar.noreplay@cimar.co.ma";

    /// <summary>Liste de diffusion : 1er = TO, les suivants = CC (dédoublonnés).</summary>
    public List<string> Recipients { get; set; } = new();
}
