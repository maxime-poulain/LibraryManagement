using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.RaiseDamageCharge;

/// <summary>
/// Prices a copy that came back spoiled.
/// </summary>
/// <param name="MemberId">Who brought it back.</param>
/// <param name="LoanId">The loan whose return carried the observation.</param>
/// <param name="CopyId">The copy that came back worse than it went out.</param>
/// <remarks>
/// The third occasion, beside the return and the write-off: not time passing, not an object gone,
/// but an object spoiled. The observation is Circulation's, made at the desk with the borrower
/// standing there; what it costs is decided here, by the tariff, exactly as lateness is.
/// </remarks>
public sealed record RaiseDamageChargeCommand(
    Guid MemberId,
    Guid LoanId,
    Guid CopyId) : ICommand<Result>;
