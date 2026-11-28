using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using D = Adawat.Core.Documents;

namespace Adawat.App.Documents;

/// <summary>
/// Draws the format-neutral <see cref="D.Doc"/> that Word, OpenDocument, RTF, EPUB, HTML and PDF
/// text already read into: headings, paragraphs, lists, tables, quotes, code and pictures. It is
/// read-only and uses no browser, so a dropped document opens in the same window as a note.
/// </summary>
internal static class DocFlow
{
    public static FlowDocument FromDoc(D.Doc doc, string? baseDir = null, Action<string>? open = null, double size = 13)
        => FromDocAsync(doc, baseDir, open, size).GetAwaiter().GetResult();

    public static async Task<FlowDocument> FromDocAsync(D.Doc doc, string? baseDir = null, Action<string>? open = null, double size = 13, bool yieldForUi = false, int initialBlocks = 0, Action<FlowDocument>? onFirstBatch = null)
    {
        var rightToLeft = D.Bidi.IsRightToLeft(doc.PlainText);
        var flow = FlowKit.NewDocument(rightToLeft, size);
        var context = new Context { BaseDir = baseDir, Size = size, Open = open, RightToLeft = rightToLeft };

        var index = 0;
        var built = 0;
        var shown = false;
        while (index < doc.Blocks.Count)
        {
            var block = doc.Blocks[index];
            if (block is D.ListItem) { AddList(flow.Blocks, doc, ref index, context); built++; }
            else { AddBlock(flow.Blocks, block, context); index++; built++; }
            if (!shown && initialBlocks > 0 && built >= initialBlocks)
            {
                shown = true;
                onFirstBatch?.Invoke(flow);
                await Dispatcher.Yield(DispatcherPriority.Background);
                continue;
            }
            if (yieldForUi && built % 8 == 0)
                await Dispatcher.Yield(DispatcherPriority.Background);
        }

        if (flow.Blocks.Count == 0) flow.Blocks.Add(FlowKit.Note("Nothing to show."));
        if (!shown) onFirstBatch?.Invoke(flow);
        return flow;
    }

    private sealed class Context
    {
        public string? BaseDir { get; init; }
        public double Size { get; init; } = 13;
        public Action<string>? Open { get; init; }
        public bool RightToLeft { get; init; }
    }

    private static void AddBlock(BlockCollection target, D.Block block, Context context)
    {
        switch (block)
        {
            case D.Heading heading:
                var level = Math.Clamp(heading.Level, 1, 6);
                var size = level switch { 1 => 23, 2 => 19, 3 => 16.5, 4 => 15, 5 => 14, _ => 13.5 };
                var title = new Paragraph
                {
                    FontSize = size,
                    FontWeight = level <= 2 ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = level == 1 ? Theme.Accent : Theme.Ink,
                    Margin = new Thickness(0, level <= 2 ? 16 : 12, 0, 6),
                    LineHeight = size * 1.35,
                    KeepWithNext = true,
                };
                AddSpans(title.Inlines, heading.Spans, context);
                target.Add(title);
                break;

            case D.Paragraph paragraph:
                var body = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 9),
                    LineHeight = context.Size * 1.6,
                    TextAlignment = paragraph.Align switch
                    {
                        D.Align.Center => TextAlignment.Center,
                        D.Align.Justify => TextAlignment.Justify,
                        D.Align.End => context.RightToLeft ? TextAlignment.Left : TextAlignment.Right,
                        _ => context.RightToLeft ? TextAlignment.Right : TextAlignment.Left,
                    },
                };
                AddSpans(body.Inlines, paragraph.Spans, context);
                if (body.Inlines.Count > 0) target.Add(body);
                break;

            case D.Quote quote:
                var quoted = new Paragraph
                {
                    BorderBrush = Theme.Accent,
                    BorderThickness = context.RightToLeft ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
                    Padding = context.RightToLeft ? new Thickness(0, 2, 12, 2) : new Thickness(12, 2, 0, 2),
                    Margin = new Thickness(2, 2, 0, 8),
                    Foreground = Theme.Muted,
                    FontStyle = FontStyles.Italic,
                    LineHeight = context.Size * 1.55,
                };
                AddSpans(quoted.Inlines, quote.Spans, context);
                target.Add(quoted);
                break;

