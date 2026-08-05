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
        loan.Return(Today);

        CodesOf(loan.Renew(Policy)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }

    // --- Return ------------------------------------------------------------------------------------

    [Fact]
    public void Return_OnTime_ClosesTheLoanWithZeroDaysLate()
    {
        // Zero is published all the same: whether a return was late is a circulation fact, and
        // Charges decides there is nothing to charge — not this context.
        var loan = ALoan().Settled();

        loan.Return(loan.DueDate).HasErrors().ShouldBeFalse();

        loan.Status.ShouldBe(LoanStatus.Returned);
        loan.ReturnedOn.ShouldBe(loan.DueDate);
        loan.Event<LoanReturned>().DaysLate.ShouldBe(0);
    }

    [Fact]
    public void Return_Late_CountsTheDays()
    {
        var loan = ALoan().Settled();

        loan.Return(loan.DueDate.AddDays(6));

        loan.Event<LoanReturned>().DaysLate.ShouldBe(6);
    }

    [Fact]
    public void Return_Early_IsNotNegativeLateness()
    {
        var loan = ALoan().Settled();

        loan.Return(Today.AddDays(1));

        loan.Event<LoanReturned>().DaysLate.ShouldBe(0);
    }

    [Fact]
    public void Return_Twice_IsRefused()
    {
        var loan = ALoan().Settled();
        loan.Return(Today);

        CodesOf(loan.Return(Today)).ShouldContain(CirculationErrorCodes.LoanNotActive);
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
        loan.Return(Today);

        loan.DeclareLost().HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Return_ADeclaredLostLoan_IsRefused()
    {
        // Terminal: the copy turning up afterwards starts a return in Holdings' world — finding
        // the copy — never a reopening of the loan the library wrote off.
        var loan = ALoan().Settled();
        loan.DeclareLost();

        CodesOf(loan.Return(Today)).ShouldContain(CirculationErrorCodes.LoanNotActive);
    }
}
