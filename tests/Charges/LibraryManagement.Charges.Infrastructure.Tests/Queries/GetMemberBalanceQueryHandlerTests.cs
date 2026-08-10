using LibraryManagement.Charges.Application.Accounts.GetMemberBalance;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Charges.Infrastructure.Queries;

namespace LibraryManagement.Charges.Infrastructure.Tests.Queries;

/// <summary>
/// The money half of the desk's member file, asked of the real store.
/// </summary>
/// <remarks>
/// The same netting the port answers through, reached by the other of its two callers — so this
/// also holds the extraction honest: were the two to drift apart, one of these figures would move
/// and the other would not.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class GetMemberBalanceQueryHandlerTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChargesPolicy Policy => ChargesPolicy.Current;

    private static MemberAccount AnAccountOwing(MemberId memberId, int daysLate = 3)
    {
        var account = MemberAccount.For(memberId);
        account.AssessOverdueFine(
            ChargeId.Generate(),
            LoanId.Generate(),
            CopyId.Generate(),
            daysLate,
            Today,
            Policy);

        return account;
    }

    private async Task StoreAsync(MemberAccount account)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(account);
        await writing.SaveChangesAsync(Token);
    }

    private async Task<MemberBalanceDto> ReadAsync(Guid memberId)
    {
        await using var reading = sqlServer.NewContext();

        var result = await new GetMemberBalanceQueryHandler(reading)
            .Handle(new GetMemberBalanceQuery(memberId), Token);

        return result.Match(
            balance => balance,
            errors => throw new InvalidOperationException(errors[0].ErrorMessage));
    }

    [Fact]
    public async Task AnAccountOnFile_AnswersWhatIsOutstanding()
    {
        var memberId = MemberId.Generate();
        var account = AnAccountOwing(memberId);
        await StoreAsync(account);

        var balance = await ReadAsync(memberId.Value);

        balance.MemberId.ShouldBe(memberId.Value);
        balance.Balance.ShouldBe(account.Balance.Amount);
    }

    [Fact]
    public async Task AMemberWithNoAccount_OwesNothing_AndThatIsAnAnswer()
    {
        // No 404 is reachable here, by design. An account opens when the first charge is raised,
        // so most of a membership has no row — and failing would make the ordinary case an error.
        (await ReadAsync(Guid.CreateVersion7())).Balance.ShouldBe(0m);
    }

    [Fact]
    public async Task APaymentAgainstACharge_IsNettedOut()
    {
        var memberId = MemberId.Generate();
        var account = AnAccountOwing(memberId);
        var owed = account.Balance;
        await StoreAsync(account);

        await using (var updating = sqlServer.NewContext())
        {
            // Through the repository, which is the aggregate's only load path — and the reason is
            // worth the line. Charges is a referenced collection, so a bare Find leaves it empty,
            // and Balance is computed from it: the account comes back looking perfectly valid and
            // owing nothing, and the payment is then refused for exceeding a balance that is only
            // missing. Nothing throws. GetByMemberAsync includes the charges.
            var loaded = await new MemberAccountRepository(updating).GetByMemberAsync(memberId, Token);
            loaded.ShouldNotBeNull().TakePayment(owed).HasErrors().ShouldBeFalse();
            await updating.SaveChangesAsync(Token);
        }

        (await ReadAsync(memberId.Value)).Balance.ShouldBe(0m);
    }
}
