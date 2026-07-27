using LibraryManagement.Shared.Domain.Errors;
using Mediator;

namespace LibraryManagement.Shared.Application.CQS;

/// <summary>
/// Checks the shape of a command or a query before it is handled: required fields, lengths, ranges,
/// formats.
/// </summary>
/// <remarks>
/// <para>
/// One contract for both, over <see cref="IMessage"/> — the marker both <see cref="ICommandBase"/>
/// and <see cref="IQuery"/> already derive from. A second interface for queries would have been the
/// same method, the same adapter and the same registration under another name: nothing about
/// checking that a field is present differs between asking for something and changing it.
/// </para>
/// <para>
/// This validates <em>input</em>, never business rules. Whether an ISBN is thirteen characters is a
/// question about the field; whether a member is allowed to borrow is a question about the library,
/// and belongs to the domain — to the aggregate that owns the rule and to the value object factories
/// that return a <see cref="Domain.Results.Result{TValue}"/>. Duplicating a domain rule here would
/// create a second source of truth that drifts from the first.
/// </para>
/// <para>
/// The abstraction exists so the application layer never names a validation library. It also lets a
/// message with no validator at all pass: an absent validator means nothing to check, not a
/// misconfiguration. Enforcing that every message declares one is an architecture test's job — it is
/// a static property of the codebase, and failing on it at dispatch time would report it in
/// production, on the first message nobody had exercised.
/// </para>
/// </remarks>
public interface IMessageValidator
{
    /// <summary>
    /// Validates the shape of <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The command or query to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// Every problem found, so an employee sees all of them at once rather than one per attempt;
    /// an empty collection when the message is well formed.
    /// </returns>
    ValueTask<IReadOnlyErrorCollection> ValidateAsync(
        IMessage message,
        CancellationToken cancellationToken = default);
}
