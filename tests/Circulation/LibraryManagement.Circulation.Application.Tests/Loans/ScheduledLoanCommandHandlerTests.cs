using LibraryManagement.Circulation.Application.Loans.DeclareOverdueLoansLost;
using LibraryManagement.Circulation.Application.Loans.RemindOfLoansDueSoon;
using LibraryManagement.Circulation.Application.Loans.RemindOfOverdueLoans;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

/// <summary>
/// The three loan moments of the daily run. Each one asks the store for candidates and lets the
/// aggregate decide, so what these prove is the narrowing and the wiring — the deciding is the
/// domain's own tests.
/// </summary>
public sealed class ScheduledLoanCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();
    private readonly InMemoryHoldQueueRepository _queues = new();

    private static CirculationPolicy Policy => CirculationPolicy.Current;

    /// <summary>A loan falling due on the given day, checked out a full period earlier.</summary>
    private Loan ALoanDueOn(DateOnly dueDate, EditionId? editionId = null)
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            editionId ?? EditionId.Generate(),
            BorrowerId.Generate(),
            dueDate.AddDays(-Policy.LoanDurationInDays),
            Policy);

        _loans.With(loan);
        loan.ClearDomainEvents();

        return loan;
    }

    private ValueTask<Result> RemindOfDueSoon()
        => new RemindOfLoansDueSoonCommandHandler(
                _loans, _queues, Policy, FrozenClock.At(Today))
            .Handle(new RemindOfLoansDueSoonCommand(), Token);

    private ValueTask<Result> RemindOfOverdue()
        => new RemindOfOverdueLoansCommandHandler(_loans, Policy, FrozenClock.At(Today))
            .Handle(new RemindOfOverdueLoansCommand(), Token);

    private ValueTask<Result> DeclareLost()
        => new DeclareOverdueLoansLostCommandHandler(_loans, Policy, FrozenClock.At(Today))
            .Handle(new DeclareOverdueLoansLostCommand(), Token);

    // --- The courtesy reminder ---------------------------------------------------------------------

    [Fact]
    public async Task RemindOfDueSoon_TellsTheBorrowersWithinTheWindow()
    {
        var soon = ALoanDueOn(Today.AddDays(Policy.CourtesyReminderDaysBeforeDue));
        var later = ALoanDueOn(Today.AddDays(Policy.CourtesyReminderDaysBeforeDue + 1));

        (await RemindOfDueSoon()).HasErrors().ShouldBeFalse();

        soon.DomainEvents.OfType<LoanDueSoon>().ShouldHaveSingleItem();
        later.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemindOfDueSoon_TellsThemWhetherSomebodyIsWaiting()
    {
        var editionId = EditionId.Generate();
        var loan = ALoanDueOn(Today.AddDays(1), editionId);

        var queue = HoldQueue.For(editionId);
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        await RemindOfDueSoon();

        loan.DomainEvents.OfType<LoanDueSoon>().Single().AnyoneIsWaiting.ShouldBeTrue();
    }

    [Fact]
    public async Task RemindOfDueSoon_WithNoQueueForTheEdition_SaysNobodyIsWaiting()
    {
        var loan = ALoanDueOn(Today.AddDays(1));

        await RemindOfDueSoon();

        loan.DomainEvents.OfType<LoanDueSoon>().Single().AnyoneIsWaiting.ShouldBeFalse();
    }

    [Fact]
    public async Task RemindOfDueSoon_RunTwice_TellsThemOnce()
    {
        var loan = ALoanDueOn(Today.AddDays(1));

        await RemindOfDueSoon();
        loan.ClearDomainEvents();
        await RemindOfDueSoon();

        loan.DomainEvents.ShouldBeEmpty();
    }

    // --- The overdue reminders ---------------------------------------------------------------------

    [Fact]
    public async Task RemindOfOverdue_TellsTheBorrowersPastTheirDate()
    {
        var late = ALoanDueOn(Today.AddDays(-1));
        var inTime = ALoanDueOn(Today);

        (await RemindOfOverdue()).HasErrors().ShouldBeFalse();

        late.DomainEvents.OfType<LoanBecameOverdue>().Single().DaysOverdue.ShouldBe(1);
        inTime.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemindOfOverdue_RunTwice_TellsThemOnce()
    {
        var late = ALoanDueOn(Today.AddDays(-7));

        await RemindOfOverdue();
        late.ClearDomainEvents();
        await RemindOfOverdue();

        late.DomainEvents.ShouldBeEmpty();
    }

    // --- Giving up -------------------------------------------------------------------------------

    [Fact]
    public async Task DeclareLost_TakesOnlyTheLoansTheLibraryHasWaitedLongEnoughFor()
    {
        var abandoned = ALoanDueOn(Today.AddDays(-Policy.DeclaredLostAfterDays));
        var stillWaiting = ALoanDueOn(Today.AddDays(-Policy.DeclaredLostAfterDays + 1));

        (await DeclareLost()).HasErrors().ShouldBeFalse();

        abandoned.Status.ShouldBe(LoanStatus.DeclaredLost);
        abandoned.DomainEvents.OfType<LoanDeclaredLost>().ShouldHaveSingleItem();
        stillWaiting.Status.ShouldBe(LoanStatus.Active);
    }

    [Fact]
    public async Task DeclareLost_RunTwice_DeclaresItOnce()
    {
        // The second run does not even see it: a loan that is no longer active is not overdue.
        var abandoned = ALoanDueOn(Today.AddDays(-Policy.DeclaredLostAfterDays));

        await DeclareLost();
        abandoned.ClearDomainEvents();
        await DeclareLost();

        abandoned.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheWholeReminderChain_EndsInADeclaration()
    {
        // The escalation a borrower actually experiences, run day by day through one loan.
        var loan = ALoanDueOn(Today.AddDays(Policy.CourtesyReminderDaysBeforeDue));

        foreach (var day in Enumerable.Range(0, Policy.DeclaredLostAfterDays
            + Policy.CourtesyReminderDaysBeforeDue + 1))
        {
            var clock = FrozenClock.At(Today.AddDays(day));

            await new RemindOfLoansDueSoonCommandHandler(_loans, _queues, Policy, clock)
                .Handle(new RemindOfLoansDueSoonCommand(), Token);
            await new RemindOfOverdueLoansCommandHandler(_loans, Policy, clock)
                .Handle(new RemindOfOverdueLoansCommand(), Token);
            await new DeclareOverdueLoansLostCommandHandler(_loans, Policy, clock)
                .Handle(new DeclareOverdueLoansLostCommand(), Token);
        }

        loan.DomainEvents.OfType<LoanDueSoon>().ShouldHaveSingleItem();
        loan.DomainEvents.OfType<LoanBecameOverdue>().Count()
            .ShouldBe(Policy.OverdueReminderDaysAfterDue.Count);
        loan.DomainEvents.OfType<LoanDeclaredLost>().ShouldHaveSingleItem();
        loan.Status.ShouldBe(LoanStatus.DeclaredLost);
    }
}
