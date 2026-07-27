using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Tests.Results;

public sealed class ResultTests
{
    private static readonly ErrorCode NotFound = new("Book.NotFound");

    private static Error AnError(string code = "Book.NotFound", string message = "message")
        => new(new ErrorCode(code), message);

    // --- Success ---------------------------------------------------------------------------------

    [Fact]
    public void Success_Match_RunsTheSuccessBranch()
    {
        Result.Success().Match(() => "success", _ => "failure").ShouldBe("success");
    }

    [Fact]
    public void Success_Switch_RunsTheSuccessBranch()
    {
        var branch = "none";

        Result.Success().Switch(() => branch = "success", _ => branch = "failure");

        branch.ShouldBe("success");
    }

    [Fact]
    public void Success_Tap_RunsTheActionAndReturnsTheSameInstance()
    {
        var ran = false;
        var result = Result.Success();

        result.Tap(() => ran = true).ShouldBeSameAs(result);

        ran.ShouldBeTrue();
    }

    [Fact]
    public void Success_TapError_DoesNotRunTheAction()
    {
        var ran = false;

        Result.Success().TapError(_ => ran = true);

        ran.ShouldBeFalse();
    }

    [Fact]
    public void Success_Bind_RunsTheContinuation()
    {
        Result.Success()
            .Bind(() => Result.Failure(NotFound, "from the continuation"))
            .Match(() => "success", errors => errors[0].ErrorMessage)
            .ShouldBe("from the continuation");
    }

    [Fact]
    public async Task Success_MatchAsync_RunsTheSuccessBranch()
    {
        var outcome = await Result.Success()
            .MatchAsync(() => ValueTask.FromResult("success"), _ => ValueTask.FromResult("failure"));

        outcome.ShouldBe("success");
    }

    [Fact]
    public async Task SuccessAsync_CompletesWithASuccessfulResult()
    {
        var result = await Result.SuccessAsync();

        result.Match(() => "success", _ => "failure").ShouldBe("success");
    }

    // --- Failure ---------------------------------------------------------------------------------

    [Fact]
    public void Failure_Match_RunsTheFailureBranchWithTheErrors()
    {
        Result.Failure(NotFound, "no book")
            .Match(() => "success", errors => errors[0].ErrorMessage)
            .ShouldBe("no book");
    }

    [Fact]
    public void Failure_Switch_RunsTheFailureBranch()
    {
        var branch = "none";

        Result.Failure(NotFound, "no book").Switch(() => branch = "success", _ => branch = "failure");

        branch.ShouldBe("failure");
    }

    [Fact]
    public void Failure_Tap_DoesNotRunTheAction()
    {
        var ran = false;

        Result.Failure(NotFound, "no book").Tap(() => ran = true);

        ran.ShouldBeFalse();
    }

    [Fact]
    public void Failure_TapError_RunsTheActionWithTheErrors()
    {
        var captured = 0;

        Result.Failure(NotFound, "no book").TapError(errors => captured = errors.Count);

        captured.ShouldBe(1);
    }

    [Fact]
    public void Failure_Bind_ShortCircuitsAndPropagatesTheOriginalErrors()
    {
        var continuationRan = false;

        var outcome = Result.Failure(NotFound, "original")
            .Bind(() =>
            {
                continuationRan = true;
                return Result.Success();
            });

        continuationRan.ShouldBeFalse();
        outcome.Match(() => "success", errors => errors[0].ErrorMessage).ShouldBe("original");
    }

    [Fact]
    public async Task Failure_MatchAsync_RunsTheFailureBranch()
    {
        var outcome = await Result.Failure(NotFound, "no book")
            .MatchAsync(() => ValueTask.FromResult("success"), _ => ValueTask.FromResult("failure"));

        outcome.ShouldBe("failure");
    }

    [Fact]
    public async Task FailureAsync_CompletesWithAFailedResult()
    {
        var result = await Result.FailureAsync([AnError()]);

        result.Match(() => 0, errors => errors.Count).ShouldBe(1);
    }

    // --- Construction guards ---------------------------------------------------------------------

    [Fact]
    public void Failure_WithNoError_Throws()
    {
        // A failure carrying no error is an incoherent state: Match would call the failure branch
        // with an empty collection.
        Should.Throw<ArgumentException>(() => Result.Failure(new ErrorCollection()))
            .ParamName.ShouldBe("errors");
    }

    [Fact]
    public void Failure_WithNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure((IEnumerable<Error>)null!));
    }

    [Fact]
    public void FromErrors_WhenEmpty_IsASuccess()
    {
        Result.FromErrors([]).Match(() => "success", _ => "failure").ShouldBe("success");
    }

    [Fact]
    public void FromErrors_WhenNotEmpty_IsAFailure()
    {
        Result.FromErrors([AnError()]).Match(() => "success", _ => "failure").ShouldBe("failure");
    }

    // --- Regression: a Result does not share storage with its accumulator ------------------------
    // Result.Failure used to keep a reference to the ErrorCollection it was given. Because that
    // collection stays mutable by design, the errors a result reported could change after it had
    // been built and handed to a caller.

    [Fact]
    public void Failure_CapturesASnapshot_NotTheLiveAccumulator()
    {
        var accumulator = new ErrorCollection();
        accumulator.Add(new ErrorCode("Book.NotFound"), "original");

        var result = Result.Failure(accumulator);

        accumulator[0] = AnError("Book.NotFound", "MUTATED AFTER THE RESULT WAS BUILT");
        accumulator.Add(AnError("Book.Extra", "added afterwards"));

        result.Match(() => 0, errors => errors.Count).ShouldBe(1);
        result.Match(() => "success", errors => errors[0].ErrorMessage).ShouldBe("original");
    }

    [Fact]
    public void FromErrors_CapturesASnapshot_NotTheLiveAccumulator()
    {
        var accumulator = new ErrorCollection();
        accumulator.Add(AnError("A"));

        var result = Result.FromErrors(accumulator);

        accumulator.Add(AnError("B"));

        result.Match(() => 0, errors => errors.Count).ShouldBe(1);
    }

    [Fact]
    public void TheAccumulatorRemainsUsableAfterAResultHasBeenBuiltFromIt()
    {
        var accumulator = new ErrorCollection();
        accumulator.Add(AnError("A"));

        _ = Result.Failure(accumulator);
        accumulator.Add(AnError("B"));

        accumulator.Count.ShouldBe(2);
    }
}
