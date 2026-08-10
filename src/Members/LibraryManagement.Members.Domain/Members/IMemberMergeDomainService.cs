using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// Joins two records that turn out to be one person.
/// </summary>
/// <remarks>
/// A domain service and not a method on either record, because the rules are about the pair and
/// belong to neither alone — and because they need no store once both are loaded, which is the
/// boundary the convention draws. The caller names both records, so the caller loads them.
/// </remarks>
public interface IMemberMergeDomainService
{
    /// <summary>
    /// Points the absorbed record at the survivor, if the two may be joined.
    /// </summary>
    /// <param name="absorbed">The record that stops being a person of its own.</param>
    /// <param name="surviving">The record that goes on being the person.</param>
    /// <returns>Success, or every reason the two could not be joined.</returns>
    Result Merge(Member absorbed, Member surviving);
}
