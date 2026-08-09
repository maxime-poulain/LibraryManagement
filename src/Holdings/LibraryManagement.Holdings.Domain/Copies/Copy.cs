using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// One physical object the library owns, of one edition. The thing on the shelf.
/// </summary>
/// <remarks>
/// <para>
/// The only aggregate in this context, and one is the right number: nothing here spans two copies.
/// Weeding a shelf of forty is forty independent decisions that happen to be taken in one afternoon.
/// </para>
/// <para>
/// It holds an <see cref="EditionId"/> and nothing else of the bibliographic record. What the thing
/// <em>is</em> belongs to Catalog and is the same for every library in the world; what this copy is,
/// where it stands and what state it is in belongs here and is ours alone.
/// </para>
/// <para>
/// It does not know whether it is out on loan, and must not learn. Availability is the conjunction
/// of a Holdings fact and a Circulation fact, computed by whoever asks and stored in neither.
/// </para>
/// </remarks>
public sealed class Copy : AggregateRoot<CopyId>
{
    private Copy(
        CopyId id,
        EditionId editionId,
        Barcode barcode,
        Shelfmark shelfmark,
        CopyCondition condition,
        CopyStatus status,
        DateOnly acquiredOn) : base(id)
    {
        EditionId = editionId;
        Barcode = barcode;
        Shelfmark = shelfmark;
        Condition = condition;
        Status = status;
        AcquiredOn = acquiredOn;
    }

    /// <summary>Gets the edition this is a copy of. Identity only, never the edition.</summary>
    public EditionId EditionId { get; }

    /// <summary>Gets the label this copy is identified by at the desk.</summary>
    public Barcode Barcode { get; private set; }

    /// <summary>Gets where this copy stands.</summary>
    public Shelfmark Shelfmark { get; private set; }

    /// <summary>Gets the physical state recorded for this copy.</summary>
    public CopyCondition Condition { get; private set; }

    /// <summary>Gets what Holdings knows about this copy's disposition.</summary>
    public CopyStatus Status { get; private set; }

    /// <summary>
    /// Gets the status a repair — or a loss — will end in, or <see langword="null"/> when the
    /// copy is in neither.
    /// </summary>
    /// <remarks>
    /// The one field that exists purely to prevent a specific accident, twice. A reference-only
    /// copy sent for rebinding must come back reference-only; returning everything to
    /// <see cref="CopyStatus.InService"/> would quietly release the library's only copy of
    /// something into the lending stock, and nobody would notice until it left the building. A
    /// loss is the same exit by another door — the 1908 volume mislaid behind a shelf and found
    /// six weeks later must not rejoin the lending stock because whoever found it forgot to say
    /// otherwise — so the loss remembers exactly as the repair does, and <see cref="Find"/>
    /// consumes the memory exactly as <see cref="ReturnFromRepair"/> does.
    /// </remarks>
    public CopyStatus? ReturnsTo { get; private set; }

    /// <summary>Gets the day the library took this copy into its collection.</summary>
    /// <remarks>
    /// Supplied rather than stamped: a batch is often accessioned weeks after it was delivered, and
    /// the date the business means is the delivery. The domain has no clock, and this is a business
    /// fact rather than the audit trail's <c>CreatedOn</c>, which the store writes on its own.
    /// </remarks>
    public DateOnly AcquiredOn { get; }

    /// <summary>
    /// Takes a copy into the collection.
    /// </summary>
    /// <param name="id">The identifier the copy will keep for its whole life.</param>
    /// <param name="editionId">The edition it is a copy of. That it exists is the caller's rule to enforce.</param>
    /// <param name="barcode">The label. That it is free is the caller's rule to enforce.</param>
    /// <param name="shelfmark">Where it will stand.</param>
    /// <param name="condition">Its state on arrival.</param>
    /// <param name="acquiredOn">The day it entered the collection.</param>
    /// <param name="referenceOnly">
    /// Whether the acquisition decision excludes it from lending from the outset.
    /// </param>
    /// <returns>The new copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is null.</exception>
    /// <remarks>
    /// Returns a <see cref="Copy"/> and not a <see cref="Result{TValue}"/>, for the reason
    /// <c>Author.Register</c> gives: every rule that could refuse one has already been enforced by
    /// the value objects. The two that remain span more than this aggregate — whether the barcode is
    /// free, whether the edition exists — and neither can be answered from inside a copy, so the
    /// handler asks them.
    /// </remarks>
    public static Copy Acquire(
        CopyId id,
        EditionId editionId,
        Barcode barcode,
        Shelfmark shelfmark,
        CopyCondition condition,
        DateOnly acquiredOn,
        bool referenceOnly = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(editionId);
        ArgumentNullException.ThrowIfNull(barcode);
        ArgumentNullException.ThrowIfNull(shelfmark);

        var copy = new Copy(
            id,
            editionId,
            barcode,
            shelfmark,
            condition,
            referenceOnly ? CopyStatus.ReferenceOnly : CopyStatus.InService,
            acquiredOn);

        copy.AddDomainEvent(new CopyAcquired(id, editionId, barcode));

        return copy;
    }

