namespace Ecare.Domain.ValueObjects;
public readonly record struct SlvId(string Value)
{
    public override string ToString() => Value;
    public static SlvId From(string v) =>
        string.IsNullOrWhiteSpace(v) ? throw new ArgumentException("SLV id required") : new(v.Trim());
}
