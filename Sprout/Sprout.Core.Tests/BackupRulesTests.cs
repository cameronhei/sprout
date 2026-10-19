using Sprout.Core;

namespace Sprout.Core.Tests;

public class BackupRulesTests
{
    private static readonly DateTime FridayAt2 = new(2026, 10, 16, 14, 0, 0);
    private static readonly DateTime FridayAt3 = new(2026, 10, 16, 15, 0, 0);

    [Fact]
    public void Never_backed_up_is_due()
    {
        Assert.True(BackupRules.IsDue(new SproutSettings(), FridayAt2));
    }

    [Fact]
    public void Off_is_never_due()
    {
        var settings = new SproutSettings { BackupFrequency = BackupFrequency.Off };

        Assert.False(BackupRules.IsDue(settings, FridayAt3));
    }

    [Fact]
    public void Backed_up_today_is_not_due()
    {
        var settings = new SproutSettings { LastBackupAt = new DateTime(2026, 10, 16, 9, 0, 0) };

        Assert.False(BackupRules.IsDue(settings, FridayAt3));
    }

    [Fact]
    public void Weekly_reminder_waits_for_the_hour()
    {
        var settings = new SproutSettings { LastBackupAt = new DateTime(2026, 10, 14, 9, 0, 0) };

        Assert.False(BackupRules.IsDue(settings, FridayAt2));
        Assert.True(BackupRules.IsDue(settings, FridayAt3));
    }

    [Fact]
    public void Overdue_reminds_on_any_day()
    {
        var settings = new SproutSettings { LastBackupAt = new DateTime(2026, 10, 7, 9, 0, 0) };
        var wednesdayMorning = new DateTime(2026, 10, 14, 9, 0, 0);

        Assert.True(BackupRules.IsDue(settings, wednesdayMorning));
    }

    [Fact]
    public void Daily_skips_weekends()
    {
        var settings = new SproutSettings
        {
            BackupFrequency = BackupFrequency.Daily,
            BackupHour = 0,
            LastBackupAt = new DateTime(2026, 10, 16, 9, 0, 0)
        };

        Assert.False(BackupRules.IsDue(settings, new DateTime(2026, 10, 17, 10, 0, 0))); // Saturday
    }

    [Theory]
    [InlineData(null, "never")]
    [InlineData(0, "today")]
    [InlineData(1, "yesterday")]
    [InlineData(4, "4 days ago (Oct 12)")]
    public void Ago_text_reads_naturally(int? daysAgo, string expected)
    {
        var today = new DateOnly(2026, 10, 16);
        DateTime? last = daysAgo is null ? null : new DateTime(2026, 10, 16, 9, 0, 0).AddDays(-daysAgo.Value);

        Assert.Equal(expected, BackupRules.AgoText(last, today));
    }

    [Fact]
    public void Schedule_text_describes_the_default()
    {
        Assert.Equal(
            "Reminds you every Friday from 3:00 PM, until you back up. If it's been more than 7 days, it reminds you on any day.",
            BackupRules.ScheduleText(new SproutSettings()));
    }

    private static SproutSettings Auto(bool failed) => new()
    {
        AutoBackup = true,
        BackupFolderName = "Sprout Backups",
        LastAutoBackupFailed = failed
    };

    [Fact]
    public void Working_automatic_backup_never_reminds()
    {
        Assert.False(BackupRules.ShouldRemind(Auto(failed: false), FridayAt3));
    }

    [Fact]
    public void Failed_automatic_backup_reminds_on_any_day()
    {
        var sunday = new DateTime(2026, 10, 18, 9, 0, 0);

        Assert.True(BackupRules.ShouldRemind(Auto(failed: true), sunday));
        Assert.Equal("Your automatic backup didn't run.", BackupRules.ReminderHeadline(Auto(failed: true), sunday));
    }

    [Fact]
    public void Auto_without_a_folder_uses_the_manual_schedule()
    {
        var settings = new SproutSettings { AutoBackup = true };   // no folder chosen, never backed up

        Assert.True(BackupRules.ShouldRemind(settings, FridayAt3));
    }

    [Fact]
    public void Auto_status_explains_each_state()
    {
        Assert.StartsWith("Choose a folder", BackupRules.AutoStatus(new SproutSettings { AutoBackup = true }, FridayAt3));
        Assert.StartsWith("Last automatic backup failed", BackupRules.AutoStatus(Auto(failed: true), FridayAt3));
        Assert.Equal("Working. Last automatic backup: never.", BackupRules.AutoStatus(Auto(failed: false), FridayAt3));
    }
}