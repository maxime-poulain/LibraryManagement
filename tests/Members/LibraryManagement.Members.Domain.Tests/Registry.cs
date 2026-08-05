using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Domain.Tests;

/// <summary>
/// The membership file these tests work from. Named for what a library keeps, so a test reads as
/// a sentence about members rather than as a sequence of constructions.
/// </summary>
internal static class Registry
{
    /// <summary>The day these tests stand on. Enrollments start here.</summary>
    internal static DateOnly Today => new(2026, 3, 14);

    internal static DateOnly ABirthDate => new(1990, 5, 1);

    internal static MemberName AName(string given = "Antoine", string family = "Doinel") =>
        MemberName.Create(given, family)
            .Match(name => name, _ => throw new InvalidOperationException($"{given} {family}"));

    internal static CardNumber ACardNumber(string value = "20260000512") =>
        CardNumber.Create(value).Match(number => number, _ => throw new InvalidOperationException(value));

    internal static ContactDetails AContact(
        string? email = "antoine.doinel@example.org",
        string? phone = null,
        string? postalAddress = null) =>
        ContactDetails.Create(email, phone, postalAddress)
            .Match(contact => contact, _ => throw new InvalidOperationException());

    internal static Guardian AGuardian(string given = "Gilberte", string family = "Doinel") =>
        Guardian.Of(AName(given, family), AContact("gilberte.doinel@example.org"));

    /// <summary>An adult member enrolled today, which is what most of a membership file is.</summary>
    internal static Member AMember(
        MemberCategory category = MemberCategory.Adult,
        Guardian? guardian = null,
        CardNumber? cardNumber = null,
        DateOnly? dateOfBirth = null)
        => Member.Enroll(
                MemberId.Generate(),
                AName(),
                dateOfBirth ?? ABirthDate,
                category,
                cardNumber ?? ACardNumber(),
                AContact(),
                guardian,
                Today)
            .Match(member => member, _ => throw new InvalidOperationException(
                "Registry.AMember built a member the aggregate refused."));

    /// <summary>Clears what enrolling raised, so a test asserts only on what it caused itself.</summary>
    internal static Member Settled(this Member member)
    {
        member.ClearDomainEvents();
        return member;
    }

    internal static T Event<T>(this Member member) => member.DomainEvents.OfType<T>().Single();
}
