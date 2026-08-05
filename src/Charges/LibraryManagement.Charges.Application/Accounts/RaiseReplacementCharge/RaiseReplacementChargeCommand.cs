using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.RaiseReplacementCharge;

/// <summary>
/// Prices a copy that will not come back.
/// </summary>
/// <param name="MemberId">Who had it.</param>
/// <param name="LoanId">The loan the library gave up on.</param>
/// <param name="CopyId">The copy that never came back.</param>
/// <remarks>
/// Reached from a fact Circulation announced. The copy is named as well as the loan because the one
/// thing that can undo this arrives from Holdings, which speaks copies and has never heard of a
/// loan.
/// </remarks>
public sealed record RaiseReplacementChargeCommand(
    Guid MemberId,
    Guid LoanId,
    Guid CopyId) : ICommand<Result>;
