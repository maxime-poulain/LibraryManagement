namespace LibraryManagement.Shared.Application;

/// <summary>
/// The employee on whose behalf the work in progress is being done.
/// </summary>
/// <remarks>
/// <para>
/// A port with no real implementation yet, and deliberately so. Staff Access is a generic subdomain
/// the strategic design names and nothing has built; whatever fills this will know about sessions,
/// tokens and roles, and none of that should reach the rest of the system. Everything that needs to
/// know who is acting asks this one question instead.
/// </para>
/// <para>
/// An <em>employee</em>, not a <em>user</em>. The strategic design spends a paragraph on the
/// distinction — a member is a domain concept with a subscription and an entitlement, an employee is
/// an access concept who authenticates and acts, and it is the employee that
/// <c>IAuditable.CreatedBy</c> must name. <c>User</c> is the word that blurs the two, and this is
/// the one place in the solution where getting it wrong would put a member's identity in an audit
/// column. <c>StaffMember</c> was the other candidate and was refused for containing <c>Member</c>,
/// which is the word the Members context owns.
/// </para>
/// <para>
/// That it names a business word inside a kernel that otherwise holds only building blocks is
/// deliberate and not new: <c>IAuditable</c> already commits to the concept, and <c>ICurrentUser</c>
/// did not avoid it — it only hid it behind a name that named nothing.
/// </para>
/// <para>
/// <see cref="EmployeeId"/> is nullable because "no one" is an answer rather than a gap. A migration,
/// a seeding script or a nightly job acts on nobody's behalf, and so does every command dispatched
/// today. Answering with an invented value — <c>system</c>, an empty string — would put something in
/// an audit column that no one can trace back to anything, and a column that quietly admits it does
/// not know is worth more than one that lies consistently.
/// </para>
/// </remarks>
public interface ICurrentEmployee
{
    /// <summary>
    /// Identifies the employee, or <see langword="null"/> when the work is attributable to no one.
    /// </summary>
    string? EmployeeId { get; }
}
