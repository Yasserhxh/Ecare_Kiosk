using Ecare.Application.Commands.Legend;
using Xunit;

namespace Ecare.Application.Tests;

/// <summary>
/// The offline (provisional) BL must print exactly like the online BL built by
/// mycimar-web-api SapShipmentController (FormatKgToTonsKg, raw product code,
/// NormalizeMaterialLibelle, paired scellés).
/// </summary>
public class ProvisionalBlPayloadTests
{
    private static OfflineLegendSnapshot SacSnapshot() => new()
    {
        TypeProduit = "SAC",
        CodeSapProduit1 = "20875",
        Produit1 = "Lamaalem 35 Sac 50kg PAL",
        Quantite1 = 27m,
        SacNumber = 540,
        PremierePoid = 16_160,
        DeuxiemePoid = 43_160
    };

    [Fact]
    public void BuildProvisionalBlPayload_ProductCode_IsPrintedRawLikeOnline()
    {
        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, SacSnapshot());

        Assert.Equal("20875", payload.Produits![0].Code);
    }

    [Fact]
    public void BuildProvisionalBlPayload_Weights_UseTonsCommaKgFormat()
    {
        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, SacSnapshot());

        Assert.Equal("16,160", payload.Pesage!.PoidsVide);
        Assert.Equal("43,160", payload.Pesage.PoidsBrut);
    }

    [Fact]
    public void BuildProvisionalBlPayload_MissingWeight_PrintsZeroLikeOnline()
    {
        var snap = SacSnapshot();
        snap.PremierePoid = null;

        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, snap);

        Assert.Equal("0,000", payload.Pesage!.PoidsVide);
    }

    [Fact]
    public void BuildProvisionalBlPayload_Bulk_QuantityIsNetChargedWeight()
    {
        var snap = SacSnapshot();
        snap.TypeProduit = "VRAC";
        snap.PremierePoid = 15_020;
        snap.DeuxiemePoid = 43_160;

        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, snap);

        Assert.Equal("28,140", payload.Produits![0].Quantite);
    }

    [Fact]
    public void BuildProvisionalBlPayload_Libelle_IsNormalizedLikeOnline()
    {
        var snap = SacSnapshot();
        snap.Produit1 = "CPJ 35 sac palettisé";

        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, snap);

        Assert.Equal("Lamaalem (CPJ35) sac 50 kg palettisé", payload.Produits![0].Libelle);
    }

    [Fact]
    public void BuildProvisionalBlPayload_Scelles_ArePairedLikeOnline()
    {
        var snap = SacSnapshot();
        snap.Plombs = "A1, A2, B1, B2, C1";

        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, snap);

        Assert.Equal(new[] { "A1,A2", "B1,B2", "C1" }, payload.Transport!.Scelles);
    }

    [Fact]
    public void BuildProvisionalBlPayload_SecondProduct_HasZeroSacsLikeOnline()
    {
        var snap = SacSnapshot();
        snap.CodeSapProduit2 = "20876";
        snap.Produit2 = "Cimartob sac 45 Kg";
        snap.Quantite2 = 3m;

        var payload = UpdateSecondWeightHandler.BuildProvisionalBlPayload(1, snap);

        Assert.Equal("20876", payload.Produits![1].Code);
        Assert.Equal(0, payload.Produits[1].Sacs);
    }
}
