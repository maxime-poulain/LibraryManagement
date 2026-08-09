using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using static LibraryManagement.Circulation.Domain.Tests.Desk;

namespace LibraryManagement.Circulation.Domain.Tests.Loans;

public sealed class LoanTests
{
    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    // --- Checkout ----------------------------------------------------------------------------------

    [Fact]
    public void CheckOut_StartsAnActiveLoanForThePolicysPeriod()
    {
        var loan = ALoan();

        loan.Status.ShouldBe(LoanStatus.Active);
        loan.CheckedOutOn.ShouldBe(Today);
        loan.DueDate.ShouldBe(Today.AddDays(Policy.LoanDurationInDays));
        loan.RenewalCount.ShouldBe(0);
        loan.ReturnedOn.ShouldBeNull();
    }

    [Fact]
    public void CheckOut_SaysSo()
    {
        var loan = ALoan();

        var checkedOut = loan.Event<LoanCheckedOut>();
        checkedOut.LoanId.ShouldBe(loan.Id);
        checkedOut.CopyId.ShouldBe(loan.CopyId);
        checkedOut.EditionId.ShouldBe(loan.EditionId);
        checkedOut.BorrowerId.ShouldBe(loan.BorrowerId);
        checkedOut.DueDate.ShouldBe(loan.DueDate);
    }

    [Fact]
    public void CheckOut_WhoseDueDateLandsOnAClosedDay_SlidesItToTheFirstOpenDay()
    {
        // Today plus the loan period is Saturday the 4th of April — the Easter closing, shut
        // through Easter Monday. A due date printed on a receipt must be a day the member can
        // actually meet, so it slides to the reopening Tuesday.
        var easterClosing = Policy with
        {
            Calendar = new OpeningCalendar(
                [],
                [new DateOnly(2026, 4, 4), new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 6)]),
        };

        var loan = ALoan(policy: easterClosing);

