using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfList = System.Windows.Documents.List;

namespace Adawat.App.Documents;

/// <summary>
/// Reads LaTeX source into the same <see cref="FlowDocument"/> the Markdown preview uses. It is a
/// reader, not a TeX engine: sections, lists, quotes, code, tables, figures, links, footnotes and the
/// usual formatting commands become normal document elements, formulas go through <see cref="MathText"/>,
/// and an unknown command keeps its argument so nothing turns into a blank page.
/// </summary>
internal static class TexFlow
{
    private enum InlineStyle { Plain, Bold, Italic, Underline, Strike, Mono, SmallCaps, Superscript, Subscript }

    private sealed class Context
    {
        public double Size { get; init; } = 13;
        public string? BaseDir { get; init; }
        public Action<string>? Open { get; init; }
        public bool RightToLeft { get; set; }
        public List<MathText.Piece> Math { get; } = [];
        public List<string> Notes { get; } = [];
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Date { get; set; } = "";
        public int NoteCount { get; set; }
    }

    private static readonly Dictionary<string, int> SectionLevels = new(StringComparer.Ordinal)
    {
        ["part"] = 1,
        ["chapter"] = 1,
        ["section"] = 2,
        ["subsection"] = 3,
        ["subsubsection"] = 4,
        ["paragraph"] = 5,
        ["subparagraph"] = 6,
    };

    private static readonly Dictionary<string, InlineStyle> Styles = new(StringComparer.Ordinal)
    {
        ["textbf"] = InlineStyle.Bold,
        ["textmd"] = InlineStyle.Plain,
        ["textit"] = InlineStyle.Italic,
        ["emph"] = InlineStyle.Italic,
        ["textsl"] = InlineStyle.Italic,
        ["textup"] = InlineStyle.Plain,
        ["textnormal"] = InlineStyle.Plain,
        ["textrm"] = InlineStyle.Plain,
        ["textsf"] = InlineStyle.Plain,
        ["text"] = InlineStyle.Plain,
        ["mbox"] = InlineStyle.Plain,
        ["hbox"] = InlineStyle.Plain,
        ["makebox"] = InlineStyle.Plain,
        ["underline"] = InlineStyle.Underline,
        ["uline"] = InlineStyle.Underline,
        ["sout"] = InlineStyle.Strike,
        ["st"] = InlineStyle.Strike,
        ["xout"] = InlineStyle.Strike,
        ["texttt"] = InlineStyle.Mono,
        ["textsc"] = InlineStyle.SmallCaps,
        ["textsuperscript"] = InlineStyle.Superscript,
        ["textsubscript"] = InlineStyle.Subscript,
    };

    private static readonly Dictionary<char, char> Accents = new()
    {
        ['\''] = '\u0301',
        ['`'] = '\u0300',
        ['^'] = '\u0302',
        ['"'] = '\u0308',
        ['~'] = '\u0303',
        ['='] = '\u0304',
        ['.'] = '\u0307',
        ['c'] = '\u0327',
        ['v'] = '\u030C',
        ['u'] = '\u0306',
        ['H'] = '\u030B',
        ['k'] = '\u0328',
        ['r'] = '\u030A',
        ['d'] = '\u0323',
        ['b'] = '\u0331',
        ['t'] = '\u0361',
    };

    private static readonly Dictionary<string, string> Letters = new(StringComparer.Ordinal)
    {
        ["oe"] = "œ", ["OE"] = "Œ", ["ae"] = "æ", ["AE"] = "Æ", ["aa"] = "å", ["AA"] = "Å",
        ["o"] = "ø", ["O"] = "Ø", ["ss"] = "ß", ["l"] = "ł", ["L"] = "Ł", ["i"] = "ı", ["j"] = "ȷ",
        ["LaTeX"] = "LaTeX", ["TeX"] = "TeX", ["BibTeX"] = "BibTeX",
    };

    private static readonly Dictionary<string, string> TextSymbols = new(StringComparer.Ordinal)
    {
        ["textbackslash"] = "\\", ["textasciitilde"] = "~", ["textasciicircum"] = "^",
        ["textless"] = "<", ["textgreater"] = ">", ["textbar"] = "|", ["textunderscore"] = "_",
        ["textbraceleft"] = "{", ["textbraceright"] = "}", ["textquotesingle"] = "'",
        ["textquotedbl"] = "\"", ["textquoteleft"] = "‘", ["textquoteright"] = "’",
        ["textquotedblleft"] = "“", ["textquotedblright"] = "”", ["textendash"] = "–",
        ["textemdash"] = "—", ["textellipsis"] = "…", ["textbullet"] = "•",
        ["textperiodcentered"] = "·", ["textdegree"] = "°", ["textpm"] = "±", ["textmu"] = "µ",
        ["textohm"] = "Ω", ["texteuro"] = "€", ["textsterling"] = "£", ["textyen"] = "¥",
        ["textcopyright"] = "©", ["textregistered"] = "®", ["texttrademark"] = "™",
        ["textsection"] = "§", ["textparagraph"] = "¶", ["textdagger"] = "†", ["textdaggerdbl"] = "‡",
    };

    private static readonly HashSet<string> MathEnvironments = new(StringComparer.Ordinal)
    {
        "equation", "equation*", "align", "align*", "alignat", "alignat*", "flalign", "flalign*",
        "gather", "gather*", "multline", "multline*", "eqnarray", "eqnarray*", "displaymath", "math",
    };

    private static readonly HashSet<string> CodeEnvironments = new(StringComparer.Ordinal)
    {
        "verbatim", "verbatim*", "lstlisting", "minted", "alltt",
    };

    private static readonly HashSet<string> QuoteEnvironments = new(StringComparer.Ordinal)
    {
        "quote", "quotation", "verse",
    };

    private static readonly HashSet<string> TheoremEnvironments = new(StringComparer.Ordinal)
    {
        "theorem", "lemma", "proposition", "corollary", "definition", "remark", "example", "proof",
        "claim", "conjecture", "assumption", "observation",
    };

