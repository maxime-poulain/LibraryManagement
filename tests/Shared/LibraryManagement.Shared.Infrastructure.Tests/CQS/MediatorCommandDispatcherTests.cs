using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

namespace LibraryManagement.Shared.Infrastructure.Tests.CQS;

/// <summary>
/// What is left of the dispatcher once validation and the unit of work are behaviors: a delegation,
/// and the application layer's own contract so a caller never names Mediator.
/// </summary>
/// <remarks>
/// The assertions that used to live here did not disappear — they moved to
/// <c>ValidationBehaviorTests</c> and <c>UnitOfWorkBehaviorTests</c>, which is where the behaviour
/// they describe now lives.
/// </remarks>
public sealed class MediatorCommandDispatcherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DispatchAsync_ReturnsTheResultProducedByTheHandler()
    {
        var expected = Result.Success();

        var actual = await new MediatorCommandDispatcher(RecordingSender.Returning(expected))
            .DispatchAsync(new TestCommand(), Token);

        actual.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task DispatchAsync_ForwardsTheCommandAndTheCancellationTokenToTheSender()
    {
        var sender = RecordingSender.Returning(Result.Success());
        var command = new TestCommand();
        using var cts = new CancellationTokenSource();

        await new MediatorCommandDispatcher(sender).DispatchAsync(command, cts.Token);

        sender.SendCount.ShouldBe(1);
        sender.LastMessage.ShouldBeSameAs(command);
        sender.LastCancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task DispatchAsync_LetsAnExceptionThrough()
    {
        var boom = new InvalidOperationException("handler blew up");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await new MediatorCommandDispatcher(RecordingSender.Throwing(boom))
                .DispatchAsync(new TestCommand(), Token));

        thrown.ShouldBeSameAs(boom);
    }

    [Fact]
    public async Task DispatchAsync_WithANullCommand_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new MediatorCommandDispatcher(RecordingSender.Returning(Result.Success()))
                .DispatchAsync(null!, Token));
    }

    [Fact]
    public void TheDispatcher_CarriesNoCrossCuttingConcernOfItsOwn()
    {
        // Validating and writing are behaviors now. A dispatcher that still took a validator or a
        // unit of work would mean one of them had crept back, and the pipeline would run it twice
        // or in the wrong place.
        var dependencies = typeof(MediatorCommandDispatcher)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        dependencies.ShouldNotContain(typeof(IMessageValidator));
        dependencies.ShouldNotContain(typeof(IUnitOfWork));
        dependencies.ShouldNotContain(typeof(IUnitOfWorkResolver));
    }

    [Fact]
    public void DispatchAsync_IsNotGenericOverTheResultType()
    {
        // Naming Result in the signature is what keeps commands from returning values: a generic
        // TResult would accept Result<T> and reopen the door the constraint closes.
        var method = typeof(ICommandDispatcher).GetMethod(nameof(ICommandDispatcher.DispatchAsync));

        method.ShouldNotBeNull();
        method.IsGenericMethod.ShouldBeFalse();
        method.ReturnType.ShouldBe(typeof(ValueTask<Result>));
    }
}
