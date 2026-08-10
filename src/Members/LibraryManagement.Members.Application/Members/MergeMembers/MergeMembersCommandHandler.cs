using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.MergeMembers;

/// <summary>
/// Handles <see cref="MergeMembersCommand"/>.
/// </summary>
/// <param name="members">The store both records are read from and the absorbed one written through.</param>
/// <param name="merge">Decides whether the two records may be joined, and joins them.</param>
/// <remarks>
/// Orchestration and nothing else: it asks the store the one question a store has to answer — do
/// these identifiers resolve? — and hands the two records to the domain service. Whether they may be
/// joined is a set of rules that need no store once both are in hand, and they live together in
/// <see cref="IMemberMergeDomainService"/> rather than half here and half on the aggregate. That
/// split is the repository's general rule and not a preference about this use case.
/// </remarks>
public sealed class MergeMembersCommandHandler(
    IMemberRepository members,
    IMemberMergeDomainService merge) : ICommandHandler<MergeMembersCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        MergeMembersCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbedId = MemberId.Create(command.AbsorbedMemberId);
        var survivingId = MemberId.Create(command.SurvivingMemberId);

        var absorbed = await members.GetByIdAsync(absorbedId, cancellationToken).ConfigureAwait(false);
        var surviving = await members.GetByIdAsync(survivingId, cancellationToken).ConfigureAwait(false);

        var errors = new ErrorCollection();

        // Both are reported together when both are missing. Somebody who mistyped one identifier may
        // well have mistyped the other, and finding out one at a time is two trips.
        if (absorbed is null)
        {
            errors.Add(
                MembersErrorCodes.MemberNotFound,
                $"No member is enrolled under '{absorbedId}'.");
        }

        if (surviving is null)
        {
            errors.Add(
                MembersErrorCodes.MemberNotFound,
                $"No member is enrolled under '{survivingId}'.");
        }

        return errors.Count > 0
            ? Result.Failure(errors)
            : merge.Merge(absorbed!, surviving!);
    }
}
