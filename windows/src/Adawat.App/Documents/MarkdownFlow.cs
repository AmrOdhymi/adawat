using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using Markdig.Extensions.Abbreviations;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.SmartyPants;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WpfBlock = System.Windows.Documents.Block;
using WpfList = System.Windows.Documents.List;

namespace Adawat.App.Documents;

/// <summary>
/// Renders Markdown into a WPF <see cref="FlowDocument"/>: headings, tables, task lists, quotes,
/// fenced code, pictures, links and footnotes, plus the inline math handled by <see cref="MathText"/>.
/// Markdig does the parsing, so GitHub's dialect arrives without a browser engine or a web view.
/// </summary>
internal static class MarkdownFlow
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public static FlowDocument FromMarkdown(string markdown, string? baseDir = null, Action<string>? open = null, double size = 13)
        => FromMarkdownAsync(markdown, baseDir, open, size).GetAwaiter().GetResult();

    public static async Task<FlowDocument> FromMarkdownAsync(string markdown, string? baseDir = null, Action<string>? open = null, double size = 13, bool yieldForUi = false)
    {
        var rightToLeft = Adawat.Core.Documents.Bidi.IsRightToLeft(markdown);
        var math = new List<MathText.Piece>();
        var prepared = MathText.Protect(markdown, math);
        var parsed = yieldForUi
            ? await Task.Run(() => Markdown.Parse(prepared, Pipeline)).ConfigureAwait(true)
            : Markdown.Parse(prepared, Pipeline);
        var document = FlowKit.NewDocument(rightToLeft, size);
        var context = new Context { Math = math, BaseDir = baseDir, Size = size, Open = open, RightToLeft = rightToLeft };

        await AddBlocksAsync(document.Blocks, parsed, context, yieldForUi).ConfigureAwait(true);
        if (document.Blocks.Count == 0) document.Blocks.Add(FlowKit.Note("Nothing to show."));
        return document;
    }

    /// <summary>Text that is not Markdown keeps its line breaks, but math is still formatted.</summary>
    public static FlowDocument FromPlain(string text, string? baseDir = null, double size = 13)
        => FromPlainAsync(text, baseDir, size).GetAwaiter().GetResult();

    public static async Task<FlowDocument> FromPlainAsync(string text, string? baseDir = null, double size = 13, bool yieldForUi = false)
    {
        var rightToLeft = Adawat.Core.Documents.Bidi.IsRightToLeft(text);
        var math = new List<MathText.Piece>();
        var prepared = yieldForUi
            ? await Task.Run(() => MathText.Protect(text, math)).ConfigureAwait(true)
            : MathText.Protect(text, math);
        var document = FlowKit.NewDocument(rightToLeft, size);

        var chunks = prepared.Split("\n\n");
        for (var i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            if (chunk.Trim().Length == 0) continue;

            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 9), LineHeight = size * 1.6 };
            var lines = chunk.Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                if (line > 0) paragraph.Inlines.Add(new LineBreak());
                MathText.AppendLiteral(paragraph.Inlines, lines[line].Replace("\t", "    "), size, math);
            }
            document.Blocks.Add(paragraph);
            if (yieldForUi && i % 8 == 7)
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }

        if (document.Blocks.Count == 0) document.Blocks.Add(FlowKit.Note("Nothing to show."));
        return document;
    }

    /// <summary>True when the text carries Markdown the reader would otherwise see as symbols.</summary>
    public static bool LooksLikeMarkdown(string text)
    {
        if (text.Length == 0) return false;

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimStart();
            if (line.Length == 0) continue;
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal)) return true;
            if (line.StartsWith("> ", StringComparison.Ordinal)) return true;
            if (line.StartsWith("# ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal)) return true;
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal)) return true;
            if (line.StartsWith('|') && line.EndsWith('|')) return true;
            if (line.Length > 2 && char.IsDigit(line[0]) && (line[1] == '.' || line[1] == ')') && line[2] == ' ') return true;
        }

        return text.Contains("**", StringComparison.Ordinal)
               || text.Contains("](", StringComparison.Ordinal)
               || text.Contains("`", StringComparison.Ordinal);
    }

    private sealed class Context
    {
        public required List<MathText.Piece> Math { get; init; }
        public string? BaseDir { get; init; }
        public double Size { get; init; } = 13;
        public Action<string>? Open { get; init; }
        public bool RightToLeft { get; init; }
    }

    private static void AddBlocks(BlockCollection target, ContainerBlock container, Context context)
    {
        foreach (var block in container) AddBlock(target, block, context);
    }

    private static async Task AddBlocksAsync(BlockCollection target, ContainerBlock container, Context context, bool yieldForUi)
    {
        var i = 0;
        foreach (var block in container)
        {
            AddBlock(target, block, context);
            i++;
            if (yieldForUi && i % 6 == 0)
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private static void AddBlock(BlockCollection target, Markdig.Syntax.Block block, Context context)
    {
        switch (block)
        {
            case HeadingBlock heading:
                AddHeading(target, heading, context);
                break;
            case ParagraphBlock paragraph:
                AddParagraph(target, paragraph, context);
                break;
            case FencedCodeBlock fenced:
                target.Add(FlowKit.CodeBlock(fenced.Lines.ToString(), fenced.Info, context.Size));
                break;
            case CodeBlock code:
                target.Add(FlowKit.CodeBlock(code.Lines.ToString(), null, context.Size));
                break;
            case MdTable table:
                AddTable(target, table, context);
                break;
            case ListBlock list:
                target.Add(BuildList(list, context));
                break;
            case QuoteBlock quote:
                AddQuote(target, quote, context);
                break;
            case ThematicBreakBlock _:
                target.Add(FlowKit.Hairline());
                break;
            case HtmlBlock html:
                var text = FlowKit.StripHtml(html.Lines.ToString());
                if (text.Length > 0) target.Add(new Paragraph(new Run(text)) { Foreground = Theme.Muted, Margin = new Thickness(0, 0, 0, 8) });
                break;
            case FootnoteGroup group:
                AddFootnotes(target, group, context);
                break;
            case LeafBlock leaf when leaf.Inline != null:
                // Definition terms, figure captions and other extension leaves keep their text.
                var extra = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = context.Size * 1.55 };
                AddInlines(extra.Inlines, leaf.Inline, context);
                if (extra.Inlines.Count > 0) target.Add(extra);
                break;
            case ContainerBlock container:
                AddBlocks(target, container, context);
                break;
        }
    }

    private static void AddHeading(BlockCollection target, HeadingBlock heading, Context context)
    {
        var level = Math.Clamp(heading.Level, 1, 6);
        var size = level switch { 1 => 23, 2 => 19, 3 => 16.5, 4 => 15, 5 => 14, _ => 13.5 };

        var paragraph = new Paragraph
        {
            FontSize = size,
            FontWeight = level <= 2 ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = level == 1 ? Theme.Accent : Theme.Ink,
            Margin = new Thickness(0, level == 1 ? 8 : 16, 0, level <= 2 ? 6 : 4),
            LineHeight = size * 1.35,
            KeepWithNext = true,
        };

        AddInlines(paragraph.Inlines, heading.Inline, context);
        target.Add(paragraph);
    }

    private static void AddParagraph(BlockCollection target, ParagraphBlock source, Context context)
    {
        if (TryBlockMath(source.Inline, context, out var math))
        {
            target.Add(math);
            return;
        }

        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 9), LineHeight = context.Size * 1.62 };
        AddInlines(paragraph.Inlines, source.Inline, context);
        if (paragraph.Inlines.Count > 0) target.Add(paragraph);
    }

    private static bool TryBlockMath(ContainerInline? container, Context context, out WpfBlock block)
    {
        block = null!;
        if (container?.FirstChild is not LiteralInline literal || literal.NextSibling != null) return false;
        if (!MathText.TryBlockPiece(literal.Content.ToString(), context.Math, out var piece)) return false;

        block = new BlockUIContainer(MathText.DisplayBlock(piece, context.Size));
        return true;
    }

    private static WpfList BuildList(ListBlock source, Context context)
    {
        var taskList = source.Any(item => item is ListItemBlock entry && HasTask(entry));
        var start = int.TryParse(source.OrderedStart, out var parsed) ? Math.Max(1, parsed) : 1;
        var list = new WpfList
        {
            MarkerStyle = taskList ? TextMarkerStyle.None : source.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            StartIndex = start,
            Margin = new Thickness(20, 2, 0, 10),
            Padding = new Thickness(0),
        };

        foreach (var block in source)
        {
            if (block is not ListItemBlock sourceItem) continue;

            var item = new ListItem();
            foreach (var child in sourceItem)
            {
                switch (child)
                {
                    case ParagraphBlock paragraph:
                        var text = new Paragraph { Margin = new Thickness(0, 1, 0, 3), LineHeight = context.Size * 1.55 };
                        AddInlines(text.Inlines, paragraph.Inline, context);
                        item.Blocks.Add(text);
                        break;
                    case ListBlock nested:
                        item.Blocks.Add(BuildList(nested, context));
                        break;
                    default:
                        AddBlock(item.Blocks, child, context);
                        break;
                }
            }

            if (item.Blocks.Count == 0) item.Blocks.Add(new Paragraph());
            list.ListItems.Add(item);
        }

        return list;
    }

    private static bool HasTask(ListItemBlock item)
    {
        foreach (var block in item)
        {
            if (block is LeafBlock { Inline: { } inline } && HasTask(inline)) return true;
        }
        return false;
    }

    private static bool HasTask(ContainerInline container)
    {
        for (var node = container.FirstChild; node != null; node = node.NextSibling)
        {
            if (node is TaskList) return true;
            if (node is ContainerInline nested && HasTask(nested)) return true;
        }
        return false;
    }

    private static void AddQuote(BlockCollection target, QuoteBlock quote, Context context)
    {
        var thickness = context.RightToLeft ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
        var padding = context.RightToLeft ? new Thickness(0, 2, 12, 2) : new Thickness(12, 2, 0, 2);

        foreach (var block in quote)
        {
            if (block is ParagraphBlock paragraph)
            {
                var quoteParagraph = new Paragraph
                {
                    BorderBrush = Theme.Accent,
                    BorderThickness = thickness,
                    Padding = padding,
                    Margin = new Thickness(2, 2, 0, 8),
                    Foreground = Theme.Muted,
                    FontStyle = FontStyles.Italic,
                    LineHeight = context.Size * 1.55,
                };
                AddInlines(quoteParagraph.Inlines, paragraph.Inline, context);
                target.Add(quoteParagraph);
            }
            else
            {
                AddBlock(target, block, context);
            }
        }
    }

    private static void AddTable(BlockCollection target, MdTable source, Context context)
    {
        var rows = source.OfType<MdTableRow>().ToList();
        if (rows.Count == 0) return;

        var columns = Math.Max(1, rows.Max(row => row.Count));
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

        foreach (var sourceRow in rows)
        {
            var header = sourceRow.IsHeader;
            var row = new TableRow { Background = header ? Theme.Card : Brushes.Transparent };

            foreach (var sourceCell in sourceRow.OfType<MdTableCell>())
            {
                var cell = new TableCell
                {
                    BorderBrush = Theme.Hairline,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(8, 5, 8, 5),
                };

                foreach (var child in sourceCell) AddBlock(cell.Blocks, child, context);
                if (cell.Blocks.Count == 0) cell.Blocks.Add(new Paragraph());

                foreach (var block in cell.Blocks.OfType<Paragraph>())
                {
                    block.FontSize = context.Size * 0.96;
                    block.FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal;
                    block.Margin = new Thickness(0);
                    block.LineHeight = context.Size * 1.45;
                }

                row.Cells.Add(cell);
            }

            group.Rows.Add(row);
        }

        target.Add(table);
    }

    private static void AddFootnotes(BlockCollection target, FootnoteGroup group, Context context)
    {
        target.Add(FlowKit.Hairline(16, 4));

        foreach (var footnote in group.OfType<Footnote>())
        {
            var label = new Paragraph { Margin = new Thickness(0, 2, 0, 0), FontSize = context.Size * 0.9, Foreground = Theme.Muted };
            label.Inlines.Add(new Run($"{footnote.Order}. "));
            target.Add(label);
            AddBlocks(target, footnote, context);
        }
    }

    private static void AddInlines(InlineCollection target, ContainerInline? container, Context context)
    {
        for (var node = container?.FirstChild; node != null; node = node.NextSibling) AddInline(target, node, context);
    }

    private static void AddInline(InlineCollection target, Markdig.Syntax.Inlines.Inline node, Context context)
    {
        switch (node)
        {
            case LiteralInline literal:
                MathText.AppendLiteral(target, literal.Content.ToString(), context.Size, context.Math);
                break;

            case EmphasisInline emphasis:
                AddEmphasis(target, emphasis, context);
                break;

            case CodeInline code:
                target.Add(new Run(code.Content)
                {
                    FontFamily = Theme.Mono,
                    FontSize = context.Size * 0.92,
                    Background = Theme.ButtonFill,
                    Foreground = Theme.Accent,
                });
                break;

            case HtmlEntityInline entity:
                target.Add(new Run(entity.Transcoded.ToString()));
                break;

            case AbbreviationInline abbreviation:
                target.Add(new Run(abbreviation.Abbreviation?.Label ?? ""));
                break;

            case MathInline math:
                target.Add(new Run(math.Content.ToString()) { FontStyle = FontStyles.Italic });
                break;

            case SmartyPant pant:
                target.Add(new Run(SmartyText(pant.Type)));
                break;

            case TaskList task:
                target.Add(new Run(task.Checked ? "☑ " : "☐ ") { Foreground = task.Checked ? Theme.Accent : Theme.Muted });
                break;

            case FootnoteLink footnote:
                target.Add(new Run($"[{footnote.Index}]")
                {
                    FontSize = context.Size * 0.75,
                    BaselineAlignment = BaselineAlignment.Superscript,
                    Foreground = Theme.Accent,
                });
                break;

            case LinkInline link when link.IsImage:
                AddImage(target, link, context);
                break;

            case LinkInline link:
                AddLink(target, link.Url ?? "", link, context);
                break;

            case AutolinkInline auto:
                var autoLink = FlowKit.Link(auto.Url ?? "", context.Open, context.Size);
                autoLink.Inlines.Add(new Run(auto.Url ?? ""));
                target.Add(autoLink);
                break;

            case LineBreakInline lineBreak:
                if (lineBreak.IsHard) target.Add(new LineBreak());
                else target.Add(new Run(" "));
                break;

            case HtmlInline html:
                if (html.Tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase)) target.Add(new LineBreak());
                break;

            case ContainerInline container:
                AddInlines(target, container, context);
                break;
        }
    }

    private static void AddEmphasis(InlineCollection target, EmphasisInline emphasis, Context context)
    {
        var span = new Span();

        switch (emphasis.DelimiterChar)
        {
            case '~' when emphasis.DelimiterCount >= 2:
                span.TextDecorations = TextDecorations.Strikethrough;
                break;
            case '=' when emphasis.DelimiterCount >= 2:
                span.Background = Theme.AccentSoft;
                break;
            default:
                if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                if (emphasis.DelimiterCount == 1 || emphasis.DelimiterCount >= 3) span.FontStyle = FontStyles.Italic;
                break;
        }

        AddInlines(span.Inlines, emphasis, context);
        target.Add(span);
    }

    private static void AddLink(InlineCollection target, string url, ContainerInline content, Context context)
    {
        var link = FlowKit.Link(url, context.Open, context.Size);
        AddInlines(link.Inlines, content, context);
        if (link.Inlines.Count == 0) link.Inlines.Add(new Run(url.Length > 0 ? url : "link"));
        target.Add(link);
    }

    private static void AddImage(InlineCollection target, LinkInline link, Context context)
    {
        var alt = FlowKit.PlainTextOf(AltOf(link));
        var image = FlowKit.LoadImage(link.Url ?? "", context.BaseDir);

        if (image == null)
        {
            target.Add(new Run(alt.Length > 0 ? $"[{alt}]" : "[image]")
            {
                Foreground = Theme.Muted,
                FontStyle = FontStyles.Italic,
            });
            return;
        }

        var picture = new Image
        {
            Source = image,
            Stretch = Stretch.Uniform,
            MaxWidth = 680,
            MaxHeight = 520,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = alt,
        };

        target.Add(new InlineUIContainer(new Border
        {
            Child = picture,
            Margin = new Thickness(0, 4, 0, 4),
        }.Radius("Theme.RadiusSmall")) { BaselineAlignment = BaselineAlignment.Center });
    }

    private static InlineCollection AltOf(LinkInline link)
    {
        var collection = new Paragraph().Inlines;
        for (var node = link.FirstChild; node != null; node = node.NextSibling)
        {
            if (node is LiteralInline literal) collection.Add(new Run(literal.Content.ToString()));
            else if (node is CodeInline code) collection.Add(new Run(code.Content));
        }
        return collection;
    }

    private static string SmartyText(SmartyPantType type) => type switch
    {
        SmartyPantType.LeftQuote => "\u2018",
        SmartyPantType.RightQuote => "\u2019",
        SmartyPantType.LeftDoubleQuote => "\u201C",
        SmartyPantType.RightDoubleQuote => "\u201D",
        SmartyPantType.LeftAngleQuote => "\u00AB",
        SmartyPantType.RightAngleQuote => "\u00BB",
        SmartyPantType.Ellipsis => "\u2026",
        SmartyPantType.Dash2 => "\u2013",
        SmartyPantType.Dash3 => "\u2014",
        _ => "\"",
    };
}
