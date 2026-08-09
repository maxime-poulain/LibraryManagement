using LibraryManagement.Circulation.Application.Loans.RecordLoanRecovery;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

/// <summary>
/// The reaction to a written-off copy turning up: the lateness it had accrued — frozen the day
/// the library stopped waiting — is announced at last.
/// </summary>
public sealed class RecordLoanRecoveryCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();

    private ValueTask<Result> Handle(Guid copyId, DateOnly today)
        => new RecordLoanRecoveryCommandHandler(
                _loans, CirculationPolicy.Current, FrozenClock.At(today))
            .Handle(new RecordLoanRecoveryCommand(copyId), Token);

    [Fact]
    public async Task Handle_AnnouncesTheWriteOffsLateness()
    {
        var copyId = CopyId.Generate();
        var loan = Loan.CheckOut(
            LoanId.Generate(), copyId, EditionId.Generate(), BorrowerId.Generate(),
            Today, CirculationPolicy.Current);
        loan.DeclareLost(loan.DueDate.AddDays(30));
        loan.ClearDomainEvents();
        _loans.With(loan);

        var foundOn = Today.AddDays(120);
        var outcome = await Handle(copyId.Value, foundOn);

        outcome.HasErrors().ShouldBeFalse();
        loan.RecoveredOn.ShouldBe(foundOn);
        loan.DomainEvents.OfType<LoanRecovered>().Single().DaysLate.ShouldBe(30);
    }

    [Fact]
    public async Task Handle_ACopyNoDeclaredLossEverInvolved_AnswersSuccess()
    {
        // A stocktake will one day find copies whose loss no loan produced; their recovery
        // prices nothing here, and refusing would jam the announcing module's drain.
        (await Handle(Guid.CreateVersion7(), Today)).HasErrors().ShouldBeFalse();
    }
}
