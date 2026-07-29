using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorLifeYears;

/// <summary>
/// Checks the shape of a <see cref="CorrectAuthorLifeYearsCommand"/>.
/// </summary>
/// <remarks>
/// Whether a year of death may precede a year of birth is a rule about a person and belongs to
/// <see cref="LifeYears"/>, exactly as it does on registration; only the range of each year is
/// checked here.
/// </remarks>
public sealed class CorrectAuthorLifeYearsCommandValidator : AbstractValidator<CorrectAuthorLifeYearsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorrectAuthorLifeYearsCommandValidator"/> class.
    /// </summary>
    public CorrectAuthorLifeYearsCommandValidator()
    {
        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");

        RuleFor(command => command.BirthYear)
            .InclusiveBetween(LifeYears.EarliestYear, LifeYears.LatestYear)
            .When(command => command.BirthYear is not null);

        RuleFor(command => command.DeathYear)
            .InclusiveBetween(LifeYears.EarliestYear, LifeYears.LatestYear)
            .When(command => command.DeathYear is not null);
    }
}
