namespace LibraryManagement.Shared.Domain.Errors;

/// <summary>
/// Identifies the kind of an <see cref="Error"/>, independently of the human readable
/// message carried alongside it. The code is the stable part of an error: it is what
/// logs are grepped for, what the presentation layer maps to a status code, and what a
/// translation key is built from. The message may be reworded freely; the code may not.
/// </summary>
/// <remarks>
/// <para>
/// Error codes are declared by each bounded context, not by this kernel. The kernel only
/// defines what a code <em>is</em>. Declare them as static readonly fields on a holder
/// class owned by the context:
/// </para>
/// <code>
/// public static class CatalogErrorCodes
/// {
///     public static readonly ErrorCode WorkNotFound = new("Catalog.WorkNotFound");
///     public static readonly ErrorCode InvalidTitle = new("Catalog.InvalidTitle");
/// }
/// </code>
/// <para>
/// Prefix every code with the name of the owning context (<c>"Catalog.WorkNotFound"</c>,
/// <c>"Circulation.LoanNotFound"</c>) — the context, not the aggregate, because the context is
/// the namespace that guarantees no two owners claim the same code, and it makes a code
/// self-describing wherever it surfaces.
/// </para>
/// </remarks>
public sealed class ErrorCode : ValueObject<ErrorCode>
{
    /// <summary>
    /// Gets the textual value of the code, for example <c>"Book.NotFound"</c>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCode"/> class.
    /// </summary>
    /// <param name="value">
    /// The textual value of the code. Conventionally <c>"&lt;Context&gt;.&lt;Reason&gt;"</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is null, empty, or white space.
    /// </exception>
    public ErrorCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>
    /// Implements value object equality by returning the components that make up this value object.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Returns the textual value of this code.
    /// </summary>
    public override string ToString() => Value;
}
