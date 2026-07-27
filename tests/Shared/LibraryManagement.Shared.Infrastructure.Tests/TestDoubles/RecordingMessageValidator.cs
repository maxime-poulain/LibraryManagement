using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using Mediator;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// Reports a fixed verdict and records that it was consulted, so a test can assert both what the
// dispatcher did with the verdict and whether it asked for one at all.
public sealed class RecordingMessageValidator(params Error[] errors) : IMessageValidator
{
    public int CallCount { get; private set; }

    public IMessage? LastMessage { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public static RecordingMessageValidator Accepting() => new();

    public static RecordingMessageValidator Rejecting(params Error[] errors) => new(errors);

    public ValueTask<IReadOnlyErrorCollection> ValidateAsync(
        IMessage message,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastMessage = message;
        LastCancellationToken = cancellationToken;

        return ValueTask.FromResult(ImmutableErrorCollection.From(errors));
    }
}
