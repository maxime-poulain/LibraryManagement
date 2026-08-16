using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Circulation.Domain;

/// <summary>
/// Every <see cref="ErrorCode"/> the Circulation context can produce.
/// </summary>
/// <remarks>
/// Declared here rather than in the shared kernel, and prefixed with the context's own name, so no
/// two contexts can claim the same code and a code is self-describing wherever it surfaces. The
/// desk reads these: each names what the librarian tells the member, which is why a renewal has
/// three distinct refusals rather than one — they call for three different things from the
/// borrower.
/// </remarks>
public static class CirculationErrorCodes
{
    /// <summary>Nobody is enrolled under that identifier — a mis-scan or another library's card.</summary>
    public static readonly ErrorCode NoSuchMember = new("Circulation.NoSuchMember");

    /// <summary>The membership has lapsed. The conversation to have is a renewal, at the Members desk.</summary>
    public static readonly ErrorCode MembershipLapsed = new("Circulation.MembershipLapsed");

    /// <summary>The borrower owes money, and a debt forbids borrowing, renewing and placing holds.</summary>
    public static readonly ErrorCode DebtForbidsIt = new("Circulation.DebtForbidsIt");

    /// <summary>Loans and holds together are at the cap. Something must come back first.</summary>
    public static readonly ErrorCode AtTheCap = new("Circulation.AtTheCap");

    /// <summary>No copy is held under that identifier — a mis-scan, or a copy never accessioned.</summary>
    public static readonly ErrorCode NoSuchCopy = new("Circulation.NoSuchCopy");

    /// <summary>Holdings will not lend this copy — in repair, reference-only, lost or withdrawn.</summary>
    public static readonly ErrorCode CopyNotLendable = new("Circulation.CopyNotLendable");

    /// <summary>The copy is already out on loan. Two people cannot hold one object.</summary>
    public static readonly ErrorCode CopyAlreadyOnLoan = new("Circulation.CopyAlreadyOnLoan");

    /// <summary>The copy is set aside for another borrower's hold, and a walk-in does not jump a queue.</summary>
    public static readonly ErrorCode TrappedForAnotherBorrower = new("Circulation.TrappedForAnotherBorrower");

    /// <summary>No loan is held under that identifier.</summary>
    public static readonly ErrorCode LoanNotFound = new("Circulation.LoanNotFound");

    /// <summary>No active loan exists for that copy — nothing is out, so nothing can come back.</summary>
    public static readonly ErrorCode NothingOnLoan = new("Circulation.NothingOnLoan");

    /// <summary>The loan is not active — returned already, or written off as lost.</summary>
    public static readonly ErrorCode LoanNotActive = new("Circulation.LoanNotActive");

    /// <summary>The loan has been renewed as many times as the policy allows.</summary>
    public static readonly ErrorCode RenewalLimitReached = new("Circulation.RenewalLimitReached");

    /// <summary>Someone is waiting for this edition, and a renewal would keep the queue from turning.</summary>
    public static readonly ErrorCode SomeoneIsWaiting = new("Circulation.SomeoneIsWaiting");

    /// <summary>The borrower already has a copy of this edition on loan — a hold would claim what they hold.</summary>
    public static readonly ErrorCode AlreadyBorrowed = new("Circulation.AlreadyBorrowed");

    /// <summary>
    /// The borrower already has a live claim in this queue. The desk refuses a second one, which is
    /// stricter than the invariant a merge may reach — see <c>HoldQueue.PlaceHold</c>.
    /// </summary>
    public static readonly ErrorCode HoldAlreadyPlaced = new("Circulation.HoldAlreadyPlaced");

    /// <summary>A copy is available on the shelf — that is a checkout, and allowing the hold would
    /// make the queue meaningless.</summary>
    public static readonly ErrorCode CopyOnTheShelf = new("Circulation.CopyOnTheShelf");

    /// <summary>No hold of this borrower waits on that edition.</summary>
    public static readonly ErrorCode NoSuchHold = new("Circulation.NoSuchHold");

    /// <summary>No edition is cataloged under that identifier, so nothing can be claimed on it.</summary>
    /// <remarks>
    /// Also the answer for a record a cataloger merged away: Catalog stops acknowledging an absorbed
    /// identifier precisely so nothing new attaches to it.
    /// </remarks>
    public static readonly ErrorCode NoSuchEdition = new("Circulation.NoSuchEdition");

    /// <summary>A hold queue was asked to absorb itself.</summary>
    public static readonly ErrorCode QueueCannotAbsorbItself = new("Circulation.QueueCannotAbsorbItself");

    /// <summary>
    /// The loan has been answered for — returned, or its written-off copy recovered — and nothing
    /// will ever be asked of it again, so no merge of member files moves it.
    /// </summary>
    public static readonly ErrorCode LoanAlreadyAnsweredFor = new("Circulation.LoanAlreadyAnsweredFor");
}