            case D.CodeBlock code:
                target.Add(FlowKit.CodeBlock(code.Code, null, context.Size));
                break;

            case D.Table table:
                AddTable(target, table, context);
                break;

            case D.Picture picture:
                AddPicture(target, picture);
                break;

            case D.PageBreak _:
                target.Add(FlowKit.Hairline(18, 18));
                break;
        }
    }

    private static void AddList(BlockCollection target, D.Doc doc, ref int index, Context context)
    {
        var first = (D.ListItem)doc.Blocks[index];
        var depth = first.Depth;
        var list = NewList(first.Ordered);

        while (index < doc.Blocks.Count && doc.Blocks[index] is D.ListItem item)
        {
            if (item.Depth > depth)
            {
                var last = list.ListItems.OfType<ListItem>().LastOrDefault();
                if (last != null) AddList(last.Blocks, doc, ref index, context);
                else index++;
                continue;
            }

            if (item.Depth < depth) break;

            var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 3), LineHeight = context.Size * 1.55 };
            AddSpans(paragraph.Inlines, item.Spans, context);
            list.ListItems.Add(new ListItem(paragraph));
            index++;
        }

        if (list.ListItems.Count > 0) target.Add(list);
    }

    private static System.Windows.Documents.List NewList(bool ordered) => new()
    {
        MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
        Margin = new Thickness(20, 2, 0, 10),
        Padding = new Thickness(0),
    };

    private static void AddSpans(InlineCollection target, IEnumerable<D.Span> spans, Context context)
    {
        foreach (var span in spans)
        {
            var lines = span.Text.Replace("\r\n", "\n").Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                if (line > 0) target.Add(new LineBreak());
                if (lines[line].Length == 0) continue;

                Inline inline = new Run(lines[line]);

                if (span.Code)
                {
                    inline.FontFamily = Theme.Mono;
                    inline.FontSize = context.Size * 0.92;
                    inline.Background = Theme.ButtonFill;
                    inline.Foreground = Theme.Accent;
                }

                if (span.Bold) inline = new Bold(inline);
                if (span.Italic) inline = new Italic(inline);
                if (span.Underline) inline = new Underline(inline);

                if (span.Link is { Length: > 0 } url)
                {
                    var link = new Hyperlink(inline) { Foreground = Theme.Accent, ToolTip = url };
                    var open = context.Open;
                    if (open != null) link.Click += (_, _) => open(url);
                    inline = link;
                }

                target.Add(inline);
            }
        }
    }

    private static void AddTable(BlockCollection target, D.Table source, Context context)
    {
        var columns = source.Rows.Count > 0 ? source.Rows.Max(row => row.Count) : 0;
        if (columns == 0) return;

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 12),
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
        };

        for (var column = 0; column < columns; column++)
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        for (var row = 0; row < source.Rows.Count; row++)
        {
            var header = row == 0 && source.HeaderRow;
            var tableRow = new TableRow { Background = header ? Theme.Card : Brushes.Transparent };

            foreach (var cellSpans in source.Rows[row])
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0),
                    FontSize = context.Size * 0.96,
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    LineHeight = context.Size * 1.45,
                };
                AddSpans(paragraph.Inlines, cellSpans, context);

                var cell = new TableCell(paragraph)
                {
                    BorderBrush = Theme.Hairline,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(8, 5, 8, 5),
                };
                tableRow.Cells.Add(cell);
            }

            group.Rows.Add(tableRow);
        }

        target.Add(table);
    }

    private static void AddPicture(BlockCollection target, D.Picture picture)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(picture.Data);
            image.EndInit();
            image.Freeze();

            target.Add(FlowKit.ImageBlock(image, picture.Alt));
        }
        catch
        {
            target.Add(FlowKit.Note(picture.Alt.Length > 0 ? $"[{picture.Alt}]" : "[image]"));
        }
    }
}
