using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

namespace LibraryManagement.Shared.Infrastructure.Tests.Behaviors;

public sealed class UnitOfWorkBehaviorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly RecordingUnitOfWork _unitOfWork = new();

    private static UnitOfWorkBehavior<TestCommand, Result> BehaviorOver(RecordingUnitOfWork unitOfWork)
        => new(new FixedUnitOfWorkResolver(unitOfWork));

    private static ValueTask<Result> Returning(Result result)
        => ValueTask.FromResult(result);

    private static Result AFailure()
        => Result.Failure(new ErrorCode("Loan.Overdue"), "the loan is overdue");

    // --- Success writes -----------------------------------------------------------------------------

    [Fact]
    public async Task WhenTheCommandSucceeds_TheWorkIsWritten()
    {
        await BehaviorOver(_unitOfWork)
            .Handle(new TestCommand(), (_, _) => Returning(Result.Success()), Token);

        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task TheResultOfTheHandler_IsReturnedUntouched()
    {
        var expected = Result.Success();

        var actual = await BehaviorOver(_unitOfWork)
            .Handle(new TestCommand(), (_, _) => Returning(expected), Token);

        actual.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task TheUnitOfWork_IsTheOneBelongingToTheCommandsModule()
    {
        // Every module owns its own store, and one pipeline serves all of them. It asks which unit
        // of work belongs to the command it was handed rather than taking one by type, which
        // several modules would each claim to satisfy.
        var resolver = new FixedUnitOfWorkResolver(_unitOfWork);
        var command = new TestCommand();

        await new UnitOfWorkBehavior<TestCommand, Result>(resolver)
            .Handle(command, (_, _) => Returning(Result.Success()), Token);

        resolver.LastCommand.ShouldBeSameAs(command);
    }

    // --- Failure does not ---------------------------------------------------------------------------

    [Fact]
    public async Task WhenTheCommandFails_NothingIsWritten()
    {
        // This is the guarantee the explicit transaction used to provide, and it now rests on
        // nothing being written before this point: repositories only track.
        await BehaviorOver(_unitOfWork)
            .Handle(new TestCommand(), (_, _) => Returning(AFailure()), Token);

        _unitOfWork.Saved.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenTheCommandFails_TheFailureIsStillReturned()
    {
        var failure = AFailure();

        var actual = await BehaviorOver(_unitOfWork)
            .Handle(new TestCommand(), (_, _) => Returning(failure), Token);

        actual.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task WhenTheHandlerThrows_NothingIsWrittenAndTheExceptionEscapes()
    {
        var boom = new InvalidOperationException("handler blew up");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await BehaviorOver(_unitOfWork)
                .Handle(new TestCommand(), (_, _) => throw boom, Token));

        thrown.ShouldBeSameAs(boom);
        _unitOfWork.Saved.ShouldBeFalse();
    }

    // --- A concurrency conflict leaves through the Result channel ------------------------------------

    [Fact]
    public async Task OnAConcurrencyConflict_AFailureIsReturnedInsteadOfThrowing()
    {
        var behavior = new UnitOfWorkBehavior<TestCommand, Result>(
            new FixedUnitOfWorkResolver(new ConflictingUnitOfWork()));

        var result = await behavior.Handle(new TestCommand(), (_, _) => Returning(Result.Success()), Token);

        result.Match(() => "success", _ => "failure").ShouldBe("failure");
    }

    [Fact]
    public async Task OnAConcurrencyConflict_TheSharedErrorCodeIsCarried()
    {
        var behavior = new UnitOfWorkBehavior<TestCommand, Result>(
            new FixedUnitOfWorkResolver(new ConflictingUnitOfWork()));

        var result = await behavior.Handle(new TestCommand(), (_, _) => Returning(Result.Success()), Token);

        result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(SharedErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task OnAConcurrencyConflict_ExactlyOneErrorIsReported()
    {
        var behavior = new UnitOfWorkBehavior<TestCommand, Result>(
            new FixedUnitOfWorkResolver(new ConflictingUnitOfWork()));

        var result = await behavior.Handle(new TestCommand(), (_, _) => Returning(Result.Success()), Token);

        result.Match(() => 0, errors => errors.Count).ShouldBe(1);
    }

    // --- Queries never come here --------------------------------------------------------------------

    [Fact]
    public void TheBehavior_AcceptsOnlyCommands()
    {
        // A query changes nothing and has nothing to write. The constraint is what keeps it out,
        // and the container honoring it is pinned by a test in the composition project.
        var messageParameter = typeof(UnitOfWorkBehavior<,>).GetGenericArguments()[0];

        messageParameter.GetGenericParameterConstraints().ShouldContain(typeof(ICommandBase));
    }

    [Fact]
    public void TheBehavior_NeverSeesATransaction()
    {
        // There is none. A single save is already atomic, and nothing is written before it, so an
        // explicit transaction would guard a rollback that cannot happen — while holding locks for
        // the whole duration of the handler.
        typeof(UnitOfWorkBehavior<,>)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ShouldBe([typeof(Application.IUnitOfWorkResolver)]);
    }
}
