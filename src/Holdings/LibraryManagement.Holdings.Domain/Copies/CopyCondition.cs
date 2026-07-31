namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// The physical state of a copy.
/// </summary>
/// <remarks>
/// <para>
/// A second axis, and deliberately not merged into <see cref="CopyStatus"/>. Most of a public
/// library's stock is <see cref="Worn"/> and perfectly lendable; a copy in <see cref="Good"/>
/// condition may be reference-only because it is the only one. One enum would force a choice between
/// recording what the object <em>is</em> and recording what may be <em>done</em> with it, and a
/// librarian needs both.
/// </para>
/// <para>
/// It decides nothing today. It is recorded because staff record it, and because the decision to
/// repair or to weed is argued from it. The day it starts blocking a loan it becomes a second input
/// to lendability, and that is a rule to write when someone asks for it.
/// </para>
/// </remarks>
public enum CopyCondition
{
    /// <summary>Sound. What a copy is when it arrives, unless the delivery said otherwise.</summary>
    Good,

    /// <summary>Used, and none the worse for lending. The ordinary state of a working collection.</summary>
    Worn,

    /// <summary>Damaged enough that someone will have to decide between repair and weeding.</summary>
    Damaged,
}
