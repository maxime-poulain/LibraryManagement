using FluentValidation;

namespace LibraryManagement.Members.Application.Members.RenewMembership;

/// <summary>
/// Checks the shape of a <see cref="RenewMembershipCommand"/>.
/// </summary>
public sealed class RenewMembershipCommandValidator : AbstractValidator<RenewMembershipCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenewMembershipCommandValidator"/> class.
    /// </summary>
    public RenewMembershipCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");
    }
}
