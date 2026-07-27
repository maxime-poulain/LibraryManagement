using LibraryManagement.Catalog.Domain.Authors;
using static LibraryManagement.Catalog.Domain.Tests.Catalogue;

namespace LibraryManagement.Catalog.Domain.Tests.Authors;

public sealed class AuthorTests
{
    // --- Opening a record -------------------------------------------------------------------------

    [Fact]
    public void Register_FilesThePersonUnderTheNameGiven()
    {
        var author = Author.Register(AuthorId.Generate(), Name("Ernaux, Annie"), Years(1940, null));

        author.AuthorizedName.ShouldBe(Name("Ernaux, Annie"));
        author.LifeYears.Birth.ShouldBe(1940);
        author.VariantNames.ShouldBeEmpty();
    }

    [Fact]
    public void Register_RaisesTheEventThatSaysSo()
    {
        var author = AnAuthor("Ernaux, Annie");

        author.DomainEvents.OfType<AuthorRegistered>().Single()
            .AuthorizedName.ShouldBe(Name("Ernaux, Annie"));
    }

    // --- Authority control: a person's name changes, their record does not ------------------------

    [Fact]
    public void Rename_FilesThePersonUnderTheNewName()
    {
        var author = AnAuthor("Smith, Jane");

        Succeeded(author.Rename(Name("Smith, Alex"))).ShouldBeTrue();

        author.AuthorizedName.ShouldBe(Name("Smith, Alex"));
    }

    [Fact]
    public void Rename_KeepsTheOutgoingNameAsAVariant()
    {
        // Nothing published under the old name stops existing, and a reader who only knows it must
        // still find the work. That is what a variant form is for.
        var author = AnAuthor("Smith, Jane");

        author.Rename(Name("Smith, Alex"));

        author.VariantNames.ShouldBe([Name("Smith, Jane")]);
    }

    [Fact]
    public void Rename_ToAFormerName_PromotesItAndDemotesTheCurrentOne()
    {
        // Returning to a name one used before is ordinary, so it is allowed — and the variant is
        // promoted rather than duplicated.
        var author = AnAuthor("Smith, Jane");
        author.Rename(Name("Smith, Alex"));

        Succeeded(author.Rename(Name("Smith, Jane"))).ShouldBeTrue();

        author.AuthorizedName.ShouldBe(Name("Smith, Jane"));
        author.VariantNames.ShouldBe([Name("Smith, Alex")]);
    }

