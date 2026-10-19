using Sprout.Core;

namespace Sprout.Services;

// Builds the weekly report from the stores, and copies it or adds it to Peter's Excel workbook.
public class ReportService(SproutDb db, TaskStore tasks, SettingsStore settings, ProjectStore projects,
    MeetingStore meetings, ReportStore reports)
{
    // Pass the page's own copy of this week's choices so unsaved edits show in the preview.
    public ReportContext Context(ReportWeek? report = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        return new ReportContext
        {
            Today = today,
            Settings = settings.Current,
            Tasks = tasks.Tasks,
            Projects = projects.All,
            Meetings = meetings.All,
            Report = report ?? reports.For(Week.StartOf(today, settings.Current.WeekStart))
        };
    }

    public async Task<string> CopyAsync(ReportDocument report)
    {
        var result = await db.CopyReportAsync(report.ToHtml(), report.ToText());

        return result switch
        {
            "html" => "Copied. Paste it into your email; the tables come with it.",
            "text" => "Copied as plain text. This browser didn't allow the formatted tables.",
            _ => "The browser blocked copying. Try the button again."
        };
    }

    public Task<bool> CanSaveToFileAsync() => db.IsFilePickerSupportedAsync();

    // Adds (or refreshes) this week's sheet in the remembered workbook. chooseFile asks for the workbook first.
    public async Task<string?> AddToExcelAsync(ReportDocument report, string sheetName, bool chooseFile)
    {
        if (chooseFile || string.IsNullOrEmpty(settings.Current.ReportWorkbookName))
        {
            var name = await db.ChooseReportWorkbookAsync();
            if (name is null)
            {
                return null;
            }

            var updated = settings.Current.Clone();
            updated.ReportWorkbookName = name;
            await settings.SaveAsync(updated);
        }

        var fileName = settings.Current.ReportWorkbookName;
        var status = await db.ReportWorkbookStatusAsync();

        if (status == "no-file")
        {
            return await AddToExcelAsync(report, sheetName, chooseFile: true);
        }

        if (status != "ok")
        {
            return $"Sprout needs permission to save to {fileName}. Click the button again and choose Allow.";
        }

        byte[] updatedBook;

        try
        {
            var bytes = await db.ReadReportWorkbookAsync();
            updatedBook = ReportWorkbook.AddSheet(bytes, ReportWorkbook.SafeSheetName(sheetName), report);
        }
        catch (WorkbookException e)
        {
            return e.Message;
        }

        var written = await db.WriteReportWorkbookAsync(updatedBook);

        return written switch
        {
            "ok" => $"Saved this week's sheet, “{sheetName}”, in {fileName}.",
            "missing" => $"Sprout couldn't find {fileName} anymore. Use “Choose a different workbook” to pick it again.",
            _ => $"Couldn't save to {fileName}. If it's open in Excel, close it and try again."
        };
    }

    // For browsers without file access: adds the sheet to an uploaded copy and downloads the result.
    public async Task<string> AddToUploadedAsync(byte[] workbook, string fileName, ReportDocument report, string sheetName)
    {
        try
        {
            var updated = ReportWorkbook.AddSheet(workbook, ReportWorkbook.SafeSheetName(sheetName), report);
            await db.DownloadBytesAsync(fileName, updated);
            return $"Downloaded {fileName} with this week's sheet. Replace your old copy with it.";
        }
        catch (WorkbookException e)
        {
            return e.Message;
        }
    }
}
