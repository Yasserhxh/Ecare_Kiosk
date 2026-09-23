using System;
using System.Collections.Generic;
using System.Linq;
using Ecare.Application.Services.Alerts;
using Xunit;

namespace Ecare.Application.Tests;

public class OverstayAlertRulesTests
{
    // Repère fixe en heure locale Maroc (PabEntryAt est stocké en heure Maroc par le kiosk).
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0);
    private static readonly int[] Paliers = { 75, 90 };

    private static ISet<int> Sent(params int[] t) => new HashSet<int>(t);

    [Fact]
    public void Pas_de_premiere_pesee_aucun_palier()
        => Assert.Empty(OverstayAlertRules.StagesToSend(null, null, Now, Paliers, Sent()));

    [Fact]
    public void Deuxieme_pesee_faite_aucun_palier()
        => Assert.Empty(OverstayAlertRules.StagesToSend(Now.AddMinutes(-120), 30000, Now, Paliers, Sent()));

    [Fact]
    public void Sous_le_premier_seuil_aucun_palier()
        => Assert.Empty(OverstayAlertRules.StagesToSend(Now.AddMinutes(-60), null, Now, Paliers, Sent()));

    [Fact]
    public void Au_seuil_exact_pas_encore_declenche()
        => Assert.Empty(OverstayAlertRules.StagesToSend(Now.AddMinutes(-75), null, Now, Paliers, Sent()));

    [Fact]
    public void Au_dela_de_75_declenche_seulement_75()
        => Assert.Equal(new[] { 75 },
            OverstayAlertRules.StagesToSend(Now.AddMinutes(-80), null, Now, Paliers, Sent()).ToArray());

    [Fact]
    public void Palier_75_deja_envoye_ne_renvoie_rien_avant_90()
        => Assert.Empty(OverstayAlertRules.StagesToSend(Now.AddMinutes(-80), null, Now, Paliers, Sent(75)));

    [Fact]
    public void Au_dela_de_90_avec_75_deja_envoye_declenche_90()
        => Assert.Equal(new[] { 90 },
            OverstayAlertRules.StagesToSend(Now.AddMinutes(-95), null, Now, Paliers, Sent(75)).ToArray());

    [Fact]
    public void Worker_rate_les_deux_paliers_les_envoie_ensemble()
        => Assert.Equal(new[] { 75, 90 },
            OverstayAlertRules.StagesToSend(Now.AddMinutes(-95), null, Now, Paliers, Sent()).ToArray());

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
