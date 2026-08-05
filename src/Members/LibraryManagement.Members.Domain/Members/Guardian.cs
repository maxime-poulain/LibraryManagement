using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// Who a member is reached through when they cannot be reached directly: a name and a way to reach
/// them, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// A member has an identity and, separately, a way of being reached that may belong to somebody
/// else — which is why this is a Members concept and not a Notifications one, as the strategic
/// design spells out.
/// </para>
/// <para>
/// <strong>Not a reference to another <see cref="Member"/>.</strong> Most guardians are not
/// members, and a link would make a parent's enrollment a precondition of a child's, which no
/// library asks for.
/// </para>
/// <para>
/// The French term is <em>représentant légal</em>, and it is wider than <em>parent</em> on
/// purpose: a protected adult under <em>tutelle</em> has one too. Which is why any category may
/// carry a guardian and only a child requires one — the invariant states the floor, not the
/// ceiling.
/// </para>
/// </remarks>
public sealed class Guardian : ValueObject<Guardian>
{
    // The materializer's path: a value whose parts are themselves values cannot arrive through a
    // constructor — the store binds only scalar-mapped properties to parameters — so the parts
    // arrive through their setters, here as on the aggregate itself.
    private Guardian()
    {
    }

    private Guardian(MemberName name, ContactDetails contact)
    {
        Name = name;
        Contact = contact;
    }

    /// <summary>Gets the guardian's name.</summary>
    public MemberName Name { get; private set; } = null!;

    /// <summary>
    /// Gets how the guardian is reached. Channels are as optional here as on the member — a
    /// guardian without one is a work-list case, not an invalid one.
    /// </summary>
    public ContactDetails Contact { get; private set; } = ContactDetails.None;

    /// <summary>
    /// Creates a guardian.
    /// </summary>
    /// <param name="name">The guardian's name.</param>
    /// <param name="contact">How they are reached.</param>
    /// <returns>The guardian.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// Returns a <see cref="Guardian"/> and not a <c>Result</c>: both parts have already enforced
    /// their own rules on creation, and nothing is left here to refuse.
    /// </remarks>
    public static Guardian Of(MemberName name, ContactDetails contact)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contact);

        return new Guardian(name, contact);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Contact;
    }
}
