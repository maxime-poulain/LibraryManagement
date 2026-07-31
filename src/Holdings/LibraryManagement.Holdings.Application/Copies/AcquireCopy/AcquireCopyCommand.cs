using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.AcquireCopy;

/// <summary>
/// Takes a copy into the collection.
/// </summary>
/// <param name="CopyId">The identifier the copy will keep for its whole life.</param>
/// <param name="EditionId">The edition it is a copy of.</param>
/// <param name="Barcode">The label it is accessioned under. Unique in the library.</param>
/// <param name="Shelfmark">Where it will stand.</param>
/// <param name="AcquiredOn">The day it entered the collection.</param>
/// <param name="Condition">Its state on arrival.</param>
/// <param name="ReferenceOnly">Whether the acquisition decision excludes it from lending from the outset.</param>
/// <remarks>
/// <para>
/// <paramref name="AcquiredOn"/> is supplied rather than stamped. A batch is often accessioned weeks
/// after it was delivered, and the date the business means is the delivery — which is also why this
/// is a business fact and not the audit trail's <c>CreatedOn</c>, written by the store on its own.
/// </para>
/// <para>
/// The shelfmark arrives as a decision already taken. It is composed from the class number and the
/// author's preferred name, but by whoever processes the copy, once, before a label is printed —
/// not computed here from Catalog data that would later move underneath it.
/// </para>
/// </remarks>
public sealed record AcquireCopyCommand(
    Guid CopyId,
    Guid EditionId,
    string Barcode,
    string Shelfmark,
    DateOnly AcquiredOn,
    CopyCondition Condition = CopyCondition.Good,
    bool ReferenceOnly = false) : ICommand<Result>;
