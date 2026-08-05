using LibraryManagement.Charges.Application.Accounts.AssessOverdueFine;
using LibraryManagement.Charges.Application.Accounts.RaiseReplacementCharge;
using LibraryManagement.Charges.Application.Accounts.TakePayment;
using LibraryManagement.Charges.Application.Accounts.WaiveCharge;
using LibraryManagement.Charges.Application.Tests.TestDoubles;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Tests.Accounts;

/// <summary>
/// The four moments, and the two questions only a handler can answer: where the account comes from,
/// and what happens when there is not one.
/// </summary>
public sealed class AccountCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChargesPolicy Policy => ChargesPolicy.Current;

    private readonly InMemoryAccounts _accounts = new();

    private ValueTask<Result> Fine(Guid memberId, int daysLate)
        => new AssessOverdueFineCommandHandler(_accounts, Policy, FrozenClock.At(Today))
            .Handle(
                new AssessOverdueFineCommand(memberId, Guid.CreateVersion7(), Guid.CreateVersion7(), daysLate),
                Token);

    private ValueTask<Result> Replacement(Guid memberId)
        => new RaiseReplacementChargeCommandHandler(_accounts, Policy, FrozenClock.At(Today))
            .Handle(
                new RaiseReplacementChargeCommand(memberId, Guid.CreateVersion7(), Guid.CreateVersion7()),
                Token);

    private ValueTask<Result> Payment(Guid memberId, decimal amount)
        => new TakePaymentCommandHandler(_accounts).Handle(new TakePaymentCommand(memberId, amount), Token);

    private ValueTask<Result> Waive(Guid memberId, Guid chargeId)
        => new WaiveChargeCommandHandler(_accounts).Handle(new WaiveChargeCommand(memberId, chargeId), Token);

    private static ErrorCode? RefusalOf(Result result)
        => result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode);

    // --- Where the account comes from --------------------------------------------------------------

    [Fact]
    public async Task TheFirstCharge_OpensTheAccount()
    {
        // Nobody performs this moment: an account per enrolled member would be a row forever
        // answering a question nobody asks, so the first charge is what brings one into being.
        (await Fine(Guid.CreateVersion7(), daysLate: 3)).HasErrors().ShouldBeFalse();

        _accounts.Opened.ShouldBeTrue();
    }

    [Fact]
    public async Task ASecondCharge_ReusesTheAccount()
    {
        var memberId = Guid.CreateVersion7();
        var account = MemberAccount.For(MemberId.Create(memberId));
        _accounts.With(account);

        await Fine(memberId, daysLate: 3);
        await Replacement(memberId);

        _accounts.Opened.ShouldBeFalse();
        account.Charges.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AReplacementCharge_OpensTheAccountToo()
    {
        (await Replacement(Guid.CreateVersion7())).HasErrors().ShouldBeFalse();

        _accounts.Opened.ShouldBeTrue();
    }

    // --- What happens when there is not one --------------------------------------------------------

    [Fact]
    public async Task APayment_ForAMemberWhoOwesNothing_IsRefused()
    {
        // The asymmetry with the two charges is deliberate. A charge arriving for a member with no
        // account is ordinary; a payment arriving for one is a mistake at the desk, and answering
        // success would leave a librarian believing money was recorded.
        RefusalOf(await Payment(Guid.CreateVersion7(), 5m))
            .ShouldBe(ChargesErrorCodes.AccountNotFound);
    }

    [Fact]
    public async Task AWaiver_ForAMemberWhoOwesNothing_IsRefused()
    {
        RefusalOf(await Waive(Guid.CreateVersion7(), Guid.CreateVersion7()))
            .ShouldBe(ChargesErrorCodes.AccountNotFound);
    }

    // --- The moments themselves --------------------------------------------------------------------

    [Fact]
    public async Task AReturnOnTime_SucceedsAndChargesNothing()
    {
        // It reaches here because Circulation announces every return: deciding there is nothing to
        // charge belongs to this context, and refusing would make the announcing drain replay a
        // message that was handled correctly.
        var memberId = Guid.CreateVersion7();

        (await Fine(memberId, daysLate: 0)).HasErrors().ShouldBeFalse();

        var account = await _accounts.GetByMemberAsync(MemberId.Create(memberId), Token);
        account.ShouldNotBeNull().Charges.ShouldBeEmpty();
    }

    [Fact]
    public async Task APayment_ClearsWhatIsOwed()
    {
        var memberId = Guid.CreateVersion7();
        await Fine(memberId, daysLate: 3);

        (await Payment(memberId, 0.60m)).HasErrors().ShouldBeFalse();

        var account = await _accounts.GetByMemberAsync(MemberId.Create(memberId), Token);
        account.ShouldNotBeNull().Balance.ShouldBe(Money.Zero);
    }

    [Fact]
    public async Task APaymentBeyondTheBalance_IsRefusedByTheAccount()
    {
        var memberId = Guid.CreateVersion7();
        await Fine(memberId, daysLate: 3);

        RefusalOf(await Payment(memberId, 50m)).ShouldBe(ChargesErrorCodes.PaymentExceedsBalance);
    }

    [Fact]
    public async Task AWaiver_CancelsTheChargeItNames()
    {
        var memberId = Guid.CreateVersion7();
        await Fine(memberId, daysLate: 3);

        var account = await _accounts.GetByMemberAsync(MemberId.Create(memberId), Token);
        var chargeId = account.ShouldNotBeNull().Charges.Single().Id;

        (await Waive(memberId, chargeId.Value)).HasErrors().ShouldBeFalse();

        account.Charges.ShouldBeEmpty();
    }

    [Fact]
    public async Task AWaiver_ForAChargeTheAccountDoesNotHold_IsRefused()
    {
        var memberId = Guid.CreateVersion7();
        await Fine(memberId, daysLate: 3);

        RefusalOf(await Waive(memberId, Guid.CreateVersion7()))
            .ShouldBe(ChargesErrorCodes.ChargeNotFound);
    }
}
