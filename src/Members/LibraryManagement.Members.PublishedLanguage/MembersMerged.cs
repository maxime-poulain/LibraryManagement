namespace LibraryManagement.Members.PublishedLanguage;

/// <summary>
/// Two records for one person became one. Whoever holds the absorbed identifier should now hold the
/// surviving one.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence. Delivery across a module boundary is at-least-once, and this is
/// what a subscriber deduplicates by.
/// </param>
/// <param name="AbsorbedMemberId">The identifier that stops naming a person of its own.</param>
/// <param name="SurvivingMemberId">The identifier to hold instead.</param>
/// <remarks>
/// <para>
/// <strong>The first thing this context announces.</strong> Members has been asked questions and has
/// answered them through <see cref="IMemberEntitlement"/>; it has stated nothing on its own
/// initiative until now. This is the fact that had to become an announcement, because no database
/// constraint crosses a schema: nothing else can carry the news that an identifier two other
/// contexts are holding has stopped meaning what it meant.
/// </para>
/// <para>
/// <strong>A contract of its own and not Catalog's.</strong> <c>EditionsMerged</c> carries the same
/// two shapes, and sharing one record between them is the false economy the published languages
/// exist to prevent: they are published by different modules, consumed by different subscribers, and
/// mean different things. One contract would make Members and Catalog change together forever.
/// </para>
/// <para>
/// <strong>Two identifiers and nothing else.</strong> Which record a member of staff judged the
/// better one, and why, is no consumer's business — and a contract offering a name would put a
/// person's name into two more schemas, which is exactly what this boundary bought by refusing it.
/// </para>
/// </remarks>
public sealed record MembersMerged(Guid EventId, Guid AbsorbedMemberId, Guid SurvivingMemberId);
