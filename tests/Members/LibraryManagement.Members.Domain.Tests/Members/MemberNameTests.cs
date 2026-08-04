using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Domain.Tests.Members;

public sealed class MemberNameTests
{
    [Fact]
    public void Create_KeepsBothPartsAsDeclared()
    {
        var name = MemberName.Create("Antoine", "Doinel")
            .Match(value => value, _ => throw new InvalidOperationException());

        name.GivenName.ShouldBe("Antoine");
        name.FamilyName.ShouldBe("Doinel");
    }

    [Fact]
    public void Create_TrimsTheSurroundingWhiteSpace()
    {
        var name = MemberName.Create("  Antoine ", " Doinel  ")
            .Match(value => value, _ => throw new InvalidOperationException());

        name.GivenName.ShouldBe("Antoine");
        name.FamilyName.ShouldBe("Doinel");
    }

    [Theory]
    [InlineData(null, "Doinel")]
    [InlineData("", "Doinel")]
    [InlineData("   ", "Doinel")]
    [InlineData("Antoine", null)]
    [InlineData("Antoine", "")]
    [InlineData("Antoine", "   ")]
    public void Create_WithoutAPart_Fails(string? given, string? family)
        => MemberName.Create(given, family).HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_WithBothPartsBlank_ReportsBothAtOnce()
    {
        // Accumulation, not short-circuit: a librarian filling a form is told everything wrong
        // with it in one refusal.
        MemberName.Create("", "").Match(
                _ => 0,
                errors => errors.Count)
            .ShouldBe(2);
    }

    [Fact]
    public void Create_LongerThanAPartMayRun_Fails()
        => MemberName.Create(new string('a', MemberName.MaxLength + 1), "Doinel")
            .HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_AtTheBound_Succeeds()
        => MemberName.Create(new string('a', MemberName.MaxLength), "Doinel")
            .HasErrors().ShouldBeFalse();

    [Theory]
    [InlineData("Jean-Baptiste", "Poquelin")]
    [InlineData("Amélie", "d'Arçonval")]
    [InlineData("María", "García Márquez")]
    public void Create_AcceptsWhateverNamesPeopleActuallyHave(string given, string family)
    {
        // The model does not parse a name. Hyphens, particles, apostrophes and accents are all
        // names, and a format rule would refuse a legitimate member at the desk.
        MemberName.Create(given, family).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public void ToString_ReadsInOrdinaryOrder()
        => MemberName.Create("Antoine", "Doinel")
            .Match(name => name.ToString(), _ => "refused")
            .ShouldBe("Antoine Doinel");

    [Fact]
    public void TwoNamesDifferingOnlyInCase_AreTwoNames()
    {
        // A correction of case is a correction, and folding it here would swallow the event that
        // announces it.
        var lower = MemberName.Create("antoine", "doinel")
            .Match(name => name, _ => throw new InvalidOperationException());
        var upper = MemberName.Create("Antoine", "Doinel")
            .Match(name => name, _ => throw new InvalidOperationException());

        lower.ShouldNotBe(upper);
    }
}
