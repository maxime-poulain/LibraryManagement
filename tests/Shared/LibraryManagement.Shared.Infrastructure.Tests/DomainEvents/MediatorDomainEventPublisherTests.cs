using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.DomainEvents;
using Mediator;

namespace LibraryManagement.Shared.Infrastructure.Tests.DomainEvents;

public sealed class MediatorDomainEventPublisherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ItPublishes_EveryEventRaised()
    {
        var publisher = new RecordingPublisher();
        var aggregate = new Raiser(new SomethingHappened(), new SomethingElseHappened());

        await new MediatorDomainEventPublisher(publisher).PublishAsync([aggregate], Token);

        publisher.Published.Select(published => published.GetType())
            .ShouldBe([typeof(SomethingHappened), typeof(SomethingElseHappened)]);
    }

    [Fact]
    public async Task ItPublishes_TheEventsRuntimeTypeRatherThanTheInterface()
    {
        // The one mistake this class exists to avoid. Handing the mediator a variable typed
        // IDomainEvent closes its generic overload on the interface, which no handler is registered
        // for: every event would be published to nobody, successfully, and forever.
        var publisher = new RecordingPublisher();

        await new MediatorDomainEventPublisher(publisher)
            .PublishAsync([new Raiser(new SomethingHappened())], Token);

        publisher.Published.ShouldHaveSingleItem().ShouldBeOfType<SomethingHappened>();
    }

    [Fact]
    public async Task ItClears_WhatItTook()
    {
        var aggregate = new Raiser(new SomethingHappened());

        await new MediatorDomainEventPublisher(new RecordingPublisher()).PublishAsync([aggregate], Token);

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task ItClears_BeforePublishingRatherThanAfter()
    {
        // The ordering the drain loop rests on. A handler is entitled to act on the same aggregate
        // and raise a further event; clearing afterwards would wipe that new event along with the one
        // that caused it, and the second round would find nothing left to publish.
        var aggregate = new Raiser(new SomethingHappened());
        var publisher = new RecordingPublisher();
        var stillHeldWhenPublishing = -1;
        publisher.OnPublish = () => stillHeldWhenPublishing = aggregate.DomainEvents.Count;

        await new MediatorDomainEventPublisher(publisher).PublishAsync([aggregate], Token);

        stillHeldWhenPublishing.ShouldBe(0);
    }

    [Fact]
    public async Task WithNothingRaised_ItPublishesNothing()
    {
        var publisher = new RecordingPublisher();

        await new MediatorDomainEventPublisher(publisher).PublishAsync([new Raiser()], Token);

        publisher.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithNullSources_ItThrows()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new MediatorDomainEventPublisher(new RecordingPublisher())
                .PublishAsync(null!, Token));
    }

    private sealed record SomethingHappened : DomainEvent;

    private sealed record SomethingElseHappened : DomainEvent;

    private sealed class Raiser(params IDomainEvent[] raised) : IHasDomainEvents
    {
        private readonly List<IDomainEvent> _raised = [.. raised];

        public IReadOnlyList<IDomainEvent> DomainEvents => _raised;

        public void ClearDomainEvents() => _raised.Clear();
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Published { get; } = [];

        public Action? OnPublish { get; set; }

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
        {
            OnPublish?.Invoke();
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
