using LibraryManagement.Catalog.Application.Works.GetWorkById;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class GetWorkByIdQueryValidatorTests
{
    private readonly GetWorkByIdQueryValidator _validator = new();

    [Fact]
    public void AQueryCarryingAnIdentifier_IsWellFormed()
    {
        _validator.Validate(new GetWorkByIdQuery(Guid.CreateVersion7())).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AQueryCarryingTheEmptyIdentifier_IsRejected()
    {
        // This is what keeps "you asked badly" apart from "there is no such work". Without it the
        // handler would answer WorkNotFound for a request that never named a work at all, and a
        // caller could not tell a typo from an empty shelf.
        var outcome = _validator.Validate(new GetWorkByIdQuery(Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Single().PropertyName.ShouldBe(nameof(GetWorkByIdQuery.WorkId));
    }

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the work exists is a question about the catalog, needs the store to answer, and
        // belongs to the handler. A validator that consulted a repository would put a second source
        // of truth in front of the first.
        typeof(GetWorkByIdQueryValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
