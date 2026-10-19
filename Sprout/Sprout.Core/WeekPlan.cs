namespace Sprout.Core;

public static class WeekPlan
{
    private static bool IsOpen(TaskItem task) => task.Status is WorkStatus.Backlog or WorkStatus.NeedsReview;

    public static List<TaskItem> DueThisWeek(IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart)
    {
        var lastWorkday = Capacity.LastWorkday(today, weekStart);

        return tasks
            .Where(t => IsOpen(t) && t.DueDate is not null && t.DueDate.Value <= lastWorkday)
            .ToList();
    }

    public static List<TaskItem> ComingUp(IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart, int count = 5)
    {
        var lastWorkday = Capacity.LastWorkday(today, weekStart);

        return tasks
            .Where(t => IsOpen(t) && t.DueDate is not null && t.DueDate.Value > lastWorkday)
            .OrderBy(t => t.DueDate)
            .Take(count)
            .ToList();
    }

    public static List<TaskItem> TradeOffs(IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart, double hoursLeftToday)
    {
        return DueThisWeek(tasks, today, weekStart)
            .Where(t => !t.IsFirm && !t.IsBlocked)
            .OrderBy(t => Recommender.Score(t, today, hoursLeftToday))
            .Take(3)
            .ToList();
    }

    public static int CarriedForward(IEnumerable<TaskItem> tasks, DateOnly today)
    {
        return tasks.Count(t => IsOpen(t) && t.DueDate is not null && t.DueDate.Value < today);
    }
}
