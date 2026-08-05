using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain.Loans;

/// <summary>
/// Identifies a <see cref="Loan"/>.
/// </summary>
public sealed class LoanId : EntityId<LoanId>, IEntityId<LoanId>
{
    private LoanId(Guid value) : base(value)
    {
    }

    static LoanId IEntityId<LoanId>.FromValue(Guid value) => new(value);
}
