namespace Sprout.Core;

// One line of a project's weekly update, e.g. "Next: Draft the SOP (due Thursday)".
public record ProjectLine(string Kind, string Text, Guid TaskId);

public static class ProjectLines
{
    // Built from the project's linked tasks: done this week, the next three open ones, what's waiting and what's handed off.
    public static List<ProjectLine> For(Guid projectId, IEnumerable<TaskItem> tasks, DateOnly today, DayOfWeek weekStart)
    {
        var linked = tasks.Where(t => t.ProjectId == projectId).ToList();
        var lines = new List<ProjectLine>();

        lines.AddRange(Week.DoneThisWeek(linked, today, weekStart)
            .Select(t => new ProjectLine("Done", t.Title, t.Id)));

        lines.AddRange(linked
            .Where(t => (t.Status == WorkStatus.Backlog && !t.IsBlocked) || t.Status == WorkStatus.NeedsReview)
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .Take(3)
            .Select(t => new ProjectLine("Next", t.Title + DueSuffix(t, today), t.Id)));

        lines.AddRange(linked
            .Where(t => t.Status == WorkStatus.Backlog && t.IsBlocked)
            .Select(t => new ProjectLine("Waiting", $"{t.Title} (on {t.BlockedBy})", t.Id)));

        lines.AddRange(linked
            .Where(t => t.Status == WorkStatus.HandedOff)
            .Select(t => new ProjectLine($"With {t.HandedTo}", t.Title, t.Id)));

        return lines;
    }

    public static int OpenCount(Guid projectId, IEnumerable<TaskItem> tasks) =>
        tasks.Count(t => t.ProjectId == projectId && t.IsOpen);

    private static string DueSuffix(TaskItem task, DateOnly today)
    {
        if (task.DueDate is null)
        {
            return "";
        }

        var label = TaskLabels.Due(task, today);
        label = label == "Overdue" ? "overdue" : "due" + label["Due".Length..];
        return $" ({label})";
    }
}
