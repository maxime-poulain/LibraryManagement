using LibraryManagement.Circulation.Application.Loans.DeclareLoanLost;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

/// <summary>
/// The desk road to the loss the scheduled process reaches by the clock: a borrower reports the
/// copy gone, and the loan ends by that decision, on that day.
/// </summary>
public sealed class DeclareLoanLostCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();

    private ValueTask<Result> Handle(Guid loanId, DateOnly today)
        => new DeclareLoanLostCommandHandler(_loans, FrozenClock.At(today))
            .Handle(new DeclareLoanLostCommand(loanId), Token);

    private Loan AnActiveLoan()
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Generate(),
            BorrowerId.Generate(),
            Today,
            CirculationPolicy.Current);

        _loans.With(loan);
        return loan;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_EndsTheLoan_OnTheDayOfTheConfession()
    {
        var loan = AnActiveLoan();
        var reportedOn = Today.AddDays(10);

        var outcome = await Handle(loan.Id.Value, reportedOn);

        outcome.HasErrors().ShouldBeFalse();
        loan.Status.ShouldBe(LoanStatus.DeclaredLost);
        loan.DeclaredLostOn.ShouldBe(reportedOn);
        loan.DomainEvents.OfType<LoanDeclaredLost>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ALoanNobodyHolds_IsRefused()
    {
        CodesOf(await Handle(Guid.CreateVersion7(), Today))
            .ShouldContain(CirculationErrorCodes.LoanNotFound);
    }

    [Fact]
    public async Task Handle_ALoanTheRunAlreadyDeclared_AnswersSuccess()
    {
        // The run may beat the desk to it; the desk's arrival is then a redelivery of the same
        // decision, not a refusal to explain to a member who just confessed.
        var loan = AnActiveLoan();
        loan.DeclareLost(Today);
        loan.ClearDomainEvents();

        var outcome = await Handle(loan.Id.Value, Today.AddDays(1));

        outcome.HasErrors().ShouldBeFalse();
        loan.DeclaredLostOn.ShouldBe(Today);
        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_AReturnedLoan_IsRefused()
    {
        var loan = AnActiveLoan();
        loan.Return(Today, CirculationPolicy.Current);

        CodesOf(await Handle(loan.Id.Value, Today.AddDays(1)))
            .ShouldContain(CirculationErrorCodes.LoanNotActive);
    }
}
