using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// A person entitled to borrow. Never an employee.
/// </summary>
/// <remarks>
/// <para>
/// The only aggregate in this context, and one is the right number: nothing here spans two
/// members. A household enrolling together is several enrollments taken in one visit.
/// </para>
/// <para>
/// A member is a <em>domain</em> concept — a subscription, a category, an entitlement. An employee
/// is an <em>access</em> concept: they authenticate and act, and it is the employee, not the
/// member, that the audit trail's <c>CreatedBy</c> names.
/// </para>
/// <para>
/// <strong>Entitlement is computed, never stored.</strong> There is no <c>Active | Expired</c>
/// status field, and the omission is the design: the passage of time is not an event, and a stored
/// status would be false from midnight until a job corrected it — silently, which is the
/// <c>OnShelf</c> mistake transposed from space to time. The aggregate stores what it can vouch
/// for, the two dates, and <see cref="MembershipIsCurrentOn"/> judges them against the day in
/// hand.
/// </para>
/// </remarks>
public sealed class Member : AggregateRoot<MemberId>
{
    /// <summary>
    /// How long a membership runs, in months. The one real lever in this context.
    /// </summary>
    /// <remarks>
    /// One lever is a setting, not a policy — which is why there is no policy object here. The day
    /// categories subscribe differently — a student membership aligned on the academic year is the
    /// ordinary case — this single duration becomes a table indexed by category, an addition
    /// rather than a rewrite.
    /// </remarks>
    public const int MembershipDurationInMonths = 12;

    // Scalar-mapped values only. The name, the channels and the guardian are deliberately not
    // parameters: the store binds only scalar-mapped properties to a constructor, and a complex
    // value arrives through its setter — so Enroll assigns them the same way, and there is one
    // construction path for the factory and the materializer alike.
    private Member(
        MemberId id,
        DateOnly dateOfBirth,
        MemberCategory category,
        CardNumber cardNumber,
        DateOnly membershipStart,
        DateOnly membershipEnd) : base(id)
    {
        DateOfBirth = dateOfBirth;
        Category = category;
        CardNumber = cardNumber;
        MembershipStart = membershipStart;
        MembershipEnd = membershipEnd;
    }

    /// <summary>Gets the member's name, in ordinary order.</summary>
    /// <remarks>
    /// The null-forgiveness is the constructor note above, paid once: every path that builds a
    /// member — <see cref="Enroll"/> or the store — assigns a name before anyone can read one.
    /// </remarks>
    public MemberName Name { get; private set; } = null!;

    /// <summary>Gets the member's date of birth.</summary>
    /// <remarks>
    /// Recorded because staff record it and because the category is argued from it — and no rule
    /// derives one from the other. Age is a fact; the category is a decision.
    /// </remarks>
    public DateOnly DateOfBirth { get; }

    /// <summary>Gets what kind of member this person is enrolled as.</summary>
    public MemberCategory Category { get; private set; }

    /// <summary>Gets the number on the card the member presents at the desk.</summary>
    public CardNumber CardNumber { get; private set; }

    /// <summary>Gets the first day of the current membership period.</summary>
    public DateOnly MembershipStart { get; private set; }

    /// <summary>Gets the last day of the current membership period, inclusive.</summary>
    /// <remarks>
    /// The current period, not a history: renewal moves this date, every renewal is published, and
    /// a read model keeps the trail. The aggregate holds only what its invariants govern, and no
    /// invariant here reads last year's dates.
    /// </remarks>
    public DateOnly MembershipEnd { get; private set; }

    /// <summary>Gets how the member is reached. Every channel is optional.</summary>
    public ContactDetails ContactDetails { get; private set; } = ContactDetails.None;

    /// <summary>
    /// Gets who the member is reached through, or <see langword="null"/> when they are reached
    /// directly. Never null while the member is a child.
    /// </summary>
    public Guardian? Guardian { get; private set; }

