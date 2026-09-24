using Ecare.Application.Security;

namespace Ecare.Application.Tests;

public class PermissionMatrixTests
{
    [Fact]
    public void Guichet_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "Commands.Read","Commands.Write","CommandsQuantities.Read","CommandsQuantities.Write",
            "FluxStatus.Read","ForceCall.Read","LogisticsData.Read","Rfid.Read","Bagging.Read",
            "LoadingQuotas.Read","Reports.Read","Weighing.Read","LoadingSettings.Read"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.AgentDeGuichet).ToHashSet());
    }

    [Fact]
    public void Logistique_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "Commands.Read","Commands.Write","CommandsQuantities.Read","CommandsQuantities.Write",
            "FluxStatus.Read","ForceCall.Read","ForceCall.Execute","LogisticsData.Read","LogisticsData.Write",
            "Rfid.Read","Rfid.Write","Bagging.Read","LoadingQuotas.Read","Reports.Read","Weighing.Read",
            "LoadingSettings.Read"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.Logistique).ToHashSet());
    }

    [Fact]
    public void Expedition_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "FluxStatus.Read","ForceCall.Read","LogisticsData.Read","Rfid.Read",
            "Bagging.Read","Bagging.Write","LoadingQuotas.Read","LoadingQuotas.Write",
            "Reports.Read","Weighing.Read","LoadingSettings.Read","LoadingSettings.Write"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.Expedition).ToHashSet());
    }

    [Fact]
    public void AdminIT_has_exactly_the_matrix_row()
    {
        var expected = new HashSet<string>
        {
            "Commands.Read","CommandsQuantities.Read","FluxStatus.Read","ForceCall.Read",
            "LogisticsData.Read","Rfid.Read","Bagging.Read","Bagging.Write","LoadingQuotas.Read",
            "Reports.Read","Weighing.Read","TraceabilityLogs.Read","LoadingSettings.Read"
        };
        Assert.Equal(expected, PermissionMatrix.For(EcareRoles.AdminIT).ToHashSet());
    }

    [Fact]
    public void Admin_maps_to_AdminIT_set()
        => Assert.Equal(PermissionMatrix.For(EcareRoles.AdminIT), PermissionMatrix.For(EcareRoles.Admin));

    [Fact]
    public void Unknown_role_has_no_permissions()
        => Assert.Empty(PermissionMatrix.For("NoSuchRole"));

    [Fact]
    public void Permissions_All_covers_every_key_used_in_the_matrix()
    {
        var used = PermissionMatrix.Roles.SelectMany(r => PermissionMatrix.For(r)).ToHashSet();
        Assert.True(used.IsSubsetOf(Permissions.All.ToHashSet()));
    }
}
