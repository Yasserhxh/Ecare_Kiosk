using System.Text.Json;
using Ecare.Application.Commands.Legend;
using Xunit;

namespace Ecare.Application.Tests;

public class UpdateAfterFirstWeightCommandTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserializes_with_deviceId()
    {
        var json = """{"rfidCard":"123","matricule":"ABC-1","premierePoid":17000,"produit1":"CPJ45","legendId":42,"deviceId":"PAB-ENTREE-1"}""";
        var cmd = JsonSerializer.Deserialize<UpdateAfterFirstWeightCommand>(json, Options);
        Assert.NotNull(cmd);
        Assert.Equal("PAB-ENTREE-1", cmd!.DeviceId);
        Assert.Equal(42, cmd.LegendId);
    }

    [Fact]
    public void Deserializes_without_deviceId_as_null()
    {
        var json = """{"rfidCard":"123","matricule":"ABC-1","premierePoid":17000,"produit1":"CPJ45"}""";
        var cmd = JsonSerializer.Deserialize<UpdateAfterFirstWeightCommand>(json, Options);
        Assert.NotNull(cmd);
        Assert.Null(cmd!.DeviceId);
    }
}
