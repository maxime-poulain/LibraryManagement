namespace LibraryManagement.Shared.Domain.Errors;

/// <summary>
/// One thing that went wrong: what kind of failure it was, a message for whoever reads it, and
/// optionally which part of the input it concerns.
/// </summary>
public class Error : ValueObject<Error>
{
    /// <summary>
    /// Gets the error message associated with this error.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the error code associated with this error.
    /// </summary>
    public ErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets what this error is about — typically the name of the offending field — or
    /// <see langword="null"/> when the error concerns the operation as a whole.
    /// </summary>
    /// <remarks>
    /// "Which field" and "what rule" are two different questions, and answering the first by
    /// encoding it into <see cref="ErrorCode"/> would leave the set of codes open-ended: one per
    /// field of every command. Keeping it separate lets a caller attach an error to the input that
    /// produced it without parsing anything.
    /// </remarks>
    public string? Target { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> class.
    /// </summary>
    /// <param name="errorCode">The error code associated with this error.</param>
    /// <param name="errorMessage">The error message associated with this error.</param>
    /// <param name="target">
    /// What the error is about, typically a field name. Omit it for an error that concerns the
    /// operation as a whole rather than one of its inputs.
    /// </param>
    public Error(ErrorCode errorCode, string errorMessage, string? target = null)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Target = target;
    }

    /// <summary>
    /// Implements value object equality by returning the components that make up this value object.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ErrorCode;
        yield return ErrorMessage;
        yield return Target;
    }

    /// <summary>
    /// Returns a string that represents this error.
    /// </summary>
    public override string ToString()
    {
        return Target is null
            ? $"{ErrorCode}: {ErrorMessage}"
            : $"{ErrorCode} ({Target}): {ErrorMessage}";
    }
}
