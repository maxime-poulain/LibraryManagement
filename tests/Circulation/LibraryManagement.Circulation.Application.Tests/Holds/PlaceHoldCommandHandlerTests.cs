using LibraryManagement.Circulation.Application.Holds.PlaceHold;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

public sealed class PlaceHoldCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();
    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubShelf _shelf = new();
    private readonly StubRegistry _registry = new();
    private readonly StubBalances _balances = new();

    private readonly Guid _borrower = Guid.CreateVersion7();
    private readonly Guid _edition = Guid.CreateVersion7();

    public PlaceHoldCommandHandlerTests()
    {
        _registry.Entitled(_borrower);
    }

    private ValueTask<Result> Handle(Guid? borrower = null, Guid? edition = null)
        => new PlaceHoldCommandHandler(
                _loans, _queues, _shelf, _registry, _balances,
                CirculationPolicy.Current, FrozenClock.At(Today))
            .Handle(
                new PlaceHoldCommand(
                    Guid.CreateVersion7(), edition ?? _edition, borrower ?? _borrower),
                Token);

    private void AnActiveLoan(Guid borrower, Guid copy, Guid edition)
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Create(copy),
            EditionId.Create(edition),
            BorrowerId.Create(borrower),
            Today,
            CirculationPolicy.Current);

        _loans.With(loan);
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_OpensTheQueueWithTheFirstClaim()
    {
        var outcome = await Handle();

        outcome.HasErrors().ShouldBeFalse();

        var queue = _queues.Added.Single();
        queue.Id.Value.ShouldBe(_edition);
        queue.Holds.Single().BorrowerId.Value.ShouldBe(_borrower);
    }

    [Fact]
    public async Task Handle_JoinsAnExistingQueue()
    {
        var queue = HoldQueue.For(EditionId.Create(_edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        (await Handle()).HasErrors().ShouldBeFalse();

        queue.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_SomeoneNobodyEnrolled_IsItsOwnRefusal()
    {
        CodesOf(await Handle(borrower: Guid.CreateVersion7()))
            .ShouldContain(CirculationErrorCodes.NoSuchMember);
    }

    [Fact]
    public async Task Handle_ADebt_Forbids()
    {
        _balances.Owing(_borrower, 0.20m);

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.DebtForbidsIt);
    }

    [Fact]
    public async Task Handle_AtTheCap_IsRefused()
    {
        // A hold occupies one of the five places, so placing one is adding — unlike collecting.
        for (var held = 0; held < CirculationPolicy.Current.MaxLoansAndHolds; held++)
        {
            AnActiveLoan(_borrower, Guid.CreateVersion7(), Guid.CreateVersion7());
        }

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.AtTheCap);
    }

    [Fact]
    public async Task Handle_AnEditionTheBorrowerAlreadyHasOut_IsRefused()
    {
        AnActiveLoan(_borrower, Guid.CreateVersion7(), _edition);

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.AlreadyBorrowed);
    }

    [Fact]
    public async Task Handle_WhileACopySitsOnTheShelf_IsRefused()
    {
        // That is a checkout, and allowing the hold would make the queue meaningless.
        _shelf.Lendable(Guid.CreateVersion7(), _edition);

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.CopyOnTheShelf);
    }

    [Fact]
    public async Task Handle_WhenEveryLendableCopyIsOut_TheHoldStands()
    {
        var copy = Guid.CreateVersion7();
        _shelf.Lendable(copy, _edition);
        AnActiveLoan(Guid.CreateVersion7(), copy, _edition);

        (await Handle()).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_WhenEveryLendableCopyIsSetAside_TheHoldStands()
    {
        var copy = Guid.CreateVersion7();
        _shelf.Lendable(copy, _edition);

        var queue = HoldQueue.For(EditionId.Create(_edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        queue.TrapOldestQueued(
            CopyId.Create(copy), Today.AddDays(7), new HashSet<BorrowerId>());
        _queues.With(queue);

        (await Handle()).HasErrors().ShouldBeFalse();
        queue.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Twice_IsRefusedByTheQueue()
    {
        (await Handle()).HasErrors().ShouldBeFalse();

        CodesOf(await Handle()).ShouldContain(CirculationErrorCodes.HoldAlreadyPlaced);
    }
}
