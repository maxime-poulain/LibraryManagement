namespace LibraryManagement.Circulation.Domain.Tests;

/// <summary>
/// The calendar answers one question — is this day open? — and these tests hold the two ways a
/// day is closed, the walk to the next open one, and the count a fine is built from.
/// </summary>
public sealed class OpeningCalendarTests
{
    // Desk.Today is Saturday 2026-03-14; the days around it are named here so a test reads as a
    // week rather than as arithmetic.
    private static readonly DateOnly Saturday = new(2026, 3, 14);
    private static readonly DateOnly Sunday = new(2026, 3, 15);
    private static readonly DateOnly Monday = new(2026, 3, 16);
    private static readonly DateOnly Tuesday = new(2026, 3, 17);

    private static OpeningCalendar ClosedOn(params DayOfWeek[] weekdays) => new(weekdays, []);

    private static OpeningCalendar ClosedFor(params DateOnly[] dates) => new([], dates);

    [Fact]
    public void TheUnconfiguredCalendar_IsOpenEveryDay()
    {
        // The default the policy ships with: before an administrator says otherwise, nothing
        // slides and nothing is left out of a count — the behavior the system always had.
        OpeningCalendar.OpenEveryDay.ClosedWeekdays.ShouldBeEmpty();
        OpeningCalendar.OpenEveryDay.ClosedDates.ShouldBeEmpty();
        OpeningCalendar.OpenEveryDay.IsOpenOn(Sunday).ShouldBeTrue();
    }

    [Fact]
    public void AWeeklyClosure_ClosesThatDayOfEveryWeek()
    {
        var calendar = ClosedOn(DayOfWeek.Sunday);

        calendar.IsOpenOn(Sunday).ShouldBeFalse();
        calendar.IsOpenOn(Sunday.AddDays(7)).ShouldBeFalse();
        calendar.IsOpenOn(Monday).ShouldBeTrue();
    }

    [Fact]
    public void ADatedClosure_ClosesThatDateAlone()
    {
        // A public holiday and an exceptional closing arrive the same way — a date the
        // administrator entered. Why the door was shut is not a circulation fact.
        var calendar = ClosedFor(Monday);

        calendar.IsOpenOn(Monday).ShouldBeFalse();
        calendar.IsOpenOn(Monday.AddDays(7)).ShouldBeTrue();
    }

    [Fact]
    public void EveryWeekdayClosed_IsRefused()
    {
        // A library with no open weekday has no first open day to slide anything to — and the
        // walk below would never end. The mistake is the host's configuration, so it throws.
        Should.Throw<ArgumentException>(() => ClosedOn(
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday,
            DayOfWeek.Sunday));
    }

    [Fact]
    public void FirstOpenDayOnOrAfter_AnOpenDay_IsTheDayItself()
    {
        ClosedOn(DayOfWeek.Sunday).FirstOpenDayOnOrAfter(Saturday).ShouldBe(Saturday);
    }

    [Fact]
    public void FirstOpenDayOnOrAfter_AClosedDay_IsTheNextOpenOne()
    {
        // Closed Sunday and Monday — the ordinary week of a French municipal library.
        var calendar = ClosedOn(DayOfWeek.Sunday, DayOfWeek.Monday);

        calendar.FirstOpenDayOnOrAfter(Sunday).ShouldBe(Tuesday);
    }

    [Fact]
    public void FirstOpenDayOnOrAfter_WalksAcrossAWholeClosedFortnight()
    {
        // The summer closing: two weeks of dated closures with the weekly day inside them, and
        // the walk lands on the reopening day.
        var summerClosing = Enumerable.Range(0, 14).Select(Sunday.AddDays).ToArray();
        var calendar = new OpeningCalendar([DayOfWeek.Sunday], summerClosing);

        calendar.FirstOpenDayOnOrAfter(Sunday).ShouldBe(Sunday.AddDays(15));
    }

    [Fact]
    public void CountOpenDays_CountsNeitherTheStartingDayNorTheClosedOnes()
    {
        // Saturday through the following Saturday, closed Sundays: seven calendar days, six of
        // them open — the window the fine is built from.
        var calendar = ClosedOn(DayOfWeek.Sunday);

        calendar.CountOpenDays(after: Saturday, through: Saturday.AddDays(7)).ShouldBe(6);
    }

    [Fact]
    public void CountOpenDays_OfAnEmptyWindow_IsZero()
    {
        // A window that ends before it starts holds nothing rather than being an error — a return
        // before the due date is early, not exceptional.
        var calendar = ClosedOn(DayOfWeek.Sunday);

        calendar.CountOpenDays(after: Saturday, through: Saturday).ShouldBe(0);
        calendar.CountOpenDays(after: Saturday, through: Saturday.AddDays(-3)).ShouldBe(0);
    }
}
