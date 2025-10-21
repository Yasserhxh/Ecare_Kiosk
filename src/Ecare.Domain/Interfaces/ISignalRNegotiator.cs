namespace Ecare.Domain.Interfaces;

public interface ISignalRNegotiator
{
    Task<NegotiatePayload> NegotiateAsync(string hubname,CancellationToken ct = default);
}

public sealed record NegotiatePayload(string Url, string AccessToken);