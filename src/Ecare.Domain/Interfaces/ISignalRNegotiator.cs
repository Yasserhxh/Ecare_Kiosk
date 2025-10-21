namespace Ecare.Domain.Interfaces;

public interface ISignalRNegotiator
{
    Task<NegotiatePayload> NegotiateAsync(CancellationToken ct = default);
}

public sealed record NegotiatePayload(string Url, string AccessToken);