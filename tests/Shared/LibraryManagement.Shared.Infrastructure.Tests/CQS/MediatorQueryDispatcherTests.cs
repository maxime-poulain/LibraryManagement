using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

namespace LibraryManagement.Shared.Infrastructure.Tests.CQS;

public sealed class MediatorQueryDispatcherTests
{
    private static readonly ErrorCode NotFound = new("Test.NotFound");

    [Fact]
    public async Task DispatchAsync_ReturnsWhatTheHandlerProduced()
    {
        var dispatcher = new MediatorQueryDispatcher(
            RecordingSender.Returning(Result<string>.Success("the answer")));

        var result = await dispatcher.DispatchAsync(new TestQuery("anything"), TestContext.Current.CancellationToken);

        result.Match(value => value, _ => "unreachable").ShouldBe("the answer");
    }

    [Fact]
    public async Task DispatchAsync_ReturnsTheFailureTheHandlerProduced()
    {
        // A query that finds nothing is an expected answer, not an exception. The dispatcher
        // carries the failure through untouched rather than translating it.
        var dispatcher = new MediatorQueryDispatcher(
            RecordingSender.Returning(Result<string>.Failure(NotFound, "No record matches that term.")));

        var result = await dispatcher.DispatchAsync(new TestQuery("anything"), TestContext.Current.CancellationToken);

        result.HasErrors().ShouldBeTrue();
        result.Match(_ => [], errors => errors.Select(error => error.ErrorCode).ToArray())
            .ShouldBe([NotFound]);
    }

    [Fact]
    public async Task DispatchAsync_ForwardsTheQueryAndTheCancellationTokenToTheSender()
    {
        var sender = RecordingSender.Returning(Result<string>.Success("the answer"));
        var dispatcher = new MediatorQueryDispatcher(sender);
        var query = new TestQuery("anything");
        using var cts = new CancellationTokenSource();

        await dispatcher.DispatchAsync(query, cts.Token);

        sender.SendCount.ShouldBe(1);
        sender.LastMessage.ShouldBeSameAs(query);
        sender.LastCancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task DispatchAsync_LetsAnExceptionThrough()
    {
        var boom = new InvalidOperationException("handler blew up");
        var dispatcher = new MediatorQueryDispatcher(RecordingSender.Throwing(boom));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await dispatcher.DispatchAsync(new TestQuery("anything"), TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(boom);
    }

    [Fact]
    public async Task DispatchAsync_WithANullQuery_Throws()
    {
        var dispatcher = new MediatorQueryDispatcher(
            RecordingSender.Returning(Result<string>.Success("the answer")));

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await dispatcher.DispatchAsync<string>(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheQueryDispatcher_TakesNoTransactionManager()
    {
        // A query changes nothing, so it has nothing to commit. The asymmetry with the command
        // dispatcher is command-query separation expressed in the dependencies themselves.
        var parameters = typeof(MediatorQueryDispatcher)
            .GetConstructors()
            .Single()
            .GetParameters();

        parameters.ShouldNotContain(p => p.ParameterType == typeof(ITransactionManager));
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
