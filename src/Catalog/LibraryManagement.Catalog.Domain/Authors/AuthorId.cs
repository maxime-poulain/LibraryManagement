using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// Identifies an <see cref="Author"/>.
/// </summary>
public sealed class AuthorId : EntityId<AuthorId>, IEntityId<AuthorId>
{
    private AuthorId(Guid value) : base(value)
    {
    }

    static AuthorId IEntityId<AuthorId>.FromValue(Guid value) => new(value);
}
