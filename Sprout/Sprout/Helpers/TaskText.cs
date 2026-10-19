using Sprout.Core;

namespace Sprout.Helpers;

public static class TaskText
{
    public static string Meta(TaskItem task)
    {
        var parts = new List<string>();

        if (task.Size is not null)
        {
            parts.Add(task.Size.Value.ToString());
        }

        if (task.DueDate is not null)
        {
            parts.Add("Due " + task.DueDate.Value.ToString("MMM d"));
        }

        if (task.IsFirm)
        {
            parts.Add("Firm");
        }

        return string.Join(" · ", parts);
    }
}
