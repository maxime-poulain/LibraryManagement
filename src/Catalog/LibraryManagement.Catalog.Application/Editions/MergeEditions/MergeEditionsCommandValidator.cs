using FluentValidation;

namespace LibraryManagement.Catalog.Application.Editions.MergeEditions;

/// <summary>
/// Checks the shape of a <see cref="MergeEditionsCommand"/>.
/// </summary>
/// <remarks>
/// Two identifiers, both required. That they name the *same* record is not checked here even
/// though it could be: the aggregate refuses it, and a rule enforced in two places is a rule that
/// will one day be enforced differently in each. What the validator owns is the shape of the
/// request; what a merge means is the domain's.
/// </remarks>
public sealed class MergeEditionsCommandValidator : AbstractValidator<MergeEditionsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MergeEditionsCommandValidator"/> class.
    /// </summary>
    public MergeEditionsCommandValidator()
    {
        RuleFor(command => command.AbsorbedEditionId)
            .NotEmpty()
            .WithMessage("The absorbed edition's identifier is required.");

        RuleFor(command => command.SurvivingEditionId)
            .NotEmpty()
            .WithMessage("The surviving edition's identifier is required.");
    }
}