    private static readonly HashSet<string> IgnoredCommands = new(StringComparer.Ordinal)
    {
        "documentclass", "usepackage", "RequirePackage", "usepackage", "bibliographystyle",
        "pagestyle", "thispagestyle", "pagenumbering", "setcounter", "addtocounter", "setlength",
        "addtolength", "newcommand", "renewcommand", "providecommand", "DeclareMathOperator",
        "hypersetup", "geometry", "lstset", "sisetup", "captionsetup", "graphicspath", "def", "let",
        "noindent", "indent", "centering", "raggedright", "raggedleft", "sloppy", "frenchspacing",
        "smallskip", "medskip", "bigskip", "vfill", "hfill", "clearpage", "newpage", "pagebreak",
        "tableofcontents", "listoffigures", "listoftables", "appendix", "protect", "ignorespaces",
        "unskip", "selectfont", "normalfont", "rmfamily", "sffamily", "ttfamily", "bfseries",
        "itshape", "slshape", "scshape", "upshape", "tiny", "scriptsize", "footnotesize", "small",
        "normalsize", "large", "Large", "LARGE", "huge", "Huge", "maketitle",
    };

    private static readonly string[] PictureExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp"];

    private static readonly HashSet<string> OneGroupCommands = new(StringComparer.Ordinal)
    {
        "documentclass", "usepackage", "RequirePackage", "bibliographystyle", "pagestyle", "thispagestyle",
        "pagenumbering", "hypersetup", "geometry", "lstset", "sisetup", "captionsetup", "graphicspath",
    };

    private static readonly HashSet<string> TwoGroupCommands = new(StringComparer.Ordinal)
    {
        "setcounter", "addtocounter", "setlength", "addtolength", "DeclareMathOperator",
    };

    private static readonly Regex RulePattern = new(
        @"\\(?:hline|toprule|midrule|bottomrule|addlinespace|specialrule|cline|cmidrule)(?:\([^)]*\))?(?:\[[^\]]*\])?(?:\{[^}]*\})*",
        RegexOptions.Compiled);

    private static readonly Regex MathCleanup = new(
        @"\\(?:label|tag)\{[^}]*\}|\\(?:nonumber|notag)",
        RegexOptions.Compiled);

    private static readonly Regex LatexCommand = new(@"\\[a-zA-Z@]+\*?", RegexOptions.Compiled);
    private static readonly Regex LatexEnvironment = new(@"\\(?:begin|end)\s*\{", RegexOptions.Compiled);

    private static readonly HashSet<string> LatexSignals = new(StringComparer.Ordinal)
    {
        "documentclass", "usepackage", "RequirePackage", "section", "subsection", "subsubsection",
        "chapter", "part", "paragraph", "subparagraph", "frac", "dfrac", "tfrac", "sqrt",
        "textbf", "textit", "emph", "underline", "cite", "ref", "label", "item",
        "includegraphics", "caption", "centering", "noindent", "tableofcontents", "maketitle",
        "bibliography", "bibliographystyle", "newpage", "clearpage", "hspace", "vspace",
    };

    /// <summary>
    /// True when text pasted without a file extension still reads as LaTeX: a document preamble,
    /// an environment, or several known structural/math commands. A Windows path or a lone
    /// backslash command is not enough.
    /// </summary>
    public static bool LooksLikeLatex(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var source = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // A real preamble is unambiguous even if the body also carries Markdown-looking text.
        if (source.Contains("\\documentclass", StringComparison.Ordinal) ||
            source.Contains("\\begin{document}", StringComparison.Ordinal) ||
            source.Contains("\\usepackage", StringComparison.Ordinal))
            return true;

        // Markdown with inline TeX math should stay Markdown; only a document without Markdown
        // structure is treated as a LaTeX fragment.
        if (MarkdownFlow.LooksLikeMarkdown(source)) return false;

        if (LatexEnvironment.IsMatch(source)) return true;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in LatexCommand.Matches(source))
        {
            var name = match.Value.TrimStart('\\').TrimEnd('*');
            if (LatexSignals.Contains(name)) seen.Add(name);
        }

