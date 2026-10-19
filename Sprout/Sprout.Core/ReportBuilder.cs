using System.Globalization;

namespace Sprout.Core;

// Everything the weekly report is built from.
public class ReportContext
{
    public required DateOnly Today { get; init; }
    public required SproutSettings Settings { get; init; }
    public required IReadOnlyList<TaskItem> Tasks { get; init; }
    public required IReadOnlyList<Project> Projects { get; init; }
    public required IReadOnlyList<Meeting> Meetings { get; init; }
    public required ReportWeek Report { get; init; }

    private List<TaskItem>? _ranked;

    public DateOnly WeekStart => Week.StartOf(Today, Settings.WeekStart);
    public DateOnly WeekEnd => WeekStart.AddDays(6);

    public List<DateOnly> Workdays
    {
        get
        {
            var days = Capacity.DaysOfWeek(Today, Settings.WeekStart).Where(Capacity.IsWorkday).ToList();
            return days.Count > 0 ? days : [WeekStart, WeekEnd];
        }
    }

    // Open tasks in the order Sprout recommends them.
    public List<TaskItem> Ranked => _ranked ??= Recommender.Rank(Tasks, Today,
        Capacity.HoursLeftToday(Settings, Tasks, Today), Settings.BacklogOrder);
}

public record ReportLine(string Kind, string Text, Guid? TaskId = null);

public record PriorityTask(TaskItem Task, bool Picked, string Detail, string DefaultAction, string Note);

public record PriorityProject(Project Project, string Name, List<PriorityTask> Tasks, bool Included, string Note, string Detail);

public record MeetingItem(Meeting Meeting, string Key, bool Included, List<ReportLine> Lines, List<string> AutoTexts,
    List<string> FinalTexts, bool Overridden, MeetingRecord? MetThisWeek, string Detail);

public record ProjectItem(Project Project, string Key, string Title, bool Included, List<ProjectLine> Lines, List<string> AutoTexts,
    List<string> FinalTexts, bool Overridden, string Detail, string NoteMeta);

public static class ReportBuilder
{
    public const string Title = "Weekly Touch Base";

    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    public static string Day(DateOnly date) => date.ToString("MMM d", En);

    public static string ShortDay(DateOnly date) => date.ToString("ddd MMM d", En);

    // "Oct 12–16", or "Sep 28–Oct 2" across months.
    public static string Range(DateOnly from, DateOnly to) =>
        from.Month == to.Month && from.Year == to.Year
            ? $"{Day(from)}–{to.Day}"
            : $"{Day(from)}–{Day(to)}";

    // The Excel sheet name, e.g. "Oct 12–16, 2026".
    public static string SheetName(DateOnly from, DateOnly to) =>
        from.Year == to.Year
            ? $"{Range(from, to)}, {to.Year}"
            : $"{Day(from)}, {from.Year}–{Day(to)}, {to.Year}";

    public static string WeekLabel(ReportContext ctx) => "Week of " + Range(ctx.Workdays[0], ctx.Workdays[^1]);

    public static string SheetName(ReportContext ctx) => SheetName(ctx.Workdays[0], ctx.Workdays[^1]);

    public static string Byline(ReportContext ctx) =>
        $"{ctx.Settings.ReportName} · {ctx.Today.ToString("M/d/yyyy", En)} · {WeekLabel(ctx)}";

    public static List<string> SplitLines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    public static string OneLine(string? text) => string.Join("; ", SplitLines(text));

    // Custom wording for this week if Peter wrote some, otherwise the automatic lines.
    public static (List<string> Lines, bool Overridden) Final(ReportWeek report, string key, List<string> auto) =>
        report.Wording.TryGetValue(key, out var custom) ? (SplitLines(custom), true) : (auto, false);

    // ---- Priorities ----

    public static HashSet<Guid> DefaultPicks(ReportContext ctx) =>
        ctx.Ranked.Where(t => !t.IsBlocked).Take(4).Select(t => t.Id).ToHashSet();

    public static string TaskKey(Guid id) => "t:" + id;

