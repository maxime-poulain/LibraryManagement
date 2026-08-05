namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// What kind of member a person is enrolled as. Decides what circulation allows, but is not itself
/// a circulation concept — Members owns what a category <em>is</em>, Circulation owns what one may
/// <em>do</em>.
/// </summary>
/// <remarks>
/// <para>
/// A decision, never a derivation. The date of birth is recorded because the category is argued
/// from it, but no rule turns one into the other: a member does not change category at midnight on
/// their eighteenth birthday — a librarian changes it, ordinarily at renewal, and an automatic
/// promotion would move a member out of <see cref="Child"/> while their guardian is still the only
/// way to reach them.
/// </para>
/// <para>
/// An enumeration today. The day the library invents a category — a senior rate, a partner
/// institution — it becomes reference data, and the tactical design keeps that question open
/// rather than pretending it is settled.
/// </para>
/// </remarks>
public enum MemberCategory
{
    /// <summary>An adult member. The ordinary case.</summary>
    Adult,

    /// <summary>A minor. The one category that requires a guardian on the record, always.</summary>
    Child,

    /// <summary>A student. An adult for every rule this context holds; the distinction exists for
    /// the circulation policy and the statistics.</summary>
    Student,
}
