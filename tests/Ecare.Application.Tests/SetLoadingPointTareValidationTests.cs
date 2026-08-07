using Ecare.Application.Commands.Legend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecare.Application.Tests;

public class SetLoadingPointTareValidationTests
{
    private static SetLoadingPointTareHandler CreateHandler() =>
        new(new ConfigurationBuilder().Build(),
            NullLogger<SetLoadingPointTareHandler>.Instance);

    [Fact]
    public async Task Rejects_non_positive_tare()
    {
        var result = await CreateHandler()
            .Handle(new SetLoadingPointTareCommand(1, 0), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_TARE", result.Error);
    }

    [Fact]
    public async Task Rejects_invalid_legend_id()
    {
        var result = await CreateHandler()
            .Handle(new SetLoadingPointTareCommand(0, 15000), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_LEGEND_ID", result.Error);
    }
}
