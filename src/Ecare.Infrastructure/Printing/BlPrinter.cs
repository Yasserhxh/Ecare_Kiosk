namespace Ecare.Infrastructure.Printing;
public interface IBlPrinter
{
    Task PrintTwoCopiesAsync(string blNumber, string htmlTemplate, CancellationToken ct);
}
public sealed class MockBlPrinter : IBlPrinter
{
    public Task PrintTwoCopiesAsync(string blNumber, string htmlTemplate, CancellationToken ct)
    { /* Wire real printer driver here */ return Task.CompletedTask; }
}
