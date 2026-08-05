namespace LibraryManagement.Circulation.Domain.Tests;

public sealed class CirculationPolicyTests
{
    // The values as the library decided them, pinned so a change is a change to a test — which is
    // what "a decision of the library must not be a deployment" looks like until the values come
    // from a table: at least the deployment says so.
    [Fact]
    public void TheDecidedValues_AreTheDesigns()
    {
        var policy = CirculationPolicy.Current;

        policy.MaxLoansAndHolds.ShouldBe(5);
        policy.LoanDurationInDays.ShouldBe(21);
        policy.MaxRenewals.ShouldBe(2);
        policy.PickupPeriodInDays.ShouldBe(7);
        policy.CourtesyReminderDaysBeforeDue.ShouldBe(3);
        policy.OverdueReminderDaysAfterDue.ShouldBe([1, 7, 14]);
        policy.DeclaredLostAfterDays.ShouldBe(30);
        policy.BlockingDebt.ShouldBe(0m);
    }

    [Fact]
    public void AnyAmountOwed_Blocks_FromTheFirstCent()
    {
        var policy = CirculationPolicy.Current;

        policy.DebtForbids(0m).ShouldBeFalse();
        policy.DebtForbids(0.01m).ShouldBeTrue();
    }

    [Fact]
    public void TheCap_ConstrainsTheAct()
    {
        var policy = CirculationPolicy.Current;

        policy.IsAtTheCap(policy.MaxLoansAndHolds - 1).ShouldBeFalse();
        policy.IsAtTheCap(policy.MaxLoansAndHolds).ShouldBeTrue();
    }

    [Fact]
    public void IndexingByCategory_WouldBeAnAddition()
    {
        // The record's init-only values are what keep the flat policy openable later: a table per
        // category arrives by construction, not by rewrite. Pinned by using it the way that day
        // would.
        var studentTerm = CirculationPolicy.Current with { LoanDurationInDays = 28 };

        studentTerm.LoanDurationInDays.ShouldBe(28);
        CirculationPolicy.Current.LoanDurationInDays.ShouldBe(21);
    }
}
