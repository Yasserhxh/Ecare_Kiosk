using System;
using Ecare.Application.Services.Alerts;
using Xunit;

namespace Ecare.Application.Tests;

public class OverstayAlertRulesTests
{
    // Repère fixe en heure locale Maroc (PabEntryAt est stocké en heure Maroc par le kiosk).
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0);

    [Fact]
    public void Pas_de_premiere_pesee_pas_de_depassement()
        => Assert.False(OverstayAlertRules.IsOverstaying(null, null, Now, 75));

    [Fact]
    public void Deuxieme_pesee_faite_pas_de_depassement()
        => Assert.False(OverstayAlertRules.IsOverstaying(Now.AddMinutes(-120), 30000, Now, 75));

    [Fact]
    public void Sous_le_seuil_pas_de_depassement()
        => Assert.False(OverstayAlertRules.IsOverstaying(Now.AddMinutes(-74), null, Now, 75));

    [Fact]
    public void Au_seuil_exact_pas_encore_depassement()
        => Assert.False(OverstayAlertRules.IsOverstaying(Now.AddMinutes(-75), null, Now, 75));

    [Fact]
    public void Au_dela_du_seuil_depassement()
        => Assert.True(OverstayAlertRules.IsOverstaying(Now.AddMinutes(-76), null, Now, 75));

    [Fact]
    public void ElapsedMinutes_arrondi_a_la_minute()
        => Assert.Equal(90, OverstayAlertRules.ElapsedMinutes(Now.AddMinutes(-90), Now));

    [Fact]
    public void SplitRecipients_premier_to_reste_cc_dedoublonne()
    {
        var (to, cc) = OverstayAlertRules.SplitRecipients(new[]
        {
            "abdelkarim.mezianemtalsi@heidelbergmaterials.com",
            "abdelkarim.mezianemtalsi@heidelbergmaterials.com", // doublon exact
            "ABD-ETTAOUAB.ELHRARTI@heidelbergmaterials.com",     // casse différente = même adresse
            "abd-ettaouab.elhrarti@heidelbergmaterials.com",
            "   ",                                                 // vide ignoré
            "ismail.chakra@heidelbergmaterials.com",
        });
        Assert.Equal("abdelkarim.mezianemtalsi@heidelbergmaterials.com", to);
        Assert.Equal("ABD-ETTAOUAB.ELHRARTI@heidelbergmaterials.com,ismail.chakra@heidelbergmaterials.com", cc);
    }

    [Fact]
    public void SplitRecipients_liste_vide_to_vide_cc_null()
    {
        var (to, cc) = OverstayAlertRules.SplitRecipients(Array.Empty<string>());
        Assert.Equal(string.Empty, to);
        Assert.Null(cc);
    }
}
