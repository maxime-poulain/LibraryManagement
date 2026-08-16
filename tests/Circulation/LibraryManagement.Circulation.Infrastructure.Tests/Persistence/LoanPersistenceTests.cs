using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class LoanPersistenceTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Loan ALoan(
        CopyId? copyId = null,
        DateOnly? checkedOutOn = null,
        EditionId? editionId = null,
        BorrowerId? borrowerId = null)
        => Loan.CheckOut(
            LoanId.Generate(),
            copyId ?? CopyId.Generate(),
            editionId ?? EditionId.Generate(),
            borrowerId ?? BorrowerId.Generate(),
            checkedOutOn ?? Today,
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
            loaded.Return(Today.AddDays(25), CirculationPolicy.Current);
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
            loaded.Return(Today.AddDays(3), CirculationPolicy.Current);
            await updating.SaveChangesAsync(Token);
        }

        await using var writing = sqlServer.NewContext();
        writing.Add(ALoan(copyId));

        await writing.SaveChangesAsync(Token);
    }

    // --- What the scheduled process reads and writes ---------------------------------------------

    [Fact]
    public async Task TheRemindersALoanHasSent_SurviveTheRoundTrip()
    {
        // The whole of the daily run's idempotence: a loan loaded without its reminders would
        // announce everything a second time.
        var loan = await StoredAsync(ALoan());

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new LoanRepository(updating).GetByIdAsync(loan.Id, Token);
            loaded!.RemindOfBeingOverdue(loan.DueDate.AddDays(9), CirculationPolicy.Current);
            await updating.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await new LoanRepository(reading).GetByIdAsync(loan.Id, Token);

        found!.RemindersSent.ShouldBe(
            [Reminder.Overdue(1), Reminder.Overdue(7)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task ARenewal_ClearsTheStoredReminders()
    {
        var loan = await StoredAsync(ALoan());

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new LoanRepository(updating).GetByIdAsync(loan.Id, Token);
            loaded!.RemindOfBeingOverdue(loan.DueDate.AddDays(1), CirculationPolicy.Current);
            loaded.Renew(CirculationPolicy.Current);
            await updating.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await new LoanRepository(reading).GetByIdAsync(loan.Id, Token);

        found!.RemindersSent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ActiveDueBetween_FindsTheLoansTheCourtesyWindowCovers()
    {
        var inside = await StoredAsync(ALoan());
        var beyond = await StoredAsync(ALoan(checkedOutOn: Today.AddDays(10)));

        await using var reading = sqlServer.NewContext();
        var found = await new LoanRepository(reading)
            .ActiveDueBetweenAsync(inside.DueDate, inside.DueDate, Token);

        // By containment rather than by count: the suite shares one database, and a query over
        // every active loan sees its neighbours' rows too.
        var identifiers = found.Select(loan => loan.Id).ToList();
        identifiers.ShouldContain(inside.Id);
        identifiers.ShouldNotContain(beyond.Id);
    }

    [Fact]
    public async Task ActiveOfEdition_FindsWhatIsOutAndNothingThatEnded()
    {
        // The scope is the rule and not a convenience: a merge in Catalog moves live state, and an
        // ended loan records what was borrowed under the identifier it was borrowed under.
        var editionId = EditionId.Generate();
        var outOnLoan = ALoan(editionId: editionId);
        var returned = ALoan(editionId: editionId);
        var writtenOff = ALoan(editionId: editionId);
        returned.Return(Today, CirculationPolicy.Current);
        writtenOff.DeclareLost(Today);

        await using (var writing = sqlServer.NewContext())
        {
            writing.AddRange(outOnLoan, returned, writtenOff, ALoan());
            await writing.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await new LoanRepository(reading).ActiveOfEditionAsync(editionId, Token);

        found.Select(loan => loan.Id).ShouldBe([outOnLoan.Id]);
    }

    [Fact]
    public async Task ActiveOfEdition_ARecordWithNothingOut_IsEmpty()
    {
        await using var reading = sqlServer.NewContext();

        var found = await new LoanRepository(reading)
            .ActiveOfEditionAsync(EditionId.Generate(), Token);

        found.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARepointedLoan_NamesTheSurvivingRecordInTheStore()
    {
        // The property gained a private setter for this one caller, and only a round trip proves
        // the store writes the new value rather than the change tracker holding it.
        var loan = await StoredAsync(ALoan());
        var surviving = EditionId.Generate();

        await using (var updating = sqlServer.NewContext())
        {
            var repointing = await new LoanRepository(updating)
                .ActiveOfEditionAsync(loan.EditionId, Token);

            repointing.ShouldHaveSingleItem().RepointTo(surviving).HasErrors().ShouldBeFalse();
            await updating.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Loan>().SingleAsync(stored => stored.Id == loan.Id, Token);

        found.EditionId.ShouldBe(surviving);
    }

    [Fact]
    public async Task NotYetAnsweredForByBorrower_FindsTheUnsettledAndLeavesTheSettled()
    {
        // The third cut, held by the store: wider than the edition sweep's — a written-off loan
        // still speaks its borrower the day its copy resurfaces — and narrower than everything,
        // because a returned or recovered loan is settled business.
        var borrowerId = BorrowerId.Generate();
        var active = ALoan(borrowerId: borrowerId);
        var writtenOff = ALoan(borrowerId: borrowerId);
        var returned = ALoan(borrowerId: borrowerId);
        var recovered = ALoan(borrowerId: borrowerId);
        writtenOff.DeclareLost(Today.AddDays(45));
        returned.Return(Today, CirculationPolicy.Current);
        recovered.DeclareLost(Today.AddDays(45));
        recovered.RecordRecovery(Today.AddDays(90), CirculationPolicy.Current);

        await using (var writing = sqlServer.NewContext())
        {
            writing.AddRange(active, writtenOff, returned, recovered, ALoan());
            await writing.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await new LoanRepository(reading)
            .NotYetAnsweredForByBorrowerAsync(borrowerId, Token);

        found.Select(loan => loan.Id).ShouldBe([active.Id, writtenOff.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ALoanRepointedToItsMergedBorrower_NamesTheSurvivorInTheStore()
    {
        // The second property to gain a private setter for a merge, proved the same way: only a
        // round trip shows the store writes the new value rather than the change tracker holding
        // it.
        var loan = await StoredAsync(ALoan());
        var surviving = BorrowerId.Generate();

        await using (var updating = sqlServer.NewContext())
        {
            var repointing = await new LoanRepository(updating)
                .NotYetAnsweredForByBorrowerAsync(loan.BorrowerId, Token);

            repointing.ShouldHaveSingleItem()
                .RepointBorrowerTo(surviving).HasErrors().ShouldBeFalse();
            await updating.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Loan>().SingleAsync(stored => stored.Id == loan.Id, Token);

        found.BorrowerId.ShouldBe(surviving);
    }

    [Fact]
    public async Task ActiveOverdue_FindsThePastDueAndLeavesTheRest()
    {
        var loan = await StoredAsync(ALoan());

        await using var reading = sqlServer.NewContext();
        var repository = new LoanRepository(reading);

        (await repository.ActiveOverdueAsync(loan.DueDate.AddDays(1), Token))
            .Select(found => found.Id).ShouldContain(loan.Id);

        (await repository.ActiveOverdueAsync(loan.DueDate, Token))
            .Select(found => found.Id).ShouldNotContain(loan.Id);
    }

    [Fact]
    public async Task ActiveOverdue_LeavesTheLoansThatEnded()
    {
        var loan = ALoan();
        loan.Return(Today, CirculationPolicy.Current);
        await StoredAsync(loan);

        await using var reading = sqlServer.NewContext();
        var found = await new LoanRepository(reading)
            .ActiveOverdueAsync(loan.DueDate.AddDays(60), Token);

        found.Select(other => other.Id).ShouldNotContain(loan.Id);
    }
}
