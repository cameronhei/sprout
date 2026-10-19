namespace Sprout.Core;

public enum ProjectStatus
{
    Active = 0,
    OnHold = 1,
    Done = 2
}

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    // Carries over from week to week in the report.
    public string Note { get; set; } = "";
    public DateOnly? NoteUpdated { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Project Clone() => (Project)MemberwiseClone();
}
