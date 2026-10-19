using Sprout.Core;

namespace Sprout.Core.Tests;

public class ReportBuilderTests
{
    // Wednesday, Oct 14 2026: the week runs Mon Oct 12 to Fri Oct 16.
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static ReportContext Context(List<TaskItem>? tasks = null, List<Project>? projects = null,
        List<Meeting>? meetings = null, ReportWeek? report = null) => new()
    {
        Today = Today,
        Settings = new SproutSettings(),
        Tasks = tasks ?? [],
        Projects = projects ?? [],
        Meetings = meetings ?? [],
        Report = report ?? new ReportWeek { Id = "2026-10-12" }
    };

    private static TaskItem Open(string title, int dueIn, Guid? project = null) => new()
    {
        Title = title,
        Status = WorkStatus.Backlog,
        Size = TaskSize.S,
        Impact = ImpactLevel.Medium,
        DueDate = Today.AddDays(dueIn),
        ProjectId = project
    };

    [Fact]
    public void Ranges_and_sheet_names_read_like_the_old_reports()
    {
        Assert.Equal("Oct 12–16", ReportBuilder.Range(new(2026, 10, 12), new(2026, 10, 16)));
        Assert.Equal("Sep 28–Oct 2", ReportBuilder.Range(new(2026, 9, 28), new(2026, 10, 2)));
        Assert.Equal("Oct 12–16, 2026", ReportBuilder.SheetName(new(2026, 10, 12), new(2026, 10, 16)));
        Assert.Equal("Dec 28, 2026–Jan 1, 2027", ReportBuilder.SheetName(new(2026, 12, 28), new(2027, 1, 1)));
    }

    [Fact]
    public void Byline_has_name_date_and_week()
    {
        Assert.Equal("Peter Heiman · 10/14/2026 · Week of Oct 12–16", ReportBuilder.Byline(Context()));
    }

    [Fact]
    public void Top_four_unblocked_tasks_are_picked_by_default()
    {
        var tasks = Enumerable.Range(1, 6).Select(i => Open("Task " + i, i)).ToList();
        tasks[0].IsBlocked = true;

        var picked = ReportBuilder.LooseTasks(Context(tasks)).Where(t => t.Picked).ToList();

        Assert.Equal(4, picked.Count);
        Assert.False(picked.Any(p => p.Task.IsBlocked));
    }

    [Fact]
    public void Unticking_a_default_pick_sticks()
    {
        var task = Open("Write CAPA", 1);
        var report = new ReportWeek { Id = "2026-10-12" };
        report.Picks[ReportBuilder.TaskKey(task.Id)] = false;

        var groups = ReportBuilder.PriorityGroups(Context([task], report: report));

        Assert.Empty(groups);
    }

    [Fact]
    public void Picked_project_tasks_group_under_the_project()
    {
        var project = new Project { Name = "Supplier audits" };
        var a = Open("Book the Ohio visit", 1, project.Id);
        var b = Open("Send the questionnaire", 2, project.Id);
        var report = new ReportWeek { Id = "2026-10-12" };
        report.ActionNotes[b.Id.ToString()] = "Sent Monday";

        var groups = ReportBuilder.PriorityGroups(Context([a, b], [project], report: report));

        Assert.Single(groups);
        Assert.Equal("Supplier audits", groups[0].Title);
        Assert.Equal(new[] { "Book the Ohio visit (due tomorrow)", "Send the questionnaire: Sent Monday" }, groups[0].Lines.ToArray());
    }

    [Fact]
    public void Loose_task_action_defaults_to_its_due_label()
    {
        var groups = ReportBuilder.PriorityGroups(Context([Open("Update the HACCP plan", 2)]));

        Assert.Equal("Due Friday", groups[0].Lines[0]);
    }

    [Fact]
    public void Meeting_lines_show_when_prep_and_agenda()
    {
        var meeting = new Meeting { Name = "Quality Review", NextDate = new DateOnly(2026, 10, 20), Agenda = "Complaints\nAudit plan" };
        var prep = Meetings.MakePrepTask(meeting, new PrepItem { Title = "Pull numbers", WorkdaysBefore = 2 }, Today);

        var lines = ReportBuilder.MeetingItems(Context([prep], meetings: [meeting]))[0].FinalTexts;

        Assert.Equal("Next week (Tue Oct 20)", lines[0]);
        Assert.Equal("Prep: Pull numbers (due Friday)", lines[1]);
        Assert.Equal("Agenda: Complaints; Audit plan", lines[2]);
    }

    [Fact]
    public void Met_notes_only_show_when_opted_in()
    {
        var meeting = new Meeting { Name = "Safety", NextDate = new DateOnly(2026, 11, 10) };
        meeting.History.Add(new MeetingRecord { Date = new DateOnly(2026, 10, 13), Notes = "Order new gloves" });
        var report = new ReportWeek { Id = "2026-10-12" };

        Assert.Single(ReportBuilder.MeetingItems(Context(meetings: [meeting], report: report))[0].FinalTexts);

        report.MeetingsWithNotes.Add(meeting.Id);
        var lines = ReportBuilder.MeetingItems(Context(meetings: [meeting], report: report))[0].FinalTexts;
        Assert.Equal("Met Tue Oct 13: Order new gloves", lines[^1]);
    }

    [Fact]
    public void Custom_wording_replaces_the_automatic_lines()
    {
        var project = new Project { Name = "Label refresh", Note = "Waiting on art" };
        var report = new ReportWeek { Id = "2026-10-12" };
        report.Wording[ReportBuilder.ProjectItemKey(project.Id)] = "On track\n\nPrinter booked";

        var item = ReportBuilder.ProjectItems(Context(projects: [project], report: report))[0];

        Assert.True(item.Overridden);
        Assert.Equal(new[] { "On track", "Printer booked" }, item.FinalTexts.ToArray());
    }

    [Fact]
    public void Project_without_activity_says_so()
    {
        var project = new Project { Name = "Quiet one" };

        var doc = ReportBuilder.Build(Context(projects: [project]));
        var table = doc.Tables.Single(t => t.Heading == "Ongoing Projects");

        Assert.Equal("No activity this week", table.Groups[0].Lines[0]);
    }

    [Fact]
    public void Highlights_table_only_when_turned_on_and_filled()
    {
        var report = new ReportWeek { Id = "2026-10-12", Accomplishments = "Closed 3 CAPAs" };
        Assert.Equal(3, ReportBuilder.Build(Context(report: report)).Tables.Count);

        report.ShowHighlights = true;
        var doc = ReportBuilder.Build(Context(report: report));
        Assert.Equal("Highlights", doc.Tables[0].Heading);
    }

    [Fact]
    public void Html_and_text_copies_carry_every_row()
    {
        var doc = new ReportDocument("Weekly Touch Base", "Peter Heiman · 10/14/2026 · Week of Oct 12–16",
            [new ReportTable("Ongoing Projects", "Updates", [new ReportGroup("Audits & visits", ["Done: Ohio", "Next: Iowa"])])]);

        var html = doc.ToHtml();
        Assert.Contains("rowspan=\"2\"", html);
        Assert.Contains("Audits &amp; visits", html);
        Assert.Contains("#CCFFFF", html);

        var text = doc.ToText();
        Assert.Contains("• Audits & visits: Done: Ohio", text);
        Assert.Contains("    Next: Iowa", text);
    }
}
