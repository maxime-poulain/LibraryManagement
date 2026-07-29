using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Mediator;

namespace LibraryManagement.Shared.Infrastructure.Behaviors;

/// <summary>
/// Checks a message's shape before its handler sees it, and answers with a failure instead of
/// calling the handler when the shape is wrong.
/// </summary>
/// <typeparam name="TMessage">Any command or query.</typeparam>
/// <typeparam name="TResponse">Its result, whichever of the two result types that is.</typeparam>
/// <param name="validator">The one validator both commands and queries go through.</param>
/// <remarks>
/// <para>
/// One behavior for both. Nothing about checking that a field is present differs between asking for
/// something and changing it, and a second type for queries would be the same method under another
/// name.
/// </para>
/// <para>
/// The constraint on <typeparamref name="TResponse"/> is what makes that possible.
/// <see cref="Result"/> and <see cref="Result{TValue}"/> share no base class — deliberately, since
/// that is what makes <c>ICommand&lt;Result&lt;T&gt;&gt;</c> a compile error — so this method could
/// not otherwise build the failure it needs to return. <see cref="IFailable{TSelf}"/> lends the
/// factory without lending a hierarchy.
/// </para>
/// <para>
/// Placed before <see cref="UnitOfWorkBehavior{TMessage, TResponse}"/> in the pipeline the
/// composition root declares, and the order is the guarantee rather than a preference: a command
/// rejected for a missing field must never reach the store. Nothing else would report the two
/// standing the wrong way round.
/// </para>
/// </remarks>
public sealed class ValidationBehavior<TMessage, TResponse>(IMessageValidator validator)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IFailable<TResponse>
{
    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var errors = await validator.ValidateAsync(message, cancellationToken).ConfigureAwait(false);

        return errors.HasErrors
            ? TResponse.Failure(errors)
            : await next(message, cancellationToken).ConfigureAwait(false);
    }
}
