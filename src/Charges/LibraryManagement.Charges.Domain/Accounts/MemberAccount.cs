using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// What one member owes, and the charges it is made of.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The consistency boundary is a person's money.</strong> A payment allocated across several
/// charges, and the balance that decides whether the account crossed a line, are one decision made
/// in one breath; charges as independent aggregates would put that decision between two
/// transactions.
/// </para>
/// <para>
/// <strong>It holds what is outstanding, not a ledger.</strong> A charge paid or waived leaves the
/// account, having announced its own ending, and the read model keeps the trail. This is
/// <c>HoldQueue</c>'s rule on a colder path — an aggregate holds only what its invariants govern —
/// and it is what stops an account from becoming the second thing in this system that grows without
/// bound: answering <em>may this person borrow</em> must not load a decade of settled forty-cent
/// fines.
/// </para>
/// <para>
/// <strong><see cref="Balance"/> is computed.</strong> There is no stored total to fall out of step
/// with the charges beneath it — the same omission Members makes for entitlement, and for the same
/// reason: the day two sources of one truth disagree, nothing says which is right.
/// </para>
/// <para>
/// The desk does not read the balance through this aggregate. <em>How much does this member owe</em>
/// is asked on every checkout and every hold, and it is answered by a query summing outstanding
/// charges without materializing anything.
/// </para>
/// </remarks>
public sealed class MemberAccount : AggregateRoot<MemberId>
{
    private readonly List<Charge> _charges = [];

    private MemberAccount(MemberId id) : base(id)
    {
    }

    // For the store.
    private MemberAccount() : base(null!)
    {
    }

    /// <summary>The charges still outstanding, oldest first is not guaranteed — see the allocation.</summary>
    public IReadOnlyList<Charge> Charges => _charges.AsReadOnly();

    /// <summary>What the member owes, every outstanding charge netted.</summary>
    public Money Balance => _charges.Aggregate(Money.Zero, (running, charge) => Money.Add(running, charge.Outstanding));

