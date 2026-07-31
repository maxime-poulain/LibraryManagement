using LibraryManagement.Catalog.Application.Works.GetWorkById;
using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Queries;

/// <summary>
/// Handles <see cref="GetWorkByIdQuery"/> against the module's store.
/// </summary>
/// <param name="context">The module's store.</param>
/// <remarks>
/// <para>
/// A query handler lives in the infrastructure, unlike a command handler. The read side has no
/// domain model to protect, so it has no application layer to speak of: a query is a projection
/// written in SQL and a shape to return it in. Putting an interface and an adapter between this
/// method and the query it runs would add a seam that isolates nothing — three types where the work
/// is one — and the argument for it is the one this codebase already rejects, that boundaries are
/// good regardless of what they hold apart.
/// </para>
/// <para>
/// The write side keeps its ports and keeps them for a reason. A repository hands back aggregates so
/// their invariants can be enforced while they change, and the domain must be able to say what it
/// needs of a store without knowing one exists. Nothing read here can violate anything, because
/// nothing read here is ever written back.
/// </para>
/// <para>
/// What stays in the application layer is the contract: <see cref="GetWorkByIdQuery"/> and
/// <see cref="WorkDetailsDto"/>. A caller depends on those and never on this class.
/// </para>
/// </remarks>
public sealed class GetWorkByIdQueryHandler(CatalogDbContext context)
    : IQueryHandler<GetWorkByIdQuery, WorkDetailsDto>
{
    /// <inheritdoc/>
    public async ValueTask<Result<WorkDetailsDto>> Handle(
        GetWorkByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // No guard against the empty Guid here. GetWorkByIdQueryValidator rejects it before this
        // runs, and repeating the check would make a malformed request indistinguishable from a work
        // that is genuinely not cataloged — the exact conflation the validator exists to prevent.
        var id = WorkId.Create(query.WorkId);

        // No aggregate is materialized: the projection is written into the query, so the database
        // returns the columns the answer needs and nothing more. Nothing is tracked either, so this
        // code could not write back even by accident.
        //
        // The identifier and the title each occupy one column through a value converter, so the
        // projection selects the whole value object. Reaching for `.Value` inside the query would
        // ask for a column that does not exist.
        var work = await context.Works
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.PreferredTitle,
                AuthorIds = candidate.AuthorIds.ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (work is null)
        {
            return NotFound(query.WorkId);
        }

        var credits = work.AuthorIds;

        // A second round trip rather than a join, because the answer wants the authors in the order
        // they are credited and a join would return them in whatever order the store finds them.
        var names = await context.Authors
            .AsNoTracking()
            .Where(author => credits.Contains(author.Id))
            .Select(author => new { author.Id, author.PreferredName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var preferredNames = names.ToDictionary(author => author.Id, author => author.PreferredName.Value);

        var credited = credits
            .Where(preferredNames.ContainsKey)
            .Select(authorId => new CreditedAuthorDto(authorId.Value, preferredNames[authorId]))
            .ToArray();

        return Result<WorkDetailsDto>.Success(
            new WorkDetailsDto(query.WorkId, work.PreferredTitle.Value, credited));
    }

    // A work that is not cataloged is a failure and not an empty answer. The caller asked for one
    // particular work by identity; handing back a null would leave them to tell "there is no such
    // work" apart from "something went wrong", which is what a Result spares them.
    private static Result<WorkDetailsDto> NotFound(Guid workId)
        => Result<WorkDetailsDto>.Failure(
            CatalogErrorCodes.WorkNotFound,
            $"No work is cataloged under '{workId}'.");
}
