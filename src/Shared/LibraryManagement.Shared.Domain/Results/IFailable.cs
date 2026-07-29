using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Results;

/// <summary>
/// A result type that can report a failure and be built from one, whichever of the two it is.
/// </summary>
/// <typeparam name="TSelf">The implementing type itself.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Result"/> and <see cref="Result{TValue}"/> share no base class, deliberately: that
/// separation is what makes <c>ICommand&lt;Result&lt;T&gt;&gt;</c> a compile error, and it is how
/// commands are kept from returning values. It also leaves code that is generic over "the result of
/// a message" unable to build a failure, because there is no type to call a factory on.
/// </para>
/// <para>
/// A <see langword="static"/> <see langword="abstract"/> member closes that gap without reopening
/// the other. The capability is shared through an interface; the class hierarchy is untouched, so
/// <see cref="Result{TValue}"/> still does not derive from <see cref="Result"/> and the constraint
/// <c>where TResult : Result</c> still refuses it. It is the same technique
/// <see cref="IEntityId{TSelf}"/> already uses for <c>FromValue</c>.
/// </para>
/// <para>
/// None of these members is new: both types already declare them with these exact signatures. This
/// interface only makes them reachable from a generic.
/// </para>
/// </remarks>
public interface IFailable<TSelf>
    where TSelf : IFailable<TSelf>
{
    /// <summary>
    /// Creates a failed result carrying <paramref name="errors"/>.
    /// </summary>
    /// <param name="errors">The errors. Must not be empty.</param>
    /// <returns>A failed result.</returns>
    static abstract TSelf Failure(IEnumerable<Error> errors);

    /// <summary>
    /// Indicates whether this result is a failure.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this result carries errors; <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    /// Prefer <c>Match</c> wherever the concrete type is known: it forces both outcomes to be
    /// handled. This member exists for code that cannot name the type, and therefore cannot supply
    /// the two branches <c>Match</c> asks for.
    /// </remarks>
    bool HasErrors();

    /// <summary>
    /// Executes the provided action if the result is a failure, then returns the same result.
    /// </summary>
    /// <param name="onFailure">An action to execute if the result is a failure, taking the error collection as a parameter.</param>
    /// <returns>The current result instance, enabling fluent chaining.</returns>
    /// <remarks>
    /// What <see cref="Failure"/> is to building a failure generically, this is to reading one: code
    /// that cannot name the concrete type — the logging behavior, reporting the codes a message was
    /// refused with — can still be handed the errors when there are any.
    /// </remarks>
    TSelf TapError(Action<IReadOnlyErrorCollection> onFailure);
}
