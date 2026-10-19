namespace Sprout.Core;

public class TaskNote
{
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.Now;
}

public class ActivityEntry
{
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.Now;
}

public class RepeatRule
{
    public int Every { get; set; } = 1;
    public RepeatUnit Unit { get; set; } = RepeatUnit.Week;
    public List<DayOfWeek> Days { get; set; } = [];

    public RepeatRule Clone()
    {
        var copy = (RepeatRule)MemberwiseClone();
        copy.Days = [.. Days];
        return copy;
    }
}
