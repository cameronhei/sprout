namespace Sprout.Core;

public static class Week
{
    public static DateOnly StartOf(DateOnly day, DayOfWeek weekStart)
    {
        var daysSinceStart = ((int)day.DayOfWeek - (int)weekStart + 7) % 7;
        return day.AddDays(-daysSinceStart);
    }

    public static List<TaskItem> DoneThisWeek(IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart)
    {
        var start = StartOf(today, weekStart);

        return tasks
            .Where(t => t.Status == WorkStatus.Done && t.CompletedAt is not null)
            .Where(t => DateOnly.FromDateTime(t.CompletedAt!.Value) >= start)
            .ToList();

    }
}
