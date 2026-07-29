using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Tests.Results;

public sealed class FailableTests
{
    private static readonly ErrorCode Code = new("Test.Failed");

    private static Error AnError() => new(Code, "something went wrong");

    // Stands in for the pipeline behavior: it knows nothing about the concrete result type and can
    // still build a failure of it. This is the whole point of the interface.
    private static TResult FailureOf<TResult>(IEnumerable<Error> errors)
        where TResult : IFailable<TResult>
        => TResult.Failure(errors);

    [Fact]
    public void BothResultTypes_AreFailable()
    {
        // Written with typeof rather than a generic assertion: an interface with a static abstract
        // member may not be used as a type argument, which is the language enforcing that nobody
        // holds one as a value and calls the factory through it.
        typeof(IFailable<Result>).IsAssignableFrom(typeof(Result)).ShouldBeTrue();
        typeof(IFailable<Result<int>>).IsAssignableFrom(typeof(Result<int>)).ShouldBeTrue();
    }

    [Fact]
    public void AFailure_CanBeBuiltWithoutNamingTheResultType()
    {
        FailureOf<Result>([AnError()]).ShouldBeAssignableTo<Result>();
        FailureOf<Result<string>>([AnError()]).ShouldBeAssignableTo<Result<string>>();
    }

    [Fact]
    public void AFailureBuiltGenerically_CarriesTheErrorsItWasGiven()
    {
        var failure = FailureOf<Result<string>>([AnError()]);

        failure.Match(_ => [], errors => errors.Select(error => error.ErrorCode).ToArray())
            .ShouldBe([Code]);
    }

    [Fact]
    public void TheErrors_AreReadableWithoutNamingTheResultType()
    {
        // What the logging behavior stands on: reporting the codes a message was refused with,
        // without knowing which of the two result types it holds — and reading nothing at all from
        // a success.
        CodesOf(Result.Failure([AnError()])).ShouldBe("Test.Failed");
        CodesOf(Result<string>.Failure(AnError())).ShouldBe("Test.Failed");
        CodesOf(Result.Success()).ShouldBeEmpty();
    }

    private static string CodesOf<TResult>(TResult result)
        where TResult : IFailable<TResult>
    {
        var codes = string.Empty;
        result.TapError(errors => codes = string.Join(", ", errors.Select(error => error.ErrorCode)));

        return codes;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasErrors_AnswersForBothResultTypes(bool failed)
    {
        var untyped = failed ? Result.Failure([AnError()]) : Result.Success();
        var typed = failed ? Result<string>.Failure(AnError()) : Result<string>.Success("value");

        untyped.HasErrors().ShouldBe(failed);
        typed.HasErrors().ShouldBe(failed);
    }

    // --- The separation the interface must not undo ------------------------------------------------

    [Fact]
    public void TheInterface_CreatesNoDerivationBetweenTheTwoResultTypes()
    {
        // Sharing a capability is not sharing a hierarchy. If this ever became true,
        // ICommand<Result<T>> would compile and commands could return values again — which the
        // constraint `where TResult : Result` is there to forbid.
        typeof(Result).IsAssignableFrom(typeof(Result<int>)).ShouldBeFalse();
        typeof(Result<int>).IsAssignableFrom(typeof(Result)).ShouldBeFalse();
    }

    [Fact]
    public void TheInterface_IsNotACommonBaseTheConstraintWouldAccept()
    {
        // Each closes IFailable over itself, so there is no single IFailable<T> that both satisfy,
        // and nothing generic over "a result" can quietly accept either where one was meant.
        typeof(IFailable<Result>).IsAssignableFrom(typeof(Result<int>)).ShouldBeFalse();
        typeof(IFailable<Result<int>>).IsAssignableFrom(typeof(Result)).ShouldBeFalse();
    }
}
