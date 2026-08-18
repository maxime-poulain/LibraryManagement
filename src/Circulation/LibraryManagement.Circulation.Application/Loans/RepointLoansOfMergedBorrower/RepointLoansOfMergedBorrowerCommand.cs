using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedBorrower;

/// <summary>
/// Points every loan an absorbed member record has not yet answered for at the record that survived
/// the merge.
/// </summary>
/// <param name="AbsorbedBorrowerId">The identifier that stopped naming a person of its own.</param>
/// <param name="SurvivingBorrowerId">The identifier the loans answer to from now on.</param>
/// <remarks>
/// <para>
/// Reached from a fact Members announced and never from the desk, which is why it is not routed:
/// the desk act was the merge itself, at the Members desk, and this is part of what that act costs
/// here. The exact arrangement of <c>RepointLoansOfMergedEditionCommand</c>, answering the other
/// merge ADR-0017 decides.
/// </para>
/// <para>
/// <strong>Not yet answered for, rather than live</strong> — the scope is a third cut, argued at
/// <c>Loan.RepointBorrowerTo</c>: a written-off loan still speaks its borrower the day its copy
/// resurfaces, so leaving it behind would have a late recovery billed to an account that no longer
/// answers for anyone.
/// </para>
/// </remarks>
public sealed record RepointLoansOfMergedBorrowerCommand(
    Guid AbsorbedBorrowerId,
    Guid SurvivingBorrowerId) : ICommand<Result>;
