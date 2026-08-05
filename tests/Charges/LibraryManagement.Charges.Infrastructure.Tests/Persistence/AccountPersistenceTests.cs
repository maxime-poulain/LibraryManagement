using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Charges.Infrastructure.PublishedLanguage;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AccountPersistenceTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChargesPolicy Policy => ChargesPolicy.Current;

    private static MemberAccount AnAccountOwing(
        MemberId memberId,
        int daysLate = 3,
        CopyId? copyId = null)
    {
        var account = MemberAccount.For(memberId);
        account.AssessOverdueFine(
            ChargeId.Generate(),
            LoanId.Generate(),
            copyId ?? CopyId.Generate(),
            daysLate,
            Today,
            Policy);

        return account;
    }

    private async Task<MemberAccount> StoredAsync(MemberAccount account)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(account);
        await writing.SaveChangesAsync(Token);
        return account;
    }

    private async Task<MemberAccount?> ReadBackAsync(MemberId memberId)
    {
        await using var reading = sqlServer.NewContext();
        return await new MemberAccountRepository(reading).GetByMemberAsync(memberId, Token);
    }

    [Fact]
    public async Task AnAccount_ComesBackWithItsChargesAndItsBalance()
    {
        var memberId = MemberId.Generate();
        await StoredAsync(AnAccountOwing(memberId));

        var found = await ReadBackAsync(memberId);

        found.ShouldNotBeNull().Balance.ShouldBe(Money.Of(0.60m));
        found.Charges.ShouldHaveSingleItem().ShouldBeOfType<OverdueFine>().DaysLate.ShouldBe(3);
    }

    [Fact]
    public async Task BothKindsOfCharge_ComeBackAsThemselves()
    {
        // One table, and the discriminator is what makes a replacement charge materialize as one.
        // Without it both would come back as the base type and the found-copy path would find
        // nothing to cancel.
        var memberId = MemberId.Generate();
        var account = AnAccountOwing(memberId);
        account.RaiseReplacementCharge(
            ChargeId.Generate(), LoanId.Generate(), CopyId.Generate(), Today, Policy);
        await StoredAsync(account);

        var found = await ReadBackAsync(memberId);

        found.ShouldNotBeNull().Charges.OfType<OverdueFine>().ShouldHaveSingleItem();
        found.Charges.OfType<ReplacementCharge>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AChargeAddedToAnAccountAlreadyOnFile_ReachesTheTable()
    {
        // The path every subscriber takes: load, change, save. The owned-collection defect lived
        // exactly here, and a test that only ever adds before the first save would not see it.
        var memberId = MemberId.Generate();
        await StoredAsync(AnAccountOwing(memberId));

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new MemberAccountRepository(updating).GetByMemberAsync(memberId, Token);
            loaded!.RaiseReplacementCharge(
                ChargeId.Generate(), LoanId.Generate(), CopyId.Generate(), Today, Policy);
            await updating.SaveChangesAsync(Token);
        }

        var found = await ReadBackAsync(memberId);

        found.ShouldNotBeNull().Charges.Count.ShouldBe(2);
        found.Balance.ShouldBe(Money.Of(0.60m + Policy.ReplacementCharge));
    }

    [Fact]
    public async Task APaidCharge_LeavesTheTable()
    {
        // The account holds what is outstanding and not a ledger, and the store has to agree —
        // otherwise the balance would be computed over rows the aggregate no longer holds.
        var memberId = MemberId.Generate();
        await StoredAsync(AnAccountOwing(memberId));

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new MemberAccountRepository(updating).GetByMemberAsync(memberId, Token);
            loaded!.TakePayment(Money.Of(0.60m));
            await updating.SaveChangesAsync(Token);
        }

        var found = await ReadBackAsync(memberId);

        found.ShouldNotBeNull().Charges.ShouldBeEmpty();
        found.Balance.ShouldBe(Money.Zero);
    }

    [Fact]
    public async Task AnAmount_KeepsItsHundredths()
    {
        // Money is counted in hundredths and a column that rounded would lose a fifth of a euro
        // per day, quietly, where a librarian would eventually notice and nobody could explain.
        var memberId = MemberId.Generate();
        await StoredAsync(AnAccountOwing(memberId, daysLate: 7));

        (await ReadBackAsync(memberId)).ShouldNotBeNull().Balance.ShouldBe(Money.Of(1.40m));
    }

    // --- The one question Circulation asks ---------------------------------------------------------

    [Fact]
    public async Task ThePort_AnswersWhatIsOutstanding()
    {
        var memberId = MemberId.Generate();
        await StoredAsync(AnAccountOwing(memberId, daysLate: 5));

        await using var reading = sqlServer.NewContext();
        var owed = await new MemberBalance(reading).OwedByAsync(memberId.Value, Token);

        owed.ShouldBe(1.00m);
    }

    [Fact]
    public async Task ThePort_AnswersZeroForAMemberWhoHasNeverBeenCharged()
    {
        // No account and a balance of nothing are the same answer to the desk, which is why no
        // account is opened when a member enrolls.
        await using var reading = sqlServer.NewContext();

        (await new MemberBalance(reading).OwedByAsync(Guid.CreateVersion7(), Token)).ShouldBe(0m);
    }

    [Fact]
    public async Task ThePort_MaterializesNoAggregate()
    {
        // It sits on the most frequent write path in the system. Loading an account to answer it
        // would put a change-tracked graph behind every checkout and every hold.
        var memberId = MemberId.Generate();
        await StoredAsync(AnAccountOwing(memberId));

        await using var reading = sqlServer.NewContext();
        await new MemberBalance(reading).OwedByAsync(memberId.Value, Token);

        reading.ChangeTracker.Entries<MemberAccount>().ShouldBeEmpty();
    }

    // --- The copy that turns up --------------------------------------------------------------------

    [Fact]
    public async Task TheAccountHoldingAReplacementForACopy_IsFoundByTheCopy()
    {
        // The reversal arrives from Holdings naming a copy and nothing else, so this is the one
        // lookup that does not start from an identity.
        var memberId = MemberId.Generate();
        var copyId = CopyId.Generate();
        var account = MemberAccount.For(memberId);
        account.RaiseReplacementCharge(ChargeId.Generate(), LoanId.Generate(), copyId, Today, Policy);
        await StoredAsync(account);

        await using var reading = sqlServer.NewContext();
        var found = await new MemberAccountRepository(reading)
            .GetByOutstandingReplacementForAsync(copyId, Token);

        found.ShouldNotBeNull().Id.ShouldBe(memberId);
    }

    [Fact]
    public async Task ACopyWithNoReplacementCharge_FindsNoAccount()
    {
        await using var reading = sqlServer.NewContext();

        (await new MemberAccountRepository(reading)
            .GetByOutstandingReplacementForAsync(CopyId.Generate(), Token))
            .ShouldBeNull();
    }

    [Fact]
    public async Task AFineForACopy_IsNotMistakenForAReplacement()
    {
        // The filtered index and the type test have to agree: a fine is charged for time, and the
        // time was still lost when the copy turns up.
        var copyId = CopyId.Generate();
        await StoredAsync(AnAccountOwing(MemberId.Generate(), copyId: copyId));

        await using var reading = sqlServer.NewContext();

        (await new MemberAccountRepository(reading)
            .GetByOutstandingReplacementForAsync(copyId, Token))
            .ShouldBeNull();
    }
}
