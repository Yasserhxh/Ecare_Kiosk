using P = Ecare.Application.Security.Permissions;

namespace Ecare.Application.Security;

public static class PermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        [EcareRoles.AgentDeGuichet] = new HashSet<string>
        {
            P.CommandsRead, P.CommandsWrite, P.CommandsQuantitiesRead, P.CommandsQuantitiesWrite,
            P.FluxStatusRead, P.ForceCallRead, P.LogisticsDataRead, P.RfidRead, P.BaggingRead,
            P.LoadingQuotasRead, P.ReportsRead, P.WeighingRead, P.LoadingSettingsRead
        },
        [EcareRoles.Logistique] = new HashSet<string>
        {
            P.CommandsRead, P.CommandsWrite, P.CommandsQuantitiesRead, P.CommandsQuantitiesWrite,
            P.FluxStatusRead, P.ForceCallRead, P.ForceCallExecute, P.LogisticsDataRead, P.LogisticsDataWrite,
            P.RfidRead, P.RfidWrite, P.BaggingRead, P.LoadingQuotasRead, P.ReportsRead, P.WeighingRead,
            P.LoadingSettingsRead
        },
        [EcareRoles.Expedition] = new HashSet<string>
        {
            P.FluxStatusRead, P.ForceCallRead, P.LogisticsDataRead, P.RfidRead,
            P.BaggingRead, P.BaggingWrite, P.LoadingQuotasRead, P.LoadingQuotasWrite,
            P.ReportsRead, P.WeighingRead, P.LoadingSettingsRead, P.LoadingSettingsWrite
        },
        [EcareRoles.AdminIT] = new HashSet<string>
        {
            P.CommandsRead, P.CommandsQuantitiesRead, P.FluxStatusRead, P.ForceCallRead,
            P.LogisticsDataRead, P.RfidRead, P.BaggingRead, P.BaggingWrite, P.LoadingQuotasRead,
            P.ReportsRead, P.WeighingRead, P.TraceabilityLogsRead, P.LoadingSettingsRead,
            P.AdminRawSql
        },
    };

    public static readonly IReadOnlyList<string> Roles = new[]
    { EcareRoles.AgentDeGuichet, EcareRoles.Logistique, EcareRoles.Expedition, EcareRoles.AdminIT };

    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    public static IReadOnlySet<string> For(string role)
    {
        if (role == EcareRoles.Admin) role = EcareRoles.AdminIT; // legacy equivalence
        return Map.TryGetValue(role, out var set) ? set : Empty;
    }
}
