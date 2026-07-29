using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.DomainEvents;
using Mediator;

namespace LibraryManagement.Shared.Infrastructure.Tests.DomainEvents;

public sealed class MediatorDomainEventPublisherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ItPublishes_TheEventsRuntimeTypeRatherThanTheInterface()
    {
        // The one mistake this class exists to avoid. Handing the mediator a parameter typed
        // IDomainEvent closes its generic overload on the interface, which no handler is registered
        // for: every event the drain delivered would reach nobody, successfully, forever.
        var publisher = new RecordingPublisher();

        await new MediatorDomainEventPublisher(publisher)
            .PublishAsync(new SomethingHappened(), Token);

        publisher.Published.ShouldHaveSingleItem().ShouldBeOfType<SomethingHappened>();
    }

    [Fact]
    public async Task WithANullEvent_ItThrows()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new MediatorDomainEventPublisher(new RecordingPublisher())
                .PublishAsync(null!, Token));
    }

    private sealed record SomethingHappened : DomainEvent;

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Published { get; } = [];

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return ValueTask.CompletedTask;
        }

        public ValueTask Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Publish((object)notification, cancellationToken);
    }
}
