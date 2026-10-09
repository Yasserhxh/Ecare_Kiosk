using Ecare.Api.Endpoints;

namespace Ecare.Api.Tests;

public class OfflineOrderEndpointsTests
{
    // Les lignes Dapper dynamiques sont sérialisées avec les noms de colonnes tels quels :
    // la page Flow lit du camelCase, donc chaque colonne doit être aliasée en camelCase.
    [Theory]
    [InlineData("id")]
    [InlineData("clientName")]
    [InlineData("chantier")]
    [InlineData("produit1")]
    [InlineData("quantite1")]
    [InlineData("matricule")]
    [InlineData("step")]
    [InlineData("codeSapCommande")]
    [InlineData("bonDeLivraison")]
    [InlineData("offlineStatus")]
    [InlineData("offlineSyncError")]
    [InlineData("offlineCreatedAt")]
    public void Operations_sql_aliase_chaque_champ_lu_par_le_front_en_camelCase(string field)
    {
        Assert.Contains($" AS {field}", OfflineOrderEndpoints.OperationsSql, StringComparison.Ordinal);
    }
}
