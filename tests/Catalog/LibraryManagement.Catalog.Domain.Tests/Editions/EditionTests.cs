using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using static LibraryManagement.Catalog.Domain.Tests.Catalog;

namespace LibraryManagement.Catalog.Domain.Tests.Editions;

public sealed class EditionTests
{
    [Fact]
    public void Register_RecordsTheWorkAndTheIsbn()
    {
        var workId = WorkId.Generate();

        var edition = Edition.Register(EditionId.Generate(), workId, AnIsbn());

        edition.WorkId.ShouldBe(workId);
        edition.Isbn.ShouldBe(AnIsbn());
    }

    [Fact]
    public void Register_WithoutAnIsbn_Succeeds()
    {
        // Grey literature, self-published works and everything printed before 1970 bear none —
        // exactly what the manual commands exist to catalog.
        Edition.Register(EditionId.Generate(), WorkId.Generate(), isbn: null).Isbn.ShouldBeNull();
    }

    [Fact]
    public void Register_RaisesTheEventThatSaysSo()
    {
        var workId = WorkId.Generate();

        var edition = Edition.Register(EditionId.Generate(), workId, AnIsbn());

        var registered = edition.DomainEvents.OfType<EditionRegistered>().Single();
        registered.EditionId.ShouldBe(edition.Id);
        registered.WorkId.ShouldBe(workId);
        registered.Isbn.ShouldBe(AnIsbn());
    }

    [Fact]
    public void Register_WithoutAnIsbn_SaysSoInTheEvent()
    {
        // The projection reads the absence from the event itself: no form to index is a fact, not
        // a gap for a consumer to go look up.
        Edition.Register(EditionId.Generate(), WorkId.Generate(), isbn: null)
            .DomainEvents.OfType<EditionRegistered>().Single()
            .Isbn.ShouldBeNull();
    }

    [Fact]
    public void Register_DemandsAnIdentityAndAWork()
    {
        Should.Throw<ArgumentNullException>(() => Edition.Register(null!, WorkId.Generate(), null));
        Should.Throw<ArgumentNullException>(() => Edition.Register(EditionId.Generate(), null!, null));
    }

    private static Edition AnEdition() =>
        Edition.Register(EditionId.Generate(), WorkId.Generate(), AnIsbn());

    [Fact]
    public void AbsorbInto_RecordsTheSurvivorAndAnnouncesIt_AndNothingElse()
    {
        // Reachable because the module shows its internals to its own tests, which is what lets
        // this method stay internal. What is asserted is the whole of what it should now do: the
        // guards are the domain service's, and this is the state change and the fact.
        var absorbed = AnEdition();
        var survivor = EditionId.Generate();

        absorbed.AbsorbInto(survivor);

        absorbed.AbsorbedInto.ShouldBe(survivor);

        var merged = absorbed.DomainEvents.OfType<EditionsMerged>().Single();
        merged.AbsorbedEditionId.ShouldBe(absorbed.Id);
        merged.SurvivingEditionId.ShouldBe(survivor);
    }

    [Fact]
    public void AbsorbInto_DemandsASurvivor()
    {
        Should.Throw<ArgumentNullException>(() => AnEdition().AbsorbInto(null!));
    }
}
