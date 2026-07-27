using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

namespace LibraryManagement.Shared.Infrastructure.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Error AFieldError(string target = "Isbn", string message = "must not be empty")
        => new(SharedErrorCodes.ValidationFailed, message, target);

    // Records whether the handler was reached at all, which is half of what these tests assert.
    private sealed class RecordingNext<TMessage, TResponse>(TResponse response)
    {
        public int Calls { get; private set; }

        public TMessage? LastMessage { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<TResponse> Handle(TMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            LastMessage = message;
            LastCancellationToken = cancellationToken;

            return ValueTask.FromResult(response);
        }
    }

    // --- A well-formed message goes through ---------------------------------------------------------

    [Fact]
    public async Task AValidCommand_ReachesItsHandler()
    {
        var next = new RecordingNext<TestCommand, Result>(Result.Success());
        var behavior = new ValidationBehavior<TestCommand, Result>(
            RecordingMessageValidator.Accepting());

        var result = await behavior.Handle(new TestCommand(), next.Handle, Token);

        next.Calls.ShouldBe(1);
        result.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task AValidQuery_ReachesItsHandler()
    {
        // The same behavior, over the other result type. One implementation covers both because
        // IFailable lends the factory without lending a hierarchy.
        var next = new RecordingNext<TestQuery, Result<string>>(Result<string>.Success("the answer"));
        var behavior = new ValidationBehavior<TestQuery, Result<string>>(
            RecordingMessageValidator.Accepting());

        var result = await behavior.Handle(new TestQuery("anything"), next.Handle, Token);

        next.Calls.ShouldBe(1);
        result.Match(value => value, _ => "unreachable").ShouldBe("the answer");
    }

    [Fact]
    public async Task TheMessageItself_IsWhatGetsValidated()
    {
        var validator = RecordingMessageValidator.Accepting();
        var command = new TestCommand();

        await new ValidationBehavior<TestCommand, Result>(validator)
            .Handle(command, new RecordingNext<TestCommand, Result>(Result.Success()).Handle, Token);

        validator.CallCount.ShouldBe(1);
        validator.LastMessage.ShouldBeSameAs(command);
    }

    // --- A malformed message does not ---------------------------------------------------------------

    [Fact]
    public async Task AnInvalidCommand_NeverReachesItsHandler()
    {
        var next = new RecordingNext<TestCommand, Result>(Result.Success());

        await new ValidationBehavior<TestCommand, Result>(
                RecordingMessageValidator.Rejecting(AFieldError()))
            .Handle(new TestCommand(), next.Handle, Token);

        next.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AnInvalidQuery_NeverReachesItsHandler()
    {
        var next = new RecordingNext<TestQuery, Result<string>>(Result<string>.Success("x"));

        await new ValidationBehavior<TestQuery, Result<string>>(
                RecordingMessageValidator.Rejecting(AFieldError("Term")))
            .Handle(new TestQuery(""), next.Handle, Token);

        next.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task AnInvalidCommand_ComesBackAsAFailure()
    {
        var result = await new ValidationBehavior<TestCommand, Result>(
                RecordingMessageValidator.Rejecting(AFieldError()))
            .Handle(new TestCommand(), new RecordingNext<TestCommand, Result>(Result.Success()).Handle, Token);

        result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode)
            .ShouldBe(SharedErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task AnInvalidQuery_ComesBackAsAFailureOfItsOwnResultType()
    {
        // The behavior built a Result<string> failure without naming Result<string> — the whole
        // reason IFailable exists.
        var result = await new ValidationBehavior<TestQuery, Result<string>>(
                RecordingMessageValidator.Rejecting(AFieldError("Term")))
            .Handle(new TestQuery(""), new RecordingNext<TestQuery, Result<string>>(Result<string>.Success("x")).Handle, Token);

        result.Match<ErrorCode?>(_ => null, errors => errors[0].ErrorCode)
            .ShouldBe(SharedErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task EveryFieldAtFault_IsReportedAtOnce()
    {
        // An employee who got two fields wrong should be told both times, not once per attempt.
        var result = await new ValidationBehavior<TestCommand, Result>(
                RecordingMessageValidator.Rejecting(AFieldError("Isbn"), AFieldError("Title")))
            .Handle(new TestCommand(), new RecordingNext<TestCommand, Result>(Result.Success()).Handle, Token);

        result.Match(() => Array.Empty<string?>(), errors => errors.Select(e => e.Target).ToArray())
            .ShouldBe(["Isbn", "Title"]);
    }

    [Fact]
    public async Task TheCancellationToken_ReachesTheValidator()
    {
        var validator = RecordingMessageValidator.Accepting();
        using var cts = new CancellationTokenSource();

        await new ValidationBehavior<TestCommand, Result>(validator)
            .Handle(new TestCommand(), new RecordingNext<TestCommand, Result>(Result.Success()).Handle, cts.Token);

        validator.LastCancellationToken.ShouldBe(cts.Token);
    }
}
