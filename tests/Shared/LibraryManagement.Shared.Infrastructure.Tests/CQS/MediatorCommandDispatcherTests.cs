using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Shared.Infrastructure.Tests.CQS;

public sealed class MediatorCommandDispatcherTests
{
    private readonly RecordingTransactionManager _transactions = new();
    private readonly RecordingCommandValidator _validator = RecordingCommandValidator.Accepting();
    private readonly FixedTransactionManagerResolver _transactionManagers;

    public MediatorCommandDispatcherTests()
        => _transactionManagers = new FixedTransactionManagerResolver(_transactions);

    private MediatorCommandDispatcher DispatcherReturning(Result result)
        => new(RecordingSender.Returning(result), _transactionManagers, _validator);

    private MediatorCommandDispatcher DispatcherThrowing(Exception exception)
        => new(RecordingSender.Throwing(exception), _transactionManagers, _validator);

    private MediatorCommandDispatcher DispatcherRejecting(params Error[] errors)
        => new(
            RecordingSender.Returning(Result.Success()),
            _transactionManagers,
            RecordingCommandValidator.Rejecting(errors));

    private static Result AFailure()
        => Result.Failure(new ErrorCode("Loan.Overdue"), "the loan is overdue");

    private static Error AFieldError(string target = "Isbn", string message = "must not be empty")
        => new(SharedErrorCodes.ValidationFailed, message, target);

    // --- The happy path -------------------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_ReturnsTheResultProducedByTheHandler()
    {
        var expected = Result.Success();

        var actual = await DispatcherReturning(expected)
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        actual.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task DispatchAsync_OpensExactlyOneTransaction()
    {
        await DispatcherReturning(Result.Success())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.BeginCount.ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAsync_OpensTheTransactionOfTheCommandsOwnModule()
    {
        // Every module owns its own store, and the dispatcher is shared by all of them. It asks
        // which transaction manager belongs to the command it was handed rather than taking one by
        // type, which several modules would each claim to satisfy.
        var command = new TestCommand();

        await DispatcherReturning(Result.Success())
            .DispatchAsync(command, TestContext.Current.CancellationToken);

        _transactionManagers.LastCommand.ShouldBeSameAs(command);
    }

    [Fact]
    public async Task DispatchAsync_WhenTheCommandSucceeds_CommitsTheTransaction()
    {
        await DispatcherReturning(Result.Success())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.Transaction!.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task DispatchAsync_CommitsBeforeDisposing()
    {
        await DispatcherReturning(Result.Success())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.Transaction!.Calls.ShouldBe(["commit", "dispose"]);
    }

    [Fact]
    public async Task DispatchAsync_ForwardsTheCommandAndTheCancellationTokenToTheSender()
    {
        var sender = RecordingSender.Returning(Result.Success());
        var dispatcher = new MediatorCommandDispatcher(sender, _transactionManagers, _validator);
        var command = new TestCommand();
        using var cts = new CancellationTokenSource();

        await dispatcher.DispatchAsync(command, cts.Token);

        sender.SendCount.ShouldBe(1);
        sender.LastMessage.ShouldBeSameAs(command);
        sender.LastCancellationToken.ShouldBe(cts.Token);
    }

    // --- Validation runs first, and before any transaction ---------------------------------------

    [Fact]
    public async Task DispatchAsync_ConsultsTheValidator()
    {
        var command = new TestCommand();

        await DispatcherReturning(Result.Success())
            .DispatchAsync(command, TestContext.Current.CancellationToken);

        _validator.CallCount.ShouldBe(1);
        _validator.LastCommand.ShouldBeSameAs(command);
    }

    [Fact]
    public async Task DispatchAsync_WhenValidationFails_ReturnsAFailure()
    {
        var result = await DispatcherRejecting(AFieldError())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        result.Match(() => "success", _ => "failure").ShouldBe("failure");
    }

    [Fact]
    public async Task DispatchAsync_WhenValidationFails_OpensNoTransactionAtAll()
    {
        // The point of validating in the dispatcher rather than in a pipeline behaviour: a command
        // rejected for a missing field never starts a transaction.
        await DispatcherRejecting(AFieldError())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.BeginCount.ShouldBe(0);
    }

    [Fact]
    public async Task DispatchAsync_WhenValidationFails_NeverReachesTheHandler()
    {
        var sender = RecordingSender.Returning(Result.Success());
        var dispatcher = new MediatorCommandDispatcher(
            sender,
            _transactionManagers,
            RecordingCommandValidator.Rejecting(AFieldError()));

        await dispatcher.DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        sender.SendCount.ShouldBe(0);
    }

    [Fact]
    public async Task DispatchAsync_WhenValidationFails_ReportsEveryFieldAtOnce()
    {
        var result = await DispatcherRejecting(AFieldError("Isbn"), AFieldError("Title"))
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        result.Match(() => Array.Empty<string?>(), errors => errors.Select(e => e.Target).ToArray())
            .ShouldBe(["Isbn", "Title"]);
    }

    [Fact]
    public async Task DispatchAsync_WhenValidationFails_CarriesTheSharedValidationCode()
    {
        var result = await DispatcherRejecting(AFieldError())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        var code = result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode);

        code.ShouldBe(SharedErrorCodes.ValidationFailed);
    }

    // --- A failed command must not be persisted --------------------------------------------------

    [Fact]
    public async Task DispatchAsync_WhenTheCommandFails_DoesNotCommit()
    {
        await DispatcherReturning(AFailure())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.Transaction!.Committed.ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAsync_WhenTheCommandFails_StillDisposesTheTransaction()
    {
        // Disposal without a commit is what rolls the work back.
        await DispatcherReturning(AFailure())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.Transaction!.Calls.ShouldBe(["dispose"]);
    }

    [Fact]
    public async Task DispatchAsync_WhenTheCommandFails_StillReturnsTheFailure()
    {
        var failure = AFailure();

        var actual = await DispatcherReturning(failure)
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        actual.ShouldBeSameAs(failure);
    }

    // --- An exception must not be persisted either ------------------------------------------------

    [Fact]
    public async Task DispatchAsync_WhenTheHandlerThrows_RethrowsTheException()
    {
        var boom = new InvalidOperationException("handler blew up");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await DispatcherThrowing(boom)
                .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(boom);
    }

    [Fact]
    public async Task DispatchAsync_WhenTheHandlerThrows_DoesNotCommitButDisposes()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await DispatcherThrowing(new InvalidOperationException())
                .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken));

        _transactions.Transaction!.Calls.ShouldBe(["dispose"]);
    }

