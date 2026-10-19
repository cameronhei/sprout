using Sprout.Core;

namespace Sprout.Core.Tests;

public class ProjectLinesTests
{
    // Wednesday, Oct 14 2026
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static TaskItem Linked(string title, WorkStatus status = WorkStatus.Backlog) =>
        new() { Title = title, Status = status, ProjectId = ProjectId };

    [Fact]
    public void Lines_come_in_done_next_waiting_with_order()
    {
        var done = Linked("Write the SOP", WorkStatus.Done);
        done.CompletedAt = new DateTime(2026, 10, 13, 9, 0, 0);
        var next = Linked("Train the line");
        next.DueDate = new DateOnly(2026, 10, 15);
        var waiting = Linked("Get lab results");
        waiting.IsBlocked = true;
        waiting.BlockedBy = "the lab";
        var handed = Linked("Order labels", WorkStatus.HandedOff);
        handed.HandedTo = "Maria";

        var lines = ProjectLines.For(ProjectId, [handed, waiting, next, done], Today, DayOfWeek.Monday);

        Assert.Equal(new[] { "Done", "Next", "Waiting", "With Maria" }, lines.Select(l => l.Kind).ToArray());
        Assert.Equal("Train the line (due tomorrow)", lines[1].Text);
        Assert.Equal("Get lab results (on the lab)", lines[2].Text);
    }

    [Fact]
    public void Next_shows_at_most_three_soonest_first()
    {
        var tasks = Enumerable.Range(1, 5).Select(i =>
        {
            var t = Linked("Task " + i);
            t.DueDate = Today.AddDays(10 - i);
            return t;
        }).ToList();

        var lines = ProjectLines.For(ProjectId, tasks, Today, DayOfWeek.Monday);

        Assert.Equal(3, lines.Count);
        Assert.StartsWith("Task 5", lines[0].Text);
    }

    [Fact]
    public void Overdue_and_undated_read_naturally()
    {
        var late = Linked("Late one");
        late.DueDate = Today.AddDays(-2);
        var open = Linked("Someday");

        var lines = ProjectLines.For(ProjectId, [late, open], Today, DayOfWeek.Monday);

        Assert.Equal("Late one (overdue)", lines[0].Text);
        Assert.Equal("Someday", lines[1].Text);
    }

    [Fact]
    public void Other_projects_and_last_weeks_done_are_left_out()
    {
        var old = Linked("Last week", WorkStatus.Done);
        old.CompletedAt = new DateTime(2026, 10, 9);
        var other = new TaskItem { Title = "Elsewhere", ProjectId = Guid.NewGuid() };

        Assert.Empty(ProjectLines.For(ProjectId, [old, other], Today, DayOfWeek.Monday));
    }
}
