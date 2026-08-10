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
    public void Return_ObservedDamaged_SaysSoBesideTheReturn()
    {
        // A fact beside the return, never a flag on it: the two have different audiences, and
        // the attribution is settled by construction — the observation rides this loan's closing.
        var loan = ALoan().Settled();

        loan.Return(loan.DueDate, Policy, returnedDamaged: true).HasErrors().ShouldBeFalse();

        loan.Event<LoanReturned>().DaysLate.ShouldBe(0);
        var damaged = loan.Event<CopyReturnedDamaged>();
        damaged.CopyId.ShouldBe(loan.CopyId);
        damaged.BorrowerId.ShouldBe(loan.BorrowerId);
    }

    [Fact]
    public void Return_Ordinary_SaysNothingAboutDamage()
    {
        var loan = ALoan().Settled();

        loan.Return(loan.DueDate, Policy);

        loan.DomainEvents.OfType<CopyReturnedDamaged>().ShouldBeEmpty();
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

        loan.DeclareLost(Today).HasErrors().ShouldBeFalse();

        loan.Status.ShouldBe(LoanStatus.DeclaredLost);
        loan.DeclaredLostOn.ShouldBe(Today);
        var declared = loan.Event<LoanDeclaredLost>();
        declared.CopyId.ShouldBe(loan.CopyId);
        declared.BorrowerId.ShouldBe(loan.BorrowerId);
    }

    [Fact]
    public void DeclareLost_Twice_IsDeclaringItOnce_AndKeepsTheFirstDate()
    {
        // The run may beat the desk to it, or the desk the run: the second arrival is a
        // redelivery, not a second decision, and the date that bounds the fine stays the first.
        var loan = ALoan().Settled();
        loan.DeclareLost(Today);
        loan.ClearDomainEvents();

        loan.DeclareLost(Today.AddDays(3)).HasErrors().ShouldBeFalse();

        loan.DomainEvents.ShouldBeEmpty();
        loan.DeclaredLostOn.ShouldBe(Today);
    }

    [Fact]
    public void RecordRecovery_AnnouncesTheLatenessFrozenAtTheWriteOff()
    {
        // Declared lost thirty days past due: the count froze there, and the years a book spends
        // behind a radiator are nobody's fine. Without this, a copy back on day forty-five owed
        // less than one back on day twenty-nine.
        var loan = ALoan().Settled();
        loan.DeclareLost(loan.DueDate.AddDays(30));
        loan.ClearDomainEvents();

        loan.RecordRecovery(Today.AddDays(200), Policy).HasErrors().ShouldBeFalse();

        loan.Status.ShouldBe(LoanStatus.DeclaredLost);
        loan.RecoveredOn.ShouldBe(Today.AddDays(200));
        loan.Event<LoanRecovered>().DaysLate.ShouldBe(30);
    }

    [Fact]
    public void RecordRecovery_Twice_AnnouncesOnce()
    {
        var loan = ALoan().Settled();
        loan.DeclareLost(loan.DueDate.AddDays(30));
        loan.RecordRecovery(Today.AddDays(60), Policy);
        loan.ClearDomainEvents();

        loan.RecordRecovery(Today.AddDays(90), Policy).HasErrors().ShouldBeFalse();

        loan.DomainEvents.ShouldBeEmpty();
        loan.RecoveredOn.ShouldBe(Today.AddDays(60));
    }

    // --- Repointing after a merge in Catalog --------------------------------------------------------

    [Fact]
    public void RepointTo_MovesALiveLoanAndCarriesBothRecords()
    {
        var loan = ALoan().Settled();
        var absorbed = loan.EditionId;
        var surviving = EditionId.Generate();

        loan.RepointTo(surviving).HasErrors().ShouldBeFalse();

        loan.EditionId.ShouldBe(surviving);

        var repointed = loan.Event<LoanRepointed>();
        repointed.LoanId.ShouldBe(loan.Id);
        repointed.PreviousEditionId.ShouldBe(absorbed);
        repointed.NewEditionId.ShouldBe(surviving);
    }

    [Fact]
    public void RepointTo_TheRecordItAlreadyNames_RecordsNothing()
    {
        // The ordinary shape of a redelivered announcement. It must not tell a projection to
        // replace an entry with itself.
        var loan = ALoan().Settled();

        loan.RepointTo(loan.EditionId).HasErrors().ShouldBeFalse();

        loan.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("returned")]
    [InlineData("lost")]
    public void RepointTo_ALoanThatHasEnded_IsRefused(string ending)
    {
        // A merge moves live state and leaves the past alone. What this identifier is for is naming
        // the queue a copy feeds at its return and at every renewal, and an ended loan asks neither
        // question again — rewriting it would change no answer while making the record of a
        // completed act disagree with the act.
        var loan = ALoan().Settled();

        if (ending == "returned")
        {
            loan.Return(Today, Policy);
        }
        else
        {
            loan.DeclareLost(Today);
        }

        var absorbed = loan.EditionId;
        loan.Settled();

        CodesOf(loan.RepointTo(EditionId.Generate()))
            .ShouldContain(CirculationErrorCodes.LoanNotActive);

        loan.EditionId.ShouldBe(absorbed);
        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RepointTo_DemandsARecordToPointAt()
        => Should.Throw<ArgumentNullException>(() => ALoan().RepointTo(null!));

    [Fact]
    public void RecordRecovery_ALoanNeverGivenUpOn_IsRefused()
    {
        var loan = ALoan().Settled();

        loan.RecordRecovery(Today, Policy).HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void DeclareLost_AReturnedLoan_IsRefused()
    {
        // The copy came back; there is nothing to stop waiting for.
        var loan = ALoan().Settled();
        loan.Return(Today, Policy);

        loan.DeclareLost(Today).HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Return_ADeclaredLostLoan_IsRefused()
    {
        // Terminal: the copy turning up afterwards starts a return in Holdings' world — finding
        // the copy — never a reopening of the loan the library wrote off.
        var loan = ALoan().Settled();
        loan.DeclareLost(Today);

        CodesOf(loan.Return(Today, Policy)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }
}
