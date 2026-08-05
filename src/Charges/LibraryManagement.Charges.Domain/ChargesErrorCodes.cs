using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Charges.Domain;

/// <summary>
/// Every way this context refuses, named once.
/// </summary>
public static class ChargesErrorCodes
{
    /// <summary>A payment of nothing, or of less than nothing.</summary>
    public static readonly ErrorCode PaymentIsNotPositive = new("Charges.PaymentIsNotPositive");

    /// <summary>More money than the member owes, which this library does not hold as credit.</summary>
    public static readonly ErrorCode PaymentExceedsBalance = new("Charges.PaymentExceedsBalance");

    /// <summary>No outstanding charge of that identity on the account.</summary>
    public static readonly ErrorCode ChargeNotFound = new("Charges.ChargeNotFound");

    /// <summary>No account for that member, and therefore nothing to act on.</summary>
    public static readonly ErrorCode AccountNotFound = new("Charges.AccountNotFound");
}
