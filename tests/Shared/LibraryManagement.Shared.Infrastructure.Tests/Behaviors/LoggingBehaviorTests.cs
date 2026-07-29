using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;
using Mediator;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Shared.Infrastructure.Tests.Behaviors;

public sealed class LoggingBehaviorTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly RecordingLogger _log = new();

    public void Dispose() => _log.Dispose();

    private ValueTask<Result> HandleCommand(Result response)
        => new LoggingBehavior<TestCommand, Result>(_log)
            .Handle(new TestCommand(), (_, _) => ValueTask.FromResult(response), Token);

    [Fact]
    public async Task ACommandThatSucceeds_LeavesOneInformationLine()
    {
        await HandleCommand(Result.Success());

        var line = _log.Lines.ShouldHaveSingleItem();
        line.Level.ShouldBe(LogLevel.Information);
        line.Message.ShouldContain(nameof(TestCommand));
    }

    [Fact]
    public async Task AQueryThatSucceeds_LeavesADebugLine()
    {
        // A command is a business fact; a query is traffic. The level is the difference, so a host
        // keeps the facts and silences the traffic without touching code.
        await new LoggingBehavior<TestQuery, Result<string>>(_log)
            .Handle(new TestQuery("anything"), (_, _) => ValueTask.FromResult(Result<string>.Success("x")), Token);

        _log.Lines.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
    }

    [Fact]
    public async Task TheCategory_IsTheMessagesOwnTypeName()
    {
        // Which is why the behavior takes the factory rather than an ILogger of itself: a host can
        // raise or silence one command's lines in configuration, like any namespace.
        await HandleCommand(Result.Success());

        _log.Category.ShouldBe(typeof(TestCommand).FullName);
    }

    [Fact]
    public async Task ARefusal_LeavesAWarningNamingTheCodes()
    {
        await HandleCommand(Result.Failure(new ErrorCode("Test.Refused"), "'Dupont, Jeanne' is taken."));

        var line = _log.Lines.ShouldHaveSingleItem();
        line.Level.ShouldBe(LogLevel.Warning);
        line.Message.ShouldContain("Test.Refused");
    }

    [Fact]
    public async Task TheErrorMessage_NeverReachesTheLine()
    {
        // The code answers the operational question; the message interpolates what an employee
        // typed, and what they typed is a person's data. What a member reads or asks for is
        // confidential by professional ethics before it is personal data by law.
        await HandleCommand(Result.Failure(new ErrorCode("Test.Refused"), "'Dupont, Jeanne' is taken."));

        _log.Lines.ShouldAllBe(line => !line.Message.Contains("Dupont", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnException_IsLoggedAsErrorAndRethrown()
    {
        // Expected failures travel as results, so whatever throws here is a defect — and this is
        // the one place that still knows which message it was.
        var boom = new InvalidOperationException("boom");
        MessageHandlerDelegate<TestCommand, Result> next = (_, _) => throw boom;

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await new LoggingBehavior<TestCommand, Result>(_log)
                .Handle(new TestCommand(), next, Token));

        var line = _log.Lines.ShouldHaveSingleItem();
        line.Level.ShouldBe(LogLevel.Error);
        line.Exception.ShouldBeSameAs(boom);
    }

    [Fact]
    public async Task Cancellation_PassesThroughSilently()
    {
        // The caller's cancellation is not news, and a line for it would dress an ordinary
        // shutdown as a failure.
        MessageHandlerDelegate<TestCommand, Result> next = (_, _) => throw new OperationCanceledException();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await new LoggingBehavior<TestCommand, Result>(_log)
                .Handle(new TestCommand(), next, Token));

        _log.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheResponse_ComesBackUntouched()
    {
        var response = Result.Success();

        var result = await HandleCommand(response);

        result.ShouldBeSameAs(response);
    }
}
