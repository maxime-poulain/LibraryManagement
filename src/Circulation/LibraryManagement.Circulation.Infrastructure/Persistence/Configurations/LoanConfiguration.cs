using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Circulation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Loan"/> to the store.
/// </summary>
public sealed class LoanConfiguration : AggregateRootConfiguration<Loan, LoanId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Loan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Loan");

        // Identifier columns, not foreign keys. They point into three other schemas, and no
        // constraint crosses a schema here — the rules those references rest on are enforced
        // where they can be explained, in the handlers.
        builder.Property(loan => loan.CopyId)
            .HasConversion(id => id.Value, value => CopyId.Create(value))
            .IsRequired();

        builder.Property(loan => loan.EditionId)
            .HasConversion(id => id.Value, value => EditionId.Create(value))
            .IsRequired();

        builder.Property(loan => loan.BorrowerId)
            .HasConversion(id => id.Value, value => BorrowerId.Create(value))
            .IsRequired();

        // One active loan per copy: two people cannot hold one object. The handler asks first so
        // the refusal can say so; this filtered index is what keeps the answer true between the
        // asking and the writing — the same division of labor as the barcode in Holdings, with
        // the filter because a copy's *returned* loans are legion and legitimate.
        builder.HasIndex(loan => loan.CopyId)
            .IsUnique()
            .HasFilter("[Status] = N'Active'");

        // The cap counts a borrower's active loans on every checkout and every hold.
        builder.HasIndex(loan => loan.BorrowerId);

        // Stored as its name rather than its number: a status is read by a human when something
        // looks wrong, and 'DeclaredLost' answers where '2' asks. It is also what the unique
        // filter above matches on, which pins the string doubly.
        builder.Property(loan => loan.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(loan => loan.CheckedOutOn).IsRequired();
        builder.Property(loan => loan.DueDate).IsRequired();
        builder.Property(loan => loan.RenewalCount).IsRequired();
        builder.Property(loan => loan.ReturnedOn);

        // A table of its own rather than a JSON column, the same call the variant names made: this
        // is a set the scheduled process asks about, and the composite key of the loan and the
        // appointment is what makes recording one twice impossible rather than merely unlikely.
        builder.OwnsMany(loan => loan.RemindersSent, reminder =>
        {
            reminder.ToTable("LoanReminderSent");
            reminder.WithOwner().HasForeignKey("LoanId");

            // The signed day count is the whole value: negative before the due date, positive
            // after, and a human reading the table sees -3 or 7 rather than an opaque code.
            reminder.Property(sent => sent.DaysFromDue)
                .HasColumnName("DaysFromDue")
                // The value comes from the domain, never from the engine. Said explicitly because
                // the convention would read an integer key as an identity column, and then a
                // reminder added to a loan already on file arrives with its key non-default: EF
                // concludes the row exists, marks it Modified, and — the table being nothing but
                // its key — has nothing to update and writes no statement at all. The day would
                // announce the same reminder again tomorrow, silently.
                .ValueGeneratedNever()
                .IsRequired();

            reminder.HasKey("LoanId", "DaysFromDue");
        });

        builder.Navigation(loan => loan.RemindersSent)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
