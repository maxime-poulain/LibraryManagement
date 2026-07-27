using System.Collections;
using System.Collections.Immutable;

namespace LibraryManagement.Shared.Domain.Errors;

/// <summary>
/// The immutable snapshot of a set of errors, and the only <see cref="IReadOnlyErrorCollection"/>
/// this kernel hands out. Backed by an <see cref="ImmutableArray{T}"/>, so no other object retains
/// a reference to its storage: once taken, a snapshot cannot change.
/// </summary>
/// <remarks>
/// This is what makes a failed <see cref="Results.Result"/> a value rather than a view. An
/// <see cref="ErrorCollection"/> is a mutable accumulator by design — errors are added to it as
/// validation proceeds — so a result that merely held on to one would keep changing as the
/// accumulator did.
/// </remarks>
public sealed class ImmutableErrorCollection : IReadOnlyErrorCollection
{
    /// <summary>
    /// The snapshot containing no error.
    /// </summary>
    public static readonly ImmutableErrorCollection Empty = new([]);

    private readonly ImmutableArray<Error> _errors;

    private ImmutableErrorCollection(ImmutableArray<Error> errors)
    {
        _errors = errors;
    }

    /// <summary>
    /// Takes a snapshot of <paramref name="errors"/>. Returns <paramref name="errors"/> unchanged
    /// when it is already a snapshot, so passing one along costs nothing.
    /// </summary>
    /// <param name="errors">The errors to capture. Must not be null.</param>
    /// <returns>An immutable collection holding the errors <paramref name="errors"/> contained at
    /// the moment of the call.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    public static IReadOnlyErrorCollection From(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors is ImmutableErrorCollection snapshot)
        {
            return snapshot;
        }

        var captured = errors.ToImmutableArray();
        return captured.IsEmpty ? Empty : new ImmutableErrorCollection(captured);
    }

    /// <inheritdoc/>
    public bool HasErrors => !_errors.IsEmpty;

    /// <inheritdoc/>
    public int Count => _errors.Length;

    /// <inheritdoc/>
    public Error this[int index] => _errors[index];

    /// <inheritdoc/>
    public IEnumerator<Error> GetEnumerator() => ((IEnumerable<Error>)_errors).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the errors of this collection, one per line.
    /// </summary>
    public override string ToString() => string.Join(Environment.NewLine, _errors);
}
