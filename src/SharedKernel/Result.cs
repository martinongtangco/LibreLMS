namespace LearningLms.SharedKernel;

/// <summary>
/// A plain success/failure wrapper for application-layer operations that can fail in an
/// expected way (validation, not-found, conflict) — used instead of throwing exceptions for
/// control flow. Unexpected failures still throw.
/// </summary>
public readonly struct Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

public readonly struct Result<TValue>
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    private readonly TValue? _value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value of a failed result: {Error}");

    private Result(bool isSuccess, TValue? value, string? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public static Result<TValue> Success(TValue value) => new(true, value, null);
    public static Result<TValue> Failure(string error) => new(false, default, error);
}