    /// <summary>
    /// Records the copy as standing somewhere else.
    /// </summary>
    /// <param name="shelfmark">Where it stands from now on.</param>
    /// <returns>Success, or the reason the move was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shelfmark"/> is null.</exception>
    /// <remarks>
    /// Reshelving to where it already stands records nothing: nothing happened, and an event saying
    /// otherwise would send a projection to replace an entry with itself.
    /// </remarks>
    public Result Reshelve(Shelfmark shelfmark)
    {
        ArgumentNullException.ThrowIfNull(shelfmark);

        var refusal = RefuseIfWithdrawn("reshelved");

        if (refusal is not null)
        {
            return refusal;
        }

        if (shelfmark == Shelfmark)
        {
            return Result.Success();
        }

        var previous = Shelfmark;
        Shelfmark = shelfmark;

        AddDomainEvent(new CopyReshelved(Id, previous, shelfmark));

        return Result.Success();
    }

    /// <summary>
    /// Replaces the copy's label.
    /// </summary>
    /// <param name="barcode">The label from now on. That it is free is the caller's rule to enforce.</param>
    /// <returns>Success, or the reason the relabelling was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="barcode"/> is null.</exception>
    /// <remarks>
    /// An ordinary operation, not a repair of a mistake: labels peel off, tear and stop scanning.
    /// The identity does not move with the label, which is the whole reason the barcode is not the
    /// identity.
    /// </remarks>
    public Result Relabel(Barcode barcode)
    {
        ArgumentNullException.ThrowIfNull(barcode);

        var refusal = RefuseIfWithdrawn("relabelled");

        if (refusal is not null)
        {
            return refusal;
        }

        if (barcode == Barcode)
        {
            return Result.Success();
        }

        var previous = Barcode;
        Barcode = barcode;

        AddDomainEvent(new CopyRelabelled(Id, previous, barcode));

        return Result.Success();
    }

    /// <summary>
    /// Records the copy's physical state.
    /// </summary>
    /// <param name="condition">The state as observed.</param>
    /// <returns>Success, or the reason the record was refused.</returns>
    public Result RecordCondition(CopyCondition condition)
    {
        var refusal = RefuseIfWithdrawn("assessed");

        if (refusal is not null)
        {
            return refusal;
        }

        if (condition == Condition)
        {
            return Result.Success();
        }

        var previous = Condition;
        Condition = condition;

        AddDomainEvent(new CopyConditionRecorded(Id, previous, condition));

        return Result.Success();
    }

    /// <summary>
    /// Sends the copy for repair, remembering what it will come back to.
    /// </summary>
    /// <returns>Success, or the reason the copy could not be sent.</returns>
    /// <remarks>
    /// Refused for a copy already in repair, for one that is withdrawn, and for one that is lost —
    /// nothing can be sent for repair before it is found.
    /// </remarks>
    public Result SendForRepair()
    {
        if (Status is not (CopyStatus.InService or CopyStatus.ReferenceOnly))
        {
            return Result.Failure(
                HoldingsErrorCodes.StatusDoesNotAllowIt,
                $"A copy that is {Describe(Status)} cannot be sent for repair.");
        }

        ReturnsTo = Status;
        Status = CopyStatus.InRepair;

        AddDomainEvent(new CopySentForRepair(Id));

        return Result.Success();
    }

    /// <summary>
    /// Brings the copy back from repair, to whatever it left.
    /// </summary>
    /// <returns>Success, or the reason the copy could not come back.</returns>
    public Result ReturnFromRepair()
    {
        if (Status != CopyStatus.InRepair)
        {
            return Result.Failure(
                HoldingsErrorCodes.StatusDoesNotAllowIt,
                $"A copy that is {Describe(Status)} is not in repair, so it cannot come back from it.");
        }

        // Never null while the status is InRepair — SendForRepair is the only way in and it always
        // sets it — and the fallback is the safe reading rather than a shrug: a copy whose
        // destination were somehow lost is better held back than released.
        Status = ReturnsTo ?? CopyStatus.ReferenceOnly;
        ReturnsTo = null;

        AddDomainEvent(new CopyReturnedFromRepair(Id, Status));

        return Result.Success();
    }

    /// <summary>
    /// Holds the copy back from lending: consultable on site, never lent.
    /// </summary>
    /// <returns>Success, or the reason the restriction was refused.</returns>
    public Result RestrictToReference() => MoveWithinService(CopyStatus.ReferenceOnly);

    /// <summary>
    /// Returns a copy that was held back to the lending stock.
    /// </summary>
    /// <returns>Success, or the reason the release was refused.</returns>
    public Result ReleaseForLending() => MoveWithinService(CopyStatus.InService);