    // --- Concurrency conflicts leave through the Result channel -----------------------------------

    [Fact]
    public async Task DispatchAsync_OnAConcurrencyConflict_ReturnsAFailureInsteadOfThrowing()
    {
        var result = await DispatcherThrowing(new DbUpdateConcurrencyException())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        result.Match(() => "success", _ => "failure").ShouldBe("failure");
    }

    [Fact]
    public async Task DispatchAsync_OnAConcurrencyConflict_CarriesTheSharedErrorCode()
    {
        var result = await DispatcherThrowing(new DbUpdateConcurrencyException())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        var code = result.Match<ErrorCode?>(() => null, errors => errors[0].ErrorCode);

        code.ShouldBe(SharedErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task DispatchAsync_OnAConcurrencyConflict_ReportsExactlyOneError()
    {
        var result = await DispatcherThrowing(new DbUpdateConcurrencyException())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        result.Match(() => 0, errors => errors.Count).ShouldBe(1);
    }

    [Fact]
    public async Task DispatchAsync_OnAConcurrencyConflict_DoesNotCommitButDisposes()
    {
        await DispatcherThrowing(new DbUpdateConcurrencyException())
            .DispatchAsync(new TestCommand(), TestContext.Current.CancellationToken);

        _transactions.Transaction!.Calls.ShouldBe(["dispose"]);
    }

    // --- Guards -----------------------------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_WithANullCommand_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await DispatcherReturning(Result.Success())
                .DispatchAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_WithANullCommand_ConsultsNoValidatorAndOpensNoTransaction()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await DispatcherReturning(Result.Success())
                .DispatchAsync(null!, TestContext.Current.CancellationToken));

        _validator.CallCount.ShouldBe(0);
        _transactions.BeginCount.ShouldBe(0);
    }

    // --- The signature itself ----------------------------------------------------------------------

    [Fact]
    public void DispatchAsync_IsNotGenericOverTheResultType()
    {
        // Naming Result in the signature is what lets this class build a failure directly, for both
        // validation and concurrency conflicts. A generic TResult would force a runtime type check
        // and an unchecked cast, since a failure can only be constructed for Result itself.
        var method = typeof(ICommandDispatcher).GetMethod(nameof(ICommandDispatcher.DispatchAsync));

        method.ShouldNotBeNull();
        method.IsGenericMethod.ShouldBeFalse();
        method.ReturnType.ShouldBe(typeof(ValueTask<Result>));
    }
}
