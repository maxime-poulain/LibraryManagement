using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;

/// <summary>
/// Reads one borrower's circulation file: what they have out, what they are waiting for, and
/// whether what they owe forbids borrowing.
/// </summary>
/// <param name="BorrowerId">The borrower to read. Carries the same value as the member's
/// identifier — it is the model that is translated at this boundary, never the identity.</param>
/// <remarks>
/// <para>
/// <strong>One question, not two.</strong> Loans and holds are asked for together because they
/// live in the same context, and that is not a convenience: they were put in the same context
/// precisely because a copy shelved that was promised must be visible at the desk in the same
/// breath as the loan it came from. Splitting the file into two queries would let a page show a
/// return and miss the hold it just satisfied.
/// </para>
/// <para>
/// <strong>A borrower nobody has heard of is an empty file, not a failure.</strong> This context
/// holds no register of people — a borrower exists here the moment they first borrow — so a
/// question about someone with no loans and no holds has a true answer, and it is the empty one.
/// Whether the person is enrolled at all is Members' question, and the composer asks it there.
/// </para>
/// </remarks>
public sealed record GetBorrowerFileQuery(Guid BorrowerId) : IQuery<BorrowerFileDto>;
