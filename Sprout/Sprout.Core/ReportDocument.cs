using System.Net;
using System.Text;

namespace Sprout.Core;

// One row group in a report table: the title sits in the left column next to its first line.
public record ReportGroup(string Title, IReadOnlyList<string> Lines);

public record ReportTable(string Heading, string Column, IReadOnlyList<ReportGroup> Groups);

// The finished weekly report, ready for the preview, the email copy and the Excel sheet.
public record ReportDocument(string Title, string Byline, IReadOnlyList<ReportTable> Tables)
{
    private const string Th = "text-align:left;background:#CCFFFF;border:1px solid #999999;padding:3px 6px;font-weight:bold";
    private const string Td = "border:1px solid #999999;padding:3px 6px;vertical-align:top";

    // HTML for pasting into Outlook: inline styles only, so the tables survive the paste.
    public string ToHtml()
    {
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:11pt\">");
        html.Append($"<p style=\"margin:0;font-weight:bold\">{Encode(Title)}</p>");
        html.Append($"<p style=\"margin:0 0 10px\">{Encode(Byline)}</p>");

        foreach (var table in Tables)
        {
            html.Append("<table style=\"border-collapse:collapse;font-family:Calibri,Arial,sans-serif;font-size:11pt;width:640px;margin-bottom:12px\">");
            html.Append($"<tr><th style=\"{Th}\">{Encode(table.Heading)}</th><th style=\"{Th}\">{Encode(table.Column)}</th></tr>");

            foreach (var group in table.Groups)
            {
                for (var i = 0; i < group.Lines.Count; i++)
                {
                    html.Append("<tr>");
                    if (i == 0)
                    {
                        var span = group.Lines.Count > 1 ? $" rowspan=\"{group.Lines.Count}\"" : "";
                        html.Append($"<td{span} style=\"{Td};width:220px\">{Encode(group.Title)}</td>");
                    }
                    html.Append($"<td style=\"{Td}\">{Encode(group.Lines[i])}</td>");
                    html.Append("</tr>");
                }
            }

            html.Append("</table>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    // Plain-text fallback for mail apps that don't take HTML.
    public string ToText()
    {
        var text = new StringBuilder();
        text.AppendLine(Title);
        text.AppendLine(Byline);

        foreach (var table in Tables)
        {
            text.AppendLine();
            text.AppendLine(table.Heading.ToUpperInvariant());

            foreach (var group in table.Groups)
            {
                for (var i = 0; i < group.Lines.Count; i++)
                {
                    text.AppendLine(i == 0 ? $"• {group.Title}: {group.Lines[i]}" : $"    {group.Lines[i]}");
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
