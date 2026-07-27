namespace LibraryManagement.Shared.Application;

/// <summary>
/// The staff member on whose behalf the work in progress is being done.
/// </summary>
/// <remarks>
/// <para>
/// A port with no real implementation yet, and deliberately so. Staff Access is a generic subdomain
/// the strategic design names and nothing has built; whatever fills this will know about sessions,
/// tokens and roles, and none of that should reach the rest of the system. Everything that needs to
/// know who is acting asks this one question instead.
/// </para>
/// <para>
/// <see cref="Identifier"/> is nullable because "no one" is an answer rather than a gap. A migration,
/// a seeding script or a nightly job acts on nobody's behalf, and so does every command dispatched
/// today. Answering with an invented value — <c>system</c>, an empty string — would put something in
/// an audit column that no one can trace back to anything, and a column that quietly admits it does
/// not know is worth more than one that lies consistently.
/// </para>
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    /// Identifies the staff member, or <see langword="null"/> when the work is attributable to no one.
    /// </summary>
    string? Identifier { get; }
}
