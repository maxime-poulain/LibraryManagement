using FluentValidation;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Validation;

/// <summary>
/// Implements <see cref="ICommandValidator"/> with FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// A command's concrete type is only known at dispatch time, so the matching
/// <c>IValidator&lt;TCommand&gt;</c> has to be resolved then. That resolution is service location,
/// which is why it is confined to this one adapter: the dispatcher depends on
/// <see cref="ICommandValidator"/> and never names a container or a validation library.
/// </para>
/// <para>
/// A command with no registered validator is valid. An absent validator means there is nothing
/// about the shape of that command worth checking, not that someone forgot — and requiring an empty
/// validator per command, as some pipelines do, buys a runtime check for something an architecture
/// test settles before the code is merged.
/// </para>
/// </remarks>
public sealed class FluentValidationCommandValidator(IServiceProvider services) : ICommandValidator
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyErrorCollection> ValidateAsync(
        ICommandBase command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validatorContract = typeof(IValidator<>).MakeGenericType(command.GetType());
        var validators = services.GetServices(validatorContract).OfType<IValidator>().ToList();

        if (validators.Count == 0)
        {
            return ImmutableErrorCollection.Empty;
        }

        var context = new ValidationContext<object>(command);
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
