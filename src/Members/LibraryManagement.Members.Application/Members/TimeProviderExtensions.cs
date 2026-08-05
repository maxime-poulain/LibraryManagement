namespace LibraryManagement.Members.Application.Members;

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
    /// decision, the day a host exists to configure a time zone. The disagreement is an hour or
    /// two around midnight on the edges of a membership — squarely inside the tolerance the
    /// strategic design's boundary test grants, and recorded here so the day it matters, the fix
    /// is one method.
    /// </remarks>
    internal static DateOnly Today(this TimeProvider clock)
        => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
