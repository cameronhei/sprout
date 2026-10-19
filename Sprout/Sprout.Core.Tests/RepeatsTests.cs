using Sprout.Core;

namespace Sprout.Core.Tests;

public class RepeatsTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 15);

    [Theory]
    [InlineData(RepeatUnit.Day, 1, "Repeats daily")]
    [InlineData(RepeatUnit.Week, 1, "Repeats weekly")]
    [InlineData(RepeatUnit.Month, 1, "Repeats monthly")]
    [InlineData(RepeatUnit.Week, 2, "Repeats every 2 weeks")]
    public void Label_reads_naturally(RepeatUnit unit, int every, string expected)
    {
        Assert.Equal(expected, Repeats.Label(new RepeatRule { Unit = unit, Every = every }));
    }

    [Fact]
    public void Weekly_label_lists_days_in_week_order()
    {
        var rule = new RepeatRule { Unit = RepeatUnit.Week, Days = [DayOfWeek.Friday, DayOfWeek.Monday] };

        Assert.Equal("Repeats weekly on Mon, Fri", Repeats.Label(rule));
    }

    [Fact]
    public void Monthly_keeps_the_end_of_the_month()
    {
        var rule = new RepeatRule { Unit = RepeatUnit.Month };

        Assert.Equal(new DateOnly(2026, 11, 30), Repeats.NextDue(rule, new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void Overdue_or_undated_repeats_from_today()
    {
        var rule = new RepeatRule { Unit = RepeatUnit.Day, Every = 2 };

        Assert.Equal(Thursday.AddDays(2), Repeats.NextDue(rule, Thursday.AddDays(-5), Thursday));
        Assert.Equal(Thursday.AddDays(2), Repeats.NextDue(rule, null, Thursday));
    }

    [Fact]
    public void Weekly_on_days_picks_the_next_listed_day()
    {
        var rule = new RepeatRule { Unit = RepeatUnit.Week, Days = [DayOfWeek.Monday, DayOfWeek.Friday] };

        Assert.Equal(new DateOnly(2026, 10, 16), Repeats.NextDue(rule, Thursday, Thursday));            // Friday
        Assert.Equal(new DateOnly(2026, 10, 19), Repeats.NextDue(rule, new DateOnly(2026, 10, 16), Thursday)); // next Monday
    }

    [Fact]
    public void Every_two_weeks_skips_a_week_when_wrapping()
    {
        var rule = new RepeatRule { Unit = RepeatUnit.Week, Every = 2, Days = [DayOfWeek.Monday] };

        Assert.Equal(new DateOnly(2026, 10, 26), Repeats.NextDue(rule, Thursday, Thursday));
    }

    [Fact]
    public void Next_repeat_is_a_fresh_open_task()
    {
        var done = new TaskItem
        {
            Title = "Weekly GMP walk",
            Size = TaskSize.M,
            DueDate = Thursday,
            Status = WorkStatus.Done,
            Repeat = new RepeatRule { Unit = RepeatUnit.Week },
            Notes = [new TaskNote { Text = "old" }]
        };

        var next = Repeats.CreateNext(done, Thursday);

        Assert.True(next.Id != done.Id);
        Assert.Equal(WorkStatus.Backlog, next.Status);
        Assert.Equal(Thursday.AddDays(7), next.DueDate!.Value);
        Assert.Empty(next.Notes);
        Assert.Equal("Created automatically from a repeating task", next.Activity[0].Text);
    }
}
