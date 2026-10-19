namespace Sprout.Core;

public static class Fires
{
    public static readonly string[] Categories = ["Customer", "Production", "Supplier", "Internal"];

    public static string FormatMinutes(int minutes) =>
        minutes < 60 ? $"{minutes} min" : $"{Math.Round(minutes / 60.0, 1)} h";

    public static double HoursOn(IEnumerable<TaskItem> tasks, DateOnly day) =>
        Math.Round(tasks
            .Where(t => t.Status == WorkStatus.Fire && t.FireDate == day)
            .Sum(t => (t.Minutes ?? 0) / 60.0), 1);

    public static List<TaskItem> InWeek(IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart)
    {
        var start = Week.StartOf(today, weekStart);
        var end = start.AddDays(6);

        return tasks
            .Where(t => t.Status == WorkStatus.Fire && t.FireDate is not null && t.FireDate >= start && t.FireDate <= end)
            .ToList();
    }

    public static double Hours(IEnumerable<TaskItem> fires) =>
        Math.Round(fires.Sum(t => (t.Minutes ?? 0) / 60.0), 1);

    // "Customer 2 h · Production 1.5 h", biggest first.
    public static string ByCategory(IEnumerable<TaskItem> fires) =>
        string.Join(" · ", fires
            .GroupBy(f => f.FireCategory == "" ? "Not typed" : f.FireCategory)
            .Select(g => (Name: g.Key, Minutes: g.Sum(f => f.Minutes ?? 0)))
            .OrderByDescending(g => g.Minutes)
            .Select(g => $"{g.Name} {FormatMinutes(g.Minutes)}"));

    public static string WhenLabel(DateOnly? day, DateOnly today)
    {
        if (day is null)
        {
            return "";
        }

        var diff = day.Value.DayNumber - today.DayNumber;
        return diff switch
        {
            0 => "Today",
            -1 => "Yesterday",
            _ => day.Value.ToString("MMM d")
        };
    }
}
