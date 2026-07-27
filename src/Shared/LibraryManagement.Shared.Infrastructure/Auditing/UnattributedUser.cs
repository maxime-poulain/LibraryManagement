using LibraryManagement.Shared.Application;

namespace LibraryManagement.Shared.Infrastructure.Auditing;

/// <summary>
/// Answers that the work in progress is attributable to no one.
/// </summary>
/// <remarks>
/// <para>
/// The placeholder until Staff Access exists, and it is registered with a <c>TryAdd</c> so that a
/// host supplying a real <see cref="ICurrentUser"/> replaces it without this having to be removed
/// first.
/// </para>
/// <para>
/// It answers <see langword="null"/> rather than a name of its own invention. Auditing that records
/// who did something is worth having; auditing that records that <c>system</c> did everything is a
/// column full of noise that reads, at a glance, exactly like a column full of facts.
/// </para>
/// </remarks>
public sealed class UnattributedUser : ICurrentUser
{
    /// <inheritdoc/>
    public string? Identifier => null;
}