    public static string ProjectKey(Guid id) => "p:" + id;

    public static string ProjectNoteKey(Guid id) => "prio-" + id;

    private static PriorityTask PriorityFor(ReportContext ctx, TaskItem task, HashSet<Guid> defaults)
    {
        var report = ctx.Report;
        var picked = report.Picks.TryGetValue(TaskKey(task.Id), out var on) ? on : defaults.Contains(task.Id);
        var due = TaskLabels.Due(task, ctx.Today);
        var meeting = task.MeetingId is null ? null : ctx.Meetings.FirstOrDefault(m => m.Id == task.MeetingId);

        var detail = (meeting is null ? "" : $"Prep · {meeting.Name} · ")
            + (task.Status == WorkStatus.NeedsReview ? "Needs review · " : "")
            + due
            + (task.IsBlocked ? " · blocked" : "");

        var note = report.ActionNotes.TryGetValue(task.Id.ToString(), out var n) ? n : "";
        return new PriorityTask(task, picked, detail, due, note);
    }

    public static List<PriorityProject> PriorityProjects(ReportContext ctx)
    {
        var defaults = DefaultPicks(ctx);

        return ctx.Projects
            .Where(p => p.Status != ProjectStatus.Done)
            .OrderBy(p => p.CreatedAt)
            .Select(p =>
            {
                var tasks = ctx.Ranked.Where(t => t.ProjectId == p.Id).Select(t => PriorityFor(ctx, t, defaults)).ToList();
                var pickedCount = tasks.Count(t => t.Picked);
                var included = (ctx.Report.Picks.TryGetValue(ProjectKey(p.Id), out var on) && on) || pickedCount > 0;
                var note = ctx.Report.ActionNotes.TryGetValue(ProjectNoteKey(p.Id), out var n) ? n : "";
                var detail = tasks.Count == 0
                    ? "No open tasks"
                    : $"{pickedCount} of {tasks.Count} task{(tasks.Count == 1 ? "" : "s")} picked";

                return new PriorityProject(p, p.Name + (p.Status == ProjectStatus.OnHold ? " (on hold)" : ""), tasks, included, note, detail);
            })
            .ToList();
    }

    public static List<PriorityTask> LooseTasks(ReportContext ctx)
    {
        var defaults = DefaultPicks(ctx);
        var active = ctx.Projects.Where(p => p.Status != ProjectStatus.Done).Select(p => p.Id).ToHashSet();

        return ctx.Ranked
            .Where(t => t.ProjectId is null || !active.Contains(t.ProjectId.Value))
            .Select(t => PriorityFor(ctx, t, defaults))
            .ToList();
    }

    private static string Lower(string due) =>
        due.StartsWith("Due ") ? "due " + due[4..]
        : due == "Overdue" ? "overdue"
        : due == "No due date" ? "no due date"
        : due;

    public static List<ReportGroup> PriorityGroups(ReportContext ctx)
    {
        var groups = new List<ReportGroup>();

        foreach (var project in PriorityProjects(ctx).Where(p => p.Included))
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(project.Note))
            {
                lines.Add(project.Note.Trim());
            }

            foreach (var item in project.Tasks.Where(t => t.Picked))
            {
                lines.Add(string.IsNullOrWhiteSpace(item.Note)
                    ? $"{item.Task.Title} ({Lower(item.DefaultAction)})"
                    : $"{item.Task.Title}: {item.Note.Trim()}");
            }

