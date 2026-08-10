using LibraryManagement.Holdings.Domain.Copies;
using static LibraryManagement.Holdings.Domain.Tests.Collection;

namespace LibraryManagement.Holdings.Domain.Tests.Copies;

public sealed class CopyTests
{
    // --- Acquiring ------------------------------------------------------------------------------

    [Fact]
    public void Acquire_RecordsWhatTheLibraryNowOwns()
    {
        var editionId = EditionId.Generate();

        var copy = Copy.Acquire(
            CopyId.Generate(), editionId, ABarcode(), AShelfmark(), CopyCondition.Good, ADate);

        copy.EditionId.ShouldBe(editionId);
        copy.Barcode.ShouldBe(ABarcode());
        copy.Shelfmark.ShouldBe(AShelfmark());
        copy.Condition.ShouldBe(CopyCondition.Good);
        copy.AcquiredOn.ShouldBe(ADate);
    }

    [Fact]
    public void Acquire_PutsTheCopyInService()
        => ACopy().Status.ShouldBe(CopyStatus.InService);

    [Fact]
    public void Acquire_WhenTheDecisionSaysSo_HoldsItBackFromTheOutset()
        => ACopy(referenceOnly: true).Status.ShouldBe(CopyStatus.ReferenceOnly);

    [Fact]
    public void Acquire_RaisesTheEventThatSaysSo()
    {
        var copy = ACopy();

        var acquired = copy.Event<CopyAcquired>();
        acquired.CopyId.ShouldBe(copy.Id);
        acquired.EditionId.ShouldBe(copy.EditionId);
        acquired.Barcode.ShouldBe(ABarcode());
    }

    // --- Reshelving, relabelling, recording condition --------------------------------------------

    [Fact]
    public void Reshelve_MovesTheCopyAndCarriesBothPlaces()
    {
        var copy = ACopy().Settled();

        copy.Reshelve(AShelfmark("JEUN 843.912 SAI")).HasErrors().ShouldBeFalse();

        copy.Shelfmark.ShouldBe(AShelfmark("JEUN 843.912 SAI"));

        // Both are carried so a projection keyed on the shelfmark can retract the old one.
        var reshelved = copy.Event<CopyReshelved>();
        reshelved.PreviousShelfmark.ShouldBe(AShelfmark());
        reshelved.NewShelfmark.ShouldBe(AShelfmark("JEUN 843.912 SAI"));
    }

    [Fact]
    public void Reshelve_ToWhereItAlreadyStands_RecordsNothing()
    {
        // Nothing happened, and an event saying otherwise would send a projection to replace an
        // entry with itself.
        var copy = ACopy().Settled();

        copy.Reshelve(AShelfmark()).HasErrors().ShouldBeFalse();

        copy.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Relabel_ReplacesTheLabelAndKeepsTheIdentity()
    {
        var copy = ACopy().Settled();
        var identity = copy.Id;

        copy.Relabel(ABarcode("30124000999")).HasErrors().ShouldBeFalse();

        // The whole reason the barcode is not the identity: a replaced label must not produce a
        // different copy, or every loan ever made against the old one points at nothing.
        copy.Id.ShouldBe(identity);
        copy.Barcode.ShouldBe(ABarcode("30124000999"));
        copy.Event<CopyRelabelled>().PreviousBarcode.ShouldBe(ABarcode());
    }

    [Fact]
    public void RecordCondition_RecordsItWithoutTouchingTheStatus()
    {
        // Two axes, not one: most of a public library's stock is worn and perfectly lendable.
        var copy = ACopy().Settled();

        copy.RecordCondition(CopyCondition.Worn).HasErrors().ShouldBeFalse();

        copy.Condition.ShouldBe(CopyCondition.Worn);
        copy.Status.ShouldBe(CopyStatus.InService);
        copy.Event<CopyConditionRecorded>().PreviousCondition.ShouldBe(CopyCondition.Good);
    }

    [Fact]
    public void RecordCondition_ToWhatItAlreadyIs_RecordsNothing()
    {
        var copy = ACopy().Settled();

        copy.RecordCondition(CopyCondition.Good).HasErrors().ShouldBeFalse();

        copy.DomainEvents.ShouldBeEmpty();
    }

    // --- Repair, and where it comes back to ------------------------------------------------------

    [Fact]
    public void SendForRepair_TakesTheCopyOutOfTheLendableStock()
    {
        var copy = ACopy().Settled();

        copy.SendForRepair().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.InRepair);
        copy.MayBeLent().ShouldBeFalse();
        copy.Event<CopySentForRepair>().CopyId.ShouldBe(copy.Id);
    }

