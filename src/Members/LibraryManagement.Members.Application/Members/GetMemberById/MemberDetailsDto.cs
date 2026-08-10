namespace LibraryManagement.Members.Application.Members.GetMemberById;

/// <summary>
/// A member as the desk sees them: who they are, how they are reached, and until when they are
/// enrolled.
/// </summary>
/// <param name="MemberId">The member. Stands whether or not it still resolves to a person.</param>
/// <param name="GivenName">The given name, or <see langword="null"/> once the record was erased.</param>
/// <param name="FamilyName">The family name, or <see langword="null"/> once the record was erased.</param>
/// <param name="Category">The category, under the name the model gives it — never its number.</param>
/// <param name="CardNumber">The current card, or <see langword="null"/> once the record was erased.</param>
/// <param name="MembershipStart">The day the current membership period began.</param>
/// <param name="MembershipEnd">The day it runs out.</param>
/// <param name="Email">The email channel, when there is one.</param>
/// <param name="Phone">The telephone channel, when there is one.</param>
/// <param name="PostalAddress">The postal channel, when there is one.</param>
/// <param name="Guardian">Who the member is reached through, when they are not reached directly.</param>
/// <param name="ErasedOn">The day this record stopped being a person, or <see langword="null"/>
/// while it still is one.</param>
/// <remarks>
/// <para>
/// Carries primitives and not the domain's own types, for the reason every answer here does: this
/// leaves the context, and shipping <c>MemberName</c> or <c>CardNumber</c> across would make every
/// consumer compile against this module's model. <c>Category</c> travels as its name for the same
/// reason the column stores it that way — a person reads this, and <c>Child</c> answers where
/// <c>1</c> asks.
/// </para>
/// <para>
/// <strong>An erased record is an answer, not a refusal.</strong> Every <em>command</em> refuses on
/// an erased member, because acting on a record that is no longer a person is exactly what erasure
/// forbids. Reading is the opposite case: a query changes nothing, so it can break nothing, and the
/// desk has a real question to ask — the loans and charges keyed to that identifier stay countable
/// after the person behind it is gone, and a librarian holding one of them needs to be told *why*
/// the name is missing. Answering <c>Members.MemberErased</c> would put that fact in an error,
/// where it reads as a fault; carrying <see cref="ErasedOn"/> puts it in the record, where it reads
/// as history. The personal fields come back null because they are genuinely gone, not withheld.
/// </para>
/// <para>
/// <strong>The date of birth is deliberately not here.</strong> It exists in the aggregate to hold
/// one invariant — a child has a guardian — and it is the most sensitive field on the row. A desk
/// screen that displays it every time a file is opened would spread it further than the rule that
/// needs it, and no moment in this context asks the desk to read it back.
/// </para>
/// </remarks>
public sealed record MemberDetailsDto(
    Guid MemberId,
    string? GivenName,
    string? FamilyName,
    string Category,
    string? CardNumber,
    DateOnly MembershipStart,
    DateOnly MembershipEnd,
    string? Email,
    string? Phone,
    string? PostalAddress,
    GuardianDetailsDto? Guardian,
    DateOnly? ErasedOn);

/// <summary>
/// The adult a child member is reached through.
/// </summary>
/// <param name="GivenName">The guardian's given name.</param>
/// <param name="FamilyName">The guardian's family name.</param>
/// <param name="Email">The guardian's email channel, when there is one.</param>
/// <param name="Phone">The guardian's telephone channel, when there is one.</param>
/// <remarks>
/// Suffixed too, though the architecture rule only reaches the answer's own type: it travels just
/// as far and is just as free of the domain. Distinct from <c>GuardianDetails</c>, which this
/// module's commands take as input — one is what a caller sends, the other what a caller receives,
/// and merging them would tie the shape of a form to the shape of a screen.
/// </remarks>
public sealed record GuardianDetailsDto(
    string GivenName,
    string FamilyName,
    string? Email,
    string? Phone);
