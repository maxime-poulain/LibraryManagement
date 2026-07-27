using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Tests.Results;

public sealed class ResultExtensionsTests
{
    private static Error AnError(string code = "Isbn.Invalid", string message = "message")
        => new(new ErrorCode(code), message);

    private static Result<TValue> FailedWith<TValue>(string code)
        => Result<TValue>.Failure([AnError(code)]);

    // --- ToResult ---------------------------------------------------------------------------------

    [Fact]
    public void ToResult_FromASuccess_IsASuccess()
    {
        Result<int>.Success(42).ToResult().Match(() => "success", _ => "failure").ShouldBe("success");
    }

    [Fact]
    public void ToResult_FromAFailure_KeepsEveryError()
    {
        var failure = Result<int>.Failure([AnError("A"), AnError("B")]);

        var collapsed = failure.ToResult();

        collapsed.Match(() => Array.Empty<string>(), errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["A", "B"]);
    }

    [Fact]
    public void ToResult_WithNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((Result<int>)null!).ToResult());
    }

    // --- Bind, from a valued result into a valueless step ----------------------------------------

    [Fact]
    public void Bind_FromASuccess_RunsTheStepWithTheValue()
    {
        var captured = 0;

        var outcome = Result<int>.Success(42).Bind(value =>
        {
            captured = value;
            return Result.Success();
        });

        captured.ShouldBe(42);
        outcome.Match(() => "success", _ => "failure").ShouldBe("success");
    }

    [Fact]
    public void Bind_FromASuccess_PropagatesAFailureProducedByTheStep()
    {
        var outcome = Result<int>.Success(42)
            .Bind(_ => Result.Failure(new ErrorCode("Step.Failed"), "the step failed"));

        outcome.Match(() => string.Empty, errors => errors[0].ErrorMessage).ShouldBe("the step failed");
    }

    [Fact]
    public void Bind_FromAFailure_ShortCircuitsAndKeepsTheOriginalErrors()
    {
        var stepRan = false;

        var outcome = FailedWith<int>("Original").Bind(_ =>
        {
            stepRan = true;
            return Result.Success();
        });

        stepRan.ShouldBeFalse();
        outcome.Match(() => string.Empty, errors => errors[0].ErrorCode.Value).ShouldBe("Original");
    }

    [Fact]
    public void Bind_FromAValuedResult_WithNullArguments_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((Result<int>)null!).Bind(_ => Result.Success()));
        Should.Throw<ArgumentNullException>(() => Result<int>.Success(1).Bind(null!));
    }

    // --- Bind, from a valueless result into a valued step ----------------------------------------

    [Fact]
    public void Bind_FromAValuelessSuccess_RunsTheStep()
    {
        var outcome = Result.Success().Bind(() => Result<string>.Success("produced"));

        outcome.Match(value => value, _ => "failure").ShouldBe("produced");
    }

    [Fact]
    public void Bind_FromAValuelessFailure_ShortCircuitsAndKeepsTheOriginalErrors()
    {
        var stepRan = false;

        var outcome = Result.Failure(new ErrorCode("Original"), "original").Bind(() =>
        {
            stepRan = true;
            return Result<string>.Success("never reached");
        });

        stepRan.ShouldBeFalse();
        outcome.Match(value => value, errors => errors[0].ErrorCode.Value).ShouldBe("Original");
    }

    [Fact]
    public void Bind_FromAValuelessSuccess_PropagatesAFailureProducedByTheStep()
    {
        var outcome = Result.Success()
            .Bind(() => Result<string>.Failure(new ErrorCode("Step.Failed"), "the step failed"));

        outcome.Match(value => value, errors => errors[0].ErrorMessage).ShouldBe("the step failed");
    }

    [Fact]
    public void Bind_FromAValuelessResult_WithNullArguments_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((Result)null!).Bind(() => Result<int>.Success(1)));
        Should.Throw<ArgumentNullException>(() => Result.Success().Bind<int>(null!));
    }

    // --- Combine, two results ---------------------------------------------------------------------

    [Fact]
    public void Combine_WhenBothSucceed_CarriesBothValues()
    {
        var combined = Result<int>.Success(42).Combine(Result<string>.Success("answer"));

        combined.Match(pair => $"{pair.First}/{pair.Second}", _ => "failure").ShouldBe("42/answer");
    }

    [Fact]
    public void Combine_WhenBothFail_ReportsEveryErrorRatherThanTheFirst()
    {
        // This is what separates Combine from Bind: an employee sees both mistakes at once.
        var combined = FailedWith<int>("First").Combine(FailedWith<string>("Second"));

        combined.Match(
                _ => Array.Empty<string>(),
                errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["First", "Second"]);
    }

    [Fact]
    public void Combine_WhenOnlyOneFails_ReportsThatOne()
    {
        var combined = Result<int>.Success(42).Combine(FailedWith<string>("Second"));

        combined.Match(_ => Array.Empty<string>(), errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["Second"]);
    }

    [Fact]
    public void Combine_AccumulatesEveryErrorOfASideThatReportedSeveral()
    {
        var first = Result<int>.Failure([AnError("A1"), AnError("A2")]);
        var second = Result<string>.Failure([AnError("B1")]);

        first.Combine(second)
            .Match(_ => Array.Empty<string>(), errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["A1", "A2", "B1"]);
    }

    [Fact]
    public void Combine_KeepsTheErrorsInTheOrderOfTheOperands()
    {
        FailedWith<int>("Left").Combine(FailedWith<string>("Right"))
            .Match(_ => Array.Empty<string>(), errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["Left", "Right"]);
    }

    [Fact]
    public void Combine_WithNullArguments_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => ((Result<int>)null!).Combine(Result<string>.Success("x")));
        Should.Throw<ArgumentNullException>(
            () => Result<int>.Success(1).Combine((Result<string>)null!));
    }

    // --- Combine, three results -------------------------------------------------------------------

    [Fact]
    public void Combine_OfThree_WhenAllSucceed_CarriesEveryValue()
    {
        var combined = Result<int>.Success(1)
            .Combine(Result<string>.Success("two"), Result<bool>.Success(true));

        combined.Match(t => $"{t.First}/{t.Second}/{t.Third}", _ => "failure").ShouldBe("1/two/True");
    }

    [Fact]
    public void Combine_OfThree_AccumulatesEveryError()
    {
        var combined = FailedWith<int>("A")
            .Combine(FailedWith<string>("B"), FailedWith<bool>("C"));

        combined.Match(_ => Array.Empty<string>(), errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["A", "B", "C"]);
    }

    [Fact]
    public void Combine_OfThree_WhenOnlyTheMiddleFails_ReportsThatOne()
    {
        var combined = Result<int>.Success(1)
            .Combine(FailedWith<string>("Middle"), Result<bool>.Success(true));

        combined.Match(_ => Array.Empty<string>(), errors => errors.Select(e => e.ErrorCode.Value).ToArray())
            .ShouldBe(["Middle"]);
    }

    [Fact]
    public void Combine_OfThree_WithNullArguments_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => Result<int>.Success(1).Combine(Result<string>.Success("x"), (Result<bool>)null!));
    }

    // --- The reason the class exists ---------------------------------------------------------------

    [Fact]
    public void AWholeCommandHandlerCollapsesToOneChain()
    {
        // Two inputs are validated independently, both errors are reported at once, and the chain
        // ends on a valueless Result — the shape every command handler has.
        var saved = string.Empty;

        var outcome = Result<string>.Success("9780321125217")
            .Combine(Result<string>.Success("Domain-Driven Design"))
            .Bind(book =>
            {
                saved = $"{book.Second} ({book.First})";
                return Result.Success();
            });

        outcome.Match(() => "success", _ => "failure").ShouldBe("success");
        saved.ShouldBe("Domain-Driven Design (9780321125217)");
    }

    [Fact]
    public void TheTwoResultTypesStillDoNotKnowEachOther()
    {
        // The conversions live here precisely so neither type has to reference the other. If that
        // ever changes, the separation that makes ICommand<Result<T>> a compile error is at risk.
        typeof(Result).IsAssignableFrom(typeof(Result<int>)).ShouldBeFalse();
        typeof(Result<int>).IsAssignableFrom(typeof(Result)).ShouldBeFalse();
    }
}