    [Fact]
    public void ReturnFromRepair_PutsAnInServiceCopyBackInService()
    {
        var copy = ACopy();
        copy.SendForRepair();
        copy.ClearDomainEvents();

        copy.ReturnFromRepair().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.InService);
        copy.Event<CopyReturnedFromRepair>().Status.ShouldBe(CopyStatus.InService);
    }

    [Fact]
    public void ReturnFromRepair_PutsAReferenceOnlyCopyBackToReferenceOnly()
    {
        // The accident ReturnsTo exists to prevent. Returning everything to service would quietly
        // release the library's only copy of something into the lending stock, and nobody would
        // notice until it left the building.
        var copy = ACopy(referenceOnly: true);
        copy.SendForRepair();

        copy.ReturnFromRepair().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.ReferenceOnly);
        copy.MayBeLent().ShouldBeFalse();
    }

    [Fact]
    public void SendForRepair_ACopyAlreadyInRepair_IsRefused()
    {
        var copy = ACopy();
        copy.SendForRepair();

        copy.SendForRepair().HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void ReturnFromRepair_ACopyThatNeverLeft_IsRefused()
        => ACopy().ReturnFromRepair().HasErrors().ShouldBeTrue();

    [Fact]
    public void SendForRepair_ALostCopy_IsRefused()
    {
        // Nothing can be sent for repair before it is found.
        var copy = ACopy();
        copy.DeclareLost();

        copy.SendForRepair().HasErrors().ShouldBeTrue();
    }

    // --- Between the lending and reference collections --------------------------------------------

    [Fact]
    public void RestrictToReference_HoldsTheCopyBackFromLending()
    {
        var copy = ACopy().Settled();

        copy.RestrictToReference().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.ReferenceOnly);
        copy.MayBeLent().ShouldBeFalse();
        copy.Event<CopyRestrictedToReference>().CopyId.ShouldBe(copy.Id);
    }

    [Fact]
    public void ReleaseForLending_ReturnsItToTheLendingStock()
    {
        var copy = ACopy(referenceOnly: true).Settled();

        copy.ReleaseForLending().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.InService);
        copy.MayBeLent().ShouldBeTrue();
    }

    [Fact]
    public void RestrictToReference_ACopyInRepair_IsRefused()
    {
        // A copy in repair belongs to ReturnsTo at this point, and changing a repair's destination
        // halfway through is a different operation nobody has asked for.
        var copy = ACopy();
        copy.SendForRepair();

        copy.RestrictToReference().HasErrors().ShouldBeTrue();
    }

    // --- Losing a copy, and finding it ------------------------------------------------------------

    [Fact]
    public void DeclareLost_TakesTheCopyOutOfTheLendableStock()
    {
        var copy = ACopy().Settled();

        copy.DeclareLost().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.Lost);
        copy.MayBeLent().ShouldBeFalse();
        copy.Event<CopyDeclaredLost>().CopyId.ShouldBe(copy.Id);
    }

    [Fact]
    public void Find_PutsALostCopyBackInService()
    {
        // What makes Lost differ in kind from Withdrawn. Copies turn up, and a model whose only
        // route out of a loss is someone editing the database teaches its users to distrust it.
        var copy = ACopy();
        copy.DeclareLost();
        copy.ClearDomainEvents();

        copy.Find().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.InService);
        copy.Event<CopyFound>().Status.ShouldBe(CopyStatus.InService);
    }

    [Fact]
    public void Find_ReturnsAReferenceOnlyCopy_ToTheReferenceCollection()
    {
        // The loss remembers exactly as the repair does: the 1908 volume mislaid behind a shelf
        // must not rejoin the lending stock because whoever found it forgot to say otherwise.
        var copy = ACopy(referenceOnly: true);
        copy.DeclareLost();

        copy.Find().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.ReferenceOnly);
    }

    [Fact]
    public void Find_ReturnsACopyLostFromRepair_ToTheRepairsOwnDestination()
    {
        // Lost while in repair, the copy keeps the destination the repair had recorded — a
        // reference-only volume that vanished at the binder's comes back reference-only.
        var copy = ACopy(referenceOnly: true);
        copy.SendForRepair();
        copy.DeclareLost();

        copy.Find().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.ReferenceOnly);
    }

    [Fact]
    public void Find_ACopyNobodyLost_IsRefused()
        => ACopy().Find().HasErrors().ShouldBeTrue();

    [Fact]
    public void DeclareLost_ACopyAlreadyLost_RecordsNothingAndDoesNotFail()
    {
        // A nightly scan and a librarian can reach the same conclusion; saying it twice is saying
        // it once.
        var copy = ACopy();
        copy.DeclareLost();
        copy.ClearDomainEvents();

        copy.DeclareLost().HasErrors().ShouldBeFalse();

        copy.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void DeclareLost_KeepsWhereARepairWasHeaded()
    {
        // This test once asserted the opposite — the loss forgot the repair's destination — and
        // that forgetting was the defect: the memory exists to survive exactly this detour.
        var copy = ACopy(referenceOnly: true);
        copy.SendForRepair();

        copy.DeclareLost();

        copy.ReturnsTo.ShouldBe(CopyStatus.ReferenceOnly);
    }

    // --- Withdrawing, which is terminal -----------------------------------------------------------

    [Fact]
    public void Withdraw_RemovesTheCopyFromTheCollection()
    {
        var copy = ACopy().Settled();

        copy.Withdraw().HasErrors().ShouldBeFalse();

        copy.Status.ShouldBe(CopyStatus.Withdrawn);
        copy.MayBeLent().ShouldBeFalse();
        copy.Event<CopyWithdrawn>().CopyId.ShouldBe(copy.Id);
    }

    [Theory]
    [InlineData("reshelve")]
    [InlineData("relabel")]
    [InlineData("recondition")]
    [InlineData("repair")]
    [InlineData("restrict")]
    [InlineData("lose")]
    public void AWithdrawnCopy_RefusesEverythingThatWouldPutItBack(string operation)
    {
        // Terminal. A copy that came back would be an accession, not an undo.
        var copy = ACopy();
        copy.Withdraw();

        var outcome = operation switch
        {
            "reshelve" => copy.Reshelve(AShelfmark("JEUN 843.912 SAI")),
            "relabel" => copy.Relabel(ABarcode("30124000999")),
            "recondition" => copy.RecordCondition(CopyCondition.Damaged),
            "repair" => copy.SendForRepair(),
            "restrict" => copy.RestrictToReference(),
            _ => copy.DeclareLost(),
        };

        outcome.HasErrors().ShouldBeTrue();
        copy.Status.ShouldBe(CopyStatus.Withdrawn);
    }

    [Fact]
    public void Withdraw_ACopyAlreadyWithdrawn_RecordsNothingAndDoesNotFail()
    {
        var copy = ACopy();
        copy.Withdraw();
        copy.ClearDomainEvents();

        copy.Withdraw().HasErrors().ShouldBeFalse();

        copy.DomainEvents.ShouldBeEmpty();
    }

    // --- Repointing after a merge ------------------------------------------------------------------

    [Fact]
    public void RepointTo_FilesTheCopyUnderTheSurvivingRecordAndCarriesBoth()
    {
        var copy = ACopy().Settled();
        var absorbed = copy.EditionId;
        var surviving = EditionId.Generate();

        copy.RepointTo(surviving);

        copy.EditionId.ShouldBe(surviving);

        // Both, for the reason a relabelling carries both labels: a ledger counting copies per
        // edition has to subtract before it adds.
        var repointed = copy.Event<CopyRepointed>();
        repointed.CopyId.ShouldBe(copy.Id);
        repointed.PreviousEditionId.ShouldBe(absorbed);
        repointed.NewEditionId.ShouldBe(surviving);
    }

    [Fact]
    public void RepointTo_TheRecordItAlreadyNames_RecordsNothing()
    {
        // The ordinary shape of a redelivered announcement, and it must not raise an event telling a
        // projection to replace an entry with itself.
        var copy = ACopy().Settled();

        copy.RepointTo(copy.EditionId);

        copy.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("withdrawn")]
    [InlineData("lost")]
    public void RepointTo_ACopyThatLeftTheShelf_MovesAllTheSame(string state)
    {
        // The one mutator that ignores the status, deliberately. The others are acts upon the
        // object's disposition, which a copy out of the collection no longer has; this is not an act
        // upon the object at all, and a copy left pointing at a record that stopped answering is
        // exactly the orphan the merge is announced to repair.
        var copy = ACopy(referenceOnly: true);

        if (state == "withdrawn")
        {
            copy.Withdraw();
        }
        else
        {
            copy.DeclareLost();
        }

        var wasReturningTo = copy.ReturnsTo;
        var surviving = EditionId.Generate();
        copy.Settled();

        copy.RepointTo(surviving);

        copy.EditionId.ShouldBe(surviving);
        copy.Event<CopyRepointed>().NewEditionId.ShouldBe(surviving);

        // And it touches nothing else: where the copy stands in its own life is not what changed.
        copy.Status.ShouldBe(state == "withdrawn" ? CopyStatus.Withdrawn : CopyStatus.Lost);
        copy.ReturnsTo.ShouldBe(wasReturningTo);
    }

    [Fact]
    public void RepointTo_DemandsARecordToPointAt()
        => Should.Throw<ArgumentNullException>(() => ACopy().RepointTo(null!));

    // --- Lendability ------------------------------------------------------------------------------

    [Fact]
    public void MayBeLent_IsTrueForOneStatusAndOnlyOne()
    {
        // Lendability, never availability: whether the copy is out on loan is Circulation's fact,
        // and this context has no opinion on it.
        ACopy().MayBeLent().ShouldBeTrue();

        var reference = ACopy(referenceOnly: true);
        reference.MayBeLent().ShouldBeFalse();

        var inRepair = ACopy();
        inRepair.SendForRepair();
        inRepair.MayBeLent().ShouldBeFalse();

        var lost = ACopy();
        lost.DeclareLost();
        lost.MayBeLent().ShouldBeFalse();

        var withdrawn = ACopy();
        withdrawn.Withdraw();
        withdrawn.MayBeLent().ShouldBeFalse();
    }
}
