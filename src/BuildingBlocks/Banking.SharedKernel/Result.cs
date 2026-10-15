namespace Banking.SharedKernel;

/// <summary>
/// The outcome of an operation that can fail for a business reason. Bugs still throw exceptions;
/// only expected outcomes (insufficient funds, a closed account) travel as a failed Result. See docs/adr/0007.
/// </summary>
public class Result
{
    private static readonly Result SuccessResult = new(null);

    protected Result(Error? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>The reason for the failure, or null when the operation succeeded.</summary>
    public Error? Error { get; }

    public static Result Success() => SuccessResult;

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    /// <summary>Lets a method that returns <see cref="Result"/> simply <c>return SomeErrors.X;</c>.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>A <see cref="Result"/> that carries a value when it succeeds.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value)
        : base(null)
    {
        _value = value;
    }

    internal Result(Error error)
        : base(error)
    {
    }

    /// <summary>The value of a successful result. Reading it from a failed result is a bug and throws.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error!.Code}).");

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
