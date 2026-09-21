using Ecare.Application.Commands.Legend;

namespace Ecare.Application.Tests;

public class OfflineSecondWeightRulesTests
{
    [Theory]
    [InlineData(true, "sync")]
    [InlineData(false, "shipment")]
    public void Cible_sap_selon_flag(bool offline, string expected)
        => Assert.Equal(expected, OfflineSecondWeightRules.ResolveSapTarget(offline));

    [Fact]
    public void Echec_connectivite_bascule_en_attente()
    {
        var r = OfflineSecondWeightRules.OnSapFailure(wasOffline: false, isConnectivity: true, "timeout");
        Assert.True(r.isOffline);
        Assert.Equal("EnAttente", r.status);
    }

    [Fact]
    public void Echec_metier_passe_en_erreur()
    {
        var r = OfflineSecondWeightRules.OnSapFailure(wasOffline: true, isConnectivity: false, "credit bloque");
        Assert.Equal("Erreur", r.status);
        Assert.Equal("credit bloque", r.error);
    }
}
