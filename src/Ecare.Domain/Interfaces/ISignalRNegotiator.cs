namespace Ecare.Domain.Interfaces;

public interface ISignalRNegotiator
{
    Task<NegotiatePayload> NegotiateAsync(CancellationToken ct = default);
    Task<NegotiatePayload> NegotiatedatahubAsync(CancellationToken ct = default);

}

public sealed record NegotiatePayload(string Url, string AccessToken);