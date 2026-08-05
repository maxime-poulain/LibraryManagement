using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;

namespace LibraryManagement.Circulation.Domain.Tests;

/// <summary>
/// The desk these tests stand at. Named for where circulation happens, so a test reads as a
/// sentence about loans and holds rather than as a sequence of constructions.
/// </summary>
internal static class Desk
{
    /// <summary>The day these tests stand on. Checkouts start here.</summary>
    internal static DateOnly Today => new(2026, 3, 14);

    /// <summary>An instant on that day, for the placements whose order is the queue.</summary>
    internal static DateTimeOffset ThisMorning => new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    internal static CirculationPolicy Policy => CirculationPolicy.Current;

    /// <summary>An active loan started today, which is what most of a desk's day produces.</summary>
    internal static Loan ALoan(
        BorrowerId? borrowerId = null,
        CopyId? copyId = null,
        EditionId? editionId = null)
        => Loan.CheckOut(
            LoanId.Generate(),
            copyId ?? CopyId.Generate(),
            editionId ?? EditionId.Generate(),
            borrowerId ?? BorrowerId.Generate(),
            Today,
            Policy);

    /// <summary>An empty queue for a fresh edition.</summary>
    internal static HoldQueue AQueue(EditionId? editionId = null)
        => HoldQueue.For(editionId ?? EditionId.Generate());

    /// <summary>Clears what earlier acts raised, so a test asserts only on what it caused itself.</summary>
    internal static Loan Settled(this Loan loan)
    {
        loan.ClearDomainEvents();
        return loan;
    }

    /// <summary>Clears what earlier acts raised, so a test asserts only on what it caused itself.</summary>
    internal static HoldQueue Settled(this HoldQueue queue)
    {
        queue.ClearDomainEvents();
        return queue;
    }

    internal static T Event<T>(this Loan loan) => loan.DomainEvents.OfType<T>().Single();

    internal static T Event<T>(this HoldQueue queue) => queue.DomainEvents.OfType<T>().Single();
}