    /// <summary>
    /// Opens an account for a member.
    /// </summary>
    /// <param name="memberId">The member.</param>
    /// <returns>The account, owing nothing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="memberId"/> is null.</exception>
    /// <remarks>
    /// Not a moment anyone performs at the desk. An account exists because a member has been charged,
    /// and the first charge is what creates it: an account per enrolled member would be a row
    /// forever, answering a question nobody asks. No account and a balance of zero are the same
    /// answer, and the port gives it for both.
    /// </remarks>
    public static MemberAccount For(MemberId memberId)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        return new MemberAccount(memberId);
    }

    /// <summary>
    /// Records what a copy returned late costs.
    /// </summary>
    /// <param name="chargeId">The charge's identity.</param>
    /// <param name="loanId">The loan returned late.</param>
    /// <param name="copyId">The copy behind it.</param>
    /// <param name="daysLate">Days past the due date, as Circulation counted them.</param>
    /// <param name="on">The day the return was recorded.</param>
    /// <param name="policy">The tariff.</param>
    /// <returns>Success — including when the tariff makes nothing of the delay.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is null.</exception>
    /// <remarks>
    /// A fine computed to nothing raises no charge at all: zero is the absence of a charge, and an
    /// account holding one would report a member as owing while the balance said otherwise. A return
    /// on time reaches here all the same — Circulation publishes every return, and deciding there is
    /// nothing to charge is this context's decision to make.
    /// </remarks>
    public Result AssessOverdueFine(
        ChargeId chargeId,
        LoanId loanId,
        CopyId copyId,
        int daysLate,
        DateOnly on,
        ChargesPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(chargeId);
        ArgumentNullException.ThrowIfNull(loanId);
        ArgumentNullException.ThrowIfNull(copyId);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfNegative(daysLate);

        if (AlreadyPriced(loanId, isReplacement: false))
        {
            // Delivery across a module boundary is at-least-once, so this is the redelivery guard —
            // and it is the aggregate's own memory rather than a table of seen event identifiers.
            return Result.Success();
        }

        var amount = policy.FineFor(daysLate);

        if (!amount.IsPositive)
        {
            return Result.Success();
        }

        var was = Balance;
        _charges.Add(OverdueFine.For(chargeId, amount, loanId, copyId, on, daysLate));

        AddDomainEvent(new OverdueFineAssessed(Id, chargeId, loanId, amount, daysLate));
        AnnounceBalance(was);

        return Result.Success();
    }

    /// <summary>
    /// Records what a copy that will not come back costs.
    /// </summary>
    /// <param name="chargeId">The charge's identity.</param>
    /// <param name="loanId">The loan given up on.</param>
    /// <param name="copyId">The copy that never came back.</param>
    /// <param name="on">The day the library stopped waiting.</param>
    /// <param name="policy">The tariff.</param>
    /// <returns>Success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is null.</exception>
    public Result RaiseReplacementCharge(
        ChargeId chargeId,
        LoanId loanId,
        CopyId copyId,
        DateOnly on,
        ChargesPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(chargeId);
        ArgumentNullException.ThrowIfNull(loanId);
        ArgumentNullException.ThrowIfNull(copyId);
        ArgumentNullException.ThrowIfNull(policy);

        if (AlreadyPriced(loanId, isReplacement: true))
        {
            return Result.Success();
        }

        var was = Balance;
        var amount = policy.ReplacementCost;
        _charges.Add(ReplacementCharge.For(chargeId, amount, loanId, copyId, on));

        AddDomainEvent(new ReplacementChargeRaised(Id, chargeId, loanId, amount));
        AnnounceBalance(was);

        return Result.Success();
    }

    /// <summary>
    /// Takes money against what is owed, oldest charge first.
    /// </summary>
    /// <param name="amount">What was received.</param>
    /// <returns>Success, or the reason the payment was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="amount"/> is null.</exception>
    /// <remarks>
    /// Oldest first is the convention, and it is the one that keeps an ancient forty-cent fine from
    /// following a member for years while newer ones are cleared. A payment naming a particular
    /// charge is an addition the day the desk asks for it, not a second concept.
    /// </remarks>
    public Result TakePayment(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!amount.IsPositive)
        {
            return Result.Failure(
                ChargesErrorCodes.PaymentIsNotPositive,
                "A payment of nothing is not a payment.");
        }

        if (amount.Exceeds(Balance))
        {
            // No credit balances, by decision: a negative balance would put a second sign into every
            // arithmetic here, and refusing costs a librarian one correction.
            return Result.Failure(
                ChargesErrorCodes.PaymentExceedsBalance,
                "That is more than the member owes, and this library holds no credit.");
        }

        var was = Balance;
        var remaining = amount;

        foreach (var charge in _charges.OrderBy(charge => charge.IncurredOn).ThenBy(charge => charge.Id.Value).ToList())
        {
            if (!remaining.IsPositive)
            {
                break;
            }

            var settled = Money.Lesser(remaining, charge.Outstanding);
            charge.Settle(settled);
            remaining = Money.Subtract(remaining, settled);

            if (charge.IsSettled)
            {
                Retire(charge, waived: false);
            }
        }

        AddDomainEvent(new PaymentTaken(Id, amount));
        AnnounceBalance(was);

        return Result.Success();
    }

    /// <summary>
    /// Cancels a charge by decision rather than by payment.
    /// </summary>
    /// <param name="chargeId">The charge to waive.</param>
    /// <returns>Success, or the reason it could not be waived.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chargeId"/> is null.</exception>
    /// <remarks>
    /// A waiver names one charge. Cancelling a whole balance in one act is an amnesty, which is a
    /// decision at a level this aggregate cannot see and is deliberately left out.
    /// </remarks>
    public Result Waive(ChargeId chargeId)
    {
        ArgumentNullException.ThrowIfNull(chargeId);

        var charge = _charges.Find(held => held.Id == chargeId);

        if (charge is null)
        {
            return Result.Failure(
                ChargesErrorCodes.ChargeNotFound,
                "This account holds no such outstanding charge.");
        }

        var was = Balance;
        Retire(charge, waived: true);
        AnnounceBalance(was);

        return Result.Success();
    }

    /// <summary>
    /// Cancels what is still owed for a copy that has turned up.
    /// </summary>
    /// <param name="copyId">The copy found.</param>
    /// <returns>Success, whether or not anything was outstanding for it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="copyId"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The second ending of a replacement charge, and the common case by some distance: a book
    /// mislaid behind a radiator reappears in weeks, long before anyone has produced the replacement
    /// cost at the desk. It is the waiver above, reached by a fact from Holdings rather than by a
    /// librarian's decision.
    /// </para>
    /// <para>
    /// A charge already paid is <em>not</em> undone here. Giving money back is a concept this
    /// context does not have, and inventing one silently at the end of this method would be the
    /// worst possible place to decide it. Answering success for a copy with nothing outstanding is
    /// also what makes this safe to redeliver.
    /// </para>
    /// </remarks>
    public Result CancelReplacementChargeFor(CopyId copyId)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        var outstanding = _charges
            .OfType<ReplacementCharge>()
            .Where(charge => charge.CopyId == copyId)
            .ToList();

        if (outstanding.Count == 0)
        {
            return Result.Success();
        }

        var was = Balance;

        foreach (var charge in outstanding)
        {
            Retire(charge, waived: true);
        }

        AnnounceBalance(was);

        return Result.Success();
    }

    // A charge that ends leaves the account, announcing how it ended — the difference between a
    // payment and a waiver is what a treasurer asks about first, so it must survive in the trail.
    private void Retire(Charge charge, bool waived)
    {
        _charges.Remove(charge);

        if (waived)
        {
            AddDomainEvent(new ChargeWaived(Id, charge.Id, charge.Outstanding));
        }
    }

    // One event carrying both amounts, and no word about owing: this context says what its own
    // arithmetic knows, and Circulation reads the pair to decide whether anything it cares about was
    // crossed. Silent when nothing moved, so a payment of nothing announces nothing.
    private void AnnounceBalance(Money was)
    {
        var now = Balance;

        if (was != now)
        {
            AddDomainEvent(new MemberBalanceChanged(Id, was, now));
        }
    }

    private bool AlreadyPriced(LoanId loanId, bool isReplacement) => _charges.Exists(
        charge => charge.LoanId == loanId && charge is ReplacementCharge == isReplacement);
}
