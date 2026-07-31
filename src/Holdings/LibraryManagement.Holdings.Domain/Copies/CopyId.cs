using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// Identifies a <see cref="Copy"/>.
/// </summary>
/// <remarks>
/// The identity, and the barcode is not: a label is replaced whenever one stops scanning, and a copy
/// whose identity moved with its label would take its whole loan history with it.
/// </remarks>
public sealed class CopyId : EntityId<CopyId>, IEntityId<CopyId>
{
    private CopyId(Guid value) : base(value)
    {
    }

    static CopyId IEntityId<CopyId>.FromValue(Guid value) => new(value);
}
