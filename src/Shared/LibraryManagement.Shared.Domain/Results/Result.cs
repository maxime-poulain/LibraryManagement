using System.Runtime.CompilerServices;
using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Results;

/// <summary>
/// Represents a result of a computation that can either be successful or result in an error.
/// This class encapsulates the Railway Oriented Programming (ROP) pattern, where computations
/// can be chained together, and errors are propagated without interrupting the flow.
/// Unlike the <see cref="Result{TValue}"/> class, this class does not hold a value in case of a successful result.
/// </summary>
public abstract class Result : IFailable<Result>
{
    /// <summary>
    /// Indicates whether this result is a failure.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this result carries errors; <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    /// Prefer <see cref="Match{TResult}"/>: it forces both outcomes to be handled and gives access
    /// to the errors, whereas this method only answers the question and leaves the caller to reach
    /// for one side unguarded. It exists for code that is generic over the result type and cannot
    /// supply the two branches <see cref="Match{TResult}"/> asks for.
    /// </remarks>
    public bool HasErrors() => Match(
        () => false,
        _ => true);

    /// <summary>
    /// Chains computations by applying the provided function.
    /// If the current result is a failure, the computation is skipped, and the error is propagated.
    /// </summary>
    /// <param name="func">A function to apply, producing a new result.</param>
    /// <returns>A new result, which can be either a success or failure, based on the provided function and the current result state.</returns>
    public abstract Result Bind(Func<Result> func);

    /// <summary>
    /// Matches the current result to either a success or failure case, allowing for explicit handling of both outcomes.
    /// This method is central to the ROP pattern, ensuring that both success and error paths are addressed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">A function to handle the success case.</param>
    /// <param name="onFailure">A function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>The result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<IReadOnlyErrorCollection, TResult> onFailure
    );

    /// <summary>
    /// Matches the current result asynchronously to either a success or failure case.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">An asynchronous function to handle the success case.</param>
    /// <param name="onFailure">An asynchronous function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>A task representing the result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract ValueTask<TResult> MatchAsync<TResult>(
        Func<ValueTask<TResult>> onSuccess,
        Func<IReadOnlyErrorCollection, ValueTask<TResult>> onFailure
    );

    /// <summary>
    /// Executes one of the provided actions based on whether the result is a success or a failure.
    /// </summary>
    /// <param name="onSuccess">An action to execute if the result is a success.</param>
    /// <param name="onFailure">An action to execute if the result is a failure, taking the error collection as a parameter.</param>
    public abstract void Switch(Action onSuccess, Action<IReadOnlyErrorCollection> onFailure);

    /// <summary>
    /// Executes the provided action if the result is a success, then returns the same result.
    /// This is useful for performing side-effects without breaking the fluent chain.
    /// Unlike <see cref="Bind"/>, this method does not short-circuit on failure.
    /// </summary>
    /// <param name="onSuccess">An action to execute if the result is a success.</param>
    /// <returns>The current result instance, enabling fluent chaining.</returns>
    public Result Tap(Action onSuccess)
    {
        Switch(onSuccess, _ => { });
        return this;
    }

    /// <summary>
    /// Executes the provided action if the result is a failure, then returns the same result.
    /// This is useful for reacting to errors — logging them, or collecting them into an
    /// <see cref="ErrorCollection"/> owned by the caller — without breaking the fluent chain.
    /// Unlike <see cref="Bind"/>, this method does not short-circuit on failure.
    /// </summary>
    /// <param name="onFailure">An action to execute if the result is a failure, taking the error collection as a parameter.</param>
    /// <returns>The current result instance, enabling fluent chaining.</returns>
    public Result TapError(Action<IReadOnlyErrorCollection> onFailure)
    {
        Switch(() => { }, onFailure);
        return this;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a successful computation.
    /// </summary>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a successful computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Success() => new SuccessResult();

    /// <summary>
    /// Creates a completed <see cref="ValueTask{TResult}"/> wrapping a <see cref="Result"/> that
    /// represents a successful computation. No asynchronous work is performed.
    /// </summary>
    /// <returns>A completed <see cref="ValueTask{TResult}"/> whose result represents a successful computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result> SuccessAsync() => ValueTask.FromResult(Success());

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errors">The errors that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed computation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors"/> is empty.</exception>
    /// <remarks>
    /// An immutable snapshot of <paramref name="errors"/> is captured, so a mutable
    /// <see cref="ErrorCollection"/> may be passed and kept in use afterwards without affecting the
    /// result returned here.
    /// </remarks>
    public static Result Failure(IEnumerable<Error> errors)
    {
        var snapshot = ImmutableErrorCollection.From(errors);
        if (!snapshot.HasErrors)
        {
            throw new ArgumentException("A failed result must carry at least one error.", nameof(errors));
        }

        return new FailureResult(snapshot);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errorCode">The error code that resulted from the failed computation.</param>
    /// <param name="errorMessage">The error message that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result"/> class that represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(ErrorCode errorCode, string errorMessage)
        => Failure([new Error(errorCode, errorMessage)]);

    /// <summary>
    /// Creates a new instance of the <see cref="Result"/> class from the given errors: a failure if
    /// there are any, a success otherwise.
    /// </summary>
    /// <param name="errors">The errors to build the result from.</param>
    /// <returns>A failed <see cref="Result"/> if <paramref name="errors"/> is not empty; a successful one otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    public static Result FromErrors(IEnumerable<Error> errors)
    {
        var snapshot = ImmutableErrorCollection.From(errors);
        return snapshot.HasErrors ? new FailureResult(snapshot) : Success();
    }

    /// <summary>
    /// Creates a completed <see cref="ValueTask{TResult}"/> wrapping a <see cref="Result"/> that
    /// represents a failed computation. No asynchronous work is performed.
    /// </summary>
    /// <param name="errors">The errors that resulted from the failed computation.</param>
    /// <returns>A completed <see cref="ValueTask{TResult}"/> whose result represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result> FailureAsync(IEnumerable<Error> errors)
        => ValueTask.FromResult(Failure(errors));

    private sealed class SuccessResult : Result
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Match<TResult>(
            Func<TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onSuccess();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<TResult> MatchAsync<TResult>(
            Func<ValueTask<TResult>> onSuccess,
            Func<IReadOnlyErrorCollection, ValueTask<TResult>> onFailure
        ) => onSuccess();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Switch(Action onSuccess, Action<IReadOnlyErrorCollection> onFailure)
            => onSuccess();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Result Bind(Func<Result> func)
            => func();
    }

    private sealed class FailureResult(IReadOnlyErrorCollection error) : Result
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Match<TResult>(
            Func<TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onFailure(error);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<TResult> MatchAsync<TResult>(
            Func<ValueTask<TResult>> onSuccess,
            Func<IReadOnlyErrorCollection, ValueTask<TResult>> onFailure
        ) => onFailure(error);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Switch(Action onSuccess, Action<IReadOnlyErrorCollection> onFailure)
            => onFailure(error);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Result Bind(Func<Result> func)
            => this;
    }
}
