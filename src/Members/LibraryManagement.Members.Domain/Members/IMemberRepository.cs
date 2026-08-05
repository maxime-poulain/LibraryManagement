namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// Stores and retrieves <see cref="Member"/> aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Declared by the domain and implemented by the infrastructure: the domain says what it needs of
/// a store, and knows nothing of how one works.
/// </para>
/// <para>
/// There is no <c>SaveAsync</c>. A command is the unit of consistency, and a pipeline behavior
/// writes everything it changed through the module's unit of work once the handler has reported
/// success.
/// </para>
/// </remarks>
public interface IMemberRepository
{
    /// <summary>
    /// Gets a member by identity.
    /// </summary>
    /// <param name="id">The member to get.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The member, or <see langword="null"/> when nobody is enrolled under that identifier.</returns>
    ValueTask<Member?> GetByIdAsync(MemberId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a card number is already assigned to a member.
    /// </summary>
    /// <param name="cardNumber">The number to look for.</param>
    /// <param name="except">
    /// A member to disregard, so that replacing a card with the number it already carries is not
    /// reported as a collision with itself.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the number is taken; <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// A card number identifies one member in the library, and a member cannot see the other
    /// members. The unique index is what holds the rule; this question exists so the refusal can
    /// name the number instead of surfacing as a constraint violation nobody can act on.
    /// </remarks>
    ValueTask<bool> CardNumberIsTakenAsync(
        CardNumber cardNumber,
        MemberId? except = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a newly enrolled member.
    /// </summary>
    /// <param name="member">The member to add.</param>
    /// <remarks>
    /// Synchronous, and deliberately so: nothing is written here — the module's unit of work
    /// writes once the command has succeeded — and identifiers are made by the domain before
    /// anything is stored, so nothing has to be fetched either.
    /// </remarks>
    void Add(Member member);
}
