using System.IO.Compression;
using System.Text;
using Sprout.Core;

namespace Sprout.Core.Tests;

public class ReportWorkbookTests
{
    private static readonly ReportDocument Report = new("Weekly Touch Base", "Peter Heiman · 10/14/2026 · Week of Oct 12–16",
    [
        new ReportTable("Priorities This Week", "Actions", [new ReportGroup("Supplier audits", ["Book Ohio (due Friday)", "Send survey"])]),
        new ReportTable("Ongoing Projects", "Updates", [new ReportGroup("Labels", ["No activity this week"])])
    ]);

    // The smallest workbook Excel itself would open: one sheet called "Oct 5–9, 2026".
    private static byte[] Workbook()
    {
        var files = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""",
            ["_rels/.rels"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""",
            ["xl/workbook.xml"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Oct 5–9, 2026" sheetId="1" r:id="rId1"/></sheets></workbook>""",
            ["xl/_rels/workbook.xml.rels"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""",
            ["xl/worksheets/sheet1.xml"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Last week</t></is></c></row></sheetData></worksheet>""",
            ["xl/styles.xml"] = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs></styleSheet>"""
        };

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
        }
        return stream.ToArray();
    }

    private static string Read(byte[] workbook, string path)
    {
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry(path)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Adds_a_new_sheet_and_keeps_the_old_one()
    {
        var result = ReportWorkbook.AddSheet(Workbook(), "Oct 12–16, 2026", Report);

        var book = Read(result, "xl/workbook.xml");
        Assert.Contains("Oct 5–9, 2026", book);
        Assert.Contains("name=\"Oct 12–16, 2026\" sheetId=\"2\"", book);
        Assert.Contains("Last week", Read(result, "xl/worksheets/sheet1.xml"));
        Assert.Contains("/xl/worksheets/sheet2.xml", Read(result, "[Content_Types].xml"));

        var sheet = Read(result, "xl/worksheets/sheet2.xml");
        Assert.Contains("Weekly Touch Base", sheet);
        Assert.Contains("Book Ohio (due Friday)", sheet);
        Assert.Contains("<mergeCell ref=\"A5:A6\"", sheet);
        Assert.Contains("FFCCFFFF", Read(result, "xl/styles.xml"));
    }

    [Fact]
    public void Exporting_the_same_week_again_replaces_its_sheet()
    {
        var once = ReportWorkbook.AddSheet(Workbook(), "Oct 12–16, 2026", Report);
        var twice = ReportWorkbook.AddSheet(once, "Oct 12–16, 2026", Report);

        var book = Read(twice, "xl/workbook.xml");
        Assert.Equal(1, book.Split("Oct 12–16, 2026").Length - 1);
    }

    [Fact]
    public void Not_a_workbook_gives_a_friendly_message()
    {
        var error = Record(() => ReportWorkbook.AddSheet(Encoding.UTF8.GetBytes("hello"), "x", Report));

        Assert.Contains("isn't an .xlsx workbook", error);
    }

    [Fact]
    public void Sheet_names_drop_characters_excel_rejects()
    {
        Assert.Equal("Oct 12–16, 2026", ReportWorkbook.SafeSheetName("Oct 12–16, 2026"));
        Assert.Equal("ab", ReportWorkbook.SafeSheetName("a/b?"));
    }

    private static string Record(Action action)
    {
        try
        {
            action();
            return "";
        }
        catch (WorkbookException e)
        {
            return e.Message;
        }
    }
}
