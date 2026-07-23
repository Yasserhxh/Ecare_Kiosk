using System.Text.Json;
using Ecare.Application.Commands.Legend;
using Xunit;

namespace Ecare.Application.Tests;

public class UpdateSecondWeightCommandTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserializes_with_deviceId()
    {
        var json = """{"rfidCard":"123","matricule":"ABC-1","deuxiemePoid":39000,"legendId":42,"deviceId":"PAB-SORTIE-1"}""";
        var cmd = JsonSerializer.Deserialize<UpdateSecondWeightCommand>(json, Options);
        Assert.NotNull(cmd);
        Assert.Equal("PAB-SORTIE-1", cmd!.DeviceId);
    }

    [Fact]
    public void Deserializes_without_deviceId_as_null()
    {
        var json = """{"rfidCard":"123","matricule":"ABC-1","deuxiemePoid":39000}""";
        var cmd = JsonSerializer.Deserialize<UpdateSecondWeightCommand>(json, Options);
        Assert.NotNull(cmd);
        Assert.Null(cmd!.DeviceId);
    }
}
