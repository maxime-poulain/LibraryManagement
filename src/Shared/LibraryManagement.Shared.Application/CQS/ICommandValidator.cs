using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Application.CQS;

/// <summary>
/// Checks the shape of a command before it is handled: required fields, lengths, ranges, formats.
/// </summary>
/// <remarks>
/// <para>
/// This validates <em>input</em>, never business rules. Whether an ISBN is thirteen characters is a
/// question about the field; whether a member is allowed to borrow is a question about the library,
/// and belongs to the domain — to the aggregate that owns the rule and to the value object factories
/// that return a <see cref="Domain.Results.Result{TValue}"/>. Duplicating a domain rule here would
/// create a second source of truth that drifts from the first.
/// </para>
/// <para>
/// The abstraction exists so the application layer never names a validation library. It also lets a
/// command with no validator at all pass: an absent validator means nothing to check, not a
/// misconfiguration. Enforcing that every command declares one is an architecture test's job — it
/// is a static property of the codebase, and failing on it at dispatch time would report it in
/// production, on the first command nobody had exercised.
/// </para>
/// </remarks>
public interface ICommandValidator
{
    /// <summary>
    /// Validates the shape of <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// Every problem found, so an employee sees all of them at once rather than one per attempt;
    /// an empty collection when the command is well formed.
    /// </returns>
    ValueTask<IReadOnlyErrorCollection> ValidateAsync(
        ICommandBase command,
        CancellationToken cancellationToken = default);
}
