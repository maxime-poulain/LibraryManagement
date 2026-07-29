using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.CreditAuthor;

/// <summary>
/// Credits an author with a work already catalogued.
/// </summary>
/// <param name="WorkId">The work to credit the author with.</param>
/// <param name="AuthorId">The author to credit.</param>
/// <remarks>
/// Attribution is scholarship, and scholarship moves after cataloguing: a work registered anonymous
/// or under an incomplete statement of responsibility gains its author the day the attribution is
/// established. This command is that day's gesture — the registration-time list covers only what
/// was known at registration.
/// </remarks>
public sealed record CreditAuthorCommand(
    Guid WorkId,
    Guid AuthorId) : ICommand<Result>;
