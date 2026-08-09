using LibraryManagement.Circulation.Application.Holds.CancelHoldsForDebt;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The reaction to a debt: every place the borrower held goes, and every copy set aside for them
/// is offered on — unless the debt is already history by the time the news arrives.
/// </summary>
public sealed class CancelHoldsForDebtCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubBalances _balances = new();

    private ValueTask<Result> Handle(Guid borrowerId)
        => new CancelHoldsForDebtCommandHandler(
                _queues, _balances, CirculationPolicy.Current, FrozenClock.At(Today))
            .Handle(new CancelHoldsForDebtCommand(borrowerId), Token);

    [Fact]
    public async Task Handle_CancelsEveryPlace_AndOffersTheReleasedCopyOn()
    {
        var owing = BorrowerId.Generate();
        var next = BorrowerId.Generate();
        _balances.Owing(owing.Value, 12m);

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), owing, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), next, ThisMorning.AddHours(1));
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), new HashSet<BorrowerId>());
        queue.ClearDomainEvents();
        _queues.With(queue);

        var outcome = await Handle(owing.Value);

        outcome.HasErrors().ShouldBeFalse();
        queue.DomainEvents.OfType<HoldCancelledForDebt>().ShouldHaveSingleItem();
        queue.DomainEvents.OfType<HoldReadyForPickup>().Single().BorrowerId.ShouldBe(next);
        queue.Holds.Single().BorrowerId.ShouldBe(next);
    }

    [Fact]
    public async Task Handle_ADebtAlreadyRepaid_CancelsNothing()
    {
        // The event is the trigger; the live balance is the truth. Between the fine and the
        // drain's delivery the most ordinary act at a desk is paying, and the borrower who just
        // cleared their debt keeps their months of queue position.
        var borrower = BorrowerId.Generate();

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), borrower, ThisMorning);
        _queues.With(queue);

        var outcome = await Handle(borrower.Value);

        outcome.HasErrors().ShouldBeFalse();
        queue.Holds.ShouldHaveSingleItem();
        queue.DomainEvents.OfType<HoldCancelledForDebt>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ABorrowerWithNoPlaces_AnswersSuccess()
    {
        // The ordinary case, arriving from another module's drain: a refusal would make that
        // drain replay a correctly handled message forever.
        var owing = BorrowerId.Generate();
        _balances.Owing(owing.Value, 3m);

        (await Handle(owing.Value)).HasErrors().ShouldBeFalse();
    }
}
