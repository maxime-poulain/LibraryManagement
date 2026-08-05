using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain;

/// <summary>
/// Identifies the copy a loan carries or a hold has trapped. Holdings' copy, named here.
/// </summary>
/// <remarks>
/// Declared in this context for the reason Holdings declares its own <c>EditionId</c>: a
/// cross-module reference is an identifier, redeclared locally, carrying the same value —
/// the model is translated at the boundary, never the identity. This context never mints a copy;
/// it is handed one, so <see cref="EntityId{T}.Create"/> is the only way one is ever built.
/// </remarks>
public sealed class CopyId : EntityId<CopyId>, IEntityId<CopyId>
{
    private CopyId(Guid value) : base(value)
    {
    }

    static CopyId IEntityId<CopyId>.FromValue(Guid value) => new(value);
}
