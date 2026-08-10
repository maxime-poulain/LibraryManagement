using FluentValidation;

namespace LibraryManagement.Members.Application.Members.GetMemberById;

/// <summary>
/// Checks the shape of a <see cref="GetMemberByIdQuery"/>.
/// </summary>
/// <remarks>
/// Whether a member is enrolled is not checked here, and could not be: that needs the store, and
/// the handler asks it. What is checked is that a question was asked at all — an empty identifier
/// is not one, and it is the difference between a malformed request and a member who is genuinely
/// not enrolled.
/// </remarks>
public sealed class GetMemberByIdQueryValidator : AbstractValidator<GetMemberByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetMemberByIdQueryValidator"/> class.
    /// </summary>
    public GetMemberByIdQueryValidator()
    {
        RuleFor(query => query.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");
    }
}
