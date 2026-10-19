using System.Text.Json;
using Sprout.Core;

namespace Sprout.Services;

public class BackupService(TaskStore tasks, SettingsStore settings, ProjectStore projects,
    MeetingStore meetings, ReportStore reports, SproutDb db)
{
    private static readonly JsonSerializerOptions Json = 
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static string FileName() => $"Sprout backup {DateTime.Now:yyyy-MM-dd}.json";

    public Task<bool> FolderSupportedAsync() => db.IsFolderBackupSupportedAsync();

    public BackupFile Snapshot() => new()
    {
        Tasks = tasks.Tasks.Select(t => t.Clone()).ToList(),
        Settings = settings.Current.Clone(),
        Projects = projects.All.Select(p => p.Clone()).ToList(),
        Meetings = meetings.All.Select(m => m.Clone()).ToList(),
        Reports = reports.All.ToList()
    };

    public async Task<string> BackUpNowAsync()
    {
        if (BackupRules.UsesFolder(settings.Current) && await WriteToFolderAsync(allowPrompt: true))
        {
            return $"Backup saved to your {settings.Current.BackupFolderName} folder.";
        }

        var text = JsonSerializer.Serialize(Snapshot(), Json);
        await db.DownloadTextAsync(FileName(), text);

        var updated = settings.Current.Clone();
        updated.LastBackupAt = DateTime.Now;
        await settings.SaveAsync(updated);

        return "Backup file saved to your computer.";
    }

    public async Task<bool?> ChooseFolderAsync()
    {
        var name = await db.ChooseBackupFolderAsync();

        if (name is null)
        {
            return null;
        }

        var updated = settings.Current.Clone();
        updated.AutoBackup = true;
        updated.BackupFolderName = name;
        await settings.SaveAsync(updated);

        return await WriteToFolderAsync(allowPrompt: true);
    }

    public async Task RunAutomaticIfDueAsync()
    {
        var current = settings.Current;

        if (!BackupRules.UsesFolder(current) || !BackupRules.IsDue(current, DateTime.Now))
        {
            return;
        }

        await WriteToFolderAsync(allowPrompt: false);
    }

    private async Task<bool> WriteToFolderAsync(bool allowPrompt)
    {
        var text = JsonSerializer.Serialize(Snapshot(), Json);
        var result = await db.WriteBackupFileAsync(FileName(), text, allowPrompt);
        var ok = result == "ok";

        var updated = settings.Current.Clone();
        updated.LastAutoBackupFailed = !ok;
        if (ok)
        {
            updated.LastBackupAt = DateTime.Now;
        }
        await settings.SaveAsync(updated);

        return ok;
    }

    public async Task RestoreAsync(BackupFile file)
    {
        var current = settings.Current;
        var restored = file.Settings.Clone();

        restored.Id = "settings";
        restored.LastBackupAt = current.LastBackupAt;
        restored.AutoBackup = current.AutoBackup;
        restored.BackupFolderName = current.BackupFolderName;
        restored.LastAutoBackupFailed = current.LastAutoBackupFailed;

        await tasks.ReplaceAllAsync(file.Tasks);
        await settings.SaveAsync(restored);
        await projects.ReplaceAllAsync(file.Projects);
        await meetings.ReplaceAllAsync(file.Meetings);
        await reports.ReplaceAllAsync(file.Reports);
    }

    public static BackupFile? Read(string text)
    {
        try
        {
            var file = JsonSerializer.Deserialize<BackupFile>(text, Json);
            return file is { App: "Sprout" } ? file : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
