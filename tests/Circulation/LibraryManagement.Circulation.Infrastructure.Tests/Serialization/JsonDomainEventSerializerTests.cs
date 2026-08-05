using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.Outbox;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Serialization;

/// <summary>
/// The serializer against this module's real events — the ones the outbox will actually store.
/// </summary>
/// <remarks>
/// Built with no converters at all, which is itself the assertion: this module's events carry
/// identifiers, dates and enums, all of which the shared serializer already speaks — the day an
/// event grows a value object, this constructor call is the first thing that fails.
/// </remarks>
public sealed class JsonDomainEventSerializerTests
{
    private static JsonDomainEventSerializer Serializer() => new([]);

    private static IDomainEvent RoundTrip(IDomainEvent domainEvent)
    {
        var serializer = Serializer();
        var stored = serializer.Serialize(domainEvent);
        return serializer.Deserialize(stored.Type, stored.Payload);
    }

    [Fact]
    public void ACheckout_RoundTrips()
    {
        var original = new LoanCheckedOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Generate(),
            BorrowerId.Generate(),
            new DateOnly(2026, 4, 4));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AReturn_RoundTripsItsLateness()
    {
        var original = new LoanReturned(
            LoanId.Generate(), CopyId.Generate(), BorrowerId.Generate(), DaysLate: 6);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARenewal_RoundTripsItsNewDueDate()
    {
        var original = new RenewalGranted(LoanId.Generate(), new DateOnly(2026, 4, 25));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ALossDeclaration_RoundTrips()
    {
        var original = new LoanDeclaredLost(
            LoanId.Generate(), CopyId.Generate(), BorrowerId.Generate());

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void APlacedHold_RoundTrips()
    {
        var original = new HoldPlaced(EditionId.Generate(), HoldId.Generate(), BorrowerId.Generate());

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ATrappedHold_RoundTripsItsDeadline()
    {
        var original = new HoldReadyForPickup(
            EditionId.Generate(),
            HoldId.Generate(),
            BorrowerId.Generate(),
            CopyId.Generate(),
            new DateOnly(2026, 3, 21));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AFulfilledHold_RoundTrips()
    {
        var original = new HoldFulfilled(
            EditionId.Generate(), HoldId.Generate(), BorrowerId.Generate(), CopyId.Generate());

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ACancelledHold_RoundTrips()
    {
        var original = new HoldCancelled(
            EditionId.Generate(), HoldId.Generate(), BorrowerId.Generate());

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void TheTypeName_CarriesNoVersion()
    {
        // The stored name is the address deserialization dials, and an address that included the
        // assembly version would orphan every row at the next version bump.
        var stored = Serializer().Serialize(
            new HoldPlaced(EditionId.Generate(), HoldId.Generate(), BorrowerId.Generate()));

        stored.Type.ShouldBe(
            "LibraryManagement.Circulation.Domain.Holds.HoldPlaced, LibraryManagement.Circulation.Domain");
    }
}
