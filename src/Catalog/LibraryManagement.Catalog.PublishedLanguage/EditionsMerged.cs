namespace LibraryManagement.Catalog.PublishedLanguage;

/// <summary>
/// Two records for one edition became one. Whoever holds the absorbed identifier should now hold
/// the surviving one.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence. Delivery across a module boundary is at-least-once, and this is
/// what a subscriber deduplicates by.
/// </param>
/// <param name="AbsorbedEditionId">The identifier that stops naming a record of its own.</param>
/// <param name="SurvivingEditionId">The identifier to hold instead.</param>
/// <remarks>
/// <para>
/// <strong>The first thing this context announces to another.</strong> Catalog has been upstream of
/// everything and silent until now — Holdings asks it a question through
/// <see cref="IEditionCatalog"/> and is told nothing on its own initiative. This is the fact that
/// had to become an announcement, because no database constraint crosses a schema: nothing else can
/// carry the news that an identifier three other contexts are holding has stopped meaning what it
/// meant.
/// </para>
/// <para>
/// <strong>A fact, not an instruction.</strong> It says two records became one. What that costs is
/// the consumer's own affair, and it differs sharply between them: Holdings repoints a column,
/// while Circulation — which keys a whole aggregate by this identifier — has two queues to make
/// into one. Naming this <c>RepointYourCopies</c> would have put a Holdings rule in Catalog's
/// vocabulary and said nothing true about the harder consumer.
/// </para>
/// <para>
/// <strong>Two identifiers and nothing else.</strong> The absorbed record's ISBN, its work, the
/// reason a cataloger judged it a duplicate — none of that is a consumer's business, and a contract
/// that offered them would invite one to find a use for them and then depend on it.
/// </para>
/// <para>
/// <strong>Merging authority records does not appear here</strong>, and the asymmetry is
/// deliberate: no module outside Catalog holds an <c>AuthorId</c>, so that merge changes nothing
/// beyond this boundary and needs no contract. What crosses is decided by who holds the identifier,
/// never by what looks symmetrical.
/// </para>
/// </remarks>
public sealed record EditionsMerged(Guid EventId, Guid AbsorbedEditionId, Guid SurvivingEditionId);
