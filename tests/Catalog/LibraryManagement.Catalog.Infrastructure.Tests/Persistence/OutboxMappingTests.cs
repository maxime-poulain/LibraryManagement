using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Persistence;

/// <summary>
/// The outbox table as the store actually enforces it.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class OutboxMappingTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OutboxMessage ARow(Guid eventId) => new()
    {
        EventId = eventId,
        Type = "LibraryManagement.Catalog.Domain.Authors.AuthorRegistered, LibraryManagement.Catalog.Domain",
        Payload = "{}",
        OccurredOn = DateTimeOffset.UtcNow,
        StoredOn = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task TheSameOccurrence_CannotBeStoredTwice()
    {
        // The unique index on EventId. The interceptor clears events after writing them so the
        // ordinary path never collides, but at-least-once machinery deserves a belt as well as
        // braces — a duplicate here would be delivered twice by design rather than by accident.
        var eventId = Guid.CreateVersion7();

        await using var context = sqlServer.NewContext();
        context.Set<OutboxMessage>().Add(ARow(eventId));
        await context.SaveChangesAsync(Token);

        context.Set<OutboxMessage>().Add(ARow(eventId));

        await Should.ThrowAsync<DbUpdateException>(async () => await context.SaveChangesAsync(Token));
    }
}
