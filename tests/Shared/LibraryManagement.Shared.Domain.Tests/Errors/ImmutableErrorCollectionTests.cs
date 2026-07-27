using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Tests.Errors;

public sealed class ImmutableErrorCollectionTests
{
    private static Error AnError(string code = "Book.NotFound", string message = "message")
        => new(new ErrorCode(code), message);

    [Fact]
    public void Empty_ContainsNothing()
    {
        ImmutableErrorCollection.Empty.Count.ShouldBe(0);
        ImmutableErrorCollection.Empty.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void From_CapturesTheErrorsOfTheSource()
    {
        var snapshot = ImmutableErrorCollection.From([AnError("A"), AnError("B")]);

        snapshot.Count.ShouldBe(2);
        snapshot.HasErrors.ShouldBeTrue();
        snapshot[0].ErrorCode.Value.ShouldBe("A");
        snapshot[1].ErrorCode.Value.ShouldBe("B");
    }

    [Fact]
    public void From_AnEmptySource_ReturnsTheSharedEmptyInstance()
    {
        ImmutableErrorCollection.From([]).ShouldBeSameAs(ImmutableErrorCollection.Empty);
    }

    [Fact]
    public void From_WithNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ImmutableErrorCollection.From(null!));
    }

    [Fact]
    public void From_DoesNotObserveLaterMutationsOfTheSource()
    {
        var source = new List<Error> { AnError("A") };
        var snapshot = ImmutableErrorCollection.From(source);

        source.Add(AnError("B"));
        source[0] = AnError("MUTATED");

        snapshot.Count.ShouldBe(1);
        snapshot[0].ErrorCode.Value.ShouldBe("A");
    }

    [Fact]
    public void From_AnExistingSnapshot_ReturnsItWithoutCopying()
    {
        var snapshot = ImmutableErrorCollection.From([AnError()]);

        ImmutableErrorCollection.From(snapshot).ShouldBeSameAs(snapshot);
    }

    [Fact]
    public void IsAReadOnlyList_SoCountAndIndexingComeForFree()
    {
        var snapshot = ImmutableErrorCollection.From([AnError("A")]);

        snapshot.ShouldBeAssignableTo<IReadOnlyList<Error>>();
        snapshot.ShouldBeAssignableTo<IReadOnlyErrorCollection>();
    }

    [Fact]
    public void CannotBeCastToTheMutableInterface()
    {
        // Boxed as object so the assertion is about the runtime type, not a compile-time relation.
        object snapshot = ImmutableErrorCollection.From([AnError()]);

        (snapshot is IErrorCollection).ShouldBeFalse();
        Should.Throw<InvalidCastException>(() => (IErrorCollection)snapshot);
    }

    [Fact]
    public void Enumeration_YieldsTheErrorsInOrder()
    {
        var snapshot = ImmutableErrorCollection.From([AnError("A"), AnError("B")]);

        snapshot.Select(e => e.ErrorCode.Value).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void ToString_ListsOneErrorPerLine()
    {
        var snapshot = ImmutableErrorCollection.From([AnError("A", "first"), AnError("B", "second")]);

        snapshot.ToString().ShouldBe($"A: first{Environment.NewLine}B: second");
    }
}