            groups.Add(new ReportGroup(project.Name, lines.Count > 0 ? lines : ["In progress"]));
        }

        foreach (var item in LooseTasks(ctx).Where(t => t.Picked))
        {
            var action = string.IsNullOrWhiteSpace(item.Note) ? item.DefaultAction : item.Note.Trim();
            groups.Add(new ReportGroup(item.Task.Title, [action]));
        }

        return groups;
    }

    // ---- Monthly meetings ----

    public static string MeetWhen(DateOnly date, ReportContext ctx)
    {
        var days = date.DayNumber - ctx.Today.DayNumber;
        if (days < 0) return "Was " + Day(date);
        if (days == 0) return "Today";
        if (days == 1) return "Tomorrow";
        if (date <= ctx.WeekEnd) return "This week";
        if (date <= ctx.WeekEnd.AddDays(7)) return "Next week";
        return $"In {(int)Math.Ceiling((date.DayNumber - ctx.WeekEnd.DayNumber) / 7.0)} weeks";
    }

    public static string PrepState(Meeting meeting, ReportContext ctx)
    {
        var tasks = Meetings.CycleTasks(meeting, ctx.Tasks);
        var done = tasks.Count(t => t.Status == WorkStatus.Done);

        if (tasks.Count == 0) return "";
        if (done == tasks.Count) return "prep done";

        if (done == 0)
        {
            var first = tasks.First(t => t.Status != WorkStatus.Done);
            return "prep due " + (first.DueDate is null ? "soon" : Meetings.DueShort(first.DueDate.Value, ctx.Today));
        }

        return $"{done} of {tasks.Count} prep done";
    }

    public static MeetingRecord? MetThisWeek(Meeting meeting, ReportContext ctx)
    {
        var last = meeting.History.OrderBy(h => h.Date).LastOrDefault();
        return last is not null && last.Date >= ctx.WeekStart && last.Date <= ctx.WeekEnd ? last : null;
    }

    public static List<ReportLine> MeetingLines(Meeting meeting, ReportContext ctx)
    {
        var lines = new List<ReportLine>
        {
            new("When", $"{MeetWhen(meeting.NextDate, ctx)} ({ShortDay(meeting.NextDate)})")
        };

        var prep = Meetings.CycleTasks(meeting, ctx.Tasks);
        if (prep.Count > 0)
        {
            var shown = prep.Take(3).Select(t => Meetings.PrepTitle(meeting, t)
                + (t.Status == WorkStatus.Done ? " (done)"
                    : t.DueDate is null ? "" : $" (due {Meetings.DueShort(t.DueDate.Value, ctx.Today)})"));
            lines.Add(new("Prep", string.Join("; ", shown) + (prep.Count > 3 ? $"; and {prep.Count - 3} more" : "")));
        }

        var agenda = OneLine(meeting.Agenda);
        if (agenda.Length > 0)
        {
            lines.Add(new("Agenda", agenda));
        }

        var met = MetThisWeek(meeting, ctx);
        if (met is not null && ctx.Report.MeetingsWithNotes.Contains(meeting.Id))
        {
            var notes = OneLine(met.Notes);
            lines.Add(new($"Met {ShortDay(met.Date)}", notes.Length > 0 ? notes : "No notes"));
        }

        return lines;
    }

    public static string LineText(ReportLine line) => line.Kind == "When" ? line.Text : $"{line.Kind}: {line.Text}";

    public static string MeetingKey(Guid id) => "meet-" + id;

    public static List<MeetingItem> MeetingItems(ReportContext ctx) =>
        ctx.Meetings.OrderBy(m => m.NextDate).Select(m =>
        {
            var key = MeetingKey(m.Id);
            var lines = MeetingLines(m, ctx);
            var auto = lines.Select(LineText).ToList();
            var (final, overridden) = Final(ctx.Report, key, auto);
            var state = PrepState(m, ctx);
            var detail = $"{ShortDay(m.NextDate)} · {MeetWhen(m.NextDate, ctx)}" + (state.Length > 0 ? " · " + state : "");

            return new MeetingItem(m, key, !ctx.Report.ExcludedMeetings.Contains(m.Id), lines, auto, final, overridden,
                MetThisWeek(m, ctx), detail);
        }).ToList();

    // ---- Ongoing projects ----

    public static string ProjectItemKey(Guid id) => "proj-" + id;

    public static string Summarize(List<ProjectLine> lines)
    {
        var bits = new List<string>();
        var done = lines.Count(l => l.Kind == "Done");
        var next = lines.Count(l => l.Kind == "Next");
        var waiting = lines.Count(l => l.Kind == "Waiting");

        if (done > 0) bits.Add($"{done} done this week");
        if (next > 0) bits.Add($"{next} next");
        if (waiting > 0) bits.Add($"{waiting} waiting");

        return bits.Count > 0 ? string.Join(" · ", bits) : "No task activity this week";
    }

    public static string NoteMeta(Project project, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(project.Note)) return "";
        if (project.NoteUpdated is null) return "Note carried over";
        return project.NoteUpdated == today ? "Note edited today" : "Note carried over from " + Day(project.NoteUpdated.Value);
    }

    public static List<ProjectItem> ProjectItems(ReportContext ctx) =>
        ctx.Projects
            .Where(p => p.Status != ProjectStatus.Done)
            .OrderBy(p => p.Status == ProjectStatus.OnHold)
            .ThenBy(p => p.CreatedAt)
            .Select(p =>
            {
                var key = ProjectItemKey(p.Id);
                var lines = ProjectLines.For(p.Id, ctx.Tasks, ctx.Today, ctx.Settings.WeekStart);
                var auto = lines.Select(l => $"{l.Kind}: {l.Text}").Concat(SplitLines(p.Note)).ToList();
                var (final, overridden) = Final(ctx.Report, key, auto.Count > 0 ? auto : ["No activity this week"]);

                return new ProjectItem(p, key, p.Name + (p.Status == ProjectStatus.OnHold ? " (on hold)" : ""),
                    !ctx.Report.ExcludedProjects.Contains(p.Id), lines, auto, final, overridden,
                    Summarize(lines), NoteMeta(p, ctx.Today));
            })
            .ToList();

    // ---- Highlights ----

    public static string FillAccomplishments(ReportContext ctx) =>
        string.Join("\n", Week.DoneThisWeek(ctx.Tasks, ctx.Today, ctx.Settings.WeekStart).Select(t => t.Title));

    public static string FillBlockers(ReportContext ctx) =>
        string.Join("\n", ctx.Tasks
            .Where(t => (t.Status == WorkStatus.Backlog && t.IsBlocked) || t.Status == WorkStatus.HandedOff)
            .Select(t => t.Status == WorkStatus.HandedOff
                ? $"{t.Title} (with {t.HandedTo})"
                : $"{t.Title} (waiting on {t.BlockedBy})"));

    public static List<ReportGroup> HighlightGroups(ReportContext ctx)
    {
        var report = ctx.Report;
        var groups = new List<ReportGroup>();

        if (!report.ShowHighlights)
        {
            return groups;
        }

        void Add(string title, string text)
        {
            var lines = SplitLines(text);
            if (lines.Count > 0)
            {
                groups.Add(new ReportGroup(title, lines));
            }
        }

        Add("Accomplishments", report.Accomplishments);
        Add("Blockers", report.Blockers);
        Add("Decisions needed", report.Decisions);

        if (report.WhatChangedInReport)
        {
            Add("What changed", report.WhatChanged);
        }

        return groups;
    }

    public static ReportDocument Build(ReportContext ctx)
    {
        var tables = new List<ReportTable>();
        var highlights = HighlightGroups(ctx);

        if (highlights.Count > 0)
        {
            tables.Add(new ReportTable("Highlights", "Details", highlights));
        }

        tables.Add(new ReportTable("Priorities This Week", "Actions", PriorityGroups(ctx)));

        tables.Add(new ReportTable("Monthly Meetings", "Actions/Notes", MeetingItems(ctx)
            .Where(i => i.Included && i.FinalTexts.Count > 0)
            .Select(i => new ReportGroup(i.Meeting.Name, i.FinalTexts))
            .ToList()));

        tables.Add(new ReportTable("Ongoing Projects", "Updates", ProjectItems(ctx)
            .Where(i => i.Included)
            .Select(i => new ReportGroup(i.Title, i.FinalTexts.Count > 0 ? i.FinalTexts : ["No activity this week"]))
            .ToList()));

        return new ReportDocument(Title, Byline(ctx), tables);
    }
}
