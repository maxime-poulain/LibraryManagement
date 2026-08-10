using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// Joins two records for one edition: one absorbs the other, which becomes a pointer at it.
/// </summary>
/// <remarks>
/// <para>
/// A domain service because the operation belongs to no single aggregate. Two of its rules are
/// about one record alone and two are about the pair, and splitting them between the aggregate and
/// a command handler put half a merge in the application layer — which is what this type exists to
/// undo. All four live here, and <c>Edition.AbsorbInto</c> is <c>internal</c> so this is the only
/// way to reach them.
/// </para>
/// <para>
/// <strong>It receives the aggregates and does not fetch them.</strong> The caller names both
/// records, so retrieving them is no part of deciding whether they may be joined — the repository
/// question (<em>does this identifier resolve?</em>) stays in the handler, where the unit of
/// consistency is visible. A domain service may take a repository when retrieval is intrinsically
/// part of its business logic; that is a rule about a set the caller cannot name in advance, and
/// this is not one.
/// </para>
/// </remarks>
public interface IEditionMergeDomainService
{
    /// <summary>
    /// Merges one record into another.
    /// </summary>
    /// <param name="absorbed">The record that stops being a record and becomes a pointer.</param>
    /// <param name="surviving">The record that goes on answering.</param>
    /// <returns>Success, or the reasons the merge was refused.</returns>
    Result Merge(Edition absorbed, Edition surviving);
}
