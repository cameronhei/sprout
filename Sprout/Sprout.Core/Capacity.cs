namespace Sprout.Core;

public static class Capacity
{
    public static double PlannablePerDay(SproutSettings settings) =>
        Math.Max(0, Math.Round(settings.HoursPerDay - settings.Buffer, 1));

    public static bool IsWorkday(DateOnly day) =>
        day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public static TimeOffPeriod? TimeOffOn(SproutSettings settings, DateOnly day) =>
        settings.TimeOff.FirstOrDefault(t => t.Covers(day));

    public static double AvailableOn(SproutSettings settings, DateOnly day) =>
        IsWorkday(day) && TimeOffOn(settings, day) is null ? PlannablePerDay(settings) : 0;

    public static double DoneHoursOn(IEnumerable<TaskItem> tasks, DateOnly day) =>
        Math.Round(tasks
            .Where(t => t.Status == WorkStatus.Done && t.CompletedAt is not null)
            .Where(t => DateOnly.FromDateTime(t.CompletedAt!.Value) == day)
            .Sum(Recommender.EstimatedHours), 1);

    // Today's capacity, minus logged fires and the estimated hours of tasks finished today.
    public static double HoursLeftToday(SproutSettings settings, IEnumerable<TaskItem> tasks, DateOnly today) =>
        Math.Max(0, Math.Round(AvailableOn(settings, today) - Fires.HoursOn(tasks, today) - DoneHoursOn(tasks, today), 1));

    public static List<DateOnly> DaysOfWeek(DateOnly today, DayOfWeek weekStart)
    {
        var start = Week.StartOf(today, weekStart);
        return Enumerable.Range(0, 7).Select(start.AddDays).ToList();
    }

    public static DateOnly LastWorkday(DateOnly today, DayOfWeek weekStart) =>
        DaysOfWeek(today, weekStart).Last(IsWorkday);

    public static double HoursLeftThisWeek(SproutSettings settings, DateOnly today) =>
        HoursLeftThisWeek(settings, [], today);

    // Plannable hours from today to the end of the week, minus fires logged on those days.
    public static double HoursLeftThisWeek(SproutSettings settings, IEnumerable<TaskItem> tasks, DateOnly today)
    {
        var list = tasks as IReadOnlyCollection<TaskItem> ?? tasks.ToList();

        return Math.Round(DaysOfWeek(today, settings.WeekStart)
            .Where(d => d >= today)
            .Sum(d => Math.Max(0, AvailableOn(settings, d) - Fires.HoursOn(list, d))), 1);
    }

    public static double PlannedThisWeek(IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart)
    {
        var lastWorkday = LastWorkday(today, weekStart);

        return Math.Round(tasks
            .Where(t => t.IsOpen)
            .Where(t => t.DueDate is not null && t.DueDate.Value <= lastWorkday)
            .Sum(Recommender.EstimatedHours), 1);
    }
}