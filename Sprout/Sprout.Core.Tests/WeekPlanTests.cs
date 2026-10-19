using Sprout.Core;

namespace Sprout.Core.Tests;

public class WeekPlanTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 15);
    private static readonly DateOnly Friday = new(2026, 10, 16);
    private static readonly DateOnly NextMonday = new(2026, 10, 19);

    [Fact]
    public void Due_this_week_includes_overdue_and_excludes_later_or_undated()
    {
        var overdue = new TaskItem { DueDate = new DateOnly(2026, 10, 12) };
        var friday = new TaskItem { DueDate = Friday };
        var later = new TaskItem { DueDate = NextMonday };
        var undated = new TaskItem();
        var done = new TaskItem { DueDate = Friday, Status = WorkStatus.Done };

        var result = WeekPlan.DueThisWeek([overdue, friday, later, undated, done], Thursday, DayOfWeek.Monday);

        Assert.Equal([overdue, friday], result);
    }

    [Fact]
    public void Coming_up_is_after_this_week_soonest_first()
    {
        var thisWeek = new TaskItem { DueDate = Friday };
        var farther = new TaskItem { DueDate = NextMonday.AddDays(7) };
        var sooner = new TaskItem { DueDate = NextMonday };

        var result = WeekPlan.ComingUp([thisWeek, farther, sooner], Thursday, DayOfWeek.Monday);

        Assert.Equal([sooner, farther], result);
    }

    [Fact]
    public void Trade_offs_skip_firm_deadlines_and_start_with_lowest_score()
    {
        var firm = new TaskItem { DueDate = Friday, IsFirm = true, Impact = ImpactLevel.Low };
        var low = new TaskItem { DueDate = Friday, Impact = ImpactLevel.Low };
        var high = new TaskItem { DueDate = Friday, Impact = ImpactLevel.High };

        var result = WeekPlan.TradeOffs([high, firm, low], Thursday, DayOfWeek.Monday, 5.5);

        Assert.Equal([low, high], result);
    }

    [Fact]
    public void Carried_forward_counts_open_overdue_tasks()
    {
        var overdue = new TaskItem { DueDate = new DateOnly(2026, 10, 13) };
        var overdueDone = new TaskItem { DueDate = new DateOnly(2026, 10, 13), Status = WorkStatus.Done };
        var future = new TaskItem { DueDate = Friday };

        Assert.Equal(1, WeekPlan.CarriedForward([overdue, overdueDone, future], Thursday));
    }
}