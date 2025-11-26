using MediatR;

public sealed record HelloDeviceCommand(
    string DeviceId,
    string? HostName,
    string? Site,
    string? AppVersion,
    string? Ip) : IRequest<HelloDeviceResult>;

public sealed record HelloDeviceResult(
    string DeviceId,
    bool Exists,
    bool IsActive,
    int? CurrentLineId);

public sealed record AssignDeviceToLineCommand(
    string DeviceId,
    int LineId,
    string? Reason) : IRequest<AssignDeviceToLineResult>;

public sealed record AssignDeviceToLineResult(
    string DeviceId,
    int LineId,
    DateTime AssignedAtUtc);

public sealed record GetDeviceAssignmentQuery(
    string DeviceId) : IRequest<GetDeviceAssignmentResult>;

public sealed record GetDeviceAssignmentResult(
    string DeviceId,
    int? LineId,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc);
