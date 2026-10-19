using Sprout.Core;

namespace Sprout.Core;

public static class BackupRules
{
    public static int? DaySince(DateTime? lastBackup, DateOnly today)
    {
        if (lastBackup is null)
        {
            return null;
        }

        return today.DayNumber - DateOnly.FromDateTime(lastBackup.Value).DayNumber;
    }

    public static int OverdueAfterDays(BackupFrequency frequency)
    {
        return frequency == BackupFrequency.Daily ? 3 : 7;
    }

    public static bool IsDue(SproutSettings settings, DateTime now)
    {
        if (settings.BackupFrequency == BackupFrequency.Off)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(now);
        var days = DaySince(settings.LastBackupAt, today);

        if (days is null)
        {
            return true;
        }

        if (days == 0)
        {
            return false;
        }

        if (days >= OverdueAfterDays(settings.BackupFrequency))
        {
            return true;
        }

        var scheduledToday = settings.BackupFrequency == BackupFrequency.Daily 
            ? Capacity.IsWorkday(today) : today.DayOfWeek == settings.BackupDay;

        return scheduledToday && now.Hour >= settings.BackupHour;
    }

    public static string AgoText(DateTime? lastBackup, DateOnly today)
    {
        return DaySince(lastBackup, today) switch
        {
            null => "never",
            0 => "today",
            1 => "yesterday",
            var days => $"{days} days ago ({lastBackup!.Value:MMM d})"
        };
    }

    public static string ReminderHeadline(SproutSettings settings, DateTime now)
    {
        if (UsesFolder(settings) && settings.LastAutoBackupFailed)
        {
            return "Your automatic backup didn't run.";
        }

        var days = DaySince(settings.LastBackupAt, DateOnly.FromDateTime(now));

        if (days is null)
        {
            return "You haven't backed up Sprout yet.";
        }

        if (days >= OverdueAfterDays(settings.BackupFrequency))
        {
            return $"It's been {days} days since your last backup.";
        }

        return settings.BackupFrequency == BackupFrequency.Weekly ? $"It's {now.DayOfWeek}. Back up your week before you head out." : "Wrap up the day with a quick backup.";
    }

    public static string HourLabel(int hour)
    {
        return $"{(hour % 12 == 0 ? 12 : hour % 12)}:00 {(hour < 12 ? "AM" : "PM")}";
    }

    public static string ScheduleText(SproutSettings settings)
    {
        if (settings.BackupFrequency == BackupFrequency.Off)
        {
            return "No reminders. You can still back up any time from here.";
        }

        var when = settings.BackupFrequency == BackupFrequency.Daily ? "Reminds you every workday" : $"Reminds you every {settings.BackupDay}";

        var from = settings.BackupHour > 0 ? $" from {HourLabel(settings.BackupHour)}" : "";

        return $"{when}{from}, until you back up. If it's been more than {OverdueAfterDays(settings.BackupFrequency)} days, it reminds you on any day.";
    }

    public static bool UsesFolder(SproutSettings settings) =>
        settings.AutoBackup && settings.BackupFolderName is not null;

    public static bool ShouldRemind(SproutSettings settings, DateTime now) =>
        UsesFolder(settings) ? settings.LastAutoBackupFailed : IsDue(settings, now);

    public static string ReminderDetail(SproutSettings settings, DateTime now)
    {
        var ago = AgoText(settings.LastBackupAt, DateOnly.FromDateTime(now));

        return UsesFolder(settings) && settings.LastAutoBackupFailed
            ? $"Sprout couldn't save to the {settings.BackupFolderName} folder (the folder permission may have expired). " +
            $"Last backup: {ago}. Back up now, or choose the folder again."
            : $"Last backup: {ago}. A backup file keeps your tasks safe if the browser data ever gets cleared.";
    }

    public static string AutoStatus(SproutSettings settings, DateTime now)
    {
        if (settings.BackupFolderName is null)
        {
            return "Choose a folder and Sprout will save a backup there on the schedule below.";
        }

        if (settings.LastAutoBackupFailed)
        {
            return "Last automatic backup failed. You'll see a reminder until it works again.";
        }

        return $"Working. Last automatic backup: {AgoText(settings.LastBackupAt, DateOnly.FromDateTime(now))}.";
    }
}
