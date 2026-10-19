namespace Sprout.Core;

public class SproutSettings
{
    public string Id { get; set; } = "settings";
    public double HoursPerDay { get; set; } = 8;
    public double Buffer { get; set; } = 2.5;
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;
    public List<TimeOffPeriod> TimeOff { get; set; } = [];
    public BackupFrequency BackupFrequency { get; set; } = BackupFrequency.Weekly;
    public DayOfWeek BackupDay { get; set; } = DayOfWeek.Friday;
    public int BackupHour { get; set; } = 15;
    public DateTime? LastBackupAt { get; set; }
    public bool AutoBackup { get; set; }
    public string? BackupFolderName { get; set; }
    public bool LastAutoBackupFailed { get; set; }

    // Shown under the report title.
    public string ReportName { get; set; } = "Peter Heiman";

    // The workbook the weekly sheet is added to (Sprout remembers the file itself).
    public string? ReportWorkbookName { get; set; }

    // Peter's drag order for the backlog (empty = let Sprout order it).
    public List<Guid> BacklogOrder { get; set; } = [];

    public SproutSettings Clone()
    {
        var copy = (SproutSettings)MemberwiseClone();
        copy.TimeOff = [.. TimeOff];
        copy.BacklogOrder = [.. BacklogOrder];
        return copy;
    }
}