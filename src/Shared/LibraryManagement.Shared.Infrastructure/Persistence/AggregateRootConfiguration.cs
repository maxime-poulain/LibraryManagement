using LibraryManagement.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Shared.Infrastructure.Persistence;

/// <summary>
/// Maps what every aggregate root has in common, and hands the rest to the module.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root being mapped.</typeparam>
/// <typeparam name="TId">Its identifier.</typeparam>
/// <remarks>
/// <para>
/// Four lines were being copied into every configuration, and three of them fail quietly when
/// forgotten. A missing <c>Ignore</c> on the domain events is the loud one — Entity Framework tries
/// to map a list of interfaces and says so. The other two are the reason this class exists: an
/// identifier left to the store's own generation, and above all a <c>rowversion</c> that was never
/// declared as one. That last omission produces a model that builds, a schema that looks right and a
/// table with a column nobody checks, so two employees editing the same record simply overwrite each
/// other and the catalog is quietly wrong.
/// </para>
/// <para>
/// The identifier is <c>ValueGeneratedNever</c>, never <c>ValueGeneratedOnAdd</c>. Identifiers here
/// come from the domain — <see cref="EntityId{T}.Generate"/>
/// produces one before an aggregate is ever persisted — and telling the store to generate keys would
/// contradict the reason a command can report success without a round trip.
/// </para>
/// <para>
/// Nothing here checks whether the aggregate has domain events or a concurrency token: the type
/// constraint already settled both. A configuration reaching this class is a configuration of an
/// <see cref="AggregateRoot{TEntityId}"/>, and a compiler saying so is worth more than a
/// <c>IsAssignableTo</c> deciding it again at model-building time.
/// </para>
/// <para>
/// The four audit columns are declared here, and they have to be declared somewhere explicitly:
/// <see cref="IAuditable.CreatedOn"/> and its three companions have no setter, and Entity Framework
/// discovers a property by its setter. Left alone it does not see them, does not map them, and
/// <see cref="Auditing.AuditInterceptor"/> fails on the first save with <c>The property
/// 'Author.CreatedOn' could not be found</c>. The missing setter is not an oversight — one would
/// offer an aggregate a way to lie about when it was written — so the mapping is what has to make up
/// for it. Writing goes through the compiler-generated backing field, which Entity Framework resolves
/// on its own.
/// </para>
/// <para>
/// <see cref="ConfigureAggregate"/> runs last, so a module can still say anything it likes about a
/// property this class already touched.
/// </para>
/// </remarks>
public abstract class AggregateRootConfiguration<TAggregate, TId> : IEntityTypeConfiguration<TAggregate>
    where TAggregate : AggregateRoot<TId>
    where TId : EntityId<TId>, IEntityId<TId>
{
    /// <summary>
    /// The fractional precision of the audit instants: milliseconds.
    /// </summary>
    /// <remarks>
    /// The granularity this solution already works in. <see cref="EntityId{T}.Generate"/> records
    /// that a version 7 identifier orders to the millisecond and no further, so an audit column of
    /// the same resolution says exactly as much as the identifier beside it and no more. Left
    /// undeclared the column would be a <c>datetimeoffset(7)</c>, offering a hundred nanoseconds of
    /// precision that neither the clock nor the business has any use for.
    /// </remarks>
    public const int AuditPrecision = 3;

    /// <summary>
    /// The longest staff identifier an audit column will hold.
    /// </summary>
    /// <remarks>
    /// Generous rather than measured, because nothing issues one yet: Staff Access might hand out an
    /// employee number, a login, or the string form of an identifier. What it buys is a column that
    /// is not <c>nvarchar(max)</c>, which cannot be indexed and which no audit column deserves.
    /// </remarks>
    public const int AuthorLength = 128;

    /// <summary>
    /// Maps what belongs to this aggregate alone: its table, its members, its indexes.
    /// </summary>
    /// <param name="builder">The builder for the aggregate.</param>
    protected abstract void ConfigureAggregate(EntityTypeBuilder<TAggregate> builder);

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TAggregate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(aggregate => aggregate.Id);

        builder.Property(aggregate => aggregate.Id)
            .HasConversion(id => id.Value, value => EntityId<TId>.Create(value))
            .ValueGeneratedNever();

        builder.Property(aggregate => aggregate.RowVersion).IsRowVersion();

        // Written by the store and never by a business rule. Only the instant of creation is
        // required: a record that has never been changed has not been changed by anyone, and a
        // record created by nobody is the ordinary case until Staff Access exists. Saying
        // IsRequired(false) on the three would only repeat what their nullable types already declare.
        builder.Property(aggregate => aggregate.CreatedOn)
            .HasPrecision(AuditPrecision)
            .IsRequired();

        builder.Property(aggregate => aggregate.CreatedBy)
            .HasMaxLength(AuthorLength);

        builder.Property(aggregate => aggregate.ModifiedOn)
            .HasPrecision(AuditPrecision);

        builder.Property(aggregate => aggregate.ModifiedBy)
            .HasMaxLength(AuthorLength);

        // Raised in memory and published before the work is saved. They are not state, and a column
        // for them would persist an intention rather than a fact.
        builder.Ignore(aggregate => aggregate.DomainEvents);

        ConfigureAggregate(builder);
    }
}