    [Fact]
    public void Rename_ToTheNameAlreadyInUse_IsRefused()
    {
        var author = AnAuthor("Smith, Jane");

        var result = author.Rename(Name("Smith, Jane"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }

    [Fact]
    public void Rename_AnnouncesBothNames()
    {
        // The search projection needs the outgoing form as much as the incoming one.
        var author = AnAuthor("Smith, Jane");

        author.Rename(Name("Smith, Alex"));

        var renamed = author.DomainEvents.OfType<AuthorRenamed>().Single();
        renamed.PreviousName.ShouldBe(Name("Smith, Jane"));
        renamed.NewName.ShouldBe(Name("Smith, Alex"));
    }

    // --- Correcting a heading: a fact about the record, not about the person ----------------------

    [Fact]
    public void CorrectHeading_ReplacesTheHeading()
    {
        var author = AnAuthor("Ernuax, Annie");

        Succeeded(author.CorrectHeading(Name("Ernaux, Annie"))).ShouldBeTrue();

        author.AuthorizedName.ShouldBe(Name("Ernaux, Annie"));
    }

    [Fact]
    public void CorrectHeading_KeepsNothingOfTheWrongForm()
    {
        // The difference with Rename, and the reason this method exists. A typo is nobody's name:
        // kept as a variant it would become a searchable access point, and the catalogue would
        // preserve forever the one thing it was asked to remove.
        var author = AnAuthor("Ernuax, Annie");

        author.CorrectHeading(Name("Ernaux, Annie"));

        author.VariantNames.ShouldBeEmpty();
        author.IsKnownAs(Name("Ernuax, Annie")).ShouldBeFalse();
    }

    [Fact]
    public void CorrectHeading_ToTheNameAlreadyInUse_IsRefused()
    {
        var author = AnAuthor("Ernaux, Annie");

        ErrorsOf(author.CorrectHeading(Name("Ernaux, Annie")))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }

    [Fact]
    public void CorrectHeading_ToAFormFiledAsAVariant_PromotesIt()
    {
        // A correction can restore a proper form someone had filed as secondary, and the invariant
        // that a heading is never also a variant must hold on the way through.
        var author = AnAuthor("Ernuax, Annie");
        author.AddVariantName(Name("Ernaux, Annie"));

        Succeeded(author.CorrectHeading(Name("Ernaux, Annie"))).ShouldBeTrue();

        author.AuthorizedName.ShouldBe(Name("Ernaux, Annie"));
        author.VariantNames.ShouldBeEmpty();
    }

    [Fact]
    public void CorrectHeading_AnnouncesARetractionRatherThanARename()
    {
        // Consumers treat the two differently: a rename keeps the outgoing form findable, a
        // correction retracts it. The search projection depends on being able to tell them apart.
        var author = AnAuthor("Ernuax, Annie");

        author.CorrectHeading(Name("Ernaux, Annie"));

        var corrected = author.DomainEvents.OfType<AuthorHeadingCorrected>().Single();
        corrected.PreviousName.ShouldBe(Name("Ernuax, Annie"));
        corrected.CorrectedName.ShouldBe(Name("Ernaux, Annie"));
        author.DomainEvents.OfType<AuthorRenamed>().ShouldBeEmpty();
    }

    // --- Variant names ----------------------------------------------------------------------------

    [Fact]
    public void AddVariantName_RecordsAnotherFormThePersonIsKnownBy()
    {
        var author = AnAuthor("Gary, Romain");

        Succeeded(author.AddVariantName(Name("Ajar, Émile"))).ShouldBeTrue();

        author.VariantNames.ShouldBe([Name("Ajar, Émile")]);
    }

    [Fact]
    public void AddVariantName_ThatIsTheHeading_IsRefused()
    {
        var author = AnAuthor("Gary, Romain");

        ErrorsOf(author.AddVariantName(Name("Gary, Romain")))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }

    [Fact]
    public void AddVariantName_Twice_IsRefused()
    {
        var author = AnAuthor("Gary, Romain");
        author.AddVariantName(Name("Ajar, Émile"));

        ErrorsOf(author.AddVariantName(Name("Ajar, Émile")))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);

        author.VariantNames.Count.ShouldBe(1);
    }

    [Fact]
    public void IsKnownAs_AnswersForTheHeadingAndForEveryVariant()
    {
        var author = AnAuthor("Gary, Romain");
        author.AddVariantName(Name("Ajar, Émile"));

        author.IsKnownAs(Name("Gary, Romain")).ShouldBeTrue();
        author.IsKnownAs(Name("Ajar, Émile")).ShouldBeTrue();
        author.IsKnownAs(Name("Someone Else")).ShouldBeFalse();
    }

    // --- The collection is a view, not a handle ---------------------------------------------------

    [Fact]
    public void VariantNames_CannotBeChangedFromOutside()
    {
        var author = AnAuthor();

        author.VariantNames.ShouldBeAssignableTo<IReadOnlyList<PersonName>>();
        (author.VariantNames as ICollection<PersonName>)?.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void Equality_IsByIdentityAndNeverByName()
    {
        // Two authors filed under the same name are two people until proven otherwise; the same
        // person renamed is still the same record.
        var id = AuthorId.Generate();
        var first = Author.Register(id, Name("Smith, Jane"), LifeYears.Unknown);
        var second = Author.Register(id, Name("Someone Else"), LifeYears.Unknown);

        first.ShouldBe(second);
        AnAuthor("Smith, Jane").ShouldNotBe(AnAuthor("Smith, Jane"));
    }
}
