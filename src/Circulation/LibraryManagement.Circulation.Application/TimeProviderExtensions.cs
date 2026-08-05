namespace LibraryManagement.Circulation.Application;

/// <summary>
/// The one place this module turns the clock into a day.
/// </summary>
internal static class TimeProviderExtensions
{
    /// <summary>
    /// The current day, in UTC.
    /// </summary>
    /// <param name="clock">The clock the host injected.</param>
    /// <returns>Today.</returns>
    /// <remarks>
    /// UTC because it is the only clock the system has: the library's own midnight is a host
    /// decision, the day a host exists to configure a time zone — the same reading, recorded for
    /// the same reasons, as the Members module's. Each module keeps its own copy of these three
    /// lines rather than sharing them, because a due date rule is a circulation fact and the
    /// shared kernel carries building blocks, never business readings of the clock.
    /// </remarks>
    internal static DateOnly Today(this TimeProvider clock)
        => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
