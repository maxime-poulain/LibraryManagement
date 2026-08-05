using LibraryManagement.Members.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Members.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="IMemberRepository"/> over <see cref="MembersDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class MemberRepository(MembersDbContext context) : IMemberRepository
{
    /// <inheritdoc/>
    public async ValueTask<Member?> GetByIdAsync(
        MemberId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        // No Include: a member owns nothing that lives in another table. The name, the channels
        // and the guardian are values on the row itself.
        return await context.Members
            .FirstOrDefaultAsync(member => member.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> CardNumberIsTakenAsync(
        CardNumber cardNumber,
        MemberId? except = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cardNumber);

        var candidates = context.Members.Where(member => member.CardNumber == cardNumber);

        if (except is not null)
        {
            candidates = candidates.Where(member => member.Id != except);
        }

        return await candidates.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>Add</c> and not <c>DbSet.AddAsync</c>: that overload exists for value generators which
    /// must reach the database to produce a key, and it would open a round trip with nothing to
    /// fetch.
    /// </remarks>
    public void Add(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);

        // Tracked, not written. The module's unit of work writes once the command has succeeded.
        context.Members.Add(member);
    }
}
