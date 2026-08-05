using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain.Loans;

/// <summary>
/// One appointment in the reminder schedule, identified by where it falls relative to the due
/// date: the courtesy reminder three days before is <c>-3</c>, the overdue reminder a week after
/// is <c>7</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>One signed number identifies each appointment, and that is the whole design.</strong>
/// A loan records which of them have gone out, and the scheduled process asks. Adding a stage to
/// the schedule — a second courtesy reminder, a reminder at twenty-one days — is a new value in
/// the set and not a migration, which is exactly what the tactical design asks of
/// <c>RemindersSent</c>: it is a set, not a flag.
/// </para>
/// <para>
/// The sign carries the kind, so no enum stands beside it saying the same thing twice: before the
/// due date is a courtesy, after it is an overdue. Zero is refused — it would be the due date
/// itself, which is not an appointment, and it would let a courtesy and an overdue reminder
/// collide on one value.
/// </para>
/// </remarks>
public sealed class Reminder : ValueObject<Reminder>
{
    private Reminder(int daysFromDue) => DaysFromDue = daysFromDue;

    /// <summary>
    /// Gets where the appointment falls relative to the due date — negative before, positive
    /// after.
    /// </summary>
    public int DaysFromDue { get; }

    /// <summary>Gets whether this is the courtesy reminder rather than an overdue one.</summary>
    public bool IsCourtesy => DaysFromDue < 0;

    /// <summary>
    /// The reminder that goes out a given number of days before the due date.
    /// </summary>
    /// <param name="daysBeforeDue">How many days ahead, as the policy states it.</param>
    /// <returns>The appointment.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is not positive.</exception>
    /// <remarks>
    /// Throws rather than returning a <c>Result</c>, unlike the value objects a librarian's typing
    /// reaches: this one is built from the policy alone, so a nonsensical value is a programming
    /// error and there is nothing for a caller to report to anybody.
    /// </remarks>
    public static Reminder Courtesy(int daysBeforeDue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysBeforeDue);

        return new Reminder(-daysBeforeDue);
    }

    /// <summary>
    /// The reminder that goes out a given number of days after the due date.
    /// </summary>
    /// <param name="daysAfterDue">How many days late, as the policy states it.</param>
    /// <returns>The appointment.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is not positive.</exception>
    public static Reminder Overdue(int daysAfterDue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysAfterDue);

        return new Reminder(daysAfterDue);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DaysFromDue;
    }

    /// <summary>Returns the appointment as a librarian would say it.</summary>
    public override string ToString()
        => IsCourtesy ? $"{-DaysFromDue} days before due" : $"{DaysFromDue} days overdue";
}
