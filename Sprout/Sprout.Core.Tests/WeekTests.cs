using Sprout.Core;

namespace Sprout.Core.Tests;

public class WeekTests
{
    private static readonly DateOnly Saturday = new(2026, 10, 17);

    [Theory]
    [InlineData(DayOfWeek.Monday, 12)]
    [InlineData(DayOfWeek.Sunday, 11)]
    public void Start_of_week_follows_the_setting(DayOfWeek weekStart, int expectedDay)
    {
        Assert.Equal(new DateOnly(2026, 10, expectedDay), Week.StartOf(Saturday, weekStart));
    }

    [Fact]
    public void First_day_of_the_week_is_its_own_start()
    {
        var monday = new DateOnly(2026, 10, 12);

        Assert.Equal(monday, Week.StartOf(monday, DayOfWeek.Monday));
    }

    [Fact]
    public void Done_this_week_ignores_last_week_and_open_tasks()
    {
        var thisWeek = new TaskItem { Status = WorkStatus.Done, CompletedAt = new DateTime(2026, 10, 13, 9, 0, 0) };
        var lastWeek = new TaskItem { Status = WorkStatus.Done, CompletedAt = new DateTime(2026, 10, 9, 9, 0, 0) };

        var open = new TaskItem { Status = WorkStatus.Backlog };
        var done = Week.DoneThisWeek([thisWeek, lastWeek, open], Saturday, DayOfWeek.Monday);

        Assert.Single(done);
        Assert.Same(thisWeek, done[0]);
    }
}