    /// <summary>
    /// Enrolls a member: identity recorded, category decided, card issued, and the first
    /// membership period started that day.
    /// </summary>
    /// <param name="id">The identifier the member will keep for their whole life.</param>
    /// <param name="name">The member's name.</param>
    /// <param name="dateOfBirth">The member's date of birth. Must be in the past.</param>
    /// <param name="category">The category the librarian decided.</param>
    /// <param name="cardNumber">The card issued. That it is free is the caller's rule to enforce.</param>
    /// <param name="contactDetails">How the member is reached, possibly not at all.</param>
    /// <param name="guardian">Who they are reached through, required when the category is
    /// <see cref="MemberCategory.Child"/>.</param>
    /// <param name="today">The day of the enrollment. The domain has no clock; the caller does.</param>
    /// <returns>The new member, or the reasons the enrollment was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required reference argument is null.</exception>
    /// <remarks>
    /// Returns a <see cref="Result{TValue}"/> and not a bare <see cref="Member"/>, for the reason
    /// <c>Work.Register</c> does: two rules remain that the value objects cannot enforce — the
    /// date of birth against the day in hand, and the guardian a child must have.
    /// </remarks>
    public static Result<Member> Enroll(
        MemberId id,
        MemberName name,
        DateOnly dateOfBirth,
        MemberCategory category,
        CardNumber cardNumber,
        ContactDetails contactDetails,
        Guardian? guardian,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(cardNumber);
        ArgumentNullException.ThrowIfNull(contactDetails);

        if (dateOfBirth >= today)
        {
            return Result<Member>.Failure(
                MembersErrorCodes.DateOfBirthNotInThePast,
                "A date of birth must be in the past.");
        }

        if (category == MemberCategory.Child && guardian is null)
        {
            return Result<Member>.Failure(
                MembersErrorCodes.GuardianRequired,
                "A child member must have a guardian.");
        }

        var member = new Member(
            id,
            dateOfBirth,
            category,
            cardNumber,
            membershipStart: today,
            membershipEnd: today.AddMonths(MembershipDurationInMonths))
        {
            Name = name,
            ContactDetails = contactDetails,
            Guardian = guardian,
        };

        member.AddDomainEvent(new MemberEnrolled(id, category, cardNumber));

        return Result<Member>.Success(member);
    }

    /// <summary>
    /// Determines whether the membership holds on a given day.
    /// </summary>
    /// <param name="day">The day to judge against.</param>
    /// <returns><see langword="true"/> when the day falls inside the current period.</returns>
    /// <remarks>
    /// The rule, written where the domain can state it. The published language answers the same
    /// question from the table without loading the aggregate, and the two are kept in step by a
    /// test rather than by a comment.
    /// </remarks>
    public bool MembershipIsCurrentOn(DateOnly day)
        => day >= MembershipStart && day <= MembershipEnd;

    /// <summary>
    /// Renews the membership. Always allowed — nothing refuses a renewal.
    /// </summary>
    /// <param name="today">The day of the renewal.</param>
    /// <remarks>
    /// <para>
    /// Two arithmetics, chosen by the calendar. Before expiry — the expiry day itself included —
    /// the new end is the old end plus the duration: renewing early costs nothing, or members
    /// would learn to let memberships lapse before renewing, a rule teaching the behavior it
    /// exists to prevent. After expiry, the new period starts today: the gap is not billed and not
    /// back-dated, because nobody was entitled during it and the record should agree.
    /// </para>
    /// <para>
    /// A member returning after three years renews; they do not re-enroll. The identity persists
    /// and the loan history with it — a re-enrollment would mint a second identity and manufacture
    /// exactly the duplicate the tactical design worries about.
    /// </para>
    /// </remarks>
    public void Renew(DateOnly today)
    {
        if (today <= MembershipEnd)
        {
            MembershipEnd = MembershipEnd.AddMonths(MembershipDurationInMonths);
        }
        else
        {
            MembershipStart = today;
            MembershipEnd = today.AddMonths(MembershipDurationInMonths);
        }

        AddDomainEvent(new MembershipRenewed(Id, MembershipEnd));
    }

