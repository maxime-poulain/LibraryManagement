using LibraryManagement.Catalog.Application.Editions.MergeEditions;

namespace LibraryManagement.Catalog.Application.Tests.Editions;

public sealed class MergeEditionsCommandValidatorTests
{
    private readonly MergeEditionsCommandValidator _validator = new();

    [Fact]
    public void ACommandNamingTwoRecords_IsWellFormed()
    {
        _validator
            .Validate(new MergeEditionsCommand(Guid.CreateVersion7(), Guid.CreateVersion7()))
            .IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AnEmptyIdentifierOnEitherSide_IsRejected(bool absorbedEmpty, bool survivingEmpty)
    {
        var command = new MergeEditionsCommand(
            absorbedEmpty ? Guid.Empty : Guid.CreateVersion7(),
            survivingEmpty ? Guid.Empty : Guid.CreateVersion7());

        _validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TwoIdenticalIdentifiers_PassTheValidator_AndTheDomainRefusesThem()
    {
        // Deliberately not checked here. The aggregate refuses a record absorbing itself, and a
        // rule enforced in two places is a rule that will one day be enforced differently in each.
        var id = Guid.CreateVersion7();

        _validator.Validate(new MergeEditionsCommand(id, id)).IsValid.ShouldBeTrue();
    }
}
