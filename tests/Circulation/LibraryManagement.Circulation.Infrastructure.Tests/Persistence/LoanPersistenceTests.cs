using LibraryManagement.Circulation.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class LoanPersistenceTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Loan ALoan(CopyId? copyId = null)
        => Loan.CheckOut(
            LoanId.Generate(),
            copyId ?? CopyId.Generate(),
            EditionId.Generate(),
            BorrowerId.Generate(),
            Today,
            CirculationPolicy.Current);

    private async Task<Loan> StoredAsync(Loan loan)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(loan);
        await writing.SaveChangesAsync(Token);
        return loan;
    }

    [Fact]
    public async Task ALoan_ComesBackWithEverythingItWasStoredWith()
    {
        var loan = await StoredAsync(ALoan());

        // A second context, so this proves the values reached the database rather than the change
        // tracker.
        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Loan>().SingleAsync(stored => stored.Id == loan.Id, Token);

        found.CopyId.ShouldBe(loan.CopyId);
        found.EditionId.ShouldBe(loan.EditionId);
        found.BorrowerId.ShouldBe(loan.BorrowerId);
        found.CheckedOutOn.ShouldBe(Today);
        found.DueDate.ShouldBe(Today.AddDays(CirculationPolicy.Current.LoanDurationInDays));
        found.RenewalCount.ShouldBe(0);
        found.ReturnedOn.ShouldBeNull();
        found.Status.ShouldBe(LoanStatus.Active);
    }

    [Fact]
    public async Task AReturn_SurvivesTheRoundTrip()
    {
        var loan = await StoredAsync(ALoan());

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await updating.Set<Loan>().SingleAsync(stored => stored.Id == loan.Id, Token);
            loaded.Return(Today.AddDays(25));
            await updating.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Loan>().SingleAsync(stored => stored.Id == loan.Id, Token);

        found.Status.ShouldBe(LoanStatus.Returned);
        found.ReturnedOn.ShouldBe(Today.AddDays(25));
    }

    [Fact]
    public async Task TwoActiveLoansForOneCopy_AreRefusedByTheStore()
    {
        // The handler asks first so the refusal can say so; this filtered index is what keeps the
        // answer true between the asking and the writing.
        var copyId = CopyId.Generate();
        await StoredAsync(ALoan(copyId));

        await using var writing = sqlServer.NewContext();
        writing.Add(ALoan(copyId));

        await Should.ThrowAsync<DbUpdateException>(() => writing.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task AReturnedLoanAndAnActiveOne_ShareACopyInPeace()
    {
        // The filter is the point: a copy's returned loans are legion and legitimate, and an
        // unfiltered unique index would allow each copy one loan in its whole life.
        var copyId = CopyId.Generate();
        var first = await StoredAsync(ALoan(copyId));

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await updating.Set<Loan>().SingleAsync(stored => stored.Id == first.Id, Token);
            loaded.Return(Today.AddDays(3));
            await updating.SaveChangesAsync(Token);
        }

        await using var writing = sqlServer.NewContext();
        writing.Add(ALoan(copyId));

        await writing.SaveChangesAsync(Token);
    }
}
