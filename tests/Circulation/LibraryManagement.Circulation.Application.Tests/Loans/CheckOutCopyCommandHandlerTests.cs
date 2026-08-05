using LibraryManagement.Circulation.Application.Loans.CheckOutCopy;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

public sealed class CheckOutCopyCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();
    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubShelf _shelf = new();
    private readonly StubRegistry _registry = new();
    private readonly StubBalances _balances = new();

    private readonly Guid _borrower = Guid.CreateVersion7();
    private readonly Guid _copy = Guid.CreateVersion7();
    private readonly Guid _edition = Guid.CreateVersion7();

    public CheckOutCopyCommandHandlerTests()
    {
        _registry.Entitled(_borrower);
        _shelf.Lendable(_copy, _edition);
    }

    private ValueTask<Result> Handle(Guid? borrower = null, Guid? copy = null, Guid? loanId = null)
        => new CheckOutCopyCommandHandler(
                _loans, _queues, _shelf, _registry, _balances,
                CirculationPolicy.Current, FrozenClock.At(Today))
            .Handle(
                new CheckOutCopyCommand(
                    loanId ?? Guid.CreateVersion7(), copy ?? _copy, borrower ?? _borrower),
                Token);

    private void AnActiveLoan(Guid borrower, Guid copy, Guid edition)
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Create(copy),
            EditionId.Create(edition),
            BorrowerId.Create(borrower),
            Today,
            CirculationPolicy.Current);

        _loans.With(loan);
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_StartsTheLoan_ForThePolicysPeriod()
    {
        var command = new CheckOutCopyCommand(Guid.CreateVersion7(), _copy, _borrower);

        var outcome = await Handle(loanId: command.LoanId);

        outcome.HasErrors().ShouldBeFalse();

        var loan = _loans.Added.Single();
        loan.Id.Value.ShouldBe(command.LoanId);
        loan.CopyId.Value.ShouldBe(_copy);
        loan.EditionId.Value.ShouldBe(_edition);
        loan.BorrowerId.Value.ShouldBe(_borrower);
        loan.DueDate.ShouldBe(Today.AddDays(CirculationPolicy.Current.LoanDurationInDays));
    }

    [Fact]
    public async Task Handle_SomeoneNobodyEnrolled_IsItsOwnRefusal()
    {
        CodesOf(await Handle(borrower: Guid.CreateVersion7()))
            .ShouldContain(CirculationErrorCodes.NoSuchMember);
    }

    [Fact]
    public async Task Handle_ALapsedMembership_IsItsOwnRefusal()
    {
        var lapsed = Guid.CreateVersion7();
        _registry.Lapsed(lapsed);

        CodesOf(await Handle(borrower: lapsed))
            .ShouldContain(CirculationErrorCodes.MembershipLapsed);
    }

    [Fact]
    public async Task Handle_ADebt_Forbids_FromTheFirstCent()
    {
        _balances.Owing(_borrower, 0.20m);

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.DebtForbidsIt);
        _loans.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ACopyNobodyAccessioned_IsItsOwnRefusal()
    {
        CodesOf(await Handle(copy: Guid.CreateVersion7()))
            .ShouldContain(CirculationErrorCodes.NoSuchCopy);
    }

    [Fact]
    public async Task Handle_ACopyHoldingsWillNotLend_IsRefused()
    {
        var inRepair = Guid.CreateVersion7();
        _shelf.NotLendable(inRepair, _edition);

        CodesOf(await Handle(copy: inRepair))
            .ShouldContain(CirculationErrorCodes.CopyNotLendable);
    }

    [Fact]
    public async Task Handle_ACopyAlreadyOut_IsRefused()
    {
        AnActiveLoan(Guid.CreateVersion7(), _copy, _edition);

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.CopyAlreadyOnLoan);
    }

    [Fact]
    public async Task Handle_ACopySetAsideForSomeoneElse_IsRefused()
    {
        // What stops a walk-in from being handed a copy someone is waiting for.
        var waiting = BorrowerId.Generate();
        var queue = HoldQueue.For(EditionId.Create(_edition));
        queue.PlaceHold(HoldId.Generate(), waiting, DateTimeOffset.UnixEpoch);
        queue.TrapOldestQueued(
            CopyId.Create(_copy), Today.AddDays(7), new HashSet<BorrowerId>());
        _queues.With(queue);

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.TrappedForAnotherBorrower);
    }

    [Fact]
    public async Task Handle_CollectingOnesOwnTrappedCopy_FulfillsTheHold()
    {
        var queue = HoldQueue.For(EditionId.Create(_edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Create(_borrower), DateTimeOffset.UnixEpoch);
        queue.TrapOldestQueued(
            CopyId.Create(_copy), Today.AddDays(7), new HashSet<BorrowerId>());
        _queues.With(queue);

        var outcome = await Handle();

        outcome.HasErrors().ShouldBeFalse();
        queue.Holds.ShouldBeEmpty();
        queue.DomainEvents.OfType<HoldFulfilled>().ShouldHaveSingleItem();
        _loans.Added.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_AtTheCap_IsRefused()
    {
        for (var held = 0; held < CirculationPolicy.Current.MaxLoansAndHolds; held++)
        {
            AnActiveLoan(_borrower, Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.AtTheCap);
    }

    [Fact]
    public async Task Handle_AtTheCap_CollectingOnesOwnTrappedCopy_StillSucceeds()
    {
        // The transition from hold to loan leaves the count unchanged, so nothing has to be
        // reconciled at pickup — the design's own words, and the reason the cap moves after the
        // trap resolution.
        for (var held = 0; held < CirculationPolicy.Current.MaxLoansAndHolds - 1; held++)
        {
            AnActiveLoan(_borrower, Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        var queue = HoldQueue.For(EditionId.Create(_edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Create(_borrower), DateTimeOffset.UnixEpoch);
        queue.TrapOldestQueued(
            CopyId.Create(_copy), Today.AddDays(7), new HashSet<BorrowerId>());
        _queues.With(queue);

        var outcome = await Handle();

        outcome.HasErrors().ShouldBeFalse();
    }
}
