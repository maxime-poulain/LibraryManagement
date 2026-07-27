using System.Runtime.CompilerServices;
using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Results;

/// <summary>
/// Represents a result of a computation that can either be successful or result in an error.
/// This class encapsulates the Railway Oriented Programming (ROP) pattern, where computations
/// can be chained together, and errors are propagated without interrupting the flow.
/// </summary>
/// <typeparam name="TValue">The type of the value in case of a successful result.</typeparam>
public abstract class Result<TValue> : IFailable<Result<TValue>>
{
    /// <summary>
    /// Indicates whether this result is a failure.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this result carries errors; <see langword="false"/> if it carries
    /// a value.
    /// </returns>
    /// <remarks>
    /// Prefer <see cref="Match{TResult}"/>: it forces both outcomes to be handled and gives access
    /// to the value or the errors, whereas this method only answers the question and leaves the
    /// caller to reach for one side unguarded.
    /// </remarks>
    public bool HasErrors() => Match(
        _ => false,
        _ => true);

    /// <summary>
    /// Matches the current result to either a success or failure case, allowing for explicit handling of both outcomes.
    /// This method is central to the ROP pattern, ensuring that both success and error paths are addressed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">A function to handle the success case, taking the successful value as a parameter.</param>
    /// <param name="onFailure">A function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>The result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<IReadOnlyErrorCollection, TResult> onFailure
    );

    /// <summary>
    /// Asynchronously matches the current result to either a success or failure case, allowing for explicit
    /// handling of both outcomes. This is the asynchronous counterpart of <see cref="Match{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after handling the success or failure.</typeparam>
    /// <param name="onSuccess">An asynchronous function to handle the success case, taking the successful value as a parameter.</param>
    /// <param name="onFailure">An asynchronous function to handle the failure case, taking the error collection as a parameter.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> whose result is the result of either the onSuccess or onFailure function, depending on the current result state.</returns>
    public abstract ValueTask<TResult> MatchAsync<TResult>(
        Func<TValue, ValueTask<TResult>> onSuccess,
        Func<IReadOnlyErrorCollection, ValueTask<TResult>> onFailure);

    /// <summary>
    /// Executes one of the provided actions based on whether the result is a success or a failure.
    /// </summary>
    /// <param name="onSuccess">An action to execute if the result is a success.</param>
    /// <param name="onFailure">An action to execute if the result is a failure.</param>
    public abstract void Switch(
        Action<TValue> onSuccess,
        Action<IReadOnlyErrorCollection> onFailure);

    /// <summary>
    /// Executes the provided action with the success value if the result is a success,
    /// then returns the same result. Useful for side-effects without breaking the fluent chain.
    /// Unlike <see cref="Bind{TNewValue}"/>, this method does not short-circuit on failure.
    /// </summary>
    /// <param name="onSuccess">An action to execute with the success value.</param>
    /// <returns>The current result instance, enabling fluent chaining.</returns>
    public Result<TValue> Tap(Action<TValue> onSuccess)
    {
        Switch(onSuccess, _ => { });
        return this;
    }

    /// <summary>
    /// Executes the provided action if the result is a failure, then returns the same result.
    /// Useful for accumulating errors from multiple results without breaking the fluent chain.
    /// Unlike <see cref="Bind{TNewValue}"/>, this method does not short-circuit on failure.
    /// </summary>
    /// <param name="onFailure">An action to execute with the error collection.</param>
    /// <returns>The current result instance, enabling fluent chaining.</returns>
    public Result<TValue> TapError(Action<IReadOnlyErrorCollection> onFailure)
    {
        Switch(_ => { }, onFailure);
        return this;
    }

    /// <summary>
    /// Executes one of the provided asynchronous actions based on whether the result is a success or a failure.
    /// </summary>
    /// <param name="onSuccess">An asynchronous action to execute if the result is a success.</param>
    /// <param name="onFailure">An asynchronous action to execute if the result is a failure.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public abstract ValueTask SwitchAsync(
        Func<TValue, ValueTask> onSuccess,
        Func<IReadOnlyErrorCollection, ValueTask> onFailure);

    /// <summary>
    /// Chains computations by applying the provided function to the successful value.
    /// If the current result is a failure, the computation is skipped, and the error is propagated.
    /// </summary>
    /// <typeparam name="TNewValue">The type of the result of the new computation.</typeparam>
    /// <param name="func">A function to apply to the successful value, producing a new result.</param>
    /// <returns>A new result, which can be either a success or failure, based on the provided function and the current result state.</returns>
    public abstract Result<TNewValue> Bind<TNewValue>(Func<TValue, Result<TNewValue>> func);

    /// <summary>
    /// Chains computations by applying the provided function to the successful value.
    /// If the current result is a failure, the computation is skipped, and the error is propagated.
    /// </summary>
    /// <typeparam name="TNewValue">The type of the result of the new computation.</typeparam>
    /// <param name="func">A function to apply to the successful value, producing a new result.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> whose result contains a new result, which can be either a success or failure, based on the provided function and the current result state.</returns>
    public abstract ValueTask<Result<TNewValue>> BindAsync<TNewValue>(Func<TValue, ValueTask<Result<TNewValue>>> func);

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a successful computation.
    /// </summary>
    /// <param name="value">The value that resulted from the successful computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a successful computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Success(TValue value) => new SuccessResult(value);

    /// <summary>
    /// Creates a completed <see cref="ValueTask{TResult}"/> wrapping a <see cref="Result{TValue}"/> that
    /// represents a successful computation.
    /// </summary>
    /// <param name="value">The value that resulted from the successful computation.</param>
    /// <returns>A completed <see cref="ValueTask{TResult}"/> whose result represents a successful computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TValue>> SuccessAsync(TValue value) => ValueTask.FromResult(Success(value));

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errors">The errors that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors"/> is empty.</exception>
    /// <remarks>
    /// An immutable snapshot of <paramref name="errors"/> is captured, so a mutable
    /// <see cref="ErrorCollection"/> may be passed and kept in use afterwards without affecting the
    /// result returned here.
    /// </remarks>
    public static Result<TValue> Failure(IEnumerable<Error> errors)
    {
        var snapshot = ImmutableErrorCollection.From(errors);
        if (!snapshot.HasErrors)
        {
            throw new ArgumentException("A failed result must carry at least one error.", nameof(errors));
        }

        return new FailureResult(snapshot);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.
    /// </summary>
    /// <param name="error">The error that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Failure(Error error) => new FailureResult(error);

    /// <summary>
    /// Creates a new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.
    /// </summary>
    /// <param name="errorCode">The error code that resulted from the failed computation.</param>
    /// <param name="errorMessage">The error message that resulted from the failed computation.</param>
    /// <returns>A new instance of the <see cref="Result{TValue}"/> class that represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Failure(ErrorCode errorCode, string errorMessage)
        => Failure(new Error(errorCode, errorMessage));

    /// <summary>
    /// Creates a completed <see cref="ValueTask{TResult}"/> wrapping a <see cref="Result{TValue}"/> that
    /// represents a failed computation.
    /// </summary>
    /// <param name="errors">The error collection that resulted from the failed computation.</param>
    /// <returns>A completed <see cref="ValueTask{TResult}"/> whose result represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TValue>> FailureAsync(IEnumerable<Error> errors) =>
        ValueTask.FromResult(Failure(errors));

    /// <summary>
    /// Creates a completed <see cref="ValueTask{TResult}"/> wrapping a <see cref="Result{TValue}"/> that
    /// represents a failed computation.
    /// </summary>
    /// <param name="error">The error that resulted from the failed computation.</param>
    /// <returns>A completed <see cref="ValueTask{TResult}"/> whose result represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TValue>> FailureAsync(Error error) =>
        ValueTask.FromResult(Failure(error));

    /// <summary>
    /// Creates a completed <see cref="ValueTask{TResult}"/> wrapping a <see cref="Result{TValue}"/> that
    /// represents a failed computation.
    /// </summary>
    /// <param name="errorCode">The error code that resulted from the failed computation.</param>
    /// <param name="errorMessage">The error message that resulted from the failed computation.</param>
    /// <returns>A completed <see cref="ValueTask{TResult}"/> whose result represents a failed computation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Result<TValue>> FailureAsync(ErrorCode errorCode, string errorMessage) =>
        ValueTask.FromResult(Failure(new Error(errorCode, errorMessage)));

    private sealed class SuccessResult(TValue value) : Result<TValue>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Match<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onSuccess(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<TResult> MatchAsync<TResult>(
            Func<TValue, ValueTask<TResult>> onSuccess,
            Func<IReadOnlyErrorCollection, ValueTask<TResult>> onFailure
        ) => onSuccess(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Switch(Action<TValue> onSuccess, Action<IReadOnlyErrorCollection> onFailure)
            => onSuccess(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask SwitchAsync(Func<TValue, ValueTask> onSuccess, Func<IReadOnlyErrorCollection, ValueTask> onFailure)
            => onSuccess(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Result<TNewValue> Bind<TNewValue>(Func<TValue, Result<TNewValue>> func)
            => func(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<Result<TNewValue>> BindAsync<TNewValue>(
            Func<TValue, ValueTask<Result<TNewValue>>> func)
        {
            return func(value);
        }
    }

    private sealed class FailureResult : Result<TValue>
    {
        private readonly IReadOnlyErrorCollection _error;

        public FailureResult(Error error)
        {
            _error = ImmutableErrorCollection.From([error]);
        }

        public FailureResult(IReadOnlyErrorCollection error)
        {
            _error = error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Match<TResult>(
            Func<TValue, TResult> onSuccess,
            Func<IReadOnlyErrorCollection, TResult> onFailure
        ) => onFailure(_error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Switch(Action<TValue> onSuccess, Action<IReadOnlyErrorCollection> onFailure)
            => onFailure(_error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Result<TNewValue> Bind<TNewValue>(Func<TValue, Result<TNewValue>> func)
            => Result<TNewValue>.Failure(_error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<Result<TNewValue>> BindAsync<TNewValue>(
            Func<TValue, ValueTask<Result<TNewValue>>> func)
            => ValueTask.FromResult(Result<TNewValue>.Failure(_error));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask<TResult> MatchAsync<TResult>(
            Func<TValue, ValueTask<TResult>> onSuccess,
            Func<IReadOnlyErrorCollection, ValueTask<TResult>> onFailure)
            => onFailure(_error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ValueTask SwitchAsync(Func<TValue, ValueTask> onSuccess, Func<IReadOnlyErrorCollection, ValueTask> onFailure)
            => onFailure(_error);
    }
}
