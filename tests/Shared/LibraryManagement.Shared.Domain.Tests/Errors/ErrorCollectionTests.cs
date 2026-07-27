using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Domain.Tests.Errors;

public sealed class ErrorCollectionTests
{
    private static Error AnError(string code = "Book.NotFound", string message = "message")
        => new(new ErrorCode(code), message);

    [Fact]
    public void ANewCollection_IsEmpty()
    {
        new ErrorCollection().Count.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithASequence_CopiesIt()
    {
        var source = new List<Error> { AnError("A") };
        var collection = new ErrorCollection(source);

        source.Add(AnError("B"));

        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WithNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ErrorCollection(null!));
    }

    [Fact]
    public void Add_AppendsTheError()
    {
        var collection = new ErrorCollection();

        collection.Add(AnError("A"));

        collection.Count.ShouldBe(1);
        collection[0].ErrorCode.Value.ShouldBe("A");
    }

    [Fact]
    public void Add_WithACodeAndMessage_BuildsTheError()
    {
        var collection = new ErrorCollection();

        collection.Add(new ErrorCode("Book.NotFound"), "no book");

        collection[0].ShouldBe(new Error(new ErrorCode("Book.NotFound"), "no book"));
    }

    [Fact]
    public void Add_WithNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ErrorCollection().Add(null!));
    }

    [Fact]
    public void AddErrors_AppendsTheWholeSequence()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A"));

        collection.AddErrors([AnError("B"), AnError("C")]);

        collection.Select(e => e.ErrorCode.Value).ShouldBe(["A", "B", "C"]);
    }

    [Fact]
    public void AddErrors_WithNullSequence_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ErrorCollection().AddErrors((IEnumerable<Error>)null!));
    }

    [Fact]
    public void AddErrors_FromAFailedResult_AccumulatesItsErrors()
    {
        var collection = new ErrorCollection();

        collection.AddErrors(Result.Failure(new ErrorCode("Loan.Overdue"), "overdue"));

        collection.Count.ShouldBe(1);
        collection[0].ErrorCode.Value.ShouldBe("Loan.Overdue");
    }

    [Fact]
    public void AddErrors_FromASuccessfulResult_AddsNothing()
    {
        var collection = new ErrorCollection();

        collection.AddErrors(Result.Success());

        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void AddErrors_WithNullResult_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ErrorCollection().AddErrors((Result)null!));
    }

    [Fact]
    public void Indexer_ReadsAndReplacesInPlace()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A", "first"));

        collection[0] = AnError("A", "reworded");

        collection[0].ErrorMessage.ShouldBe("reworded");
    }

    [Fact]
    public void Enumeration_YieldsTheAccumulatedErrorsInOrder()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A"));
        collection.Add(AnError("B"));

        collection.Select(e => e.ErrorCode.Value).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void TheMutableInterface_IsEnumerable_SoLinqWorksOnTheAccumulatorItself()
    {
        typeof(IEnumerable<Error>).IsAssignableFrom(typeof(IErrorCollection)).ShouldBeTrue();

        var collection = new ErrorCollection();
        collection.Add(AnError("A"));
        collection.Add(AnError("B"));

        collection.Where(e => e.ErrorCode.Value == "B").ShouldHaveSingleItem();
    }

    // --- Regression: an accumulator is not a read-only set --------------------------------------
    // ErrorCollection used to implement IReadOnlyErrorCollection and AsReadOnly() returned `this`,
    // so a mutable accumulator could be handed anywhere an immutable set of errors was expected.

    [Fact]
    public void IsNotAssignableToTheReadOnlyInterface()
    {
        typeof(IReadOnlyErrorCollection).IsAssignableFrom(typeof(ErrorCollection)).ShouldBeFalse();
    }

    [Fact]
    public void AsReadOnly_ReturnsASnapshot_NotTheCollectionItself()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A"));

        var snapshot = collection.AsReadOnly();

        collection.Add(AnError("B"));
        collection[0] = AnError("MUTATED");

        snapshot.Count.ShouldBe(1);
        snapshot[0].ErrorCode.Value.ShouldBe("A");
    }

    // --- Bridging to Result ---------------------------------------------------------------------

    [Fact]
    public void ToResult_WhenEmpty_IsASuccess()
    {
        new ErrorCollection().ToResult().Match(() => "success", _ => "failure").ShouldBe("success");
    }

    [Fact]
    public void ToResult_WhenNotEmpty_IsAFailureCarryingTheErrors()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A"));

        collection.ToResult().Match(() => 0, errors => errors.Count).ShouldBe(1);
    }

    [Fact]
    public void ToResultOfT_WhenEmpty_CarriesTheValue()
    {
        new ErrorCollection().ToResult(42).Match(value => value, _ => -1).ShouldBe(42);
    }

    [Fact]
    public void ToResultOfT_WhenNotEmpty_IsAFailure()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A"));

        collection.ToResult(42).HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Match_WhenEmpty_RunsTheSuccessBranch()
    {
        new ErrorCollection().Match(() => "success", _ => "failure").ShouldBe("success");
    }

    [Fact]
    public void Match_WhenNotEmpty_RunsTheFailureBranchWithASnapshot()
    {
        var collection = new ErrorCollection();
        collection.Add(AnError("A"));

        var captured = collection.Match(() => (IReadOnlyErrorCollection)null!, errors => errors);

        collection.Add(AnError("B"));

        captured.Count.ShouldBe(1);
    }
}
