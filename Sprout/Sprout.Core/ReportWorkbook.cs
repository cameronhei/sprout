using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Sprout.Core;

public class WorkbookException(string message) : Exception(message);

// Adds this week's report as a new sheet to an existing .xlsx workbook, leaving every other sheet as it was.
// If a sheet with the same name is already there (the report was exported earlier this week), it's replaced.
public static class ReportWorkbook
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string WorksheetType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
    private const string StylesType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
    private const string WorksheetContent = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";

    public static byte[] AddSheet(byte[] workbook, string sheetName, ReportDocument report)
    {
        Dictionary<string, byte[]> parts;
        List<string> order;

        try
        {
            using var input = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
            order = input.Entries.Select(e => e.FullName).ToList();
            parts = input.Entries.ToDictionary(e => e.FullName, e =>
            {
                using var stream = e.Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            });
        }
        catch (InvalidDataException)
        {
            throw new WorkbookException("That file isn't an .xlsx workbook. If it's an older .xls file, open it in Excel and save it as .xlsx first.");
        }

        // Find the workbook part through the package relationships.
        var rootRels = Load(parts, "_rels/.rels") ?? throw Invalid();
        var workbookPath = rootRels.Root!.Elements(Pkg + "Relationship")
            .FirstOrDefault(r => ((string?)r.Attribute("Type") ?? "").EndsWith("/officeDocument"))
            ?.Attribute("Target")?.Value.TrimStart('/') ?? throw Invalid();

        var folder = workbookPath.Contains('/') ? workbookPath[..workbookPath.LastIndexOf('/')] : "";
        var relsPath = (folder.Length > 0 ? folder + "/" : "") + "_rels/" + Path.GetFileName(workbookPath) + ".rels";

        var book = Load(parts, workbookPath) ?? throw Invalid();
        var rels = Load(parts, relsPath) ?? throw Invalid();
        var types = Load(parts, "[Content_Types].xml") ?? throw Invalid();

        string Resolve(string target) => target.StartsWith('/') ? target.TrimStart('/') : (folder.Length > 0 ? folder + "/" : "") + target;

        var stylesRel = rels.Root!.Elements(Pkg + "Relationship").FirstOrDefault(r => (string?)r.Attribute("Type") == StylesType)
            ?? throw new WorkbookException("This workbook has no styles part, so Sprout can't format the new sheet.");
        var stylesPath = Resolve(stylesRel.Attribute("Target")!.Value);
        var styles = Load(parts, stylesPath) ?? throw Invalid();
        var ids = AddStyles(styles);

        var sheets = book.Root!.Element(Main + "sheets") ?? throw Invalid();
        var existing = sheets.Elements(Main + "sheet")
            .FirstOrDefault(s => string.Equals((string?)s.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));

        string sheetPath;

        if (existing is not null)
        {
            var relId = existing.Attribute(R + "id")!.Value;
            var rel = rels.Root.Elements(Pkg + "Relationship").First(r => (string?)r.Attribute("Id") == relId);
            sheetPath = Resolve(rel.Attribute("Target")!.Value);
        }
        else
        {
            var n = 1;
            while (parts.ContainsKey($"{folder}/worksheets/sheet{n}.xml".TrimStart('/')))
            {
                n++;
            }
            sheetPath = $"{folder}/worksheets/sheet{n}.xml".TrimStart('/');

            var relIds = rels.Root.Elements(Pkg + "Relationship").Select(r => (string?)r.Attribute("Id") ?? "").ToHashSet();
            var k = relIds.Count + 1;
            while (relIds.Contains("rId" + k))
            {
                k++;
            }
            var newRelId = "rId" + k;

            rels.Root.Add(new XElement(Pkg + "Relationship",
                new XAttribute("Id", newRelId),
                new XAttribute("Type", WorksheetType),
                new XAttribute("Target", $"worksheets/sheet{n}.xml")));

            var sheetId = sheets.Elements(Main + "sheet").Select(s => (int?)s.Attribute("sheetId") ?? 0).DefaultIfEmpty(0).Max() + 1;
            sheets.Add(new XElement(Main + "sheet",
                new XAttribute("name", sheetName),
                new XAttribute("sheetId", sheetId),
                new XAttribute(R + "id", newRelId)));

            types.Root!.Add(new XElement(Ct + "Override",
                new XAttribute("PartName", "/" + sheetPath),
                new XAttribute("ContentType", WorksheetContent)));

            order.Add(sheetPath);
        }

        parts[sheetPath] = Save(Worksheet(report, ids));
        parts[stylesPath] = Save(styles);
        parts[workbookPath] = Save(book);
        parts[relsPath] = Save(rels);
        parts["[Content_Types].xml"] = Save(types);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in order)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(parts[name]);
            }
        }

        return output.ToArray();
    }

    public static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim('\'');
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static WorkbookException Invalid() =>
        new("Sprout couldn't read that workbook. Make sure it's an .xlsx file that opens in Excel.");

    private static XDocument? Load(Dictionary<string, byte[]> parts, string path)
    {
        if (!parts.TryGetValue(path, out var bytes))
        {
            return null;
        }

        using var stream = new MemoryStream(bytes);
        return XDocument.Load(stream);
    }

    private static byte[] Save(XDocument doc)
    {
        doc.Declaration ??= new XDeclaration("1.0", "UTF-8", "yes");
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            doc.Save(writer);
        }
        return stream.ToArray();
    }

    private record StyleIds(int Title, int Header, int Body, int Group);

    // Adds the fonts, fill, border and cell formats the report uses, and returns their cell format indexes.
    private static StyleIds AddStyles(XDocument styles)
    {
        var root = styles.Root!;
        var fonts = root.Element(Main + "fonts") ?? throw Invalid();
        var fills = root.Element(Main + "fills") ?? throw Invalid();
        var borders = root.Element(Main + "borders") ?? throw Invalid();
        var cellXfs = root.Element(Main + "cellXfs") ?? throw Invalid();

        int Append(XElement list, XElement item)
        {
            list.Add(item);
            var count = list.Elements().Count();
            list.SetAttributeValue("count", count);
            return count - 1;
        }

        XElement Font(int size, bool bold) => new(Main + "font",
            bold ? new XElement(Main + "b") : null,
            new XElement(Main + "sz", new XAttribute("val", size)),
            new XElement(Main + "name", new XAttribute("val", "Calibri")),
            new XElement(Main + "family", new XAttribute("val", 2)));

        var titleFont = Append(fonts, Font(14, true));
        var boldFont = Append(fonts, Font(11, true));
        var bodyFont = Append(fonts, Font(11, false));

        var cyan = Append(fills, new XElement(Main + "fill",
            new XElement(Main + "patternFill", new XAttribute("patternType", "solid"),
                new XElement(Main + "fgColor", new XAttribute("rgb", "FFCCFFFF")),
                new XElement(Main + "bgColor", new XAttribute("indexed", 64)))));

        XElement Side(string name) => new(Main + name, new XAttribute("style", "thin"),
            new XElement(Main + "color", new XAttribute("rgb", "FF999999")));

        var thin = Append(borders, new XElement(Main + "border",
            Side("left"), Side("right"), Side("top"), Side("bottom"), new XElement(Main + "diagonal")));

        XElement Xf(int font, int fill, int border, bool wrap) => new(Main + "xf",
            new XAttribute("numFmtId", 0),
            new XAttribute("fontId", font),
            new XAttribute("fillId", fill),
            new XAttribute("borderId", border),
            new XAttribute("xfId", 0),
            new XAttribute("applyFont", 1),
            fill > 0 ? new XAttribute("applyFill", 1) : null,
            border > 0 ? new XAttribute("applyBorder", 1) : null,
            wrap ? new XAttribute("applyAlignment", 1) : null,
            wrap ? new XElement(Main + "alignment", new XAttribute("vertical", "top"), new XAttribute("wrapText", 1)) : null);

        return new StyleIds(
            Title: Append(cellXfs, Xf(titleFont, 0, 0, false)),
            Header: Append(cellXfs, Xf(boldFont, cyan, thin, true)),
            Body: Append(cellXfs, Xf(bodyFont, 0, thin, true)),
            Group: Append(cellXfs, Xf(bodyFont, 0, thin, true)));
    }

    private static XDocument Worksheet(ReportDocument report, StyleIds ids)
    {
        var rows = new List<XElement>();
        var merges = new List<string>();
        var r = 0;

        XElement Cell(string column, int row, string? text, int style)
        {
            var cell = new XElement(Main + "c", new XAttribute("r", column + row), new XAttribute("s", style));
            if (!string.IsNullOrEmpty(text))
            {
                cell.Add(new XAttribute("t", "inlineStr"),
                    new XElement(Main + "is", new XElement(Main + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
            }
            return cell;
        }

        void Row(params XElement[] cells)
        {
            r++;
            rows.Add(new XElement(Main + "row", new XAttribute("r", r), cells));
        }

        Row(Cell("A", r + 1, report.Title, ids.Title));
        Row(Cell("A", r + 1, report.Byline, 0));

        foreach (var table in report.Tables)
        {
            r++; // blank row before each table
            Row(Cell("A", r + 1, table.Heading, ids.Header), Cell("B", r + 1, table.Column, ids.Header));

            foreach (var group in table.Groups)
            {
                var first = r + 1;
                for (var i = 0; i < group.Lines.Count; i++)
                {
                    Row(Cell("A", r + 1, i == 0 ? group.Title : null, ids.Group), Cell("B", r + 1, group.Lines[i], ids.Body));
                }

                if (group.Lines.Count > 1)
                {
                    merges.Add($"A{first}:A{r}");
                }
            }
        }

        var sheet = new XElement(Main + "worksheet",
            new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
            new XElement(Main + "sheetPr", new XElement(Main + "pageSetUpPr", new XAttribute("fitToPage", 1))),
            new XElement(Main + "sheetViews", new XElement(Main + "sheetView", new XAttribute("workbookViewId", 0))),
            new XElement(Main + "sheetFormatPr", new XAttribute("defaultRowHeight", 15)),
            new XElement(Main + "cols",
                new XElement(Main + "col", new XAttribute("min", 1), new XAttribute("max", 1), new XAttribute("width", 40), new XAttribute("customWidth", 1)),
                new XElement(Main + "col", new XAttribute("min", 2), new XAttribute("max", 2), new XAttribute("width", 90), new XAttribute("customWidth", 1))),
            new XElement(Main + "sheetData", rows));

        if (merges.Count > 0)
        {
            sheet.Add(new XElement(Main + "mergeCells", new XAttribute("count", merges.Count),
                merges.Select(m => new XElement(Main + "mergeCell", new XAttribute("ref", m)))));
        }

        sheet.Add(new XElement(Main + "pageMargins",
            new XAttribute("left", 0.7), new XAttribute("right", 0.7), new XAttribute("top", 0.75),
            new XAttribute("bottom", 0.75), new XAttribute("header", 0.3), new XAttribute("footer", 0.3)));

        // Prints on one page width, landscape.
        sheet.Add(new XElement(Main + "pageSetup",
            new XAttribute("orientation", "landscape"), new XAttribute("fitToWidth", 1), new XAttribute("fitToHeight", 0)));

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), sheet);
    }
}
