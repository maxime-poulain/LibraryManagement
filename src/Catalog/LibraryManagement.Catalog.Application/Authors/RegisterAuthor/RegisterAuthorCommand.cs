using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.RegisterAuthor;

/// <summary>
/// Opens an authority record for a person.
/// </summary>
/// <param name="AuthorId">
/// The identifier the record will keep. Supplied by the caller rather than assigned by the store:
/// a command returns no value, so generating it here is what lets the caller know the identity of
/// what it just created.
/// </param>
/// <param name="PreferredName">The name to file the person under.</param>
/// <param name="BirthYear">The year of birth, if known.</param>
/// <param name="DeathYear">The year of death, if known.</param>
/// <remarks>
/// The identifier crosses as a <see cref="Guid"/> and not as an <c>AuthorId</c>. A command is the
/// shape of a request as it arrives — from a form, from JSON — and a strongly typed identifier with
/// a closed constructor cannot be deserialized into. The handler converts, once, after the validator
/// has established the value is there.
/// </remarks>
public sealed record RegisterAuthorCommand(
    Guid AuthorId,
    string PreferredName,
    int? BirthYear,
    int? DeathYear) : ICommand<Result>;
