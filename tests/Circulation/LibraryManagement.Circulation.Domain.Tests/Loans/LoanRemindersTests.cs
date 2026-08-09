using LibraryManagement.Circulation.Domain.Loans;
using static LibraryManagement.Circulation.Domain.Tests.Desk;

namespace LibraryManagement.Circulation.Domain.Tests.Loans;

/// <summary>
/// What the scheduled process asks of a loan, and what the loan remembers having said. The whole
/// of the daily run's idempotence lives here.
/// </summary>
public sealed class LoanRemindersTests
{
    private static Loan ADueSoonLoan(out DateOnly today)
    {
        var loan = ALoan().Settled();
        today = loan.DueDate.AddDays(-Policy.CourtesyReminderDaysBeforeDue);
        return loan;
    }

    // --- The courtesy reminder ---------------------------------------------------------------------

    [Fact]
    public void RemindOfDueDate_OnTheDayItComesIntoReach_SaysSo()
    {
        var loan = ADueSoonLoan(out var today);

        loan.RemindOfDueDate(today, anyoneIsWaiting: false, Policy);

        var reminder = loan.Event<LoanDueSoon>();
        reminder.LoanId.ShouldBe(loan.Id);
        reminder.CopyId.ShouldBe(loan.CopyId);
        reminder.BorrowerId.ShouldBe(loan.BorrowerId);
        reminder.DueDate.ShouldBe(loan.DueDate);
        reminder.AnyoneIsWaiting.ShouldBeFalse();

        loan.RemindersSent.ShouldBe([Reminder.Courtesy(Policy.CourtesyReminderDaysBeforeDue)]);
    }

    [Fact]
    public void RemindOfDueDate_CarriesWhetherSomebodyIsWaiting()
    {
        // What decides the sentence: "renew it", or "someone is waiting, please bring it back".
        var loan = ADueSoonLoan(out var today);

        loan.RemindOfDueDate(today, anyoneIsWaiting: true, Policy);

        loan.Event<LoanDueSoon>().AnyoneIsWaiting.ShouldBeTrue();
    }

    [Fact]
    public void RemindOfDueDate_Twice_SaysItOnce()
    {
        // The literal requirement of the scheduled process: running it twice notifies nobody twice.
        var loan = ADueSoonLoan(out var today);
        loan.RemindOfDueDate(today, anyoneIsWaiting: false, Policy);
        loan.ClearDomainEvents();

        loan.RemindOfDueDate(today, anyoneIsWaiting: false, Policy);

        loan.DomainEvents.ShouldBeEmpty();
        loan.RemindersSent.Count.ShouldBe(1);
    }

    [Fact]
    public void RemindOfDueDate_BeforeTheWindowOpens_SaysNothing()
    {
        var loan = ADueSoonLoan(out var today);

        loan.RemindOfDueDate(today.AddDays(-1), anyoneIsWaiting: false, Policy);

        loan.DomainEvents.ShouldBeEmpty();
        loan.RemindersSent.ShouldBeEmpty();
    }

