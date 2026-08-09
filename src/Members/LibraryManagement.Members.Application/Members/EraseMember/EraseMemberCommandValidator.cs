using FluentValidation;

namespace LibraryManagement.Members.Application.Members.EraseMember;

/// <summary>
/// Checks the shape of an <see cref="EraseMemberCommand"/>.
/// </summary>
public sealed class EraseMemberCommandValidator : AbstractValidator<EraseMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EraseMemberCommandValidator"/> class.
    /// </summary>
    public EraseMemberCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");
    }
}
