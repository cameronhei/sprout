namespace Sprout.Core;

public class TimeOffPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "Time off";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }

    public bool Covers(DateOnly day) => day >= Start && day <= End;
}