        return seen.Count >= 2 || (seen.Count >= 1 && source.TrimStart().StartsWith('\\'));
    }

    public static FlowDocument FromLatex(string source, string? baseDir = null, Action<string>? open = null, double size = 13)
    {
        var text = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var context = new Context { Size = size, BaseDir = baseDir, Open = open };
        var clean = RemoveComments(text);
        context.Title = ReadMetadata(clean, "title");
        context.Author = ReadMetadata(clean, "author");
        context.Date = ReadMetadata(clean, "date");

        var body = DocumentBody(clean);
        context.RightToLeft = Adawat.Core.Documents.Bidi.IsRightToLeft(body);
        var prepared = MathText.Protect(body, context.Math);

        var document = FlowKit.NewDocument(context.RightToLeft, size);
        RenderBlocks(document.Blocks, prepared, context);
        AddNotes(document.Blocks, context);
        if (document.Blocks.Count == 0) document.Blocks.Add(FlowKit.Note("Nothing to show."));
        return document;
    }

    // MARK: Block structure

    private static void RenderBlocks(BlockCollection target, string text, Context context)
    {
        var paragraph = new StringBuilder();
        var i = 0;

        void Flush()
        {
            var value = paragraph.ToString().Trim();
            paragraph.Clear();
            if (value.Length == 0) return;

            if (MathText.TryBlockPiece(value, context.Math, out var piece))
            {
                target.Add(new BlockUIContainer(MathText.DisplayBlock(piece, context.Size))
                {
                    Margin = new Thickness(0, 8, 0, 12),
                });
                return;
            }

            var block = new Paragraph { Margin = new Thickness(0, 0, 0, 9), LineHeight = context.Size * 1.62 };
            AppendInlines(block.Inlines, value, context);
            if (block.Inlines.Count > 0) target.Add(block);
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\n') { Flush(); i++; continue; }
            if (c is '\r' or ' ' or '\t') { i++; continue; }

            var lineEnd = text.IndexOf('\n', i);
            if (lineEnd < 0) lineEnd = text.Length;
            var line = text[i..lineEnd].Trim();
            if (line.Length == 0)
            {
                Flush();
                i = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
                continue;
            }

            if (MathText.TryBlockPiece(line, context.Math, out var display))
            {
                Flush();
                target.Add(new BlockUIContainer(MathText.DisplayBlock(display, context.Size))
                {
                    Margin = new Thickness(0, 8, 0, 12),
                });
                i = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
                continue;
            }

            if (line[0] == '\\')
            {
                var after = i;
                var name = ReadCommandName(text, ref after);

                if (name == "begin")
                {
                    Flush();
                    if (TryRenderEnvironment(target, text, i, context, out var end))
                    {
                        i = end;
                        continue;
                    }
                }
                else if (name == "end")
                {
                    var closer = after;
                    SkipSpaces(text, ref closer);
                    ReadGroup(text, ref closer);
                    i = NextLine(text, closer);
                    continue;
                }
                else if (SectionLevels.TryGetValue(name, out var level))
                {
                    Flush();
                    AddSection(target, text, ref after, level, context);
                    i = after;
                    continue;
                }
                else if (name == "maketitle")
                {
                    Flush();
                    AddTitle(target, context);
                    i = NextLine(text, after);
                    continue;
                }
                else if (name is "appendix")
                {
                    Flush();
                    AddHeading(target, 1, "Appendix", context);
                    i = NextLine(text, after);
                    continue;
                }
                else if (name is "printbibliography" or "bibliography")
                {
                    Flush();
                    AddHeading(target, 2, "References", context);
                    i = NextLine(text, after);
                    continue;
                }
                else if (name is "newpage" or "clearpage" or "pagebreak" or "hrule")
                {
                    Flush();
                    target.Add(FlowKit.Hairline(10, 12));
                    i = NextLine(text, after);
                    continue;
                }
                else if (name == "rule")
                {
                    Flush();
                    ReadOptional(text, ref after);
                    ReadGroup(text, ref after);
                    ReadGroup(text, ref after);
                    target.Add(FlowKit.Hairline(10, 12));
                    i = NextLine(text, after);
                    continue;
                }
                else if (name == "includegraphics")
                {
                    Flush();
                    AddPictureBlock(target, text, ref after, context);
                    i = NextLine(text, after);
                    continue;
                }
                else if (name == "caption")
                {
                    Flush();
                    ReadOptional(text, ref after);
                    var caption = ReadGroup(text, ref after);
                    if (caption.Length > 0) AddCaption(target, caption, context);
                    i = NextLine(text, after);
                    continue;
                }
                else if (name is "title" or "author" or "date")
                {
                    ReadGroup(text, ref after);
                    i = after;
                    continue;
                }
                else if (IgnoredCommands.Contains(name))
                {
                    // Style switches and preamble commands change how the page looks, not what it says,
                    // so only their own arguments are dropped; text after them on the line stays.
                    SkipIgnored(text, ref after, name);
                    i = after;
                    continue;
                }
            }

            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(line);
            i = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
        }

        Flush();
    }

    private static bool TryRenderEnvironment(BlockCollection target, string text, int start, Context context, out int end)
    {
        end = start;
        var i = start;
        if (ReadCommandName(text, ref i) != "begin") return false;
        SkipSpaces(text, ref i);
        var environment = ReadGroup(text, ref i);
        if (environment.Length == 0) return false;

        var (body, after) = ExtractEnvironment(text, i, environment);
        if (after < 0) return false;

        end = after;
        RenderEnvironment(target, environment, body, context);
        return true;
    }

    private static void RenderEnvironment(BlockCollection target, string environment, string body, Context context)
    {
        switch (environment)
        {
            case "itemize":
                AddList(target, body, ordered: false, description: false, context);
                return;
            case "enumerate":
                AddList(target, body, ordered: true, description: false, context);
                return;
            case "description":
                AddList(target, body, ordered: false, description: true, context);
                return;
            case "tabular":
            case "tabular*":
            case "tabularx":
            case "longtable":
            case "array":
                AddTable(target, body, context);
                return;
            case "center":
                AddAligned(target, body, TextAlignment.Center, context);
                return;
            case "flushleft":
                AddAligned(target, body, TextAlignment.Left, context);
                return;
            case "flushright":
                AddAligned(target, body, TextAlignment.Right, context);
                return;
            case "abstract":
                AddHeading(target, 4, "Abstract", context);
                RenderBlocks(target, body, context);
                return;
            case "document":
            case "figure":
            case "figure*":
            case "table":
            case "table*":
            case "wrapfigure":
            case "wraptable":
            case "sidewaysfigure":
            case "sidewaystable":
            case "minipage":
            case "titlepage":
            case "multicols":
            case "multicols*":
                RenderBlocks(target, EnvironmentBody(environment, body), context);
                return;
            case "thebibliography":
                AddHeading(target, 2, "References", context);
                RenderBlocks(target, body, context);
                return;
        }

        if (MathEnvironments.Contains(environment))
        {
            AddMath(target, body, context);
            return;
        }

        if (CodeEnvironments.Contains(environment))
        {
            AddCode(target, environment, body, context);
            return;
        }

        if (QuoteEnvironments.Contains(environment))
        {
            AddQuote(target, body, context);
            return;
        }

        if (TheoremEnvironments.Contains(environment))
        {
            AddTheorem(target, environment, body, context);
            return;
        }

        // Unknown environments keep their contents, which is what makes a custom class readable.
        RenderBlocks(target, body, context);
    }

    private static string EnvironmentBody(string environment, string body)
    {
        var i = 0;
        SkipSpaces(body, ref i);

        if (environment is "figure" or "table" or "figure*" or "table*" or "wrapfigure" or "wraptable")
        {
            if (i < body.Length && body[i] == '[') ReadGroup(body, ref i, '[', ']');
            if (environment is "wrapfigure" or "wraptable")
            {
                ReadGroup(body, ref i);
                ReadGroup(body, ref i);
            }
        }
        else if (environment is "minipage" or "multicols" or "multicols*")
        {
            ReadOptional(body, ref i);
            ReadGroup(body, ref i);
        }

        return body[i..];
    }

    private static void AddSection(BlockCollection target, string text, ref int i, int level, Context context)
    {
        SkipSpaces(text, ref i);
        if (i < text.Length && text[i] == '*') i++;
        SkipSpaces(text, ref i);
        var optional = ReadOptional(text, ref i);
        var title = ReadGroup(text, ref i);
        if (title.Length == 0) title = optional;
        AddHeading(target, level, title, context);
    }

    private static void AddHeading(BlockCollection target, int level, string content, Context context)
    {
        level = Math.Clamp(level, 1, 6);
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

        AppendInlines(paragraph.Inlines, content, context);
        target.Add(paragraph);
    }

    private static void AddTitle(BlockCollection target, Context context)
    {
        var any = false;
        if (context.Title.Length > 0)
        {
            var title = new Paragraph { FontSize = 26, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 4) };
            AppendInlines(title.Inlines, MathText.Protect(context.Title, context.Math), context);
            target.Add(title);
            any = true;
        }

        if (context.Author.Length > 0)
        {
            var author = new Paragraph { FontSize = 14, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
            AppendInlines(author.Inlines, MathText.Protect(context.Author, context.Math), context);
            target.Add(author);
            any = true;
        }

        if (context.Date.Length > 0)
        {
            var date = new Paragraph { FontSize = 12, Foreground = Theme.Muted, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            AppendInlines(date.Inlines, MathText.Protect(context.Date, context.Math), context);
            target.Add(date);
            any = true;
        }

        if (any) target.Add(FlowKit.Hairline(8, 14));
    }

    private static void AddList(BlockCollection target, string body, bool ordered, bool description, Context context)
    {
        var items = SplitItems(body);
        if (items.Count == 0)
        {
            RenderBlocks(target, body, context);
            return;
        }

        var list = new WpfList
        {
            MarkerStyle = description ? TextMarkerStyle.None : ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(20, 2, 0, 10),
            Padding = new Thickness(6, 0, 0, 0),
        };

        foreach (var (label, content) in items)
        {
            var item = new ListItem();
            RenderBlocks(item.Blocks, content, context);
            if (item.Blocks.Count == 0) item.Blocks.Add(new Paragraph());

            if (label.Length > 0 && item.Blocks.FirstBlock is Paragraph first)
            {
                var prefix = new Run(label + (description ? " — " : " "))
                {
                    FontWeight = description ? FontWeights.SemiBold : FontWeights.Normal,
                };
                if (first.Inlines.FirstInline is { } start) first.Inlines.InsertBefore(start, prefix);
                else first.Inlines.Add(prefix);
            }

            list.ListItems.Add(item);
        }

        target.Add(list);
    }

    private static List<(string Label, string Content)> SplitItems(string body)
    {
        var starts = new List<(int Index, int After)>();
        var braces = 0;
        var environments = 0;
        var i = 0;

        while (i < body.Length)
        {
            if (body[i] == '\\')
            {
                if (body.AsSpan(i).StartsWith("\\begin{")) { environments++; i += 7; continue; }
                if (body.AsSpan(i).StartsWith("\\end{")) { environments = Math.Max(0, environments - 1); i += 5; continue; }
                if (environments == 0 && braces == 0 && body.AsSpan(i).StartsWith("\\item"))
                {
                    var after = i + 5;
                    if (after >= body.Length || !char.IsLetter(body[after]))
                    {
                        starts.Add((i, after));
                        i = after;
                        continue;
                    }
                }

                i += 2;
                continue;
            }

            if (body[i] == '{') braces++;
            else if (body[i] == '}' && braces > 0) braces--;
            i++;
        }

        var items = new List<(string Label, string Content)>();
        for (var index = 0; index < starts.Count; index++)
        {
            var start = starts[index].After;
            var end = index + 1 < starts.Count ? starts[index + 1].Index : body.Length;
            var segment = body[start..end];
            var offset = 0;
            SkipSpaces(segment, ref offset);
            var label = ReadOptional(segment, ref offset);
            items.Add((label, segment[offset..].Trim()));
        }

        return items;
    }

    private static void AddTable(BlockCollection target, string body, Context context)
    {
        var offset = 0;
        for (var argument = 0; argument < 2; argument++)
        {
            SkipSpaces(body, ref offset);
            if (offset < body.Length && body[offset] == '[') ReadGroup(body, ref offset, '[', ']');
            SkipSpaces(body, ref offset);
            if (offset < body.Length && body[offset] == '{') ReadGroup(body, ref offset);
            else break;
        }

        var rowsText = body[offset..];
        var header = Regex.IsMatch(rowsText, @"\\(?:hline|toprule|midrule|bottomrule)");
        rowsText = RulePattern.Replace(rowsText, "");

        var rows = SplitTopLevel(rowsText, "\\\\")
            .Select(row => SplitTopLevel(row, "&").Select(cell => cell.Trim()).ToList())
            .Where(row => row.Any(cell => cell.Length > 0))
            .ToList();

        if (rows.Count == 0 || rows.Max(row => row.Count) == 0)
        {
            RenderBlocks(target, body, context);
            return;
        }

        var columns = rows.Max(row => row.Count);
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 14) };
        for (var column = 0; column < columns; column++)
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var source = rows[rowIndex];
            var row = new TableRow();
            if (header && rowIndex == 0) row.FontWeight = FontWeights.SemiBold;

            for (var column = 0; column < columns; column++)
            {
                var cell = new TableCell
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                };
                cell.SetResourceReference(TableCell.BorderBrushProperty, "Theme.Hairline");

                var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = context.Size * 1.45 };
                if (column < source.Count) AppendInlines(paragraph.Inlines, source[column], context);
                cell.Blocks.Add(paragraph);
                row.Cells.Add(cell);
            }

            group.Rows.Add(row);
        }

        target.Add(table);
    }

    private static void AddMath(BlockCollection target, string body, Context context)
    {
        var cleaned = MathCleanup.Replace(body, "");
        foreach (var row in SplitTopLevel(cleaned, "\\\\"))
        {
            var formula = row.Replace("&", " ").Trim();
            if (formula.Length == 0) continue;
            target.Add(new BlockUIContainer(MathText.DisplayBlock(new MathText.Piece(formula, true), context.Size))
            {
                Margin = new Thickness(0, 8, 0, 12),
            });
        }
    }

    private static void AddCode(BlockCollection target, string environment, string body, Context context)
    {
        var offset = 0;
        var language = "";

        if (environment == "minted")
        {
            language = ReadGroup(body, ref offset);
        }
        else if (environment == "lstlisting")
        {
            SkipSpaces(body, ref offset);
            if (offset < body.Length && body[offset] == '[')
            {
                var options = ReadGroup(body, ref offset, '[', ']');
                var match = Regex.Match(options, @"language\s*=\s*\[?([A-Za-z0-9+#_-]+)\]?");
                if (match.Success) language = match.Groups[1].Value;
            }
        }

        target.Add(FlowKit.CodeBlock(body[offset..].TrimStart('\n', '\r'), string.IsNullOrWhiteSpace(language) ? null : language, context.Size));
    }

    private static void AddQuote(BlockCollection target, string body, Context context)
    {
        var section = new Section
        {
            Margin = new Thickness(0, 4, 0, 10),
            Padding = new Thickness(14, 2, 0, 2),
            BorderThickness = new Thickness(2, 0, 0, 0),
            Foreground = Theme.Muted,
            FontStyle = FontStyles.Italic,
        };
        section.SetResourceReference(Block.BorderBrushProperty, "Theme.LineStrong");
        RenderBlocks(section.Blocks, body, context);
        if (section.Blocks.Count == 0) section.Blocks.Add(new Paragraph());
        target.Add(section);
    }

    private static void AddAligned(BlockCollection target, string body, TextAlignment alignment, Context context)
    {
        var section = new Section
        {
            TextAlignment = alignment,
            Margin = new Thickness(0, 2, 0, 8),
        };
        RenderBlocks(section.Blocks, body, context);
        if (section.Blocks.Count == 0) section.Blocks.Add(new Paragraph());
        target.Add(section);
    }

    private static void AddTheorem(BlockCollection target, string environment, string body, Context context)
    {
        var offset = 0;
        var title = ReadOptional(body, ref offset);
        var label = char.ToUpperInvariant(environment[0]) + environment[1..];
        if (title.Length > 0) label += $" ({title})";

        var heading = new Paragraph
        {
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 3),
            KeepWithNext = true,
        };
        AppendInlines(heading.Inlines, label + ".", context);
        target.Add(heading);
        RenderBlocks(target, body[offset..], context);
    }

    private static void AddCaption(BlockCollection target, string caption, Context context)
    {
        var paragraph = new Paragraph
        {
            FontSize = context.Size * 0.9,
            FontStyle = FontStyles.Italic,
            Foreground = Theme.Muted,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 12),
        };
        AppendInlines(paragraph.Inlines, caption, context);
        target.Add(paragraph);
    }

    private static void AddPictureBlock(BlockCollection target, string text, ref int i, Context context)
    {
        ReadOptional(text, ref i);
        var path = ReadGroup(text, ref i);
        if (LoadPicture(path, context.BaseDir) is { } picture)
            target.Add(FlowKit.ImageBlock(picture, Path.GetFileName(path)));
        else
            target.Add(FlowKit.Note($"[image: {path}]"));
    }

    private static void AddNotes(BlockCollection target, Context context)
    {
        if (context.Notes.Count == 0) return;

        target.Add(FlowKit.Hairline(18, 6));
        target.Add(new Paragraph(new Run("Notes"))
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = context.Size,
            Foreground = Theme.Muted,
            Margin = new Thickness(0, 0, 0, 4),
        });

        for (var index = 0; index < context.Notes.Count; index++)
        {
            var paragraph = new Paragraph
            {
                FontSize = context.Size * 0.86,
                Foreground = Theme.Muted,
                Margin = new Thickness(0, 0, 0, 3),
                LineHeight = context.Size * 1.35,
            };
            paragraph.Inlines.Add(new Run($"{index + 1}. "));
            AppendInlines(paragraph.Inlines, context.Notes[index], context);
            target.Add(paragraph);
        }
    }

    // MARK: Inline structure

    private static void AppendInlines(InlineCollection target, string text, Context context)
    {
        var plain = new StringBuilder();
        var i = 0;

        void Flush()
        {
            if (plain.Length == 0) return;
            var value = plain.ToString();
            plain.Clear();
            MathText.AppendLiteral(target, value, context.Size, context.Math);
        }

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '\\')
            {
                var name = ReadCommandName(text, ref i);
                if (name.Length == 0) continue;

                if (name == "\\")
                {
                    SkipSpaces(text, ref i);
                    if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                    Flush();
                    target.Add(new LineBreak());
                    continue;
                }

                if (name is "newline" or "linebreak" or "pagebreak")
                {
                    SkipSpaces(text, ref i);
                    if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                    SkipSpaces(text, ref i);
                    if (i < text.Length && text[i] == '*') i++;
                    Flush();
                    target.Add(new LineBreak());
                    continue;
                }

                if (name == "par")
                {
                    Flush();
                    target.Add(new LineBreak());
                    target.Add(new LineBreak());
                    continue;
                }

                if (name is " " or "," or ";" or ":")
                {
                    plain.Append(' ');
                    continue;
                }

                if (name == "!") continue;
                if (name == "quad") { plain.Append('\u2003'); continue; }
                if (name == "qquad") { plain.Append("\u2003\u2003"); continue; }
                if (name == "~") { plain.Append('\u00A0'); continue; }
                if (name is "%" or "&" or "_" or "#" or "$" or "{" or "}") { plain.Append(name[0]); continue; }

                if (name is "verb" or "lstinline")
                {
                    SkipSpaces(text, ref i);
                    if (name == "lstinline" && i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                    if (i >= text.Length) continue;
                    var delimiter = text[i++];
                    var close = text.IndexOf(delimiter, i);
                    var code = close < 0 ? text[i..] : text[i..close];
                    if (close >= 0) i = close + 1;
                    Flush();
                    target.Add(new Run(code) { FontFamily = Theme.Mono, FontSize = context.Size * 0.92 });
                    continue;
                }

                if (name.Length == 1 && Accents.TryGetValue(name[0], out var accent))
                {
                    if (i < text.Length && text[i] == '{')
                    {
                        var content = ReadGroup(text, ref i);
                        plain.Append((content + accent).Normalize(NormalizationForm.FormC));
                    }
                    else if (i < text.Length)
                    {
                        plain.Append((text[i] + accent.ToString()).Normalize(NormalizationForm.FormC));
                        i++;
                    }
                    continue;
                }

                if (Letters.TryGetValue(name, out var letter))
                {
                    plain.Append(letter);
                    continue;
                }

                if (name == "today")
                {
                    plain.Append(DateTime.Today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
                    continue;
                }

                if (TextSymbols.TryGetValue(name, out var textSymbol))
                {
                    plain.Append(textSymbol);
                    continue;
                }

                if (Styles.TryGetValue(name, out var style))
                {
                    var content = ReadGroup(text, ref i);
                    if (content.Length == 0) continue;
                    Flush();
                    AppendStyled(target, content, style, context);
                    continue;
                }

                if (name is "ensuremath")
                {
                    var content = ReadGroup(text, ref i);
                    if (content.Length == 0) continue;
                    Flush();
                    MathText.AppendMath(target, content, context.Size);
                    continue;
                }

                if (name is "mathrm" or "mathbf" or "mathit" or "mathsf" or "mathtt" or "mathcal" or "mathbb"
                    or "mathfrak" or "operatorname" or "boldsymbol" or "bm" or "ce")
                {
                    var content = ReadGroup(text, ref i);
                    if (content.Length == 0) continue;
                    Flush();
                    MathText.AppendMath(target, content, context.Size);
                    continue;
                }

                if (name == "href")
                {
                    var url = ReadGroup(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AddLink(target, url, content.Length > 0 ? content : url, context, mono: false);
                    continue;
                }

                if (name == "url")
                {
                    var url = ReadGroup(text, ref i);
                    Flush();
                    AddLink(target, url, url, context, mono: true);
                    continue;
                }

                if (name == "hyperref")
                {
                    ReadOptional(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name == "includegraphics")
                {
                    ReadOptional(text, ref i);
                    var path = ReadGroup(text, ref i);
                    Flush();
                    if (LoadPicture(path, context.BaseDir) is { } picture)
                    {
                        target.Add(new InlineUIContainer(new Image
                        {
                            Source = picture,
                            MaxWidth = 480,
                            MaxHeight = 360,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(2, 2, 2, 2),
                        })
                        {
                            BaselineAlignment = BaselineAlignment.Center,
                        });
                    }
                    else
                    {
                        target.Add(new Run($"[image: {path}]") { Foreground = Theme.Muted });
                    }
                    continue;
                }

                if (name == "footnote")
                {
                    var content = ReadGroup(text, ref i);
                    Flush();
                    context.NoteCount++;
                    target.Add(new Run(context.NoteCount.ToString(CultureInfo.InvariantCulture))
                    {
                        BaselineAlignment = BaselineAlignment.Superscript,
                        FontSize = context.Size * 0.72,
                        Foreground = Theme.Accent,
                    });
                    context.Notes.Add(content);
                    continue;
                }

                if (name == "footnotetext")
                {
                    ReadOptional(text, ref i);
                    context.Notes.Add(ReadGroup(text, ref i));
                    continue;
                }

                if (name is "cite" or "citep" or "citet" or "parencite" or "textcite" or "autocite" or "footcite")
                {
                    ReadOptional(text, ref i);
                    ReadOptional(text, ref i);
                    var keys = ReadGroup(text, ref i);
                    Flush();
                    target.Add(new Run(keys.Length > 0 ? $"[{keys}]" : "") { Foreground = Theme.Accent });
                    continue;
                }

                if (name is "ref" or "autoref" or "cref" or "Cref" or "nameref" or "pageref" or "eqref")
                {
                    var label = ReadGroup(text, ref i);
                    Flush();
                    target.Add(new Run(label.Length == 0 ? "" : name == "eqref" ? $"({label})" : $"[{label}]")
                    {
                        Foreground = Theme.Muted,
                    });
                    continue;
                }

                if (name is "label" or "index" or "glossary" or "nocite" or "addcontentsline" or "bibliography")
                {
                    ReadOptional(text, ref i);
                    ReadGroup(text, ref i);
                    ReadGroup(text, ref i);
                    continue;
                }

                if (name == "item")
                {
                    var label = ReadOptional(text, ref i);
                    plain.Append('\u2022').Append(' ');
                    if (label.Length > 0) plain.Append(label).Append(' ');
                    continue;
                }

                if (name is "multicolumn" or "multirow")
                {
                    ReadOptional(text, ref i);
                    ReadGroup(text, ref i);
                    ReadOptional(text, ref i);
                    ReadGroup(text, ref i);
                    ReadOptional(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name is "rowcolor" or "cellcolor" or "columncolor")
                {
                    if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                    ReadGroup(text, ref i);
                    continue;
                }

                if (name is "textcolor" or "colorbox")
                {
                    if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                    ReadGroup(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name is "fcolorbox")
                {
                    if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                    ReadGroup(text, ref i);
                    ReadGroup(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name == "scalebox")
                {
                    ReadGroup(text, ref i);
                    ReadOptional(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name == "resizebox")
                {
                    ReadGroup(text, ref i);
                    ReadGroup(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name == "raisebox")
                {
                    ReadGroup(text, ref i);
                    ReadOptional(text, ref i);
                    ReadOptional(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }

                if (name is "SI" or "qty")
                {
                    var number = ReadGroup(text, ref i);
                    var unit = ReadGroup(text, ref i);
                    plain.Append(number);
                    if (number.Length > 0 && unit.Length > 0) plain.Append(' ');
                    plain.Append(unit);
                    continue;
                }

                if (name is "si" or "unit")
                {
                    plain.Append(ReadGroup(text, ref i));
                    continue;
                }

                if (name == "ang")
                {
                    plain.Append(ReadGroup(text, ref i)).Append('°');
                    continue;
                }

                if (name is "num" or "acrshort" or "acrlong" or "acrfull" or "acs")
                {
                    plain.Append(ReadGroup(text, ref i));
                    continue;
                }

                if (name is "hspace" or "vspace")
                {
                    ReadOptional(text, ref i);
                    if (i < text.Length && text[i] == '*') i++;
                    ReadGroup(text, ref i);
                    continue;
                }

                if (name is "todo" or "marginpar" or "caption")
                {
                    ReadOptional(text, ref i);
                    var content = ReadGroup(text, ref i);
                    Flush();
                    target.Add(new Run(content)
                    {
                        FontSize = context.Size * 0.9,
                        Foreground = Theme.Accent,
                        FontStyle = FontStyles.Italic,
                    });
                    continue;
                }

                if (name is "begin" or "end")
                {
                    ReadGroup(text, ref i);
                    continue;
                }

                if (MathText.TrySymbol(name, out var symbol))
                {
                    plain.Append(symbol);
                    continue;
                }

                // An unknown command keeps its argument, so a custom macro never hides the text.
                if (i < text.Length && text[i] == '*') i++;
                SkipSpaces(text, ref i);
                if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
                SkipSpaces(text, ref i);
                if (i < text.Length && text[i] == '{')
                {
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                }
                continue;
            }

            switch (c)
            {
                case '{':
                {
                    var content = ReadGroup(text, ref i);
                    Flush();
                    AppendInlines(target, content, context);
                    continue;
                }
                case '}':
                    i++;
                    continue;
                case '~':
                    plain.Append('\u00A0');
                    i++;
                    continue;
                case '&':
                    i++;
                    continue;
                case '-':
                    if (i + 1 < text.Length && text[i + 1] == '-')
                    {
                        if (i + 2 < text.Length && text[i + 2] == '-') { plain.Append('—'); i += 3; }
                        else { plain.Append('–'); i += 2; }
                        continue;
                    }
                    break;
                case '`':
                    if (i + 1 < text.Length && text[i + 1] == '`') { plain.Append('“'); i += 2; }
                    else { plain.Append('‘'); i++; }
                    continue;
                case '\'':
                    if (i + 1 < text.Length && text[i + 1] == '\'') { plain.Append('”'); i += 2; }
                    else { plain.Append('’'); i++; }
                    continue;
                case '!':
                    if (i + 1 < text.Length && text[i + 1] == '`') { plain.Append('¡'); i += 2; continue; }
                    break;
                case '?':
                    if (i + 1 < text.Length && text[i + 1] == '`') { plain.Append('¿'); i += 2; continue; }
                    break;
            }

            plain.Append(c);
            i++;
        }

        Flush();
    }

    private static void AppendStyled(InlineCollection target, string content, InlineStyle style, Context context)
    {
        switch (style)
        {
            case InlineStyle.Bold:
            {
                var span = new Bold();
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.Italic:
            {
                var span = new Italic();
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.Underline:
            {
                var span = new Underline();
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.Strike:
            {
                var span = new Span { TextDecorations = TextDecorations.Strikethrough };
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.Mono:
            {
                var span = new Span { FontFamily = Theme.Mono, FontSize = context.Size * 0.92 };
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.SmallCaps:
            {
                var span = new Span { FontSize = context.Size * 0.9 };
                Typography.SetCapitals(span, FontCapitals.SmallCaps);
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.Superscript:
            {
                var span = new Span { BaselineAlignment = BaselineAlignment.Superscript, FontSize = context.Size * 0.75 };
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            case InlineStyle.Subscript:
            {
                var span = new Span { BaselineAlignment = BaselineAlignment.Subscript, FontSize = context.Size * 0.75 };
                AppendInlines(span.Inlines, content, context);
                target.Add(span);
                return;
            }
            default:
                AppendInlines(target, content, context);
                return;
        }
    }

    private static void AddLink(InlineCollection target, string url, string content, Context context, bool mono)
    {
        var link = FlowKit.Link(url.Trim(), context.Open, context.Size);
        if (mono)
            link.Inlines.Add(new Run(content) { FontFamily = Theme.Mono, FontSize = context.Size * 0.92 });
        else
            AppendInlines(link.Inlines, content, context);
        target.Add(link);
    }

    private static BitmapSource? LoadPicture(string path, string? baseDir)
    {
        var name = path.Trim().Trim('"');
        if (name.Length == 0) return null;

        if (Path.GetExtension(name).Length == 0)
        {
            foreach (var extension in PictureExtensions)
            {
                if (FlowKit.LoadImage(name + extension, baseDir) is { } picture) return picture;
            }
        }

        return FlowKit.LoadImage(name, baseDir);
    }

    // MARK: Source helpers

    private static string RemoveComments(string text)
    {
        var output = new StringBuilder(text.Length);
        var raw = "";

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                if (TryVerbSpan(text, i, out var verbEnd))
                {
                    output.Append(text, i, verbEnd - i);
                    i = verbEnd - 1;
                    continue;
                }

                if (RawBoundary(text, i, true, out var opened)) raw = opened;
                else if (RawBoundary(text, i, false, out _)) raw = "";

                output.Append(text[i]).Append(text[i + 1]);
                i++;
                continue;
            }

            if (text[i] == '%' && raw.Length == 0)
            {
                while (i < text.Length && text[i] != '\n') i++;
                if (i < text.Length) output.Append('\n');
                continue;
            }

            output.Append(text[i]);
        }

        return output.ToString();
    }

    /// <summary>Matches <c>\begin{verbatim}</c> and friends, whose contents keep their percent signs.</summary>
    private static bool RawBoundary(string text, int start, bool opening, out string environment)
    {
        environment = "";
        foreach (var name in CodeEnvironments)
        {
            var token = opening ? "\\begin{" + name + "}" : "\\end{" + name + "}";
            if (!text.AsSpan(start).StartsWith(token)) continue;
            environment = name;
            return true;
        }

        return false;
    }

    /// <summary>Matches <c>\verb|...|</c> and <c>\verb*|...|</c>.</summary>
    private static bool TryVerbSpan(string text, int start, out int end)
    {
        end = start;
        if (!text.AsSpan(start).StartsWith("\\verb")) return false;

        var i = start + 5;
        if (i < text.Length && (char.IsLetter(text[i]) || text[i] == '@')) return false;
        if (i < text.Length && text[i] == '*') i++;
        if (i >= text.Length) return false;

        var delimiter = text[i];
        if (char.IsWhiteSpace(delimiter)) return false;

        var close = text.IndexOf(delimiter, i + 1);
        end = close < 0 ? text.Length : close + 1;
        return true;
    }

    private static string DocumentBody(string text)
    {
        const string open = "\\begin{document}";
        var start = text.IndexOf(open, StringComparison.Ordinal);
        if (start < 0) return text;

        start += open.Length;
        var end = text.IndexOf("\\end{document}", start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>
    /// Consumes the arguments of a command whose only job is layout. The command itself and its
    /// arguments are dropped; anything the author wrote after them stays in the paragraph.
    /// </summary>
    private static void SkipIgnored(string text, ref int i, string name)
    {
        SkipSpaces(text, ref i);
        if (i < text.Length && text[i] == '*') i++;
        SkipSpaces(text, ref i);

        if (name is "newcommand" or "renewcommand" or "providecommand")
        {
            ReadGroup(text, ref i);
            ReadOptional(text, ref i);
            ReadOptional(text, ref i);
            ReadGroup(text, ref i);
            return;
        }

        if (name is "def" or "let")
        {
            i = NextLine(text, i);
            return;
        }

        if (OneGroupCommands.Contains(name))
        {
            if (i < text.Length && text[i] == '[') ReadGroup(text, ref i, '[', ']');
            ReadGroup(text, ref i);
            return;
        }

        if (TwoGroupCommands.Contains(name))
        {
            ReadGroup(text, ref i);
            ReadGroup(text, ref i);
        }
    }

    private static string ReadMetadata(string text, string command)
    {
        var i = 0;
        while (i < text.Length)
        {
            var at = text.IndexOf('\\', i);
            if (at < 0) break;

            i = at;
            var name = ReadCommandName(text, ref i);
            if (name == command)
            {
                SkipSpaces(text, ref i);
                if (i < text.Length && text[i] == '*') i++;
                return ReadGroup(text, ref i);
            }

            i++;
        }

        return "";
    }

    private static (string Body, int End) ExtractEnvironment(string text, int start, string environment)
    {
        var open = $"\\begin{{{environment}}}";
        var close = $"\\end{{{environment}}}";
        var depth = 1;
        var i = start;

        while (i < text.Length)
        {
            var nextOpen = text.IndexOf(open, i, StringComparison.Ordinal);
            var nextClose = text.IndexOf(close, i, StringComparison.Ordinal);
            if (nextClose < 0) return ("", -1);

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                i = nextOpen + open.Length;
                continue;
            }

            depth--;
            if (depth == 0) return (text[start..nextClose], nextClose + close.Length);
            i = nextClose + close.Length;
        }

        return ("", -1);
    }

    private static List<string> SplitTopLevel(string text, string separator)
    {
        var parts = new List<string>();
        var braces = 0;
        var start = 0;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\')
            {
                if (separator == "\\\\" && i + 1 < text.Length && text[i + 1] == '\\' && braces == 0)
                {
                    parts.Add(text[start..i]);
                    i += 2;

                    // \\[2mm] is row spacing, not the first cell of the next row.
                    var probe = i;
                    while (probe < text.Length && char.IsWhiteSpace(text[probe])) probe++;
                    if (probe < text.Length && text[probe] == '[')
                    {
                        var close = text.IndexOf(']', probe + 1);
                        if (close > probe) i = close + 1;
                    }

                    start = i;
                    continue;
                }

                i += 2;
                continue;
            }

            if (c == '{') braces++;
            else if (c == '}' && braces > 0) braces--;
            else if (separator == "&" && c == '&' && braces == 0)
            {
                parts.Add(text[start..i]);
                i++;
                start = i;
                continue;
            }

            i++;
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static string ReadCommandName(string text, ref int i)
    {
        if (i < text.Length && text[i] == '\\') i++;
        if (i >= text.Length) return "";

        if (char.IsLetter(text[i]))
        {
            var start = i;
            while (i < text.Length && char.IsLetter(text[i])) i++;
            return text[start..i];
        }

        return text[i++].ToString();
    }

    private static string ReadGroup(string text, ref int i, char open = '{', char close = '}')
    {
        SkipSpaces(text, ref i);
        if (i >= text.Length || text[i] != open) return "";

        var depth = 0;
        var start = i + 1;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\') { i++; continue; }
            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    var body = text[start..i];
                    i++;
                    return body;
                }
            }
        }

        return start <= text.Length ? text[start..] : "";
    }

    private static string ReadOptional(string text, ref int i) => ReadGroup(text, ref i, '[', ']');

    private static void SkipSpaces(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
    }

    private static int NextLine(string text, int i)
    {
        var end = text.IndexOf('\n', Math.Min(i, text.Length));
        return end < 0 ? text.Length : end + 1;
    }
}
