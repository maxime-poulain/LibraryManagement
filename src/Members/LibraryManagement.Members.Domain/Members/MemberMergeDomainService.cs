using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// The rules that decide whether two records describe one person, and the act of joining them.
/// </summary>
/// <remarks>
/// <para>
/// Two records for one person is the data quality problem of every membership system: somebody
/// enrolls twice under a maiden name, a card is reissued as a new record, a form is typed twice on a
/// busy afternoon. What the model can decide is not <em>whether</em> they are the same person —
/// that is the judgement of the member of staff with both files open — but whether the two records
/// are in a state where the question can be answered at all.
/// </para>
/// <para>
/// <strong>All the rules are here.</strong> The aggregate's <c>MergeInto</c> is internal behind this
/// service and does only what remains once the decision is made, so there is one way in rather than
/// one among two — the arrangement Catalog's merge settled and Circulation's queues repeated.
/// </para>
/// <para>
/// <strong>Every refusal is reported, not just the first.</strong> Somebody looking at two records
/// one of which was already merged away, the other erased, is better served learning both than
/// making two trips.
/// </para>
/// </remarks>
public sealed class MemberMergeDomainService : IMemberMergeDomainService
{
    /// <inheritdoc/>
    public Result Merge(Member absorbed, Member surviving)
    {
        ArgumentNullException.ThrowIfNull(absorbed);
        ArgumentNullException.ThrowIfNull(surviving);

        var errors = new ErrorCollection();

        if (absorbed.Id == surviving.Id)
        {
            // Checked first and alone: every rule below reads as nonsense about one record compared
            // with itself, and reporting them would describe a mistake nobody made.
            return Result.Failure(
                MembersErrorCodes.MemberCannotAbsorbItself,
                "A member record cannot be merged into itself.");
        }

        Absorbable(absorbed, "absorbed", errors);
        Absorbable(surviving, "surviving", errors);

        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        absorbed.MergeInto(surviving.Id);

        return Result.Success();
    }

    // Both sides answer to the same two conditions, which is worth saying once rather than twice
    // with the words swapped.
    private static void Absorbable(Member member, string side, ErrorCollection errors)
    {
        if (member.MergedInto is not null)
        {
            // Neither silence nor a chain: a record merged twice would leave whoever holds its
            // identifier pointing at a pointer, and a consumer follows it exactly once. Whoever
            // meant it merges the survivor instead.
            errors.Add(
                MembersErrorCodes.MemberAlreadyMerged,
                $"The {side} record '{member.Id}' was already merged into '{member.MergedInto}'.");
        }

        if (member.ErasedOn is not null)
        {
            // An erased record has no name, no card and no address left, so nothing remains by
            // which anyone could have judged it a duplicate. A merge is a judgement about people,
            // and there is no longer a person here to judge.
            errors.Add(
                MembersErrorCodes.MemberErased,
                $"The {side} record '{member.Id}' was erased at its member's request; "
                + "nothing is left to recognize a duplicate by.");
        }
    }
}
