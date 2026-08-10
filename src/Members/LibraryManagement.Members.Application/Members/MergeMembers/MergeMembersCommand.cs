using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.MergeMembers;

/// <summary>
/// Joins two records that turn out to be one person.
/// </summary>
/// <param name="AbsorbedMemberId">The record that stops being a person of its own.</param>
/// <param name="SurvivingMemberId">The record that goes on being the person.</param>
/// <remarks>
/// <para>
/// A desk act, and routed like one. Somebody enrolls twice under a maiden name, a card is reissued
/// as a new record, a form is typed twice on a busy afternoon — and the person who notices is the
/// member of staff with both files open. That judgement is theirs, exactly as judging two catalog
/// records one edition is a cataloger's.
/// </para>
/// <para>
/// <strong>Which record survives is the caller's choice and nothing second-guesses it.</strong> The
/// one with the current address, the longer history, the card in the person's hand — none of it is
/// visible to the model, and picking for them would be picking badly.
/// </para>
/// </remarks>
public sealed record MergeMembersCommand(Guid AbsorbedMemberId, Guid SurvivingMemberId)
    : ICommand<Result>;
