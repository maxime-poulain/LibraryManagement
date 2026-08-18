using LibraryManagement.Circulation.Application.Holds.CombineHoldsOfMergedBorrower;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The store question a member merge asks of the queues — which of them does the absorbed record
/// wait in — and nothing about which claims survive: that is each queue's own rule, and this class
/// asserts through the aggregates it hands the handler.
/// </summary>
public sealed class CombineHoldsOfMergedBorrowerCommandHandlerTests
{
    private static readonly DateTimeOffset ThisMorning =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();

    private ValueTask<Result> Handle(BorrowerId absorbed, BorrowerId surviving)
        => new CombineHoldsOfMergedBorrowerCommandHandler(_queues)
            .Handle(new CombineHoldsOfMergedBorrowerCommand(absorbed.Value, surviving.Value), Token);

    private HoldQueue AQueueWith(params (BorrowerId Borrower, int HoursIn)[] claims)
    {
        var queue = HoldQueue.For(EditionId.Generate());

        foreach (var (borrower, hoursIn) in claims)
        {
            queue.PlaceHold(HoldId.Generate(), borrower, ThisMorning.AddHours(hoursIn));
        }

        queue.ClearDomainEvents();
        _queues.With(queue);
        return queue;
    }

    [Fact]
    public async Task Handle_CombinesTheClaimsInEveryQueueTheAbsorbedRecordWaitsIn()
    {
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var one = AQueueWith((absorbed, 0));
        var another = AQueueWith((absorbed, 0), (BorrowerId.Generate(), 1));

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        one.Holds.ShouldHaveSingleItem().BorrowerId.ShouldBe(surviving);
        another.Holds.Count(hold => hold.BorrowerId == surviving).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_AQueueWhereBothFilesWait_KeepsTheEarliestClaim()
    {
        // The handler only finds the queue; that the earliest claim survives is the queue's own
        // rule, exercised here through the real aggregate rather than simulated.
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var queue = AQueueWith((absorbed, 0), (surviving, 1));

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        var remaining = queue.Holds.ShouldHaveSingleItem();
        remaining.BorrowerId.ShouldBe(surviving);
        remaining.PlacedOn.ShouldBe(ThisMorning);
    }

    [Fact]
    public async Task Handle_ARecordWaitingNowhere_Succeeds()
    {
        // The ordinary case, and the one a refusal would turn into a message the announcing drain
        // replays forever.
        var outcome = await Handle(BorrowerId.Generate(), BorrowerId.Generate());

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ARedeliveredAnnouncement_ChangesNothingASecondTime()
    {
        // After the first pass the absorbed identifier is on no live claim, so the sweep finds no
        // queue at all — idempotence by the question asked, not by a mark kept.
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var queue = AQueueWith((absorbed, 0), (surviving, 1));

        await Handle(absorbed, surviving);
        queue.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        queue.Holds.ShouldHaveSingleItem();
        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_DemandsACommand()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await new CombineHoldsOfMergedBorrowerCommandHandler(_queues).Handle(null!, Token));
    }
}
