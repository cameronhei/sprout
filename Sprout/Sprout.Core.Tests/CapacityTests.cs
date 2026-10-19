using Sprout.Core;

namespace Sprout.Core.Tests;

public class CapacityTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 15);
    private static readonly DateOnly Friday = new(2026, 10, 16);
    private static readonly DateOnly Saturday = new(2026, 10, 17);

    [Fact]
    public void Plannable_is_workday_minus_buffer()
    {
        Assert.Equal(5.5, Capacity.PlannablePerDay(new SproutSettings()));
    }

    [Fact]
    public void Plannable_is_never_negative()
    {
        Assert.Equal(0, Capacity.PlannablePerDay(new SproutSettings { HoursPerDay = 2, Buffer = 3 }));
    }

    [Fact]
    public void Weekends_have_no_capacity()
    {
        var settings = new SproutSettings();

        Assert.Equal(0, Capacity.AvailableOn(settings, Saturday));
        Assert.Equal(5.5, Capacity.AvailableOn(settings, Thursday));
    }

    [Fact]
    public void Time_off_days_have_no_capacity()
    {
        var settings = new SproutSettings();
        settings.TimeOff.Add(new TimeOffPeriod { Start = Friday, End = Friday });

        Assert.Equal(0, Capacity.AvailableOn(settings, Friday));
        Assert.Equal(5.5, Capacity.AvailableOn(settings, Thursday));
    }

    [Fact]
    public void Hours_left_today_subtracts_finished_work()
    {
        var done = new TaskItem
        {
            Size = TaskSize.M,
            Status = WorkStatus.Done,
            CompletedAt = new DateTime(2026, 10, 15, 10, 0, 0)
        };

        Assert.Equal(3, Capacity.HoursLeftToday(new SproutSettings(), [done], Thursday));
    }

    [Fact]
    public void Hours_left_this_week_counts_today_onward()
    {
        Assert.Equal(11, Capacity.HoursLeftThisWeek(new SproutSettings(), Thursday)); // Thu + Fri
    }

    [Fact]
    public void Planned_counts_overdue_and_due_this_week_only()
    {
        TaskItem[] tasks =
        [
            new() { Size = TaskSize.S, DueDate = new DateOnly(2026, 10, 12) },                         // overdue: 1
            new() { Size = TaskSize.M, DueDate = Friday },                                             // this week: 2.5
            new() { Size = TaskSize.L, DueDate = new DateOnly(2026, 10, 19) },                         // next week
            new() { Size = TaskSize.XL },                                                              // no date
            new() { Size = TaskSize.L, DueDate = Friday, Status = WorkStatus.Done }                    // finished
        ];

        Assert.Equal(3.5, Capacity.PlannedThisWeek(tasks, Thursday, DayOfWeek.Monday));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Sunday)]
    public void Last_workday_is_friday_either_way(DayOfWeek weekStart)
    {
        Assert.Equal(Friday, Capacity.LastWorkday(Thursday, weekStart));
    }
}