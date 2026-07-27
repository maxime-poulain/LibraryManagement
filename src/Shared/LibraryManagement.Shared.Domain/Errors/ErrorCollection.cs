using System.Collections;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Errors;

/// <inheritdoc cref="IErrorCollection"/>
/// <remarks>
/// This type deliberately does not implement <see cref="IReadOnlyErrorCollection"/>: an accumulator
/// must never be passable where an immutable set of errors is expected. Call
/// <see cref="AsReadOnly"/> to obtain a snapshot.
/// </remarks>
public sealed class ErrorCollection : IErrorCollection
{
    private readonly List<Error> _errors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCollection"/> class.
    /// </summary>
    public ErrorCollection()
    {
        _errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCollection"/> class with the specified collection of errors.
    /// </summary>
    /// <param name="errors">The collection of errors to add to the new error collection.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    public ErrorCollection(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors = new List<Error>(errors);
    }

    /// <inheritdoc/>
    public int Count => _errors.Count;

    /// <inheritdoc/>
    public void Add(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _errors.Add(error);
    }

    /// <inheritdoc/>
    public void Add(ErrorCode errorCode, string errorMessage)
    {
        Add(new Error(errorCode, errorMessage));
    }

    /// <inheritdoc/>
    public void AddErrors(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors.AddRange(errors);
    }

    /// <inheritdoc/>
    public void AddErrors(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.TapError(AddErrors);
    }

    /// <inheritdoc/>
    public IReadOnlyErrorCollection AsReadOnly()
    {
        return ImmutableErrorCollection.From(_errors);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Error"/> objects in this error collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator{T}"/> that can be used to iterate through the <see cref="Error"/> objects in this error collection.</returns>
    public IEnumerator<Error> GetEnumerator()
    {
        return _errors.GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Turns everything accumulated so far into a <see cref="Result"/>: a failure carrying a
    /// snapshot of these errors if any were added, a success otherwise. This is the bridge between
    /// accumulating errors imperatively and returning them through the railway-oriented pipeline.
    /// </summary>
    /// <returns>
    /// A failed <see cref="Result"/> if this collection contains at least one error;
    /// a successful one otherwise.
    /// </returns>
    public Result ToResult()
    {
        return _errors.Count > 0 ? Result.Failure(_errors) : Result.Success();
    }

    /// <summary>
    /// Turns everything accumulated so far into a <see cref="Result{TValue}"/>: a failure carrying a
    /// snapshot of these errors if any were added, or a success carrying <paramref name="value"/> otherwise.
    /// </summary>
    /// <typeparam name="TValue">The type of the value carried on success.</typeparam>
    /// <param name="value">
    /// The value to carry if no error was accumulated. It is evaluated by the caller either way,
    /// so it must be safe to build even when this collection is not empty.
    /// </param>
    /// <returns>
    /// A failed <see cref="Result{TValue}"/> if this collection contains at least one error;
    /// a successful one carrying <paramref name="value"/> otherwise.
    /// </returns>
    public Result<TValue> ToResult<TValue>(TValue value)
    {
        return _errors.Count > 0 ? Result<TValue>.Failure(_errors) : Result<TValue>.Success(value);
    }

    /// <summary>
    /// Branches on whether anything was accumulated, without going through a
    /// <see cref="Result"/> first.
    /// </summary>
    /// <typeparam name="TResult">The type produced by both branches.</typeparam>
    /// <param name="onSuccess">Invoked when this collection is empty.</param>
    /// <param name="onFailure">Invoked with a snapshot of this collection when it contains at least one error.</param>
    /// <returns>Whatever the branch that ran returned.</returns>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<IReadOnlyErrorCollection, TResult> onFailure)
    {
        return _errors.Count > 0 ?
            onFailure(AsReadOnly()) :
            onSuccess();
    }

    /// <inheritdoc/>
    public Error this[int index]
    {
        get => _errors[index];
        set => _errors[index] = value;
    }
}
