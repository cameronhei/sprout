namespace Sprout.Core;

public class BackupFile
{
    public string App { get; set; } = "Sprout";

    // 1: tasks and settings. 2: adds projects, meetings and report choices.
    public int Version { get; set; } = 2;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<TaskItem> Tasks { get; set; } = [];
    public SproutSettings Settings { get; set; } = new();
    public List<Project> Projects { get; set; } = [];
    public List<Meeting> Meetings { get; set; } = [];
    public List<ReportWeek> Reports { get; set; } = [];
}
