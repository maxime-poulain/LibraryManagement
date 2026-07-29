using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.RetitleWork;

/// <summary>
/// Records a work under a different title.
/// </summary>
/// <param name="WorkId">The work to retitle.</param>
/// <param name="Title">The title from now on.</param>
/// <remarks>
/// Retitling to the current title succeeds and records nothing: nothing happened, and the model
/// keeps no memory of former titles. The search projection follows the same shape — the new title
/// replaces the old as an access point rather than demoting it, because a work's former title is
/// not a variant form the way an author's former name is.
/// </remarks>
public sealed record RetitleWorkCommand(
    Guid WorkId,
    string Title) : ICommand<Result>;
