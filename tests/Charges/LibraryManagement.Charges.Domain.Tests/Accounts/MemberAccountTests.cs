using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Domain.Tests.Accounts;

/// <summary>
/// What an account does with the facts it is told, and what it says about the amount afterwards.
/// </summary>
public sealed class MemberAccountTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static ChargesPolicy Policy => ChargesPolicy.Current;

    private readonly MemberAccount _account = MemberAccount.For(MemberId.Generate());

    private Result Fine(int daysLate, LoanId? loanId = null, CopyId? copyId = null, DateOnly? on = null)
        => _account.AssessOverdueFine(
            ChargeId.Generate(),
            loanId ?? LoanId.Generate(),
            copyId ?? CopyId.Generate(),
            daysLate,
            on ?? Today,
            Policy);

    private Result Replacement(LoanId? loanId = null, CopyId? copyId = null)
        => _account.RaiseReplacementCharge(
            ChargeId.Generate(),
            loanId ?? LoanId.Generate(),
            copyId ?? CopyId.Generate(),
            Today,
            Policy);

    private T Event<T>() => _account.DomainEvents.OfType<T>().Single();

    // --- Pricing a delay ---------------------------------------------------------------------------

    [Fact]
    public void ANewAccount_OwesNothing()
    {
        _account.Balance.ShouldBe(Money.Zero);
        _account.Charges.ShouldBeEmpty();
    }

    [Fact]
    public void ALateReturn_IsPricedByTheTariff()
    {
        Fine(daysLate: 3).HasErrors().ShouldBeFalse();

        _account.Balance.ShouldBe(Money.Of(0.60m));
        Event<OverdueFineAssessed>().DaysLate.ShouldBe(3);
    }

    [Fact]
    public void AReturnOnTime_IsPricedAtNothingAndRaisesNoCharge()
    {
        // Zero is the absence of a charge, not a charge of zero: an account holding one would
        // report a member as owing while the balance said otherwise.
        Fine(daysLate: 0).HasErrors().ShouldBeFalse();

        _account.Charges.ShouldBeEmpty();
        _account.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AVeryLateReturn_IsCappedRatherThanUnbounded()
    {
        // Without the cap a copy returned two years late would owe more than replacing it, which no
        // library charges and no member would pay.
        Fine(daysLate: 400);

        _account.Balance.ShouldBe(Money.Of(Policy.MaxFinePerLoan));
    }

    [Fact]
    public void ACopyThatWillNotComeBack_IsPricedAtTheReplacementCost()
    {
        Replacement().HasErrors().ShouldBeFalse();

        _account.Balance.ShouldBe(Money.Of(Policy.ReplacementCharge));
        Event<ReplacementChargeRaised>().Amount.ShouldBe(Money.Of(Policy.ReplacementCharge));
    }

    // --- Redelivery --------------------------------------------------------------------------------

    [Fact]
    public void TheSameLateReturn_PricedTwice_ChargesOnce()
    {
        // Delivery across a module boundary is at-least-once, and the account's own memory of what
        // it has priced is the guard — no table of seen event identifiers.
        var loanId = LoanId.Generate();

        Fine(daysLate: 3, loanId);
        Fine(daysLate: 3, loanId);

        _account.Charges.ShouldHaveSingleItem();
        _account.Balance.ShouldBe(Money.Of(0.60m));
    }

    [Fact]
    public void TheSameLoss_RaisedTwice_ChargesOnce()
    {
        var loanId = LoanId.Generate();

        Replacement(loanId);
        Replacement(loanId);

        _account.Charges.ShouldHaveSingleItem();
    }

    [Fact]
    public void AFineAndAReplacement_ForOneLoan_AreBothCharged()
    {
        // The guard is per kind, and it has to be: the two occasions are disjoint in practice, but
        // a guard keyed on the loan alone would silently swallow the second if they ever met.
        var loanId = LoanId.Generate();

        Fine(daysLate: 3, loanId);
        Replacement(loanId);

        _account.Charges.Count.ShouldBe(2);
    }

    // --- Paying ------------------------------------------------------------------------------------

    [Fact]
    public void APayment_SettlesTheOldestFirst()
    {
        Fine(daysLate: 5, on: Today.AddDays(-10));
        Fine(daysLate: 10, on: Today);

        _account.TakePayment(Money.Of(1.00m)).HasErrors().ShouldBeFalse();

        // The older euro is gone entirely; the newer is partly settled.
        _account.Charges.ShouldHaveSingleItem();
        _account.Balance.ShouldBe(Money.Of(2.00m - 1.00m + 1.00m - 1.00m + 1.00m));
    }

    [Fact]
    public void APaymentThatClearsACharge_TakesItOutOfTheAccount()
    {
        Fine(daysLate: 3);

        _account.TakePayment(Money.Of(0.60m));

        _account.Charges.ShouldBeEmpty();
        _account.Balance.ShouldBe(Money.Zero);
    }

    [Fact]
    public void APaymentBeyondTheBalance_IsRefused()
    {
        // No credit balances, by decision: a negative balance would put a second sign into every
        // arithmetic here, and refusing costs a librarian one correction.
        Fine(daysLate: 3);

        var refusal = _account.TakePayment(Money.Of(5.00m));

        refusal.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(ChargesErrorCodes.PaymentExceedsBalance);
        _account.Balance.ShouldBe(Money.Of(0.60m));
    }

    [Fact]
    public void APaymentOfNothing_IsRefused()
    {
        _account.TakePayment(Money.Zero)
            .Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(ChargesErrorCodes.PaymentIsNotPositive);
    }

    // --- Waiving -----------------------------------------------------------------------------------

    [Fact]
    public void AWaivedCharge_LeavesTheAccountAndSaysWhatWasForgone()
    {
        Fine(daysLate: 3);
        var chargeId = _account.Charges.Single().Id;

        _account.Waive(chargeId).HasErrors().ShouldBeFalse();

        _account.Charges.ShouldBeEmpty();
        Event<ChargeWaived>().Amount.ShouldBe(Money.Of(0.60m));
    }

    [Fact]
    public void WaivingAChargeTheAccountDoesNotHold_IsRefused()
    {
        _account.Waive(ChargeId.Generate())
            .Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(ChargesErrorCodes.ChargeNotFound);
    }

    // --- The copy that turns up --------------------------------------------------------------------

    [Fact]
    public void ACopyFound_CancelsWhatIsStillOwedForIt()
    {
        var copyId = CopyId.Generate();
        Replacement(copyId: copyId);

        _account.CancelReplacementChargeFor(copyId).HasErrors().ShouldBeFalse();

        _account.Balance.ShouldBe(Money.Zero);
        Event<ChargeWaived>().Amount.ShouldBe(Money.Of(Policy.ReplacementCharge));
    }

    [Fact]
    public void ACopyFound_LeavesAFineForTheSameCopyAlone()
    {
        // A fine is charged for time and the time was still lost; only the charge for the object
        // itself is undone by the object turning up.
        var copyId = CopyId.Generate();
        Fine(daysLate: 3, copyId: copyId);
        Replacement(copyId: copyId);

        _account.CancelReplacementChargeFor(copyId);

        _account.Charges.ShouldHaveSingleItem().ShouldBeOfType<OverdueFine>();
    }

    [Fact]
    public void ACopyFound_WithNothingOutstandingForIt_ChangesNothingAndSucceeds()
    {
        // What makes the reversal safe to redeliver, and what keeps the already-paid case out of a
        // method that has no way to give money back.
        _account.CancelReplacementChargeFor(CopyId.Generate()).HasErrors().ShouldBeFalse();

        _account.DomainEvents.ShouldBeEmpty();
    }

    // --- What the account says about the amount ----------------------------------------------------

    [Fact]
    public void EveryMovement_AnnouncesBothAmounts()
    {
        Fine(daysLate: 3);

        var announced = Event<MemberBalanceChanged>();
        announced.PreviousBalance.ShouldBe(Money.Zero);
        announced.CurrentBalance.ShouldBe(Money.Of(0.60m));
    }

    [Fact]
    public void AMovementThatCrossesNothing_IsAnnouncedAllTheSame()
    {
        // The threshold lives in Circulation, so this context announces every movement and judges
        // none of them. A pair of transition events would have gone silent here.
        Fine(daysLate: 3);
        _account.ClearDomainEvents();

        Fine(daysLate: 5);

        var announced = Event<MemberBalanceChanged>();
        announced.PreviousBalance.ShouldBe(Money.Of(0.60m));
        announced.CurrentBalance.ShouldBe(Money.Of(1.60m));
    }

    [Fact]
    public void AnActionThatMovesNothing_AnnouncesNothing()
    {
        Fine(daysLate: 0);

        _account.DomainEvents.OfType<MemberBalanceChanged>().ShouldBeEmpty();
    }

    [Fact]
    public void TheBalanceReturningToNothing_IsAnnouncedLikeAnyOtherMovement()
    {
        Fine(daysLate: 3);
        _account.ClearDomainEvents();

        _account.TakePayment(Money.Of(0.60m));

        var announced = Event<MemberBalanceChanged>();
        announced.CurrentBalance.ShouldBe(Money.Zero);
    }
}
