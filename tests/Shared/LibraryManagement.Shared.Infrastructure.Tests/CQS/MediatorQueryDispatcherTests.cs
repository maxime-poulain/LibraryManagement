using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

namespace LibraryManagement.Shared.Infrastructure.Tests.CQS;

public sealed class MediatorQueryDispatcherTests
{
    private static readonly ErrorCode NotFound = new("Test.NotFound");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DispatchAsync_ReturnsWhatTheHandlerProduced()
    {
        var result = await new MediatorQueryDispatcher(
                RecordingSender.Returning(Result<string>.Success("the answer")))
            .DispatchAsync(new TestQuery("anything"), Token);

        result.Match(value => value, _ => "unreachable").ShouldBe("the answer");
    }

    [Fact]
    public async Task DispatchAsync_ReturnsTheFailureTheHandlerProduced()
    {
        // A query that finds nothing is an expected answer, not an exception. The dispatcher carries
        // the failure through untouched rather than translating it.
        var result = await new MediatorQueryDispatcher(
                RecordingSender.Returning(Result<string>.Failure(NotFound, "No record matches that term.")))
            .DispatchAsync(new TestQuery("anything"), Token);

        result.HasErrors().ShouldBeTrue();
        result.Match(_ => [], errors => errors.Select(error => error.ErrorCode).ToArray())
            .ShouldBe([NotFound]);
    }

    [Fact]
    public async Task DispatchAsync_ForwardsTheQueryAndTheCancellationTokenToTheSender()
    {
        var sender = RecordingSender.Returning(Result<string>.Success("the answer"));
        var query = new TestQuery("anything");
        using var cts = new CancellationTokenSource();

        await new MediatorQueryDispatcher(sender).DispatchAsync(query, cts.Token);

        sender.SendCount.ShouldBe(1);
        sender.LastMessage.ShouldBeSameAs(query);
        sender.LastCancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task DispatchAsync_LetsAnExceptionThrough()
    {
        var boom = new InvalidOperationException("handler blew up");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await new MediatorQueryDispatcher(RecordingSender.Throwing(boom))
                .DispatchAsync(new TestQuery("anything"), Token));

        thrown.ShouldBeSameAs(boom);
    }

    [Fact]
    public async Task DispatchAsync_WithANullQuery_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new MediatorQueryDispatcher(
                    RecordingSender.Returning(Result<string>.Success("the answer")))
                .DispatchAsync<string>(null!, Token));
    }

    [Fact]
    public void TheQueryDispatcher_NeverSeesAUnitOfWork()
    {
        // A query changes nothing, so it has nothing to write. The asymmetry that used to be
        // expressed in this type's dependencies now lives in a constraint —
        // UnitOfWorkBehavior accepts only commands — but the property is the same and still worth
        // pinning here.
        typeof(MediatorQueryDispatcher)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ShouldBe([typeof(Mediator.ISender)]);
    }

    [Fact]
    public void BothDispatchers_AreTheSameShape()
    {
        // Once the cross-cutting concerns became behaviors, nothing distinguishes the two but the
        // contract they implement. If one of them grows a dependency again, a concern has leaked
        // back out of the pipeline.
        static Type[] DependenciesOf(Type dispatcher)
            => [.. dispatcher.GetConstructors().Single().GetParameters().Select(p => p.ParameterType)];

        DependenciesOf(typeof(MediatorQueryDispatcher))
            .ShouldBe(DependenciesOf(typeof(MediatorCommandDispatcher)));
    }

    [Fact]
    public void AQuery_AnswersWithAResultOfItsValue()
    {
        // IQuery<TValue> names the value a successful answer carries, and closes IRequest<T> over
        // Result<TValue>. Declaring IQuery<BookDto> and handling it by returning a bare BookDto is
        // therefore a compile error, which is what keeps a failed read from travelling as a null.
        typeof(TestQuery).GetInterfaces()
            .ShouldContain(typeof(Mediator.IRequest<Result<string>>));
    }
}
