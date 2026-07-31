using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.ReshelveCopy;

/// <summary>
/// Records a copy as standing somewhere else.
/// </summary>
/// <param name="CopyId">The copy that moved.</param>
/// <param name="Shelfmark">Where it stands from now on.</param>
/// <remarks>
/// A decision and never a propagation. A shelfmark is composed from Catalog's facts once, then
/// printed on a label and stuck to a spine; correcting an author's preferred name afterwards must
/// not move it, because the labels on the shelves have not moved. Relabelling a shelf after a
/// reclassification is work a library schedules, and it arrives here as this command.
/// </remarks>
public sealed record ReshelveCopyCommand(Guid CopyId, string Shelfmark) : ICommand<Result>;
