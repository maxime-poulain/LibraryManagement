using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.RemoveAuthorCredit;

/// <summary>
/// Withdraws an author's credit from a work.
/// </summary>
/// <param name="WorkId">The work to remove the credit from.</param>
/// <param name="AuthorId">The author whose credit to remove.</param>
/// <remarks>
/// The reverse of attribution, for when scholarship reverses: a credit given in error, or an
/// attribution later disproved. A work may end with no author at all, which the model allows for
/// the same reason anonymous works exist — better no author than an invented one.
/// </remarks>
public sealed record RemoveAuthorCreditCommand(
    Guid WorkId,
    Guid AuthorId) : ICommand<Result>;
