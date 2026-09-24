namespace Ecare.Application.Security;

public static class Permissions
{
    public const string CommandsRead            = "Commands.Read";
    public const string CommandsWrite           = "Commands.Write";
    public const string CommandsQuantitiesRead  = "CommandsQuantities.Read";
    public const string CommandsQuantitiesWrite = "CommandsQuantities.Write";
    public const string FluxStatusRead          = "FluxStatus.Read";
    public const string ForceCallRead           = "ForceCall.Read";
    public const string ForceCallExecute        = "ForceCall.Execute";
    public const string LogisticsDataRead       = "LogisticsData.Read";
    public const string LogisticsDataWrite      = "LogisticsData.Write";
    public const string RfidRead                = "Rfid.Read";
    public const string RfidWrite               = "Rfid.Write";
    public const string BaggingRead             = "Bagging.Read";
    public const string BaggingWrite            = "Bagging.Write";
    public const string LoadingQuotasRead       = "LoadingQuotas.Read";
    public const string LoadingQuotasWrite      = "LoadingQuotas.Write";
    public const string ReportsRead             = "Reports.Read";
    public const string WeighingRead            = "Weighing.Read";
    public const string TraceabilityLogsRead    = "TraceabilityLogs.Read";
    public const string LoadingSettingsRead     = "LoadingSettings.Read";
    public const string LoadingSettingsWrite    = "LoadingSettings.Write";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CommandsRead, CommandsWrite, CommandsQuantitiesRead, CommandsQuantitiesWrite,
        FluxStatusRead, ForceCallRead, ForceCallExecute, LogisticsDataRead, LogisticsDataWrite,
        RfidRead, RfidWrite, BaggingRead, BaggingWrite, LoadingQuotasRead, LoadingQuotasWrite,
        ReportsRead, WeighingRead, TraceabilityLogsRead, LoadingSettingsRead, LoadingSettingsWrite
    };
}
