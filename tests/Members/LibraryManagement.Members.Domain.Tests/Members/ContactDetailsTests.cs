using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Domain.Tests.Members;

public sealed class ContactDetailsTests
{
    [Fact]
    public void Create_WithNoChannelAtAll_Succeeds()
    {
        // The legitimate state the work list exists for, not an edge case to be validated away.
        var contact = ContactDetails.Create()
            .Match(value => value, _ => throw new InvalidOperationException());

        contact.Email.ShouldBeNull();
        contact.Phone.ShouldBeNull();
        contact.PostalAddress.ShouldBeNull();
        contact.ShouldBe(ContactDetails.None);
    }

    [Fact]
    public void Create_TreatsABlankChannelAsNoChannel()
    {
        var contact = ContactDetails.Create("   ", "", null)
            .Match(value => value, _ => throw new InvalidOperationException());

        contact.ShouldBe(ContactDetails.None);
    }

    [Fact]
    public void Create_TrimsEachChannel()
    {
        var contact = ContactDetails.Create("  a@b.example  ", " 0612345678 ", "  12 rue de la Paix ")
            .Match(value => value, _ => throw new InvalidOperationException());

        contact.Email.ShouldBe("a@b.example");
        contact.Phone.ShouldBe("0612345678");
        contact.PostalAddress.ShouldBe("12 rue de la Paix");
    }

    [Theory]
    [InlineData("not-an-email-at-all")]
    [InlineData("téléphone rouge")]
    public void Create_DoesNotParseAChannel(string value)
    {
        // Whether a channel works is an operational fact the day a message is sent, not a modeling
        // one the day it is recorded.
        ContactDetails.Create(value, value, value).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public void Create_AChannelPastItsBound_Fails()
    {
        ContactDetails.Create(new string('a', ContactDetails.MaxEmailLength + 1))
            .HasErrors().ShouldBeTrue();
        ContactDetails.Create(phone: new string('1', ContactDetails.MaxPhoneLength + 1))
            .HasErrors().ShouldBeTrue();
        ContactDetails.Create(postalAddress: new string('a', ContactDetails.MaxPostalAddressLength + 1))
            .HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Create_ReportsEveryOverrunAtOnce()
        => ContactDetails.Create(
                new string('a', ContactDetails.MaxEmailLength + 1),
                new string('1', ContactDetails.MaxPhoneLength + 1))
            .Match(_ => 0, errors => errors.Count)
            .ShouldBe(2);

    [Fact]
    public void TwoSetsOfChannels_AreEqualByValue()
    {
        static ContactDetails WithEmail() => ContactDetails.Create("a@b.example")
            .Match(contact => contact, _ => throw new InvalidOperationException());

        WithEmail().ShouldBe(WithEmail());
        WithEmail().ShouldNotBe(ContactDetails.None);
    }
}
