using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.CheckOutCopy;

/// <summary>
/// Starts a loan: one copy, one borrower, one period. The act the glossary calls a checkout.
/// </summary>
/// <param name="LoanId">The identifier the loan will keep for its whole life.</param>
/// <param name="CopyId">The copy in hand, usually resolved from a barcode at the desk.</param>
/// <param name="BorrowerId">Who takes it, usually resolved from a card at the desk.</param>
/// <remarks>
/// No date travels in the command: the checkout the business means is the one happening at the
/// desk, and the due date is the policy's decision, not the caller's.
/// </remarks>
public sealed record CheckOutCopyCommand(Guid LoanId, Guid CopyId, Guid BorrowerId) : ICommand<Result>;
