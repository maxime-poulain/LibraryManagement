using LibraryManagement.Charges.Application.Accounts.CancelReplacementCharge;
using LibraryManagement.Charges.Application.Tests.TestDoubles;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Tests.Accounts;

/// <summary>
/// The second ending of a replacement charge: the copy turned up, and what is still owed for it
/// is cancelled — reached by a fact from Holdings rather than by a librarian's decision.
/// </summary>
public sealed class CancelReplacementChargeCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryAccounts _accounts = new();

    private ValueTask<Result> Handle(Guid copyId)
        => new CancelReplacementChargeCommandHandler(_accounts)
            .Handle(new CancelReplacementChargeCommand(copyId), Token);

    [Fact]
    public async Task Handle_CancelsWhatIsStillOwedForTheCopy()
    {
        var memberId = MemberId.Generate();
        var copyId = CopyId.Generate();
        var account = MemberAccount.For(memberId);
        account.RaiseReplacementCharge(
            ChargeId.Generate(), LoanId.Generate(), copyId, Today, ChargesPolicy.Current);
        account.ClearDomainEvents();
        _accounts.With(account);

        var outcome = await Handle(copyId.Value);

        outcome.HasErrors().ShouldBeFalse();
        account.Balance.ShouldBe(Money.Zero);
        account.DomainEvents.OfType<ChargeWaived>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ACopyNothingIsOutstandingFor_AnswersSuccess()
    {
        // Paid, already cancelled, or never charged — each an answer, none a failure: this
        // arrives from another module's drain, and a paid charge is not undone (no refunds).
        (await Handle(Guid.CreateVersion7())).HasErrors().ShouldBeFalse();
    }
}
