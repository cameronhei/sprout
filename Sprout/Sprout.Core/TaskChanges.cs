namespace Sprout.Core;

public static class TaskChanges
{
    private static string SizeName(TaskSize size) => size switch
    {
        TaskSize.XS => "extra small",
        TaskSize.S => "small",
        TaskSize.M => "medium",
        TaskSize.L => "large",
        _ => "extra large"
    };

    // One activity line per change, in the mockup's wording.
    public static List<string> Describe(TaskItem before, TaskItem after, Func<Guid, string?> projectName)
    {
        var lines = new List<string>();

        if (after.Title != before.Title)
        {
            lines.Add($"Renamed to “{after.Title}”");
        }

        if (after.Description != before.Description)
        {
            lines.Add("Updated the description");
        }

        if (after.Size != before.Size)
        {
            lines.Add(after.Size is null ? "Size cleared" : $"Size set to {SizeName(after.Size.Value)}");
        }

        if (after.DueDate != before.DueDate)
        {
            lines.Add(after.DueDate is null ? "Due date cleared" : $"Due date set to {after.DueDate.Value:MMM d}");
        }

        if (after.Impact != before.Impact)
        {
            lines.Add(after.Impact is null ? "Impact cleared" : $"Impact set to {after.Impact.Value.ToString().ToLower()}");
        }

        if (after.IsBlocked && !before.IsBlocked)
        {
            lines.Add($"Marked blocked: waiting on {after.BlockedBy}");
        }
        else if (!after.IsBlocked && before.IsBlocked)
        {
            lines.Add("Unblocked");
        }
        else if (after.IsBlocked && after.BlockedBy != before.BlockedBy)
        {
            lines.Add($"Now waiting on {after.BlockedBy}");
        }

        var oldRepeat = Repeats.Label(before.Repeat);
        var newRepeat = Repeats.Label(after.Repeat);
        if (oldRepeat != newRepeat)
        {
            lines.Add(newRepeat == "" ? "Stopped repeating" : newRepeat);
        }

        if (after.IsFirm != before.IsFirm)
        {
            lines.Add(after.IsFirm ? "Marked as a firm deadline" : "No longer a firm deadline");
        }

        if (after.ProjectId != before.ProjectId)
        {
            var name = after.ProjectId is null ? null : projectName(after.ProjectId.Value);
            lines.Add(name is null ? "Unlinked from its project" : $"Linked to project {name}");
        }

        return lines;
    }

    public static List<string> DescribeFire(TaskItem before, TaskItem after)
    {
        var lines = new List<string>();

        if (after.Title != before.Title)
        {
            lines.Add($"Renamed to “{after.Title}”");
        }

        if (after.FireCategory != before.FireCategory)
        {
            lines.Add(after.FireCategory == "" ? "Type cleared" : $"Type set to {after.FireCategory}");
        }

        if (after.RequestedBy != before.RequestedBy)
        {
            lines.Add(after.RequestedBy == "" ? "Cleared who pulled you in" : $"Pulled in by {after.RequestedBy}");
        }

        if (after.Description != before.Description)
        {
            lines.Add("Updated why you were involved");
        }

        if (after.Minutes != before.Minutes)
        {
            lines.Add(after.Minutes is null ? "Time spent cleared" : $"Time spent set to {Fires.FormatMinutes(after.Minutes.Value)}");
        }

        if (after.FireDate != before.FireDate && after.FireDate is not null)
        {
            lines.Add($"Date set to {after.FireDate.Value:MMM d}");
        }

        if (after.Outcome != before.Outcome)
        {
            lines.Add("Updated the outcome");
        }

        return lines;
    }

    // Moves a task between Needs review and Backlog based on whether it has a size.
    // Returns the message to show.
    public static string ApplyReviewRule(TaskItem after, bool anyChanges)
    {
        if (after.Status == WorkStatus.NeedsReview && after.Size is not null)
        {
            after.Status = WorkStatus.Backlog;
            after.Log("Reviewed and moved to backlog");
            return "Moved to your backlog. It can be recommended now.";
        }

        if (after.Status == WorkStatus.NeedsReview)
        {
            return "Saved. Add a size to move it to your backlog; until then it counts as Small.";
        }

        if (after.Status == WorkStatus.Backlog && after.Size is null)
        {
            after.Status = WorkStatus.NeedsReview;
            after.Log("Moved back to needs review");
            return "Moved back to Needs review: it has no size.";
        }

        return anyChanges ? "Saved. Recommendation recalculated." : "No changes.";
    }
}
