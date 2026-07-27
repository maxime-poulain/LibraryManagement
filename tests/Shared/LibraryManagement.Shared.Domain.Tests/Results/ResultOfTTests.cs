using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Tests.Results;

public sealed class ResultOfTTests
{
    private static readonly ErrorCode Invalid = new("Isbn.Invalid");

    private static Error AnError(string code = "Isbn.Invalid", string message = "message")
        => new(new ErrorCode(code), message);

    // --- Success ---------------------------------------------------------------------------------

    [Fact]
    public void Success_Match_ReceivesTheValue()
    {
        Result<int>.Success(42).Match(value => value, _ => -1).ShouldBe(42);
    }

    [Fact]
    public void Success_HasErrors_IsFalse()
    {
        Result<int>.Success(42).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public void Success_Switch_RunsTheSuccessBranchWithTheValue()
    {
        var captured = 0;

        Result<int>.Success(42).Switch(value => captured = value, _ => captured = -1);

        captured.ShouldBe(42);
    }

    [Fact]
    public void Success_Tap_RunsTheActionAndReturnsTheSameInstance()
    {
        var captured = 0;
        var result = Result<int>.Success(42);

        result.Tap(value => captured = value).ShouldBeSameAs(result);

        captured.ShouldBe(42);
    }

    [Fact]
    public void Success_TapError_DoesNotRunTheAction()
    {
        var ran = false;

        Result<int>.Success(42).TapError(_ => ran = true);

        ran.ShouldBeFalse();
    }

    [Fact]
    public void Success_Bind_ChainsOntoTheValue()
    {
        Result<int>.Success(21)
            .Bind(value => Result<string>.Success($"{value * 2}"))
            .Match(value => value, _ => "failure")
            .ShouldBe("42");
    }

    [Fact]
    public async Task Success_BindAsync_ChainsOntoTheValue()
    {
        var outcome = await Result<int>.Success(21)
            .BindAsync(value => ValueTask.FromResult(Result<string>.Success($"{value * 2}")));

        outcome.Match(value => value, _ => "failure").ShouldBe("42");
    }

    [Fact]
    public async Task Success_MatchAsync_ReceivesTheValue()
    {
        var outcome = await Result<int>.Success(42)
            .MatchAsync(value => ValueTask.FromResult(value), _ => ValueTask.FromResult(-1));

        outcome.ShouldBe(42);
    }

    [Fact]
    public async Task Success_SwitchAsync_RunsTheSuccessBranch()
    {
        var captured = 0;

        await Result<int>.Success(42).SwitchAsync(
            value => { captured = value; return ValueTask.CompletedTask; },
            _ => ValueTask.CompletedTask);

        captured.ShouldBe(42);
    }

    [Fact]
    public async Task SuccessAsync_CompletesWithTheValue()
    {
        var result = await Result<int>.SuccessAsync(42);

        result.Match(value => value, _ => -1).ShouldBe(42);
    }

    // --- Failure ---------------------------------------------------------------------------------

    [Fact]
    public void Failure_FromASingleError_CarriesIt()
    {
        Result<int>.Failure(AnError(message: "must be 13 characters"))
            .Match(_ => "success", errors => errors[0].ErrorMessage)
            .ShouldBe("must be 13 characters");
    }

    [Fact]
    public void Failure_FromACodeAndMessage_CarriesThem()
    {
        Result<int>.Failure(Invalid, "must be 13 characters")
            .Match(_ => string.Empty, errors => errors[0].ToString())
            .ShouldBe("Isbn.Invalid: must be 13 characters");
    }

    [Fact]
    public void Failure_HasErrors_IsTrue()
    {
        Result<int>.Failure(Invalid, "invalid").HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Failure_Tap_DoesNotRunTheAction()
    {
        var ran = false;

        Result<int>.Failure(Invalid, "invalid").Tap(_ => ran = true);

        ran.ShouldBeFalse();
    }

    [Fact]
    public void Failure_TapError_RunsTheActionWithTheErrors()
    {
        var captured = 0;

        Result<int>.Failure(Invalid, "invalid").TapError(errors => captured = errors.Count);

        captured.ShouldBe(1);
    }

    [Fact]
    public void Failure_Bind_ShortCircuitsAndPropagatesTheOriginalErrors()
    {
        var continuationRan = false;

        var outcome = Result<int>.Failure(Invalid, "original")
            .Bind(_ =>
            {
                continuationRan = true;
                return Result<string>.Success("never reached");
            });

        continuationRan.ShouldBeFalse();
        outcome.Match(value => value, errors => errors[0].ErrorMessage).ShouldBe("original");
    }

    [Fact]
    public async Task Failure_BindAsync_ShortCircuits()
    {
        var continuationRan = false;

        var outcome = await Result<int>.Failure(Invalid, "original")
            .BindAsync(_ =>
            {
                continuationRan = true;
                return ValueTask.FromResult(Result<string>.Success("never reached"));
            });

        continuationRan.ShouldBeFalse();
        outcome.Match(value => value, errors => errors[0].ErrorMessage).ShouldBe("original");
    }

    [Fact]
    public async Task Failure_SwitchAsync_RunsTheFailureBranch()
    {
        var branch = "none";

        await Result<int>.Failure(Invalid, "invalid").SwitchAsync(
            _ => ValueTask.CompletedTask,
            _ => { branch = "failure"; return ValueTask.CompletedTask; });

        branch.ShouldBe("failure");
    }

    [Fact]
    public async Task FailureAsync_CompletesWithAFailedResult()
    {
        var result = await Result<int>.FailureAsync(Invalid, "invalid");

        result.HasErrors().ShouldBeTrue();
    }

    // --- Construction guards and immutability ---------------------------------------------------

    [Fact]
    public void Failure_WithNoError_Throws()
    {
        Should.Throw<ArgumentException>(() => Result<int>.Failure(new ErrorCollection()))
            .ParamName.ShouldBe("errors");
    }

    [Fact]
    public void Failure_CapturesASnapshot_NotTheLiveAccumulator()
    {
        var accumulator = new ErrorCollection();
        accumulator.Add(AnError("A", "original"));

        var result = Result<int>.Failure(accumulator);

        accumulator[0] = AnError("A", "MUTATED");
        accumulator.Add(AnError("B"));

        result.Match(_ => 0, errors => errors.Count).ShouldBe(1);
        result.Match(_ => "success", errors => errors[0].ErrorMessage).ShouldBe("original");
    }

    [Fact]
    public void Bind_AcrossSeveralSteps_PropagatesTheFirstFailure()
    {
        var outcome = Result<int>.Success(1)
            .Bind(_ => Result<int>.Failure(Invalid, "step two failed"))
            .Bind(_ => Result<string>.Success("step three"));

        outcome.Match(value => value, errors => errors[0].ErrorMessage).ShouldBe("step two failed");
    }

    [Fact]
    public void ResultOfT_DoesNotDeriveFromResult_WhichIsWhatEnforcesStrictCqs()
    {
        // ICommand<TResult> is constrained to `where TResult : Result`, so ICommand<Result<T>> is a
        // compile error. Commands change state and return no value; queries return values.
        typeof(Result).IsAssignableFrom(typeof(Result<int>)).ShouldBeFalse();
    }
}
