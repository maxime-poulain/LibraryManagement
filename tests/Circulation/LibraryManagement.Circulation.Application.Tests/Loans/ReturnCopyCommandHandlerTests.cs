using LibraryManagement.Circulation.Application.Loans.ReturnCopy;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

public sealed class ReturnCopyCommandHandlerTests
{
    private static readonly DateOnly CheckedOutOn = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();
    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubBalances _balances = new();

    private ValueTask<Result> Handle(Guid copyId, DateOnly today, CirculationPolicy? policy = null)
        => new ReturnCopyCommandHandler(
                _loans, _queues, _balances, policy ?? CirculationPolicy.Current, FrozenClock.At(today))
            .Handle(new ReturnCopyCommand(copyId), Token);

    private Loan AnActiveLoan(Guid copy, Guid edition)
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Create(copy),
            EditionId.Create(edition),
            BorrowerId.Generate(),
            CheckedOutOn,
            CirculationPolicy.Current);

        _loans.With(loan);
        return loan;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_ClosesTheLoan_AndRecordsTheLateness()
    {
        var copy = Guid.CreateVersion7();
        var loan = AnActiveLoan(copy, Guid.CreateVersion7());

        var outcome = await Handle(copy, loan.DueDate.AddDays(4));

        outcome.HasErrors().ShouldBeFalse();
        loan.Status.ShouldBe(LoanStatus.Returned);
        loan.DomainEvents.OfType<LoanReturned>().Single().DaysLate.ShouldBe(4);
    }

    [Fact]
    public async Task Handle_ACopyNobodyHasOut_IsRefused()
    {
        CodesOf(await Handle(Guid.CreateVersion7(), CheckedOutOn))
            .ShouldContain(CirculationErrorCodes.NothingOnLoan);
    }

    [Fact]
    public async Task Handle_WhenSomeoneWaits_TrapsForTheOldestClaimInGoodStanding()
    {
        // The moment that justifies holds and loans living in one context: the loan closes and
        // the queue traps, in one command, on the aggregates, directly.
        var copy = Guid.CreateVersion7();
        var edition = Guid.CreateVersion7();
        AnActiveLoan(copy, edition);

        var owing = BorrowerId.Generate();
        var clear = BorrowerId.Generate();
        _balances.Owing(owing.Value, 3.50m);

        var queue = HoldQueue.For(EditionId.Create(edition));
        queue.PlaceHold(HoldId.Generate(), owing, DateTimeOffset.UnixEpoch);
        queue.PlaceHold(HoldId.Generate(), clear, DateTimeOffset.UnixEpoch.AddHours(1));
        _queues.With(queue);

        var returnedOn = CheckedOutOn.AddDays(10);
        var outcome = await Handle(copy, returnedOn);

        outcome.HasErrors().ShouldBeFalse();

        var ready = queue.DomainEvents.OfType<HoldReadyForPickup>().Single();
        ready.BorrowerId.ShouldBe(clear);
        ready.CopyId.Value.ShouldBe(copy);
        ready.PickupDeadline.ShouldBe(
            returnedOn.AddDays(CirculationPolicy.Current.PickupPeriodInDays));
    }

    [Fact]
    public async Task Handle_SlidesThePickupDeadlineOffAClosedDay()
    {
        // The calendar's wiring into the trap: the deadline the queue announces is the policy's,
        // slid off an exceptional closing — not the plain seven days.
        var copy = Guid.CreateVersion7();
        var edition = Guid.CreateVersion7();
        AnActiveLoan(copy, edition);

        var queue = HoldQueue.For(EditionId.Create(edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        var returnedOn = CheckedOutOn.AddDays(2);
        var closedOnTheLanding = CirculationPolicy.Current with
        {
            Calendar = new OpeningCalendar([], [returnedOn.AddDays(7)]),
        };

        var outcome = await Handle(copy, returnedOn, closedOnTheLanding);

        outcome.HasErrors().ShouldBeFalse();
        queue.DomainEvents.OfType<HoldReadyForPickup>().Single()
            .PickupDeadline.ShouldBe(returnedOn.AddDays(8));
    }

    [Fact]
    public async Task Handle_WhenEveryQueuedBorrowerIsBlocked_TheCopyGoesToTheShelf()
    {
        // Skipped, never removed — and nothing is recorded: Holdings never learns a return
        // happened, by design.
        var copy = Guid.CreateVersion7();
        var edition = Guid.CreateVersion7();
        AnActiveLoan(copy, edition);

        var owing = BorrowerId.Generate();
        _balances.Owing(owing.Value, 1m);

        var queue = HoldQueue.For(EditionId.Create(edition));
        queue.PlaceHold(HoldId.Generate(), owing, DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        var outcome = await Handle(copy, CheckedOutOn.AddDays(1));

        outcome.HasErrors().ShouldBeFalse();
        queue.DomainEvents.OfType<HoldReadyForPickup>().ShouldBeEmpty();
        queue.Holds.Single().Status.ShouldBe(HoldStatus.Queued);
    }

    [Fact]
    public async Task Handle_WithNoQueueAtAll_JustClosesTheLoan()
    {
        var copy = Guid.CreateVersion7();
        AnActiveLoan(copy, Guid.CreateVersion7());

        (await Handle(copy, CheckedOutOn.AddDays(2))).HasErrors().ShouldBeFalse();
    }
}
