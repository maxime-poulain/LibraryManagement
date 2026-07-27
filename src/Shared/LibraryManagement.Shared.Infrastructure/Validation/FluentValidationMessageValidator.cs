using FluentValidation;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Validation;

/// <summary>
/// Implements <see cref="IMessageValidator"/> with FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// A message's concrete type is only known at dispatch time, so the matching
/// <c>IValidator&lt;TMessage&gt;</c> has to be resolved then. That resolution is service location,
/// which is why it is confined to this one adapter: the dispatchers depend on
/// <see cref="IMessageValidator"/> and never name a container or a validation library.
/// </para>
/// <para>
/// A message with no registered validator is valid. An absent validator means there is nothing about
/// its shape worth checking, not that someone forgot — and requiring an empty validator per message,
/// as some pipelines do, buys a runtime check for something an architecture test settles before the
/// code is merged.
/// </para>
/// </remarks>
public sealed class FluentValidationMessageValidator(IServiceProvider services) : IMessageValidator
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyErrorCollection> ValidateAsync(
        IMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var validatorContract = typeof(IValidator<>).MakeGenericType(message.GetType());
        var validators = services.GetServices(validatorContract).OfType<IValidator>().ToList();

        if (validators.Count == 0)
        {
            return ImmutableErrorCollection.Empty;
        }

        var context = new ValidationContext<object>(message);
        var errors = new ErrorCollection();

        // Sequentially rather than in parallel: an asynchronous rule may reach the database, and the
        // DbContext behind it is not safe to use from several tasks at once.
        foreach (var validator in validators)
        {
            var outcome = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);

            foreach (var failure in outcome.Errors)
            {
                errors.Add(new Error(
                    SharedErrorCodes.ValidationFailed,
                    failure.ErrorMessage,
                    failure.PropertyName));
            }
        }

        return errors.AsReadOnly();
    }
}