    /// <summary>
    /// Records that the copy is unaccounted for.
    /// </summary>
    /// <returns>Success, or the reason the copy could not be declared lost.</returns>
    /// <remarks>
    /// Reachable from this context's own initiative, and not only from a circulation event. A
    /// stocktake that failed to find a copy is the other way in, and the day it is modeled it must
    /// find a route already here.
    /// </remarks>
    public Result DeclareLost()
    {
        if (Status == CopyStatus.Withdrawn)
        {
            return Result.Failure(
                HoldingsErrorCodes.CopyIsWithdrawn,
                "A copy that left the collection cannot be declared lost.");
        }

        if (Status == CopyStatus.Lost)
        {
            return Result.Success();
        }

        // A copy lost from repair keeps the destination the repair had already recorded; every
        // other departure records the status it leaves. Erasing the memory here was the original
        // defect: a reference-only copy that vanished and turned up came back lendable, the exact
        // accident ReturnsTo exists to prevent, through the one other door out of service.
        if (Status != CopyStatus.InRepair)
        {
            ReturnsTo = Status;
        }

        Status = CopyStatus.Lost;

        AddDomainEvent(new CopyDeclaredLost(Id));

        return Result.Success();
    }

    /// <summary>
    /// Records that a copy nobody could account for has turned up. It returns to the status it
    /// was lost from — a decision nobody at the finding end has to remember to make.
    /// </summary>
    /// <returns>Success, or the reason the copy could not be found.</returns>
    /// <remarks>
    /// <para>
    /// This is what makes <see cref="CopyStatus.Lost"/> differ from <see cref="CopyStatus.Withdrawn"/>
    /// in kind rather than in degree. Copies turn up — reshelved two rows down, returned in a book
    /// drop months later — and a model whose only route out of a loss is someone editing the database
    /// teaches its users to distrust it.
    /// </para>
    /// <para>
    /// No destination parameter, on purpose: the memory decides, and a finder who judges the copy
    /// belongs elsewhere says so with the ordinary moments — <see cref="RestrictToReference"/>,
    /// <see cref="ReleaseForLending"/> — as an explicit correction after the find, never through a
    /// default whose quiet answer is the lending stock.
    /// </para>
    /// </remarks>
    public Result Find()
    {
        if (Status != CopyStatus.Lost)
        {
            return Result.Failure(
                HoldingsErrorCodes.StatusDoesNotAllowIt,
                $"A copy that is {Describe(Status)} was not lost, so it cannot be found.");
        }

        // Never null while the status is Lost — DeclareLost always records a destination — and
        // the fallback is the safe reading rather than a shrug, exactly as the repair's: a copy
        // whose destination were somehow forgotten is better held back than released.
        Status = ReturnsTo ?? CopyStatus.ReferenceOnly;
        ReturnsTo = null;

        AddDomainEvent(new CopyFound(Id, Status));

        return Result.Success();
    }

    /// <summary>
    /// Removes the copy from the collection, on purpose.
    /// </summary>
    /// <returns>Success, or the reason the withdrawal was refused.</returns>
    /// <remarks>
    /// <para>
    /// Terminal. Nothing that follows can put this copy back, and a copy that returned would be
    /// accessioned afresh with an identity of its own.
    /// </para>
    /// <para>
    /// Nothing checks that the copy is not on loan, and that is deliberate. Weeding is a shelf
    /// operation: the librarian is holding the object, so the rule is satisfied physically. Enforcing
    /// it would mean this context asking Circulation a question, reversing the supplier relationship
    /// for a case that cannot arise at the desk.
    /// </para>
    /// </remarks>
    public Result Withdraw()
    {
        if (Status == CopyStatus.Withdrawn)
        {
            return Result.Success();
        }

        Status = CopyStatus.Withdrawn;
        ReturnsTo = null;

        AddDomainEvent(new CopyWithdrawn(Id));

        return Result.Success();
    }

    /// <summary>
    /// Determines whether this copy may be lent, as far as this context can tell.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when nothing here prevents a loan; <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    /// Lendability and never availability: whether the copy is already out on loan is Circulation's
    /// own fact, and Circulation checks it on the line after this one.
    /// </remarks>
    public bool MayBeLent() => Status == CopyStatus.InService;

    private Result MoveWithinService(CopyStatus destination)
    {
        if (Status == destination)
        {
            return Result.Success();
        }

        if (Status is not (CopyStatus.InService or CopyStatus.ReferenceOnly))
        {
            // A copy in repair belongs to ReturnsTo at this point, and changing a repair's
            // destination halfway through is a different operation nobody has asked for.
            return Result.Failure(
                HoldingsErrorCodes.StatusDoesNotAllowIt,
                $"A copy that is {Describe(Status)} cannot be moved between the lending and "
                + "reference collections.");
        }

        Status = destination;

        AddDomainEvent(destination == CopyStatus.ReferenceOnly
            ? new CopyRestrictedToReference(Id)
            : new CopyReleasedForLending(Id));

        return Result.Success();
    }

    private Result? RefuseIfWithdrawn(string operation)
        => Status == CopyStatus.Withdrawn
            ? Result.Failure(
                HoldingsErrorCodes.CopyIsWithdrawn,
                $"A copy that left the collection cannot be {operation}.")
            : null;

    private static string Describe(CopyStatus status) => status switch
    {
        CopyStatus.InService => "in service",
        CopyStatus.InRepair => "in repair",
        CopyStatus.ReferenceOnly => "reference-only",
        CopyStatus.Withdrawn => "withdrawn",
        _ => "lost",
    };
}
