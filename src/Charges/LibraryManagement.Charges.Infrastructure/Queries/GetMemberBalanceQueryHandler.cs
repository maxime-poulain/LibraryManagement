using LibraryManagement.Charges.Application.Accounts.GetMemberBalance;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Infrastructure.Queries;

/// <summary>
/// Handles <see cref="GetMemberBalanceQuery"/> against the module's store.
/// </summary>
/// <param name="context">The module's store.</param>
/// <remarks>
/// Answers through the same netting the port answers through, for the reason
/// <see cref="OutstandingCharges"/> gives: two contracts, one read.
/// </remarks>
public sealed class GetMemberBalanceQueryHandler(ChargesDbContext context)
    : IQueryHandler<GetMemberBalanceQuery, MemberBalanceDto>
{
    /// <inheritdoc/>
    public async ValueTask<Result<MemberBalanceDto>> Handle(
        GetMemberBalanceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var memberId = MemberId.Create(query.MemberId);

        // No 404 is possible here, and that is the model rather than an omission. An account opens
        // when the first charge is raised, so a member with no account is a member who owes
        // nothing — the same answer, reached without a row. Failing instead would make the desk
        // treat the most common case as an error.
        var owed = await OutstandingCharges
            .OwedByAsync(context, memberId, cancellationToken)
            .ConfigureAwait(false);

        return Result<MemberBalanceDto>.Success(new MemberBalanceDto(query.MemberId, owed));
    }
}