    /// <summary>
    /// Changes what kind of member this person is enrolled as.
    /// </summary>
    /// <param name="category">The category from now on.</param>
    /// <returns>Success, or the reason the change was refused.</returns>
    /// <remarks>
    /// Entering <see cref="MemberCategory.Child"/> requires a guardian on the record first, so the
    /// invariant holds at every instant. Leaving it removes nothing: changing what a member may do
    /// does not change who is reached, and a guardian outlives the category — a protected adult
    /// has one too.
    /// </remarks>
    public Result ChangeCategory(MemberCategory category)
    {
        if (category == Category)
        {
            return Result.Success();
        }

        if (category == MemberCategory.Child && Guardian is null)
        {
            return Result.Failure(
                MembersErrorCodes.GuardianRequired,
                "A child member must have a guardian; record one before the change.");
        }

        var previous = Category;
        Category = category;

        AddDomainEvent(new MemberCategoryChanged(Id, previous, category));

        return Result.Success();
    }

    /// <summary>
    /// Replaces the member's card.
    /// </summary>
    /// <param name="cardNumber">The number from now on. That it is free is the caller's rule to enforce.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cardNumber"/> is null.</exception>
    /// <remarks>
    /// An ordinary operation, not a repair of a mistake: cards are lost, chewed and demagnetized.
    /// The identity does not move with the card, which is the whole reason the number is not the
    /// identity. Replacing a card with itself records nothing — nothing happened.
    /// </remarks>
    public void ReplaceCard(CardNumber cardNumber)
    {
        ArgumentNullException.ThrowIfNull(cardNumber);

        if (cardNumber == CardNumber)
        {
            return;
        }

        var previous = CardNumber;
        CardNumber = cardNumber;

        AddDomainEvent(new CardReplaced(Id, previous, cardNumber));
    }

    /// <summary>
    /// Records new ways of reaching the member.
    /// </summary>
    /// <param name="contactDetails">The channels from now on, possibly none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contactDetails"/> is null.</exception>
    public void UpdateContactDetails(ContactDetails contactDetails)
    {
        ArgumentNullException.ThrowIfNull(contactDetails);

        if (contactDetails == ContactDetails)
        {
            return;
        }

        ContactDetails = contactDetails;

        AddDomainEvent(new ContactDetailsChanged(Id, contactDetails));
    }

    /// <summary>
    /// Records who the member is reached through, or that they now are reached directly.
    /// </summary>
    /// <param name="guardian">The guardian from now on, or <see langword="null"/> to remove one.</param>
    /// <returns>Success, or the reason the change was refused.</returns>
    /// <remarks>
    /// Removing the guardian of a child is refused: the operation that legitimately ends a child's
    /// guardianship is the category change, not a deletion that would leave a minor unreachable.
    /// </remarks>
    public Result ChangeGuardian(Guardian? guardian)
    {
        if (guardian is null && Category == MemberCategory.Child)
        {
            return Result.Failure(
                MembersErrorCodes.GuardianRequired,
                "A child member must have a guardian; change the category before removing one.");
        }

        if (Equals(guardian, Guardian))
        {
            return Result.Success();
        }

        Guardian = guardian;

        AddDomainEvent(new GuardianChanged(Id, guardian));

        return Result.Success();
    }

    /// <summary>
    /// Records the member under a different name.
    /// </summary>
    /// <param name="name">The name from now on.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <remarks>
    /// One operation, not two. Catalog needs <c>Rename</c> and <c>CorrectPreferredName</c> because
    /// a variant name survives the first and not the second; a member has no variant forms and no
    /// access points, so the distinction buys nothing here, and one operation carries the marriage
    /// and the typo alike.
    /// </remarks>
    public void Rename(MemberName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name == Name)
        {
            return;
        }

        var previous = Name;
        Name = name;

        AddDomainEvent(new MemberRenamed(Id, previous, name));
    }
}
