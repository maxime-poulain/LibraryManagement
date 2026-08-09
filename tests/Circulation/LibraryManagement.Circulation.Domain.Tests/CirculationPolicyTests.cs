namespace LibraryManagement.Circulation.Domain.Tests;

public sealed class CirculationPolicyTests
{
    // The values as the library decided them, pinned so a change is a change to a test — which is
    // what "a decision of the library must not be a deployment" looks like until the values come
    // from a table: at least the deployment says so.
    [Fact]
    public void TheDecidedValues_AreTheDesigns()
    {
        var policy = CirculationPolicy.Current;

        policy.MaxLoansAndHolds.ShouldBe(5);
        policy.LoanDurationInDays.ShouldBe(21);
        policy.MaxRenewals.ShouldBe(2);
        policy.PickupPeriodInDays.ShouldBe(7);
        policy.CourtesyReminderDaysBeforeDue.ShouldBe(3);
        policy.OverdueReminderDaysAfterDue.ShouldBe([1, 7, 14]);
        policy.DeclaredLostAfterDays.ShouldBe(30);
        policy.BlockingDebt.ShouldBe(0m);

        // No closure configured: the arithmetic ships, the schedule is the administrator's datum.
        policy.Calendar.ClosedWeekdays.ShouldBeEmpty();
        policy.Calendar.ClosedDates.ShouldBeEmpty();
    }

    [Fact]
    public void AnyAmountOwed_Blocks_FromTheFirstCent()
    {
        var policy = CirculationPolicy.Current;

        policy.DebtForbids(0m).ShouldBeFalse();
        policy.DebtForbids(0.01m).ShouldBeTrue();
    }

    [Fact]
    public void TheCap_ConstrainsTheAct()
    {
        var policy = CirculationPolicy.Current;

        policy.IsAtTheCap(policy.MaxLoansAndHolds - 1).ShouldBeFalse();
        policy.IsAtTheCap(policy.MaxLoansAndHolds).ShouldBeTrue();
    }

    // --- The calendar ---------------------------------------------------------------------------

    // Desk.Today is Saturday 2026-03-14. Closed Sunday and Monday is the ordinary week of a
    // French municipal library, and the dates below are named for how they fall around it.
    private static readonly DateOnly Saturday = new(2026, 3, 14);

    private static CirculationPolicy ClosedSundayAndMonday => CirculationPolicy.Current with
    {
        Calendar = new OpeningCalendar([DayOfWeek.Sunday, DayOfWeek.Monday], []),
    };

    [Fact]
    public void DueDateFollowing_AnOpenLanding_IsThePlainArithmetic()
    {
        var policy = ClosedSundayAndMonday;

        // Saturday plus 21 days is a Saturday — open, so nothing slides.
        policy.DueDateFollowing(Saturday).ShouldBe(Saturday.AddDays(21));
    }

    [Fact]
    public void DueDateFollowing_LandingOnAClosedDay_SlidesToTheFirstOpenOne()
    {
        // A deadline a member must meet never falls on a day they cannot meet it: the Sunday
        // landing slides past the Monday too, to the first day the door is actually open.
        var policy = ClosedSundayAndMonday;
        var sunday = Saturday.AddDays(1);

        policy.DueDateFollowing(sunday).ShouldBe(sunday.AddDays(23));
    }

    [Fact]
    public void PickupDeadlineFor_LandingOnAClosedDay_SlidesToTheFirstOpenOne()
    {
        // Trapped on a Sunday, seven days later is a Sunday: the last day the copy waits must be
        // one the member can walk in on.
        var policy = ClosedSundayAndMonday;
        var sunday = Saturday.AddDays(1);

        policy.PickupDeadlineFor(sunday).ShouldBe(sunday.AddDays(9));
    }

    [Fact]
    public void BillableDaysLate_OfAReturnOnTheDueDate_IsZero()
    {
        ClosedSundayAndMonday.BillableDaysLate(Saturday, returnedOn: Saturday).ShouldBe(0);
    }

    [Fact]
    public void BillableDaysLate_OfAnEarlyReturn_IsZeroAndNeverNegative()
    {
        ClosedSundayAndMonday.BillableDaysLate(Saturday, returnedOn: Saturday.AddDays(-5)).ShouldBe(0);
    }

    [Fact]
    public void BillableDaysLate_LeavesTheClosedDaysOut()
    {
        // A week late across a closed Sunday and Monday: seven elapsed days, five billed. A fine
        // is charged for days the borrower let pass, and a day nobody could return is not one.
        var policy = ClosedSundayAndMonday;

        policy.BillableDaysLate(Saturday, returnedOn: Saturday.AddDays(7)).ShouldBe(5);
    }

    [Fact]
    public void BillableDaysLate_ForgivesAClosureDeclaredAfterTheAct()
    {
        // The strike: the stored due date was open when the checkout slid it, and the calendar
        // closed it afterwards. The date on the receipt does not move; the judgement forgives
        // what the receipt could not know — a return on the first open day is on time.
        var strikeOnTheDueDate = CirculationPolicy.Current with
        {
            Calendar = new OpeningCalendar([], [Saturday]),
        };

        strikeOnTheDueDate.BillableDaysLate(Saturday, returnedOn: Saturday.AddDays(1)).ShouldBe(0);
    }

    [Fact]
    public void BillableDaysLate_AfterAForgivenClosure_CountsFromTheEffectiveDueDate()
    {
        // The member who misses the reopening day too is late by that day alone.
        var strikeOnTheDueDate = CirculationPolicy.Current with
        {
            Calendar = new OpeningCalendar([], [Saturday]),
        };

        strikeOnTheDueDate.BillableDaysLate(Saturday, returnedOn: Saturday.AddDays(2)).ShouldBe(1);
    }

    [Fact]
    public void BillableDaysLate_AcrossTheYearEndClosing_BillsTheOpenDaysAlone()
    {
        // Due the 24th of December — an open day the member missed — with the library closed
        // through the 2nd of January. Returned on reopening day: one open day passed, one billed,
        // where a calendar-day count would have charged ten.
        var christmasEve = new DateOnly(2026, 12, 24);
        var closing = Enumerable.Range(25, 7).Select(day => new DateOnly(2026, 12, day))
            .Append(new DateOnly(2027, 1, 1))
            .Append(new DateOnly(2027, 1, 2))
            .ToArray();
        var policy = CirculationPolicy.Current with { Calendar = new OpeningCalendar([], closing) };

        policy.BillableDaysLate(christmasEve, returnedOn: new DateOnly(2027, 1, 3)).ShouldBe(1);
    }

    [Fact]
    public void IndexingByCategory_WouldBeAnAddition()
    {
        // The record's init-only values are what keep the flat policy openable later: a table per
        // category arrives by construction, not by rewrite. Pinned by using it the way that day
        // would.
        var studentTerm = CirculationPolicy.Current with { LoanDurationInDays = 28 };

        studentTerm.LoanDurationInDays.ShouldBe(28);
        CirculationPolicy.Current.LoanDurationInDays.ShouldBe(21);
    }
}
