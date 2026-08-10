using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.GetMemberBalance;

/// <summary>
/// Checks the shape of a <see cref="GetMemberBalanceQuery"/>.
/// </summary>
/// <remarks>
/// An empty identifier is not a question. Whether the member exists is Members' business and not
/// this module's: an account is keyed by an identifier this context never issued and never
/// validates, which is what keeps a charge countable after the person behind it is erased.
/// </remarks>
public sealed class GetMemberBalanceQueryValidator : AbstractValidator<GetMemberBalanceQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetMemberBalanceQueryValidator"/> class.
    /// </summary>
    public GetMemberBalanceQueryValidator()
    {
        RuleFor(query => query.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");
    }
}
