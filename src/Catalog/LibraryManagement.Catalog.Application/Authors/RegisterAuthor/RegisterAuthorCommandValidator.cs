using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Authors.RegisterAuthor;

/// <summary>
/// Checks the shape of a <see cref="RegisterAuthorCommand"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only the shape. Whether a year of death may precede a year of birth is a rule about a person and
/// belongs to <see cref="LifeYears"/>; restating it here would create a second source of truth that
/// drifts from the first.
/// </para>
/// <para>
/// The empty identifier is the exception that proves the division. <c>AuthorId</c> refuses it by
/// throwing, because an identifier that is not one is a programmer error — but a request arriving
/// from outside can carry it, and that is a missing field. Catching it here is what keeps the
/// handler's conversion safe.
/// </para>
/// </remarks>
public sealed class RegisterAuthorCommandValidator : AbstractValidator<RegisterAuthorCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterAuthorCommandValidator"/> class.
    /// </summary>
    public RegisterAuthorCommandValidator()
    {
        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");

        RuleFor(command => command.AuthorizedName)
            .NotEmpty()
            .WithMessage("An authorized name is required.")
            .MaximumLength(PersonName.MaxLength)
            .WithMessage($"An authorized name may not exceed {PersonName.MaxLength} characters.");

        RuleFor(command => command.BirthYear)
            .InclusiveBetween(LifeYears.EarliestYear, LifeYears.LatestYear)
            .When(command => command.BirthYear is not null);

        RuleFor(command => command.DeathYear)
            .InclusiveBetween(LifeYears.EarliestYear, LifeYears.LatestYear)
            .When(command => command.DeathYear is not null);
    }
}
