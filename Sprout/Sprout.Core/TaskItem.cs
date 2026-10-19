namespace Sprout.Core;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public WorkStatus Status { get; set; } = WorkStatus.NeedsReview;
    public TaskSize? Size { get; set; }
    public DateOnly? DueDate { get; set; }
    public ImpactLevel? Impact { get; set; }
    public bool IsFirm { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    // Planning
    public Guid? ProjectId { get; set; }
    public Guid? MeetingId { get; set; }
    public Guid? PrepItemId { get; set; }
    public RepeatRule? Repeat { get; set; }
    public bool IsBlocked { get; set; }
    public string BlockedBy { get; set; } = "";

    // Handed off
    public string HandedTo { get; set; } = "";
    public string HandoffNote { get; set; } = "";
    public DateOnly? FollowUpOn { get; set; }

    // Firefights
    public string FireCategory { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateOnly? FireDate { get; set; }
    public int? Minutes { get; set; }
    public string Outcome { get; set; } = "";

    public List<TaskNote> Notes { get; set; } = [];
    public List<ActivityEntry> Activity { get; set; } = [];

    public bool IsOpen => Status is WorkStatus.Backlog or WorkStatus.NeedsReview;

    public TaskItem Clone()
    {
        var copy = (TaskItem)MemberwiseClone();
        copy.Notes = [.. Notes];
        copy.Activity = [.. Activity];
        copy.Repeat = Repeat?.Clone();
        return copy;
    }

    public void Log(string text) => Activity.Add(new ActivityEntry { Text = text });
}
