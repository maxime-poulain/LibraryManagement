using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Results;

/// <summary>
/// Converts between <see cref="Result"/> and <see cref="Result{TValue}"/>, and combines several
/// valued results into one.
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own type on purpose. <see cref="Result"/> and <see cref="Result{TValue}"/> do
/// not derive from one another and do not reference one another — that separation is what makes
/// <c>ICommand&lt;Result&lt;T&gt;&gt;</c> a compile error, and therefore what enforces commands
/// returning no value. Putting a conversion on either type would create the very dependency the
/// design avoids, so the knowledge of both lives here, in one file that can be deleted without
/// touching either.
/// </para>
/// <para>
/// What it is for: a command handler builds value objects, which yield <see cref="Result{TValue}"/>,
/// and must return a <see cref="Result"/>. Without these, every handler ends in nested
/// <c>Match</c> calls whose depth grows with the number of values it assembled.
/// </para>
/// </remarks>
public static class ResultExtensions
{
    /// <summary>
    /// Discards the value of a result, keeping only whether it succeeded and the errors it carries.
    /// </summary>
    /// <typeparam name="TValue">The type of the discarded value.</typeparam>
    /// <param name="result">The result to collapse.</param>
    /// <returns>
    /// A successful <see cref="Result"/> when <paramref name="result"/> succeeded; a failed one
    /// carrying the same errors otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static Result ToResult<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Match(_ => Result.Success(), Result.Failure);
    }

    /// <summary>
    /// Chains a valued result into a step that produces no value — typically the last step of a
    /// command handler, where the work is performed and only success or failure is reported.
    /// </summary>
    /// <typeparam name="TValue">The type of the value the chain carries so far.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="func">The step to run when <paramref name="result"/> succeeded.</param>
    /// <returns>
    /// Whatever <paramref name="func"/> returned, or a failure carrying the errors of
    /// <paramref name="result"/> without running <paramref name="func"/> at all.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result"/> or <paramref name="func"/> is null.
    /// </exception>
    public static Result Bind<TValue>(this Result<TValue> result, Func<TValue, Result> func)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(func);

        return result.Match(func, Result.Failure);
    }

    /// <summary>
    /// Chains a valueless result into a step that produces a value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value the step produces.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="func">The step to run when <paramref name="result"/> succeeded.</param>
    /// <returns>
    /// Whatever <paramref name="func"/> returned, or a failure carrying the errors of
    /// <paramref name="result"/> without running <paramref name="func"/> at all.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result"/> or <paramref name="func"/> is null.
    /// </exception>
    public static Result<TValue> Bind<TValue>(this Result result, Func<Result<TValue>> func)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(func);

        return result.Match(func, Result<TValue>.Failure);
    }

    /// <summary>
    /// Combines two results, accumulating the errors of <b>both</b> rather than stopping at the
    /// first failure.
    /// </summary>
    /// <typeparam name="TFirst">The type of the first value.</typeparam>
    /// <typeparam name="TSecond">The type of the second value.</typeparam>
    /// <param name="first">The first result.</param>
    /// <param name="second">The second result.</param>
    /// <returns>
    /// A success carrying both values, or a failure carrying every error either side reported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// Accumulation is the point. Independent inputs — an ISBN and a title read from the same form —
    /// should all be reported at once; telling an employee about the second mistake only after they
    /// fixed the first is a worse experience than <c>Bind</c>'s short-circuit would suggest.
    /// </remarks>
    public static Result<(TFirst First, TSecond Second)> Combine<TFirst, TSecond>(
        this Result<TFirst> first,
        Result<TSecond> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var errors = new ErrorCollection();
        first.TapError(errors.AddErrors);
        second.TapError(errors.AddErrors);

        return errors.Count > 0
            ? Result<(TFirst First, TSecond Second)>.Failure(errors)
            : Result<(TFirst First, TSecond Second)>.Success((ValueOf(first), ValueOf(second)));
    }

    /// <summary>
    /// Combines three results, accumulating the errors of all of them.
    /// </summary>
    /// <typeparam name="TFirst">The type of the first value.</typeparam>
    /// <typeparam name="TSecond">The type of the second value.</typeparam>
    /// <typeparam name="TThird">The type of the third value.</typeparam>
    /// <param name="first">The first result.</param>
    /// <param name="second">The second result.</param>
    /// <param name="third">The third result.</param>
    /// <returns>
    /// A success carrying the three values, or a failure carrying every error reported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// There is deliberately no overload beyond three. An operation that assembles more values than
    /// that is telling you the assembling belongs in the aggregate's own factory, which can
    /// accumulate into an <see cref="ErrorCollection"/> and return a single
    /// <see cref="Result{TValue}"/>.
    /// </remarks>
    public static Result<(TFirst First, TSecond Second, TThird Third)> Combine<TFirst, TSecond, TThird>(
        this Result<TFirst> first,
        Result<TSecond> second,
        Result<TThird> third)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(third);

        var errors = new ErrorCollection();
        first.TapError(errors.AddErrors);
        second.TapError(errors.AddErrors);
        third.TapError(errors.AddErrors);

        return errors.Count > 0
            ? Result<(TFirst First, TSecond Second, TThird Third)>.Failure(errors)
            : Result<(TFirst First, TSecond Second, TThird Third)>.Success(
                (ValueOf(first), ValueOf(second), ValueOf(third)));
    }

    // Only ever called after the caller has established that every result succeeded, so the failure
    // branch is unreachable. It exists to avoid nesting one Match per combined value.
    private static TValue ValueOf<TValue>(Result<TValue> result)
        => result.Match(
            value => value,
            _ => throw new InvalidOperationException(
                "Combine read the value of a failed result. This is a bug in ResultExtensions: the "
                + "error accumulation above must run before any value is read."));
}
