using LibraryManagement.Circulation.Domain.Loans;

namespace LibraryManagement.Circulation.Domain.Tests.Loans;

public sealed class ReminderTests
{
    [Fact]
    public void ACourtesy_FallsBeforeTheDueDate()
    {
        var reminder = Reminder.Courtesy(3);

        reminder.DaysFromDue.ShouldBe(-3);
        reminder.IsCourtesy.ShouldBeTrue();
    }

    [Fact]
    public void AnOverdue_FallsAfterIt()
    {
        var reminder = Reminder.Overdue(7);

        reminder.DaysFromDue.ShouldBe(7);
        reminder.IsCourtesy.ShouldBeFalse();
    }

    [Fact]
    public void TwoAppointmentsAtTheSameOffset_AreTheSameAppointment()
    {
        // Equality by value is what lets a loan ask "have I already said this" of a set.
        Reminder.Overdue(7).ShouldBe(Reminder.Overdue(7));
        Reminder.Overdue(7).ShouldNotBe(Reminder.Overdue(14));
        Reminder.Courtesy(3).ShouldNotBe(Reminder.Overdue(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnAppointmentOnTheDueDateItself_IsRefused(int days)
    {
        // Zero is the due date, which is not an appointment — and it would let a courtesy and an
        // overdue reminder collide on one value.
        Should.Throw<ArgumentOutOfRangeException>(() => Reminder.Courtesy(days));
        Should.Throw<ArgumentOutOfRangeException>(() => Reminder.Overdue(days));
    }

    [Fact]
    public void ToString_ReadsAsALibrarianWouldSayIt()
    {
        Reminder.Courtesy(3).ToString().ShouldBe("3 days before due");
        Reminder.Overdue(14).ToString().ShouldBe("14 days overdue");
    }
}
