namespace Sprout.Core;

// Everything Peter picked or typed for one week's report. Keyed by the week's start date.
public class ReportWeek
{
    public string Id { get; set; } = "";

    public bool ShowHighlights { get; set; }
    public string Accomplishments { get; set; } = "";
    public string Blockers { get; set; } = "";
    public string Decisions { get; set; } = "";

    // From the This week tab's Week in review.
    public string WhatChanged { get; set; } = "";
    public bool WhatChangedInReport { get; set; } = true;

    // Ticks Peter changed in Priorities this week, keyed "t:{taskId}" or "p:{projectId}".
    // Anything missing uses the default (the top four tasks Sprout recommends).
    public Dictionary<string, bool> Picks { get; set; } = new();

    // What he typed in the Actions column, keyed by task id or "prio-{projectId}".
    public Dictionary<string, string> ActionNotes { get; set; } = new();

    // Meetings and projects are included unless Peter unticks them.
    public List<Guid> ExcludedMeetings { get; set; } = [];
    public List<Guid> MeetingsWithNotes { get; set; } = [];
    public List<Guid> ExcludedProjects { get; set; } = [];

    // Custom wording for this week only, one line per row.
    public Dictionary<string, string> Wording { get; set; } = new();

    public static string KeyFor(DateOnly weekStart) => weekStart.ToString("yyyy-MM-dd");
}
