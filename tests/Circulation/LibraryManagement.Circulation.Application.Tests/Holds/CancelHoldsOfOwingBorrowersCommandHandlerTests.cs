using LibraryManagement.Circulation.Application.Holds.CancelHoldsOfOwingBorrowers;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The daily reconciliation behind the debt event: a message can die on the drain's floor, and
/// the sweep is what keeps the convergence converging.
/// </summary>
public sealed class CancelHoldsOfOwingBorrowersCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubBalances _balances = new();

    private ValueTask<Result> Handle()
        => new CancelHoldsOfOwingBorrowersCommandHandler(
                _queues, _balances, CirculationPolicy.Current, FrozenClock.At(Today))
            .Handle(new CancelHoldsOfOwingBorrowersCommand(), Token);

    [Fact]
    public async Task Handle_CancelsTheZombieClaims_AndOnlyThose()
    {
        // The zombie: the debt event died after five attempts, and this borrower kept a place a
        // debt forbids — skipped at every promotion, never told, never removed.
        var owing = BorrowerId.Generate();
        var clear = BorrowerId.Generate();
        _balances.Owing(owing.Value, 5m);

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), owing, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), clear, ThisMorning.AddHours(1));
        _queues.With(queue);

        var outcome = await Handle();

        outcome.HasErrors().ShouldBeFalse();
        queue.DomainEvents.OfType<HoldCancelledForDebt>().Single().BorrowerId.ShouldBe(owing);
        queue.Holds.Single().BorrowerId.ShouldBe(clear);
    }

    [Fact]
    public async Task Handle_OnADayEveryEventArrived_FindsNothing()
    {
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        _queues.With(queue);

        var outcome = await Handle();

        outcome.HasErrors().ShouldBeFalse();
        queue.Holds.ShouldHaveSingleItem();
        queue.DomainEvents.OfType<HoldCancelledForDebt>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Twice_CancelsOnce()
    {
        // Idempotent by state rather than by memory: a borrower cancelled today holds nothing
        // tomorrow.
        var owing = BorrowerId.Generate();
        _balances.Owing(owing.Value, 5m);

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), owing, ThisMorning);
        _queues.With(queue);

        await Handle();
        queue.ClearDomainEvents();

        (await Handle()).HasErrors().ShouldBeFalse();

        queue.DomainEvents.ShouldBeEmpty();
    }
}
