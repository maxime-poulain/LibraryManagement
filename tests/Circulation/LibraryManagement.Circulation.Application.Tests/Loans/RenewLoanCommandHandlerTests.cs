using LibraryManagement.Circulation.Application.Loans.RenewLoan;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

public sealed class RenewLoanCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();
    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubRegistry _members = new();
    private readonly StubBalances _balances = new();

    private ValueTask<Result> Handle(Guid loanId)
        => new RenewLoanCommandHandler(_loans, _queues, _members, _balances, CirculationPolicy.Current)
            .Handle(new RenewLoanCommand(loanId), Token);

    private Loan AnActiveLoan(out Guid edition)
    {
        var editionId = EditionId.Generate();
        edition = editionId.Value;

        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            editionId,
            BorrowerId.Generate(),
            Today,
            CirculationPolicy.Current);

        _loans.With(loan);
        _members.Entitled(loan.BorrowerId.Value);
        return loan;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_ALapsedMembership_IsRefused()
    {
        // A renewal is a fresh loan period, not the tail of an old one: without this gate an
        // expired membership could no longer borrow but could renew indefinitely.
        var loan = AnActiveLoan(out _);
        _members.Lapsed(loan.BorrowerId.Value);

        CodesOf(await Handle(loan.Id.Value))
            .ShouldContain(CirculationErrorCodes.MembershipLapsed);
        loan.RenewalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ABorrowerTheRegistryDoesNotKnow_IsRefused()
    {
        var loan = AnActiveLoan(out _);
        _members.Forget(loan.BorrowerId.Value);

        CodesOf(await Handle(loan.Id.Value))
            .ShouldContain(CirculationErrorCodes.NoSuchMember);
    }

    [Fact]
    public async Task Handle_MovesTheDueDateForward()
    {
        var loan = AnActiveLoan(out _);
        var oldDue = loan.DueDate;

        var outcome = await Handle(loan.Id.Value);

        outcome.HasErrors().ShouldBeFalse();
        loan.DueDate.ShouldBe(oldDue.AddDays(CirculationPolicy.Current.LoanDurationInDays));
    }

    [Fact]
    public async Task Handle_ALoanNobodyHolds_IsRefused()
    {
        CodesOf(await Handle(Guid.CreateVersion7()))
            .ShouldContain(CirculationErrorCodes.LoanNotFound);
    }

    [Fact]
    public async Task Handle_ADebt_Forbids()
    {
        var loan = AnActiveLoan(out _);
        _balances.Owing(loan.BorrowerId.Value, 2m);

        CodesOf(await Handle(loan.Id.Value)).ShouldContain(CirculationErrorCodes.DebtForbidsIt);
    }

    [Fact]
    public async Task Handle_SomeoneWaiting_IsRefused()
    {
        // What makes a queue move: without this, a borrower renews indefinitely and the people
        // behind them never get anything.
        var loan = AnActiveLoan(out var edition);
        var queue = HoldQueue.For(EditionId.Create(edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        CodesOf(await Handle(loan.Id.Value)).ShouldContain(CirculationErrorCodes.SomeoneIsWaiting);
    }

    [Fact]
    public async Task Handle_AClaimAlreadyServed_DoesNotDeny()
    {
        // The hold awaiting pickup has its copy on the hold shelf; refusing a renewal for its
        // sake would serve nobody.
        var loan = AnActiveLoan(out var edition);
        var queue = HoldQueue.For(EditionId.Create(edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), new HashSet<BorrowerId>());
        _queues.With(queue);

        (await Handle(loan.Id.Value)).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_PastTheLimit_IsRefused()
    {
        var loan = AnActiveLoan(out _);
        loan.Renew(CirculationPolicy.Current);
        loan.Renew(CirculationPolicy.Current);

        CodesOf(await Handle(loan.Id.Value))
            .ShouldContain(CirculationErrorCodes.RenewalLimitReached);
    }

    [Fact]
    public async Task Handle_AReturnedLoan_IsRefusedWithoutConsultingAnyone()
    {
        // Reporting a debt to someone whose loan is simply over would be the wrong refusal.
        var loan = AnActiveLoan(out _);
        loan.Return(Today, CirculationPolicy.Current);
        _balances.Owing(loan.BorrowerId.Value, 5m);

        CodesOf(await Handle(loan.Id.Value)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }
}
