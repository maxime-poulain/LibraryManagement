using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Errors;

/// <summary>
/// A mutable collection of <see cref="Error"/> objects: accumulate errors into it as validation
/// proceeds, then turn it into a <see cref="Result"/> once every rule has had its say.
/// Enumerate it to inspect what has been accumulated so far.
/// </summary>
public interface IErrorCollection : IEnumerable<Error>
{
    /// <summary>
    /// Gets the number of errors accumulated so far.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Adds an <see cref="Error"/> to the collection.
    /// </summary>
    /// <param name="error">The error to add to the collection. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    void Add(Error error);

    /// <summary>
    /// Adds an error to the collection by specifying an error code and an error message.
    /// </summary>
    /// <param name="errorCode">The <see cref="ErrorCode"/> associated with the error.</param>
    /// <param name="errorMessage">The error message associated with the error.</param>
    void Add(ErrorCode errorCode, string errorMessage);

    /// <summary>
    /// Adds a collection of <see cref="Error"/> objects to the collection.
    /// </summary>
    /// <param name="errors">The collection of errors to add to the collection. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    void AddErrors(IEnumerable<Error> errors);

    /// <summary>
    /// Adds the <see cref="Error"/> objects carried by the specified <see cref="Result"/> to the
    /// collection. A successful result contributes nothing.
    /// </summary>
    /// <param name="result">The result whose errors are to be accumulated. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    void AddErrors(Result result);

    /// <summary>
    /// Takes an immutable snapshot of the errors accumulated so far.
    /// </summary>
    /// <returns>
    /// An <see cref="IReadOnlyErrorCollection"/> holding the errors this collection contained at
    /// the moment of the call. Later mutations of this collection do not affect it.
    /// </returns>
    IReadOnlyErrorCollection AsReadOnly();

    /// <summary>
    /// Gets or replaces the <see cref="Error"/> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the error.</param>
    /// <returns>The <see cref="Error"/> at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is outside the bounds of the collection.
    /// </exception>
    /// <remarks>
    /// The setter mutates this accumulator in place, which is its purpose. It cannot affect a
    /// <see cref="Result"/> already built from this collection: a result captures an immutable
    /// snapshot rather than the collection itself.
    /// </remarks>
    Error this[int index]
    {
        get;
        set;
    }
}
