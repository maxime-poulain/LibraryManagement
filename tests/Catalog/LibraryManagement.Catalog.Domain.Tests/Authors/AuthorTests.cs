using LibraryManagement.Catalog.Domain.Authors;
using static LibraryManagement.Catalog.Domain.Tests.Catalog;

namespace LibraryManagement.Catalog.Domain.Tests.Authors;

public sealed class AuthorTests
{
    // --- Opening a record -------------------------------------------------------------------------

    [Fact]
    public void Register_FilesThePersonUnderTheNameGiven()
    {
        var author = Author.Register(AuthorId.Generate(), Name("Ernaux, Annie"), Years(1940, null));

        author.PreferredName.ShouldBe(Name("Ernaux, Annie"));
        author.LifeYears.Birth.ShouldBe(1940);
        author.VariantNames.ShouldBeEmpty();
    }

    [Fact]
    public void Register_RaisesTheEventThatSaysSo()
    {
        var author = AnAuthor("Ernaux, Annie");

        author.DomainEvents.OfType<AuthorRegistered>().Single()
            .PreferredName.ShouldBe(Name("Ernaux, Annie"));
    }

    // --- Authority control: a person's name changes, their record does not ------------------------

    [Fact]
    public void Rename_FilesThePersonUnderTheNewName()
    {
        var author = AnAuthor("Smith, Jane");

        Succeeded(author.Rename(Name("Smith, Alex"))).ShouldBeTrue();

        author.PreferredName.ShouldBe(Name("Smith, Alex"));
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

        author.PreferredName.ShouldBe(Name("Smith, Jane"));
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

    // --- Correcting a preferred name: a fact about the record, not about the person ----------------------

    [Fact]
    public void CorrectPreferredName_ReplacesThePreferredName()
    {
        var author = AnAuthor("Ernuax, Annie");

        Succeeded(author.CorrectPreferredName(Name("Ernaux, Annie"))).ShouldBeTrue();

        author.PreferredName.ShouldBe(Name("Ernaux, Annie"));
    }

    [Fact]
    public void CorrectPreferredName_KeepsNothingOfTheWrongForm()
    {
        // The difference with Rename, and the reason this method exists. A typo is nobody's name:
        // kept as a variant it would become a searchable access point, and the catalog would
        // preserve forever the one thing it was asked to remove.
        var author = AnAuthor("Ernuax, Annie");

        author.CorrectPreferredName(Name("Ernaux, Annie"));

        author.VariantNames.ShouldBeEmpty();
        author.IsKnownAs(Name("Ernuax, Annie")).ShouldBeFalse();
    }

    [Fact]
    public void CorrectPreferredName_ToTheNameAlreadyInUse_IsRefused()
    {
        var author = AnAuthor("Ernaux, Annie");

        ErrorsOf(author.CorrectPreferredName(Name("Ernaux, Annie")))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }

    [Fact]
    public void CorrectPreferredName_ToAFormFiledAsAVariant_PromotesIt()
    {
        // A correction can restore a proper form someone had filed as secondary, and the invariant
        // that a preferred name is never also a variant must hold on the way through.
        var author = AnAuthor("Ernuax, Annie");
        author.AddVariantName(Name("Ernaux, Annie"));

        Succeeded(author.CorrectPreferredName(Name("Ernaux, Annie"))).ShouldBeTrue();

        author.PreferredName.ShouldBe(Name("Ernaux, Annie"));
        author.VariantNames.ShouldBeEmpty();
    }

    [Fact]
    public void CorrectPreferredName_AnnouncesARetractionRatherThanARename()
    {
        // Consumers treat the two differently: a rename keeps the outgoing form findable, a
        // correction retracts it. The search projection depends on being able to tell them apart.
        var author = AnAuthor("Ernuax, Annie");

        author.CorrectPreferredName(Name("Ernaux, Annie"));

        var corrected = author.DomainEvents.OfType<AuthorPreferredNameCorrected>().Single();
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
    public void AddVariantName_ThatIsThePreferredName_IsRefused()
    {
        var author = AnAuthor("Gary, Romain");

        ErrorsOf(author.AddVariantName(Name("Gary, Romain")))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }

    [Fact]
    public void AddVariantName_AnnouncesTheForm()
    {
        // A variant exists to be searched by, so the projection must learn of it the moment it is
        // recorded — exactly as it learns of the preferred name.
        var author = AnAuthor("Gary, Romain");

        author.AddVariantName(Name("Ajar, Émile"));

        author.DomainEvents.OfType<AuthorVariantNameAdded>().Single()
            .VariantName.ShouldBe(Name("Ajar, Émile"));
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
    public void IsKnownAs_AnswersForThePreferredNameAndForEveryVariant()
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

        author.VariantNames.ShouldBeAssignableTo<IReadOnlyList<NameForm>>();
        (author.VariantNames as ICollection<NameForm>)?.IsReadOnly.ShouldBeTrue();
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
