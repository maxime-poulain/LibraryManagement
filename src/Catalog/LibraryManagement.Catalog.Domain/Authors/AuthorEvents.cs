using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// An authority record was opened for a person.
/// </summary>
/// <param name="AuthorId">The new record.</param>
/// <param name="PreferredName">The name the catalog files the person under.</param>
/// <remarks>
/// Domain events carry the domain's own types rather than primitives. They never leave the context —
/// a handler that reacts to one is compiled against the same model. Flattening to strings and Guids
/// is what an integration event does at the boundary, and doing it here would pay that cost with
/// nothing bought.
/// </remarks>
public sealed record AuthorRegistered(AuthorId AuthorId, NameForm PreferredName) : DomainEvent;

/// <summary>
/// A person is now filed under a different name.
/// </summary>
/// <param name="AuthorId">The record that changed.</param>
/// <param name="PreviousName">The preferred name until now, kept as a variant.</param>
/// <param name="NewName">The preferred name from now on.</param>
/// <remarks>
/// Both names are carried because a reader who knew the old one must keep finding the work: the
/// search projection needs the outgoing form as much as the incoming one.
/// </remarks>
public sealed record AuthorRenamed(
    AuthorId AuthorId,
    NameForm PreviousName,
    NameForm NewName) : DomainEvent;

/// <summary>
/// A preferred name was wrong, and has been corrected.
/// </summary>
/// <param name="AuthorId">The record that changed.</param>
/// <param name="PreviousName">The form that was wrong. It stops being findable.</param>
/// <param name="CorrectedName">The preferred name from now on.</param>
/// <remarks>
/// A different statement from <see cref="AuthorRenamed"/>, and consumers must treat the two
/// differently: a rename keeps the outgoing form as a searchable variant, a correction retracts it.
/// The search projection adds an access point on the first and removes one on the second — which is
/// the whole reason these are two events rather than one with a flag.
/// </remarks>
public sealed record AuthorPreferredNameCorrected(
    AuthorId AuthorId,
    NameForm PreviousName,
    NameForm CorrectedName) : DomainEvent;

/// <summary>
/// Another form the person is known by was recorded.
/// </summary>
/// <param name="AuthorId">The record it leads back to.</param>
/// <param name="VariantName">The form.</param>
/// <remarks>
/// A variant exists to be searched by — it is the entire reason authority files record them — so the
/// search projection must learn of it the moment it is recorded, exactly as it learns of the preferred name.
/// </remarks>
public sealed record AuthorVariantNameAdded(AuthorId AuthorId, NameForm VariantName) : DomainEvent;

/// <summary>
/// The years of birth and death on record were wrong, and have been corrected.
/// </summary>
/// <param name="AuthorId">The record that changed.</param>
/// <param name="PreviousLifeYears">The years as the record had them.</param>
/// <param name="CorrectedLifeYears">The years from now on.</param>
/// <remarks>
/// A correction of the record, never a fact about the person — nobody's years change; what was
/// wrong is what the catalog said about them. Both values are carried, the shape every corrected
/// pair here takes, so a consumer keyed on the old reading can retract it. Registration is not a
/// correction: an author registered with known years announces them in no event but the opening of
/// the record, and this event marks only the repair of a reading that stood.
/// </remarks>
public sealed record AuthorLifeYearsCorrected(
    AuthorId AuthorId,
    LifeYears PreviousLifeYears,
    LifeYears CorrectedLifeYears) : DomainEvent;
