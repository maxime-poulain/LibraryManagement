using LibraryManagement.Circulation.Application.Holds.MergeHoldQueues;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The store questions a merge asks, and nothing about which claims survive — that is the domain
/// service's, and this class injects the real one so a refusal is propagated rather than simulated.
/// </summary>
public sealed class MergeHoldQueuesCommandHandlerTests
{
    private static readonly DateTimeOffset ThisMorning =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();

    private readonly HoldQueueMergeDomainService _merge = new();

    private ValueTask<Result> Handle(EditionId absorbed, EditionId surviving)
        => new MergeHoldQueuesCommandHandler(_queues, _merge)
            .Handle(new MergeHoldQueuesCommand(absorbed.Value, surviving.Value), Token);

    private HoldQueue AQueueWithAClaim(BorrowerId borrowerId, int hoursIn = 0)
    {
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning.AddHours(hoursIn));
        queue.ClearDomainEvents();
        _queues.With(queue);
        return queue;
    }

    [Fact]
    public async Task Handle_MovesTheClaimsIntoTheSurvivingQueue()
    {
        var absorbed = AQueueWithAClaim(BorrowerId.Generate());
        var surviving = AQueueWithAClaim(BorrowerId.Generate(), hoursIn: 1);

        var outcome = await Handle(absorbed.Id, surviving.Id);

        outcome.HasErrors().ShouldBeFalse();
        absorbed.Holds.ShouldBeEmpty();
        surviving.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_ARecordNobodyQueuedFor_Succeeds()
    {
        // A queue exists from its first claim on, so most editions have none. This arrives from
        // another module's drain, where a refusal would replay a message handled correctly forever.
        var outcome = await Handle(EditionId.Generate(), EditionId.Generate());

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ASurvivorWithNoQueueOfItsOwn_GetsOne()
    {
        // The absorbed queue's claims have to land somewhere: an edition nobody had queued for
        // inherits the people waiting for what turns out to be the same book.
        var absorbed = AQueueWithAClaim(BorrowerId.Generate());
        var survivingId = EditionId.Generate();

        var outcome = await Handle(absorbed.Id, survivingId);

        outcome.HasErrors().ShouldBeFalse();
        _queues.Added.ShouldContain(queue => queue.Id == survivingId && queue.Holds.Count == 1);
    }

    [Fact]
    public async Task Handle_ARedeliveredAnnouncement_ChangesNothingASecondTime()
    {
        var absorbed = AQueueWithAClaim(BorrowerId.Generate());
        var surviving = AQueueWithAClaim(BorrowerId.Generate(), hoursIn: 1);

        await Handle(absorbed.Id, surviving.Id);
        surviving.ClearDomainEvents();

        var outcome = await Handle(absorbed.Id, surviving.Id);

        outcome.HasErrors().ShouldBeFalse();
        surviving.Holds.Count.ShouldBe(2);
        surviving.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_AnEmptyAbsorbedQueue_DoesNotOpenOneForTheSurvivor()
    {
        // An empty queue row is not a claim on anything, and a merge that manufactured one would
        // leave the store with a queue nobody ever asked for.
        var absorbed = HoldQueue.For(EditionId.Generate());
        _queues.With(absorbed);
        var survivingId = EditionId.Generate();

        (await Handle(absorbed.Id, survivingId)).HasErrors().ShouldBeFalse();

        _queues.Added.ShouldNotContain(queue => queue.Id == survivingId);
    }

    [Fact]
    public async Task Handle_PropagatesTheServicesRefusal()
    {
        var queue = AQueueWithAClaim(BorrowerId.Generate());

        var outcome = await Handle(queue.Id, queue.Id);

        outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList())
            .ShouldContain(CirculationErrorCodes.QueueCannotAbsorbItself);
    }

    [Fact]
    public async Task Handle_DemandsACommand()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await new MergeHoldQueuesCommandHandler(_queues, _merge).Handle(null!, Token));
    }
}
