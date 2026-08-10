using LibraryManagement.Members.Application.Members.GetMemberById;
using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Members.Infrastructure.Queries;

/// <summary>
/// Handles <see cref="GetMemberByIdQuery"/> against the module's store.
/// </summary>
/// <param name="context">The module's store.</param>
/// <remarks>
/// A query handler lives in the infrastructure, unlike a command handler, for the reason
/// <c>GetWorkByIdQueryHandler</c> sets out at length: the read side has no domain model to protect,
/// so it has no application layer to speak of. What stays above is the contract —
/// <see cref="GetMemberByIdQuery"/> and <see cref="MemberDetailsDto"/> — and a caller depends on
/// those and never on this class.
/// </remarks>
public sealed class GetMemberByIdQueryHandler(MembersDbContext context)
    : IQueryHandler<GetMemberByIdQuery, MemberDetailsDto>
{
    /// <inheritdoc/>
    public async ValueTask<Result<MemberDetailsDto>> Handle(
        GetMemberByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // No guard against the empty Guid here. GetMemberByIdQueryValidator rejects it before this
        // runs, and repeating the check would make a malformed request indistinguishable from a
        // member who is genuinely not enrolled.
        var id = MemberId.Create(query.MemberId);

        // Nothing is tracked, so this code could not write back even by accident. The name, the
        // channels and the guardian are selected whole: each is a complex value spread across
        // columns of this same row, and the two that are optional come back null when the store
        // says they are absent — the erased member and the member reached directly.
        var member = await context.Members
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.Category,
                candidate.CardNumber,
                candidate.MembershipStart,
                candidate.MembershipEnd,
                candidate.ContactDetails,
                candidate.Guardian,
                candidate.ErasedOn,
                candidate.MergedInto,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (member is null)
        {
            return NotFound(query.MemberId);
        }

        // An erased record answers, and answers that it was erased. Its personal fields are null
        // because they are gone, which is what the null means here and nowhere else in this type.
        var guardian = member.Guardian is null
            ? null
            : new GuardianDetailsDto(
                member.Guardian.Name.GivenName,
                member.Guardian.Name.FamilyName,
                member.Guardian.Contact.Email,
                member.Guardian.Contact.Phone);

        return Result<MemberDetailsDto>.Success(
            new MemberDetailsDto(
                query.MemberId,
                member.Name?.GivenName,
                member.Name?.FamilyName,
                member.Category.ToString(),
                member.CardNumber?.Value,
                member.MembershipStart,
                member.MembershipEnd,
                member.ContactDetails.Email,
                member.ContactDetails.Phone,
                member.ContactDetails.PostalAddress,
                guardian,
                member.ErasedOn,
                member.MergedInto?.Value));
    }

    // A member who was never enrolled is a failure and not an empty answer, exactly as an
    // uncatalogued work is: the caller asked for one person by identity, and a null would leave
    // them to tell "no such member" apart from "something went wrong". An *erased* member is not
    // this case — that record exists and answers, carrying the day it stopped being a person.
    private static Result<MemberDetailsDto> NotFound(Guid memberId)
        => Result<MemberDetailsDto>.Failure(
            MembersErrorCodes.MemberNotFound,
            $"No member is enrolled under '{memberId}'.");
}
