using LibraryManagement.Circulation.Application.Holds.ReleaseTrappedCopy;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The reaction to a copy leaving service: any promise it carried is taken back, and the claim
/// returns to the head of its queue.
/// </summary>
public sealed class ReleaseTrappedCopyCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();

    private ValueTask<Result> Handle(Guid copyId)
        => new ReleaseTrappedCopyCommandHandler(_queues)
            .Handle(new ReleaseTrappedCopyCommand(copyId), Token);

    [Fact]
    public async Task Handle_ReleasesTheClaimTheCopyWasSetAsideFor()
    {
        var promised = BorrowerId.Generate();
        var copyId = CopyId.Generate();

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), promised, ThisMorning);
        queue.TrapOldestQueued(copyId, Today.AddDays(7), new HashSet<BorrowerId>());
        queue.ClearDomainEvents();
        _queues.With(queue);

        var outcome = await Handle(copyId.Value);

        outcome.HasErrors().ShouldBeFalse();
        queue.Holds.Single().Status.ShouldBe(HoldStatus.Queued);
        queue.DomainEvents.OfType<HoldPickupWithdrawn>().Single().BorrowerId.ShouldBe(promised);
    }

    [Fact]
    public async Task Handle_ACopyPromisedToNobody_AnswersSuccess()
    {
        // The ordinary case by far, and the redelivery case too: it arrives from another
        // module's drain, and a refusal would replay a correctly handled message forever.
        (await Handle(Guid.CreateVersion7())).HasErrors().ShouldBeFalse();
    }
}
