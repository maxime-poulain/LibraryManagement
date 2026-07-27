using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// Reports a fixed verdict and records that it was consulted, so a test can assert both what the
// dispatcher did with the verdict and whether it asked for one at all.
public sealed class RecordingCommandValidator(params Error[] errors) : ICommandValidator
{
    public int CallCount { get; private set; }

    public ICommandBase? LastCommand { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public static RecordingCommandValidator Accepting() => new();

    public static RecordingCommandValidator Rejecting(params Error[] errors) => new(errors);

    public ValueTask<IReadOnlyErrorCollection> ValidateAsync(
        ICommandBase command,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastCommand = command;
        LastCancellationToken = cancellationToken;

        return ValueTask.FromResult(ImmutableErrorCollection.From(errors));
    }
}
