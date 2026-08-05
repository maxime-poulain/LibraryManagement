using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// The ways a person can be reached. Every channel is optional, and a member with none is
/// legitimate.
/// </summary>
/// <remarks>
/// <para>
/// Not an edge case to be validated away: the notification design counts three outcomes, and
/// <em>no channel available</em> is the third — it surfaces a work list for staff to phone or
/// write, and this type's job is to make that state representable rather than to forbid it.
/// </para>
/// <para>
/// <strong>The model does not parse a channel.</strong> An address that looks right still bounces
/// and one that looks wrong still delivers; whether a channel works is an operational fact the day
/// a message is sent, not a modeling one the day it is recorded. Length bounds are the whole of
/// the validation, for the reason the card number gives.
/// </para>
/// </remarks>
public sealed class ContactDetails : ValueObject<ContactDetails>
{
    /// <summary>The greatest number of characters an email address may run to.</summary>
    /// <remarks>The bound the mail RFCs put on an address, kept rather than invented.</remarks>
    public const int MaxEmailLength = 254;

    /// <summary>The greatest number of characters a phone number may run to.</summary>
    /// <remarks>Room for an international prefix, an extension and the spacing people type.</remarks>
    public const int MaxPhoneLength = 32;

    /// <summary>The greatest number of characters a postal address may run to.</summary>
    public const int MaxPostalAddressLength = 256;

    /// <summary>No channel at all — the state the work list exists for.</summary>
    public static readonly ContactDetails None = new(null, null, null);

    private ContactDetails(string? email, string? phone, string? postalAddress)
    {
        Email = email;
        Phone = phone;
        PostalAddress = postalAddress;
    }

    /// <summary>Gets the email address, or <see langword="null"/> when the member has none.</summary>
    public string? Email { get; }

    /// <summary>Gets the phone number, or <see langword="null"/> when the member has none.</summary>
    public string? Phone { get; }

    /// <summary>Gets the postal address, or <see langword="null"/> when the member has none.</summary>
    public string? PostalAddress { get; }

    /// <summary>
    /// Creates contact details, trimming each channel and reporting every problem at once. A blank
    /// channel is no channel.
    /// </summary>
    /// <param name="email">The email address, if any.</param>
    /// <param name="phone">The phone number, if any.</param>
    /// <param name="postalAddress">The postal address, if any.</param>
    /// <returns>The contact details, or the reasons they are not any.</returns>
    public static Result<ContactDetails> Create(
        string? email = null,
        string? phone = null,
        string? postalAddress = null)
    {
        var errors = new ErrorCollection();

        var trimmedEmail = CheckChannel(email, "email address", MaxEmailLength, errors);
        var trimmedPhone = CheckChannel(phone, "phone number", MaxPhoneLength, errors);
        var trimmedAddress = CheckChannel(
            postalAddress, "postal address", MaxPostalAddressLength, errors);

        return errors.Count > 0
            ? Result<ContactDetails>.Failure(errors)
            : Result<ContactDetails>.Success(
                new ContactDetails(trimmedEmail, trimmedPhone, trimmedAddress));
    }

    private static string? CheckChannel(
        string? value,
        string label,
        int maxLength,
        ErrorCollection errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            errors.Add(
                MembersErrorCodes.InvalidContactDetails,
                $"A {label} may not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Email;
        yield return Phone;
        yield return PostalAddress;
    }
}