    [Fact]
    public void RemindOfDueDate_OnTheDueDateItself_StillSaysSo()
    {
        // The window closes when the due date passes, not before it arrives.
        var loan = ALoan().Settled();

        loan.RemindOfDueDate(loan.DueDate, anyoneIsWaiting: false, Policy);

        loan.DomainEvents.OfType<LoanDueSoon>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RemindOfDueDate_OnceOverdue_SaysNothing()
    {
        // An overdue loan gets the overdue reminder, not a courtesy about a date already gone.
        var loan = ALoan().Settled();

        loan.RemindOfDueDate(loan.DueDate.AddDays(1), anyoneIsWaiting: false, Policy);

        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RemindOfDueDate_OnALoanThatEnded_SaysNothing()
    {
        var loan = ALoan();
        loan.Return(Today, Policy);
        loan.ClearDomainEvents();

        loan.RemindOfDueDate(Today, anyoneIsWaiting: false, Policy);

        loan.DomainEvents.ShouldBeEmpty();
    }

    // --- The overdue reminders ---------------------------------------------------------------------

    [Fact]
    public void RemindOfBeingOverdue_AtTheFirstStage_SaysSo()
    {
        var loan = ALoan().Settled();

        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(1), Policy);

        var reminder = loan.Event<LoanBecameOverdue>();
        reminder.DueDate.ShouldBe(loan.DueDate);
        reminder.DaysOverdue.ShouldBe(1);
        loan.RemindersSent.ShouldBe([Reminder.Overdue(1)]);
    }

    [Fact]
    public void RemindOfBeingOverdue_Twice_SaysItOnce()
    {
        var loan = ALoan().Settled();
        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(1), Policy);
        loan.ClearDomainEvents();

        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(1), Policy);

        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RemindOfBeingOverdue_AtEachStage_SaysSoAgain()
    {
        var loan = ALoan().Settled();

        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(1), Policy);
        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(7), Policy);
        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(14), Policy);

        loan.DomainEvents.OfType<LoanBecameOverdue>().Count().ShouldBe(3);
        loan.RemindersSent.Count.ShouldBe(3);
    }

    [Fact]
    public void RemindOfBeingOverdue_AfterARunThatDidNotHappen_CatchesUpInOneMessage()
    {
        // Nine days late with neither the first nor the seventh stage announced: one message, at
        // the truth of the day, and both stages marked spent. Three messages in one morning is
        // what teaches a borrower to filter everything the library sends.
        var loan = ALoan().Settled();

        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(9), Policy);

        loan.Event<LoanBecameOverdue>().DaysOverdue.ShouldBe(9);
        loan.RemindersSent.ShouldBe([Reminder.Overdue(1), Reminder.Overdue(7)], ignoreOrder: true);
    }

    [Fact]
    public void RemindOfBeingOverdue_WhenTheStagesAreSpent_SaysNothingMore()
    {
        var loan = ALoan().Settled();
        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(14), Policy);
        loan.ClearDomainEvents();

        loan.RemindOfBeingOverdue(loan.DueDate.AddDays(20), Policy);

        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RemindOfBeingOverdue_WhileStillInTime_SaysNothing()
    {
        var loan = ALoan().Settled();

        loan.RemindOfBeingOverdue(loan.DueDate, Policy);

        loan.DomainEvents.ShouldBeEmpty();
        loan.RemindersSent.ShouldBeEmpty();
    }

    // --- What a renewal does to the schedule -------------------------------------------------------

    [Fact]
    public void Renew_StartsTheScheduleAgain()
    {
        // The borrower was told about a due date that no longer exists. Keeping the appointment
        // spent would silence the courtesy reminder of every renewed loan.
        var loan = ADueSoonLoan(out var today);
        loan.RemindOfDueDate(today, anyoneIsWaiting: false, Policy);

        loan.Renew(Policy).HasErrors().ShouldBeFalse();

        loan.RemindersSent.ShouldBeEmpty();

        loan.ClearDomainEvents();
        loan.RemindOfDueDate(
            loan.DueDate.AddDays(-Policy.CourtesyReminderDaysBeforeDue),
            anyoneIsWaiting: false,
            Policy);

        loan.DomainEvents.OfType<LoanDueSoon>().ShouldHaveSingleItem();
    }

    // --- Giving up -------------------------------------------------------------------------------

    [Fact]
    public void IsLongOverdue_OnlyOnceThePatienceIsSpent()
    {
        var loan = ALoan().Settled();

        loan.IsLongOverdue(loan.DueDate.AddDays(Policy.DeclaredLostAfterDays - 1), Policy)
            .ShouldBeFalse();
        loan.IsLongOverdue(loan.DueDate.AddDays(Policy.DeclaredLostAfterDays), Policy)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsLongOverdue_OfALoanThatEnded_IsFalse()
    {
        var loan = ALoan();
        loan.Return(Today, Policy);

        loan.IsLongOverdue(loan.DueDate.AddDays(Policy.DeclaredLostAfterDays), Policy)
            .ShouldBeFalse();
    }
}
