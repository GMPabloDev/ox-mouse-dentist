using MouseDent.Domain.Locales;
using Shouldly;

namespace MouseDent.Application.Tests;

public sealed class WeeklyScheduleTests
{
    [Fact]
    public void A_week_with_closed_days_and_one_open_day_is_valid()
    {
        var result = WeeklySchedule.Parse(Schedules.Inputs(day => day == DayOfWeek.Wednesday
            ? Open(day, 9, 0, 18, 0)
            : Closed(day)));

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Schedule!.Days.Count.ShouldBe(7);
        result.Schedule.Days.Single(day => day.IsOpen).Day.ShouldBe(DayOfWeek.Wednesday);
        result.Schedule.HasOpenDay.ShouldBeTrue();
    }

    [Fact]
    public void A_week_without_open_days_is_rejected()
    {
        var result = WeeklySchedule.Parse(Schedules.Inputs(Closed));

        result.IsValid.ShouldBeFalse();
        result.Schedule.ShouldBeNull();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("El horario debe tener al menos un día abierto.");
    }

    [Fact]
    public void An_open_day_that_does_not_close_after_opening_is_rejected()
    {
        var result = WeeklySchedule.Parse(Schedules.Inputs(day => day == DayOfWeek.Friday
            ? Open(day, 18, 0, 9, 0)
            : Closed(day)));

        result.IsValid.ShouldBeFalse();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Day.ShouldBe(DayOfWeek.Friday);
        error.Member.ShouldBe(ScheduleMember.ClosesAt);
    }

    [Fact]
    public void An_open_day_with_the_same_opening_and_closing_time_is_rejected()
    {
        var result = WeeklySchedule.Parse(Schedules.Inputs(day => day == DayOfWeek.Monday
            ? Open(day, 9, 0, 9, 0)
            : Closed(day)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Day == DayOfWeek.Monday && error.Member == ScheduleMember.ClosesAt);
    }

    [Fact]
    public void A_missing_day_is_rejected()
    {
        var days = Schedules.Inputs(Closed).Where(input => input.Day != DayOfWeek.Sunday).ToArray();

        var result = WeeklySchedule.Parse(days);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Day == DayOfWeek.Sunday && error.Member == ScheduleMember.Day);
    }

    [Fact]
    public void A_repeated_day_is_rejected()
    {
        var days = Schedules.Inputs(Closed).Append(Open(DayOfWeek.Monday, 9, 0, 18, 0)).ToArray();

        var result = WeeklySchedule.Parse(days);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Day == DayOfWeek.Monday && error.Member == ScheduleMember.Day);
    }

    private static ScheduleDayInput Open(DayOfWeek day, int openHour, int openMinute, int closeHour, int closeMinute) =>
        new(day, false, new TimeOnly(openHour, openMinute), new TimeOnly(closeHour, closeMinute));

    private static ScheduleDayInput Closed(DayOfWeek day) => new(day, true, default, default);
}

internal static class Schedules
{
    public static IEnumerable<ScheduleDayInput> Inputs(Func<DayOfWeek, ScheduleDayInput> factory) =>
        WeeklySchedule.WeekDays.Select(factory).ToArray();

    public static WeeklySchedule WeekFromNineToEighteen() =>
        WeeklySchedule.Parse(Inputs(day =>
            new ScheduleDayInput(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0)))).Schedule!;
}
