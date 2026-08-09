namespace LibraryManagement.Circulation.Domain;

/// <summary>
/// The days the library is open: the weekly closed days and the dated closures, as the
/// administrator declares them. The calendar answers one question — is this day open? — and the
/// policy that carries it decides what a closed day does to a due date, a pickup deadline or a
/// count of late days.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Closures are declared, never derived.</strong> A public holiday arrives here as a
/// date the administrator entered, exactly as an exceptional closing does — the model does not
/// distinguish them, because <em>why</em> the door was shut is not a circulation fact. Computing
/// the holidays instead was rejected: Easter moves by an arithmetic no library system should own,
/// two départements keep holidays the rest of France does not, and the set itself changes by
/// decree. A rule would be wrong somewhere every year; a list is retyped once a year and wrong
/// nowhere.
/// </para>
/// <para>
/// <strong>The sets name the closed days, not the open ones</strong>, because that is the fact a
/// library states — <em>fermé le lundi</em> is what the sign on the door says — and because the
/// empty calendar then means open every day, which is the behavior the system had before it knew
/// calendars existed. Stated the other way round, an unconfigured host would be a library that is
/// never open, and every deadline would slide forever.
/// </para>
/// <para>
/// A sealed class rather than a record, deliberately: the constructor guards an invariant, and a
/// record's <c>with</c> would clone past the guard. The policy swaps whole calendars; nothing
/// edits one.
/// </para>
/// </remarks>
public sealed class OpeningCalendar
{
    private readonly HashSet<DayOfWeek> _closedWeekdays;
    private readonly HashSet<DateOnly> _closedDates;

    /// <summary>
    /// The calendar of a library that never closes — the state of the world before an
    /// administrator has said otherwise, and the default the policy ships with.
    /// </summary>
    public static OpeningCalendar OpenEveryDay { get; } = new([], []);

    /// <summary>
    /// Declares the library's closures.
    /// </summary>
    /// <param name="closedWeekdays">The days of every week the library is closed.</param>
    /// <param name="closedDates">The dates the library is closed — public holidays and
    /// exceptional closings alike.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when all seven weekdays are closed. A library
    /// with no open weekday has no <em>first open day</em> to slide anything to, and the mistake
    /// is the host's configuration, not a refusal a librarian could have caused.</exception>
    public OpeningCalendar(IEnumerable<DayOfWeek> closedWeekdays, IEnumerable<DateOnly> closedDates)
    {
        ArgumentNullException.ThrowIfNull(closedWeekdays);
        ArgumentNullException.ThrowIfNull(closedDates);

        _closedWeekdays = [.. closedWeekdays];
        _closedDates = [.. closedDates];

        if (_closedWeekdays.Count == 7)
        {
            throw new ArgumentException(
                "Every weekday is declared closed; a library that is never open has no calendar, "
                + "it has a padlock.",
                nameof(closedWeekdays));
        }
    }

    /// <summary>Gets the days of every week the library is closed.</summary>
    public IReadOnlySet<DayOfWeek> ClosedWeekdays => _closedWeekdays;

    /// <summary>Gets the dates the library is closed, holidays and exceptional closings alike.</summary>
    public IReadOnlySet<DateOnly> ClosedDates => _closedDates;

    /// <summary>
    /// Answers whether the library is open on a day.
    /// </summary>
    /// <param name="day">The day in question.</param>
    /// <returns><see langword="true"/> when neither the weekly pattern nor a dated closure shuts it.</returns>
    public bool IsOpenOn(DateOnly day)
        => !_closedWeekdays.Contains(day.DayOfWeek) && !_closedDates.Contains(day);

    /// <summary>
    /// Returns the day itself when the library is open on it, and the next open day otherwise.
    /// </summary>
    /// <param name="day">The day a period's arithmetic landed on.</param>
    /// <returns>The first day on or after <paramref name="day"/> the library is open.</returns>
    /// <remarks>
    /// The walk terminates because the constructor holds the invariant it needs: the dated
    /// closures are finitely many, so past the last of them only the weekly pattern closes
    /// anything, and at least one weekday of that pattern is open.
    /// </remarks>
    public DateOnly FirstOpenDayOnOrAfter(DateOnly day)
    {
        while (!IsOpenOn(day))
        {
            day = day.AddDays(1);
        }

        return day;
    }

    /// <summary>
    /// Counts the open days strictly after one day and up to another, inclusive.
    /// </summary>
    /// <param name="after">The day the count starts after — itself never counted.</param>
    /// <param name="through">The last day counted, when the library is open on it.</param>
    /// <returns>The number of open days in the window, and zero when the window is empty.</returns>
    /// <remarks>
    /// The bounds are half-open on purpose, because the caller's question is <em>how many open
    /// days did the borrower let pass</em>: the effective due date itself is not one of them, and
    /// the day of the return is. A window that ends before it starts holds nothing rather than
    /// being an error — a return before the due date is early, not exceptional.
    /// </remarks>
    public int CountOpenDays(DateOnly after, DateOnly through)
    {
        var count = 0;

        for (var day = after.AddDays(1); day <= through; day = day.AddDays(1))
        {
            if (IsOpenOn(day))
            {
                count++;
            }
        }

        return count;
    }
}
