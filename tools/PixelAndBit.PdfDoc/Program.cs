using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var repoRoot = FindRepoRoot();
var inPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repoRoot, "docs", "FINAL_PROJECT_DOCUMENTATION.md");
var outPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(repoRoot, "docs", "FINAL_PROJECT_DOCUMENTATION.pdf");

if (!File.Exists(inPath))
{
    Console.Error.WriteLine("Input not found: " + inPath);
    return 1;
}

var md = File.ReadAllText(inPath, Encoding.UTF8);
var blocks = BlockParser.Parse(md);

Document.Create(document =>
{
    document.Page(cover =>
    {
        cover.Size(PageSizes.A4);
        cover.Margin(2.2f, Unit.Centimetre);
        cover.DefaultTextStyle(t => t.FontSize(11f).FontFamily("Segoe UI"));
        cover.Footer()
            .AlignCenter()
            .Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Colors.Grey.Medium));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        cover.Content()
            .AlignCenter()
            .AlignMiddle()
            .Column(c =>
            {
                c.Item().Text("Pixel&Bit").FontSize(28f).Bold().FontColor(Colors.Blue.Darken4);
                c.Item().Height(8);
                c.Item().Text("Technical Documentation").FontSize(14f).FontColor(Colors.Grey.Darken1);
            });
    });

    document.Page(content =>
    {
        content.Size(PageSizes.A4);
        content.MarginHorizontal(1.4f, Unit.Centimetre);
        content.MarginTop(0.7f, Unit.Centimetre);
        content.MarginBottom(0.6f, Unit.Centimetre);
        content.DefaultTextStyle(t => t.FontSize(9.5f).LineHeight(1.35f).FontFamily("Segoe UI"));
        content.Header()
            .Text("Pixel&Bit")
            .FontSize(7.5f)
            .FontColor(Colors.Grey.Medium)
            .Bold();
        content.Footer()
            .AlignCenter()
            .Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Colors.Grey.Medium));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        content.Content()
            .Column(col =>
        {
            foreach (var b in blocks)
                b.Add(col);
        });
    });
}).GeneratePdf(outPath);

Console.WriteLine("Wrote: " + outPath);
return 0;

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null)
    {
        if (File.Exists(Path.Combine(d.FullName, "PixelAndBit.Web", "PixelAndBit.Web.csproj")))
            return d.FullName;
        d = d.Parent;
    }
    return Directory.GetCurrentDirectory();
}

file static class BlockParser
{
    public static List<IBlock> Parse(string md)
    {
        var lines = md.Split(["\r\n", "\n"], StringSplitOptions.None);
        var list = new List<IBlock>();
        for (var i = 0; i < lines.Length; i++)
        {
            var t = lines[i].TrimEnd();
            if (t.Trim().Length == 0) continue;
            if (t.Trim() == "---") { list.Add(new HRuleBlock()); continue; }
            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                var sb = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    sb.AppendLine(lines[i]);
                    i++;
                }
                list.Add(new CodeBlockBlock(sb.ToString().TrimEnd()));
                continue;
            }
            if (t.StartsWith('|') && t.Contains('|', StringComparison.Ordinal))
            {
                var tableLines = new List<string> { t };
                i++;
                while (i < lines.Length)
                {
                    var l = lines[i].Trim();
                    if (l.Length == 0) break;
                    if (Regex.IsMatch(l, @"^\|[-:\s|]+\|")) { i++; continue; }
                    if (!l.StartsWith('|')) { i--; break; }
                    tableLines.Add(l);
                    i++;
                }
                if (tableLines.Count > 0)
                    list.Add(new TableBlock(ParseTable(tableLines)));
                continue;
            }
            if (t.StartsWith("### ", StringComparison.Ordinal))
            { list.Add(new H3Block(Clean(t[4..]))); continue; }
            if (t.StartsWith("## ", StringComparison.Ordinal))
            { list.Add(new H2Block(Clean(t[3..]))); continue; }
            if (t.StartsWith("# ", StringComparison.Ordinal))
            { list.Add(new H1Block(Clean(t[2..]))); continue; }
            if (t.StartsWith("- ", StringComparison.Ordinal))
            { list.Add(new BulletBlock(Clean(t[2..]))); continue; }
            if (Regex.IsMatch(t, @"^\d+\.\s"))
            {
                var m = Regex.Match(t, @"^\d+\.\s*(.*)$");
                list.Add(new NumberBlock(Clean(m.Groups[1].Value)));
                continue;
            }
            list.Add(new ParaBlock(Clean(t)));
        }
        return list;
    }

    private static IReadOnlyList<string[]> ParseTable(List<string> lines)
    {
        var rows = new List<string[]>();
        foreach (var line in lines)
        {
            var s = line.Trim();
            if (Regex.IsMatch(s, @"^\|[-:\s|]+\|")) continue;
            if (!s.StartsWith('|') || s.Length < 2) continue;
            var parts = s.TrimStart(['|']).TrimEnd(['|']).Split('|');
            for (var j = 0; j < parts.Length; j++) parts[j] = Clean(parts[j].Trim());
            if (parts.Length == 0 || (parts.Length == 1 && parts[0].Length == 0)) continue;
            rows.Add(parts);
        }
        return rows;
    }

    private static string Clean(string s) => s.Replace("**", "", StringComparison.Ordinal).Trim();
}

