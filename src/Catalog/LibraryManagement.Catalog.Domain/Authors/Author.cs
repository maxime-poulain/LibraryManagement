using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// A person or body responsible for a <see cref="Works.Work"/> — an authority record, in the
/// profession's terms.
/// </summary>
/// <remarks>
/// <para>
/// An author is an aggregate root rather than a value copied onto every work, because a person
/// outlives the description of any one of their books and changes independently of them. A name is
/// corrected, a person marries, transitions, adopts a pen name or drops one; with one record and one
/// identifier, that is a single change. Copied onto each work, it would be a migration.
/// </para>
/// <para>
/// The model follows authority control: one <see cref="AuthorizedName"/> that the catalogue files
/// under, and <see cref="VariantNames"/> that every former or alternative form falls back to. The
/// variants are not decoration — they are what lets a search for a name someone no longer uses still
/// find their work, which is the entire reason authority files record them.
/// </para>
/// </remarks>
public sealed class Author : AggregateRoot<AuthorId>
{
    private readonly List<PersonName> _variantNames = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Author"/> class with an identity and a heading.
    /// </summary>
    /// <remarks>
    /// Takes only what identifies the record and what it cannot exist without. The years are set
    /// afterwards, by <see cref="CorrectLifeYears"/>, which is also what lets a mapper rebuild an
    /// author from the store: a complex value cannot be passed through a constructor there.
    /// </remarks>
    private Author(AuthorId id, PersonName authorizedName) : base(id)
    {
        AuthorizedName = authorizedName;
        LifeYears = LifeYears.Unknown;
    }

    /// <summary>Gets the name the catalogue files this person under.</summary>
    public PersonName AuthorizedName { get; private set; }

    /// <summary>Gets the years of birth and death, either of which may be unknown.</summary>
    public LifeYears LifeYears { get; private set; }

    /// <summary>
    /// Gets every other form the person has been known by, each of which leads back to this record.
    /// </summary>
    public IReadOnlyList<PersonName> VariantNames => _variantNames.AsReadOnly();

    /// <summary>
    /// Opens an authority record for a person.
    /// </summary>
    /// <param name="id">The identifier the record will keep for its whole life.</param>
    /// <param name="authorizedName">The name to file the person under.</param>
    /// <param name="lifeYears">The years of birth and death, or <see cref="LifeYears.Unknown"/>.</param>
    /// <returns>The new record.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// Returns an <see cref="Author"/> and not a <see cref="Result{TValue}"/>: every rule that could
    /// refuse one has already been enforced by <see cref="PersonName"/> and <see cref="LifeYears"/>,
    /// so this cannot fail. A factory that wrapped a value it can always produce would ask every
    /// caller to handle a failure that does not exist.
    /// </remarks>
    public static Author Register(AuthorId id, PersonName authorizedName, LifeYears lifeYears)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(authorizedName);
        ArgumentNullException.ThrowIfNull(lifeYears);

        var author = new Author(id, authorizedName);
        author.CorrectLifeYears(lifeYears);
        author.AddDomainEvent(new AuthorRegistered(id, authorizedName));

