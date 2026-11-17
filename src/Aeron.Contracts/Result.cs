namespace Aeron.Contracts;

/// <summary>
/// Represents the explicit outcome of a fail-closed operation without return data.
/// </summary>
public sealed record Result
{
    private Result(bool isSuccess, string errorCode, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string ErrorCode { get; }

    public string ErrorMessage { get; }

    public static Result Success() => new(true, string.Empty, string.Empty);

    public static Result Failure(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new Result(false, errorCode, errorMessage);
    }
}

/// <summary>
/// Represents the explicit outcome of a fail-closed operation returning a value on success.
/// </summary>
public sealed record Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, string errorCode, string errorMessage)
    {
        IsSuccess = isSuccess;
        _value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string ErrorCode { get; }

    public string ErrorMessage { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value from failed result ({ErrorCode}: {ErrorMessage}).");

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, string.Empty, string.Empty);
    }

    public static Result<T> Failure(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new Result<T>(false, default, errorCode, errorMessage);
    }
}
