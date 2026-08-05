namespace LibraryManagement.Charges.Application;

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
    /// UTC because it is the only clock the system has, the same reading Circulation and Members
    /// each keep their own copy of. The day it matters here is narrow: a charge incurred an hour
    /// either side of midnight is filed on one day or the next, and nothing in this context reads
    /// that date for a rule — it is what a librarian sees beside the amount.
    /// </remarks>
    internal static DateOnly Today(this TimeProvider clock)
        => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