        loan.DueDate.ShouldBe(new DateOnly(2026, 4, 7));
    }

    // --- Renewal -----------------------------------------------------------------------------------

    [Fact]
    public void Renew_ExtendsFromTheDueDate_AndSaysSo()
    {
        // From the due date, not from today: renewing early costs nothing — the arithmetic the
        // membership renewal already decided, for the same reason.
        var loan = ALoan().Settled();
        var oldDue = loan.DueDate;

        loan.Renew(Policy).HasErrors().ShouldBeFalse();

        loan.DueDate.ShouldBe(oldDue.AddDays(Policy.LoanDurationInDays));
        loan.RenewalCount.ShouldBe(1);
        loan.Event<RenewalGranted>().NewDueDate.ShouldBe(loan.DueDate);
    }

    [Fact]
    public void Renew_SlidesTheNewDueDateOffAClosedDay()
    {
        var loan = ALoan().Settled();
        var oldDue = loan.DueDate;
        var closedOnTheLanding = Policy with
        {
            Calendar = new OpeningCalendar([], [oldDue.AddDays(Policy.LoanDurationInDays)]),
        };

        loan.Renew(closedOnTheLanding).HasErrors().ShouldBeFalse();

        loan.DueDate.ShouldBe(oldDue.AddDays(Policy.LoanDurationInDays + 1));
    }

    [Fact]
    public void Renew_PastTheLimit_IsRefused()
    {
        var loan = ALoan().Settled();

        for (var renewal = 0; renewal < Policy.MaxRenewals; renewal++)
        {
            loan.Renew(Policy).HasErrors().ShouldBeFalse();
        }

        var outcome = loan.Renew(Policy);

        CodesOf(outcome).ShouldContain(CirculationErrorCodes.RenewalLimitReached);
        loan.RenewalCount.ShouldBe(Policy.MaxRenewals);
    }

    [Fact]
    public void Renew_AReturnedLoan_IsRefused()
    {
        var loan = ALoan().Settled();
        loan.Return(Today, Policy);

        CodesOf(loan.Renew(Policy)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }

    // --- Return ------------------------------------------------------------------------------------

    [Fact]
    public void Return_OnTime_ClosesTheLoanWithZeroDaysLate()
    {
        // Zero is published all the same: whether a return was late is a circulation fact, and
        // Charges decides there is nothing to charge — not this context.
        var loan = ALoan().Settled();

        loan.Return(loan.DueDate, Policy).HasErrors().ShouldBeFalse();

        loan.Status.ShouldBe(LoanStatus.Returned);
        loan.ReturnedOn.ShouldBe(loan.DueDate);
        loan.Event<LoanReturned>().DaysLate.ShouldBe(0);
    }

    [Fact]
    public void Return_Late_CountsTheDays()
    {
        var loan = ALoan().Settled();

        loan.Return(loan.DueDate.AddDays(6), Policy);

        loan.Event<LoanReturned>().DaysLate.ShouldBe(6);
    }

    [Fact]
    public void Return_Early_IsNotNegativeLateness()
    {
        var loan = ALoan().Settled();

        loan.Return(Today.AddDays(1), Policy);

        loan.Event<LoanReturned>().DaysLate.ShouldBe(0);
    }

    [Fact]
    public void Return_BillsOnlyTheOpenDaysAmongTheLateOnes()
    {
        // Six elapsed days across a closed Sunday and Monday: four billed. A fine is charged for
        // days the borrower let pass, and a day nobody could return is not one of them.
        var loan = ALoan().Settled();
        var closedSundayAndMonday = Policy with
        {
            Calendar = new OpeningCalendar([DayOfWeek.Sunday, DayOfWeek.Monday], []),
        };

        loan.Return(loan.DueDate.AddDays(6), closedSundayAndMonday);

        loan.Event<LoanReturned>().DaysLate.ShouldBe(4);
    }

    [Fact]
    public void Return_OnTheFirstOpenDayAfterAClosureTookTheDueDate_IsNotLate()
    {
        // The strike that lands on a due date already printed: the receipt does not move, and
        // the judgement forgives what the receipt could not know.
        var loan = ALoan().Settled();
        var strikeOnTheDueDate = Policy with
        {
            Calendar = new OpeningCalendar([], [loan.DueDate]),
        };

        loan.Return(loan.DueDate.AddDays(1), strikeOnTheDueDate);

        loan.Event<LoanReturned>().DaysLate.ShouldBe(0);
    }

    [Fact]
    public void Return_Twice_IsRefused()
    {
        var loan = ALoan().Settled();
        loan.Return(Today, Policy);

        CodesOf(loan.Return(Today, Policy)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }

    // --- Declared lost -----------------------------------------------------------------------------

    [Fact]
    public void DeclareLost_EndsTheLoanByDecision_AndSaysSo()
    {
        var loan = ALoan().Settled();

        loan.DeclareLost().HasErrors().ShouldBeFalse();

        loan.Status.ShouldBe(LoanStatus.DeclaredLost);
        var declared = loan.Event<LoanDeclaredLost>();
        declared.CopyId.ShouldBe(loan.CopyId);
        declared.BorrowerId.ShouldBe(loan.BorrowerId);
    }

    [Fact]
    public void DeclareLost_Twice_IsDeclaringItOnce()
    {
        var loan = ALoan().Settled();
        loan.DeclareLost();
        loan.ClearDomainEvents();

        loan.DeclareLost().HasErrors().ShouldBeFalse();

        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void DeclareLost_AReturnedLoan_IsRefused()
    {
        // The copy came back; there is nothing to stop waiting for.
        var loan = ALoan().Settled();
        loan.Return(Today, Policy);

        loan.DeclareLost().HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Return_ADeclaredLostLoan_IsRefused()
    {
        // Terminal: the copy turning up afterwards starts a return in Holdings' world — finding
        // the copy — never a reopening of the loan the library wrote off.
        var loan = ALoan().Settled();
        loan.DeclareLost();

        CodesOf(loan.Return(Today, Policy)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }
}
