namespace Sprout.Core;

public static class TaskLabels
{
    public static string Due(TaskItem task, DateOnly today)
    {
        var days = Recommender.DaysUntilDue(task, today);

        return days switch
        {
            null => "No due date",
            < 0 => "Overdue",
            0 => "Due today",
            1 => "Due tomorrow",
            < 7 => "Due " + task.DueDate!.Value.DayOfWeek,
            _ => "Due " + task.DueDate!.Value.ToString("MMM d")
        };
    }

    public static string Size(TaskItem task)
    {
        if (task.Size is null)
        {
            return "Size not set";
        }

        return $"{task.Size} · {task.Size.Value.Hours()} h est.";
    }

    // Short state for task lists inside the project and meeting dialogs.
    public static string State(TaskItem task, DateOnly today) => task.Status switch
    {
        WorkStatus.NeedsReview => "Needs review",
        WorkStatus.Backlog => Due(task, today),
        WorkStatus.Done => "Done",
        WorkStatus.OnHold => "On hold",
        WorkStatus.HandedOff => $"With {task.HandedTo}",
        WorkStatus.Fire => "Firefight",
        _ => "Closed"
    };
}
