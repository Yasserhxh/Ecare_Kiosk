namespace Ecare.Shared;

public readonly record struct Result(bool Success, string? Error = null)
{
    public static Result Ok() => new(true);
    public static Result Fail(string e) => new(false, e);
}

public readonly record struct Result<T>(bool Success, T? Value, string? Error = null)
{
    public static Result<T> Ok(T v) => new(true, v);
    public static Result<T> Fail(string e) => new(false, default, e);
}