file interface IBlock
{
    void Add(ColumnDescriptor c);
}

file sealed class HRuleBlock : IBlock
{
    public void Add(ColumnDescriptor c) => c.Item().LineHorizontal(0.7f).LineColor(Colors.Grey.Lighten1);
}

file sealed class H1Block(string T) : IBlock
{
    public void Add(ColumnDescriptor c) => c.Item().PaddingTop(8)
        .Text(T).FontSize(14f).Bold().FontColor(Colors.Blue.Darken4);
}

file sealed class H2Block(string T) : IBlock
{
    public void Add(ColumnDescriptor c) => c.Item().PaddingTop(6).Text(T).FontSize(12f).Bold();
}

file sealed class H3Block(string T) : IBlock
{
    public void Add(ColumnDescriptor c) => c.Item().PaddingTop(4).Text(T).FontSize(10.5f).Bold();
}

file sealed class ParaBlock : IBlock
{
    public ParaBlock(string t) => Text = t;
    public string Text { get; }
    public void Add(ColumnDescriptor c) => c.Item().PaddingTop(1).Text(Text).AlignLeft();
}

file sealed class BulletBlock : IBlock
{
    public BulletBlock(string t) => Text = t;
    public string Text { get; }
    public void Add(ColumnDescriptor c) => c.Item().Row(r =>
    {
        r.AutoItem().Text("•");
        r.RelativeItem(1).Text(Text);
    });
}

file sealed class NumberBlock : IBlock
{
    public NumberBlock(string t) => Text = t;
    public string Text { get; }
    public void Add(ColumnDescriptor c) => c.Item().Text(Text);
}

file sealed class CodeBlockBlock : IBlock
{
    public CodeBlockBlock(string t) => Code = t;
    public string Code { get; }
    public void Add(ColumnDescriptor c) => c.Item()
        .PaddingTop(2)
        .Background(Colors.Grey.Lighten4)
        .Border(0.3f)
        .BorderColor(Colors.Grey.Lighten1)
        .Padding(6)
        .Text(Code)
        .FontSize(7.5f)
        .FontFamily("Consolas")
        .LineHeight(1.25f);
}

file sealed class TableBlock : IBlock
{
    public TableBlock(IReadOnlyList<string[]> rows) => Rows = rows;
    public IReadOnlyList<string[]> Rows { get; }
    public void Add(ColumnDescriptor c) => c.Item().PaddingTop(2).Table(t =>
    {
        if (Rows.Count == 0) return;
        var n = Rows[0].Length;
        t.ColumnsDefinition(cols =>
        {
            for (var i = 0; i < n; i++) cols.RelativeColumn();
        });
        foreach (var row in Rows)
        for (var j = 0; j < n; j++)
            t.Cell()
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(5)
                .Text(row.Length > j ? row[j] : "");
    });
}
