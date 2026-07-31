using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorLifeYears;

/// <summary>
/// Corrects the years of birth and death on an authority record.
/// </summary>
/// <param name="AuthorId">The author whose years to correct.</param>
/// <param name="BirthYear">The year of birth, if known.</param>
/// <param name="DeathYear">The year of death, if known.</param>
/// <remarks>
/// Both years absent is a valid correction, not a missing field: it records that the years are not
/// known after all — a catalog frequently knows a name and nothing else. The commonest real use is
/// the other way round: an author dies, and a record that carried only a birth year gains its second
/// date.
/// </remarks>
public sealed record CorrectAuthorLifeYearsCommand(
    Guid AuthorId,
    int? BirthYear,
    int? DeathYear) : ICommand<Result>;