        return author;
    }

    /// <summary>
    /// Files the person under a new name, keeping the old one as a variant.
    /// </summary>
    /// <param name="newAuthorizedName">The name to file the person under from now on.</param>
    /// <returns>Success, or the reason the name was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="newAuthorizedName"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The previous heading becomes a variant rather than disappearing. Nothing published under it
    /// stops existing — a book printed in 1990 still bears the name on its title page — and a reader
    /// who only knows the old name must still be able to find the work.
    /// </para>
    /// <para>
    /// Renaming to a name already recorded as a variant is the ordinary case of someone returning to
    /// a former name, so it is allowed: the variant is promoted and the outgoing heading takes its
    /// place among the variants.
    /// </para>
    /// <para>
    /// This records a fact about the <em>person</em>. A heading that was simply wrong — a typo, a
    /// mistranscription — is repaired with <see cref="CorrectHeading"/> instead, which keeps
    /// nothing: the two operations differ in exactly what they leave behind.
    /// </para>
    /// </remarks>
    public Result Rename(PersonName newAuthorizedName)
    {
        ArgumentNullException.ThrowIfNull(newAuthorizedName);

        if (newAuthorizedName == AuthorizedName)
        {
            return Result.Failure(
                CatalogErrorCodes.DuplicateName,
                $"'{newAuthorizedName}' is already the authorized name.");
        }

        var previous = AuthorizedName;

        _variantNames.Remove(newAuthorizedName);
        AuthorizedName = newAuthorizedName;
        _variantNames.Add(previous);

        AddDomainEvent(new AuthorRenamed(Id, previous, newAuthorizedName));

        return Result.Success();
    }

    /// <summary>
    /// Replaces a heading that was wrong, keeping nothing of it.
    /// </summary>
    /// <param name="correctedName">The heading as it should have read.</param>
    /// <returns>Success, or the reason the correction was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="correctedName"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <see cref="Rename"/> records a fact about the person; this records a fact about the record.
    /// A typo is nobody's name: kept as a variant it would become a searchable access point, and the
    /// catalogue would preserve forever the one thing it was asked to remove. So nothing is kept,
    /// and <see cref="AuthorHeadingCorrected"/> tells consumers to retract the old form where
    /// <see cref="AuthorRenamed"/> tells them to keep it findable.
    /// </para>
    /// <para>
    /// Correcting to a form already recorded as a variant promotes it, exactly as
    /// <see cref="Rename"/> does — a correction can restore a proper form someone had filed as
    /// secondary — and the invariant that a heading is never also a variant holds either way.
    /// </para>
    /// </remarks>
    public Result CorrectHeading(PersonName correctedName)
    {
        ArgumentNullException.ThrowIfNull(correctedName);

        if (correctedName == AuthorizedName)
        {
            return Result.Failure(
                CatalogErrorCodes.DuplicateName,
                $"'{correctedName}' is already the authorized name.");
        }

        var previous = AuthorizedName;

        _variantNames.Remove(correctedName);
        AuthorizedName = correctedName;

        AddDomainEvent(new AuthorHeadingCorrected(Id, previous, correctedName));

        return Result.Success();
    }

    /// <summary>
    /// Records another form the person is known by.
    /// </summary>
    /// <param name="variantName">The alternative form.</param>
    /// <returns>Success, or the reason the name was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variantName"/> is null.</exception>
    public Result AddVariantName(PersonName variantName)
    {
        ArgumentNullException.ThrowIfNull(variantName);

        if (variantName == AuthorizedName)
        {
            return Result.Failure(
                CatalogErrorCodes.DuplicateName,
                $"'{variantName}' is the authorized name, so it cannot also be a variant.");
        }

        if (_variantNames.Contains(variantName))
        {
            return Result.Failure(
                CatalogErrorCodes.DuplicateName,
                $"'{variantName}' is already recorded as a variant.");
        }

        _variantNames.Add(variantName);
        AddDomainEvent(new AuthorVariantNameAdded(Id, variantName));

        return Result.Success();
    }

    /// <summary>
    /// Corrects the years of birth and death.
    /// </summary>
    /// <param name="lifeYears">The years to record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lifeYears"/> is null.</exception>
    public void CorrectLifeYears(LifeYears lifeYears)
    {
        ArgumentNullException.ThrowIfNull(lifeYears);

        LifeYears = lifeYears;
    }

    /// <summary>
    /// Determines whether this person is known by <paramref name="name"/> under any of its forms.
    /// </summary>
    /// <param name="name">The name to look for.</param>
    /// <returns>
    /// <see langword="true"/> when the name is the heading or one of its variants;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public bool IsKnownAs(PersonName name)
        => AuthorizedName == name || _variantNames.Contains(name);
}
