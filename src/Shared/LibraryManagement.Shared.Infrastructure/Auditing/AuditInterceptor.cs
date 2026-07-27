using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LibraryManagement.Shared.Infrastructure.Auditing;

/// <summary>
/// Stamps when a row was written and by whom, on the way into the store.
/// </summary>
/// <param name="clock">Reads the instant to stamp.</param>
/// <param name="currentUser">Names the staff member to attribute the write to, if there is one.</param>
/// <remarks>
/// <para>
/// <see cref="Entity{TEntityId}"/> has carried <see cref="IAuditable"/> from the start, so the four
/// columns already existed and were already mapped — and were written as
/// <c>0001-01-01T00:00:00+00:00</c> on every row, because nothing filled them. This fills them.
/// </para>
/// <para>
/// An interceptor rather than a base-class hook or a repository call, because the change tracker
/// already knows which rows are being inserted and which updated, and asking a handler to remember
/// makes forgetting possible. The domain keeps declaring the properties and never assigns them:
/// audit metadata is a fact about the write, not about the business, and no rule ever reads it.
/// </para>
/// <para>
/// Runs in <c>SavingChanges</c>, after <see cref="DomainEvents.DomainEventInterceptor"/>. Order is
/// the guarantee, not a preference: a domain event handler is entitled to change another aggregate,
/// and those changes are still untracked when the first interceptor starts. Stamping first would
/// leave them with no audit at all.
/// </para>
/// <para>
/// The clock is a <see cref="TimeProvider"/> rather than <c>DateTimeOffset.UtcNow</c> so a test can
/// pin the instant and assert on it, and the values are <see cref="DateTimeOffset"/> rather than
/// <see cref="DateTime"/> because an instant without its offset is ambiguous the moment a second
/// site, a second time zone or a daylight saving change enters the picture.
/// </para>
/// </remarks>
public sealed class AuditInterceptor(TimeProvider clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overridden too, unlike the domain event interceptor's. Stamping is synchronous, so the
    /// synchronous path costs nothing to support and leaving it out would mean a
    /// <c>SaveChanges</c> that quietly wrote rows with no audit at all.
    /// </remarks>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        var now = clock.GetUtcNow();
        var author = currentUser.Identifier;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedOn)).CurrentValue = now;
                    Attribute(entry.Property(nameof(IAuditable.CreatedBy)), author);
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.ModifiedOn)).CurrentValue = now;
                    Attribute(entry.Property(nameof(IAuditable.ModifiedBy)), author);

                    // The creation columns were settled once and stay settled. A materialised entity
                    // already carries what the store holds, so an update that resent them could only
                    // ever rewrite history — never correct it.
                    entry.Property(nameof(IAuditable.CreatedOn)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;

                default:
                    // Unchanged, Deleted and Detached are left alone. A deleted row is about to stop
                    // existing, and recording who last touched it on the way out would be a fact
                    // nobody can read afterwards.
                    break;
            }
        }
    }

    // Left untouched when nobody is acting, rather than filled with a stand-in. The property keeps
    // whatever the domain declared as its default, and that emptiness is the honest reading: this
    // write is not attributable to a person.
    private static void Attribute(
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property,
        string? author)
    {
        if (author is not null)
        {
            property.CurrentValue = author;
        }
    }
}
