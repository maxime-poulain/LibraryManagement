namespace LibraryManagement.Circulation.Domain.Loans;

/// <summary>
/// Where a loan stands in its life.
/// </summary>
/// <remarks>
/// <see cref="Returned"/> and <see cref="DeclaredLost"/> are distinct because loan statistics
/// depend on it: a library's budget is argued from its circulation figures, and a copy that never
/// came back is not a completed loan. The participle carries the difference from Holdings' own
/// <c>Lost</c> — a copy is <c>Lost</c> when nobody knows where it is, a state discovered; a loan
/// is <see cref="DeclaredLost"/> when the library decides to stop waiting, a state chosen.
/// </remarks>
public enum LoanStatus
{
    /// <summary>The copy is out, expected back by the due date.</summary>
    Active,

    /// <summary>The copy came back. The ordinary end of a loan.</summary>
    Returned,

    /// <summary>The library stopped waiting. Terminal: finding the copy afterwards does not
    /// reopen the loan — it starts a return in Holdings' world, not this one.</summary>
    DeclaredLost,
}
