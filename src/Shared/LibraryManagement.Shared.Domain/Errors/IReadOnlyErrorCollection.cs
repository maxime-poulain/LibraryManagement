namespace LibraryManagement.Shared.Domain.Errors;

/// <summary>
/// An immutable set of <see cref="Error"/> objects. This is the read side of the error channel:
/// what a failed <see cref="Results.Result"/> hands to its consumer, and what an
/// <see cref="IErrorCollection"/> produces through <see cref="IErrorCollection.AsReadOnly"/>.
/// </summary>
/// <remarks>
/// Deriving from <see cref="IReadOnlyList{T}"/> gives positional access,
/// <see cref="IReadOnlyCollection{T}.Count"/> and enumeration, which is the shape the framework
/// uses for read-only sequences.
/// Implementations handed out by this kernel are genuinely immutable — nothing else retains a
/// reference to their storage — so a result can be passed around without any risk of it changing
/// underneath its holder.
/// </remarks>
public interface IReadOnlyErrorCollection : IReadOnlyList<Error>
{
    /// <summary>
    /// Indicates whether this collection contains at least one error.
    /// </summary>
    bool HasErrors { get; }
}
