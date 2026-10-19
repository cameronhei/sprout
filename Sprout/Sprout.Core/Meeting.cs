namespace Sprout.Core;

public enum MeetingRuleKind
{
    Monthly = 0,
    EveryWeeks = 1
}

public class MeetingRule
{
    public MeetingRuleKind Kind { get; set; } = MeetingRuleKind.Monthly;

    // Monthly: 1st-4th, or 5 for the last one in the month.
    public int Nth { get; set; } = 1;
    public DayOfWeek Weekday { get; set; } = DayOfWeek.Tuesday;

    // Every N weeks
    public int EveryWeeks { get; set; } = 1;

    public MeetingRule Clone() => (MeetingRule)MemberwiseClone();
}

public class PrepItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public TaskSize Size { get; set; } = TaskSize.S;
    public int WorkdaysBefore { get; set; } = 1;
}

public class MeetingRecord
{
    public DateOnly Date { get; set; }
    public string Agenda { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public MeetingRule Rule { get; set; } = new();
    public DateOnly NextDate { get; set; }
    public ImpactLevel Impact { get; set; } = ImpactLevel.Medium;
    public List<PrepItem> Prep { get; set; } = [];

    // For the upcoming meeting; moved into History once the date passes.
    public string Agenda { get; set; } = "";
    public string Notes { get; set; } = "";

    // Status line for the weekly report.
    public string ReportNote { get; set; } = "";
    public List<MeetingRecord> History { get; set; } = [];

    public Meeting Clone()
    {
        var copy = (Meeting)MemberwiseClone();
        copy.Rule = Rule.Clone();
        copy.Prep = Prep.Select(p => new PrepItem { Id = p.Id, Title = p.Title, Size = p.Size, WorkdaysBefore = p.WorkdaysBefore }).ToList();
        copy.History = History.Select(h => new MeetingRecord { Date = h.Date, Agenda = h.Agenda, Notes = h.Notes }).ToList();
        return copy;
    }
}

// One prep row while the meeting dialog is open. Id is null for a row added in this edit.
public class PrepDraft
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = "";
    public TaskSize Size { get; set; } = TaskSize.S;
    public int WorkdaysBefore { get; set; } = 1;
    public bool Recurring { get; set; } = true;
}
