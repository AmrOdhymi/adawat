using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Adawat.App.Documents;

/// <summary>
/// Reads the LaTeX a note or a clipboard entry carries and shows it as readable text:
/// <c>\alpha</c> becomes α, <c>\frac{a}{b}</c> a stacked fraction, <c>x^2</c> a superscript and
/// <c>x_i</c> a subscript. This is a presentation aid, not a typesetter: a command it does not
/// know keeps its name instead of vanishing, and nothing is executed.
/// </summary>
internal static class MathText
{
    private const char MarkOpen = '\uE000';
    private const char MarkClose = '\uE001';

    public sealed record Piece(string Latex, bool Display);

    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        // Greek, lower case
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε",
        ["varepsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ", ["vartheta"] = "ϑ",
        ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ",
        ["omicron"] = "ο", ["pi"] = "π", ["varpi"] = "ϖ", ["rho"] = "ρ", ["varrho"] = "ϱ",
        ["sigma"] = "σ", ["varsigma"] = "ς", ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "φ",
        ["varphi"] = "ϕ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω",
        // Greek, upper case
        ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ",
        ["Pi"] = "Π", ["Sigma"] = "Σ", ["Upsilon"] = "Υ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
        // Operators and relations
        ["pm"] = "±", ["mp"] = "∓", ["times"] = "×", ["div"] = "÷", ["cdot"] = "⋅", ["ast"] = "∗",
        ["star"] = "⋆", ["circ"] = "∘", ["bullet"] = "•", ["oplus"] = "⊕", ["ominus"] = "⊖",
        ["otimes"] = "⊗", ["oslash"] = "⊘", ["odot"] = "⊙", ["leq"] = "≤", ["le"] = "≤", ["geq"] = "≥",
        ["ge"] = "≥", ["neq"] = "≠", ["ne"] = "≠", ["approx"] = "≈", ["sim"] = "∼", ["simeq"] = "≃",
        ["cong"] = "≅", ["equiv"] = "≡", ["propto"] = "∝", ["ll"] = "≪", ["gg"] = "≫",
        ["prec"] = "≺", ["succ"] = "≻", ["subset"] = "⊂", ["subseteq"] = "⊆", ["supset"] = "⊃",
        ["supseteq"] = "⊇", ["in"] = "∈", ["notin"] = "∉", ["ni"] = "∋", ["perp"] = "⊥",
        ["parallel"] = "∥", ["mid"] = "∣", ["nmid"] = "∤",
        // Arrows
        ["to"] = "→", ["rightarrow"] = "→", ["leftarrow"] = "←", ["gets"] = "←", ["Rightarrow"] = "⇒",
        ["Leftarrow"] = "⇐", ["leftrightarrow"] = "↔", ["Leftrightarrow"] = "⇔", ["mapsto"] = "↦",
        ["implies"] = "⇒", ["iff"] = "⇔", ["uparrow"] = "↑", ["downarrow"] = "↓", ["nearrow"] = "↗",
        ["searrow"] = "↘",
        // Logic and sets
        ["forall"] = "∀", ["exists"] = "∃", ["nexists"] = "∄", ["neg"] = "¬", ["lnot"] = "¬",
        ["land"] = "∧", ["wedge"] = "∧", ["lor"] = "∨", ["vee"] = "∨", ["emptyset"] = "∅",
        ["varnothing"] = "∅", ["setminus"] = "∖", ["cup"] = "∪", ["cap"] = "∩", ["bigcup"] = "⋃",
        ["bigcap"] = "⋂", ["therefore"] = "∴", ["because"] = "∵",
        // Big operators and symbols
        ["infty"] = "∞", ["partial"] = "∂", ["nabla"] = "∇", ["sum"] = "∑", ["prod"] = "∏",
        ["coprod"] = "∐", ["int"] = "∫", ["iint"] = "∬", ["iiint"] = "∭", ["oint"] = "∮",
        ["angle"] = "∠", ["measuredangle"] = "∡", ["degree"] = "°", ["prime"] = "′", ["dprime"] = "″",
        ["ldots"] = "…", ["dots"] = "…", ["cdots"] = "⋯", ["vdots"] = "⋮", ["ddots"] = "⋱",
        ["hbar"] = "ℏ", ["ell"] = "ℓ", ["Re"] = "ℜ", ["Im"] = "ℑ", ["aleph"] = "ℵ", ["wp"] = "℘",
        ["imath"] = "ı", ["jmath"] = "ȷ", ["checkmark"] = "✓", ["dagger"] = "†", ["ddagger"] = "‡",
        ["S"] = "§", ["P"] = "¶", ["copyright"] = "©", ["registered"] = "®", ["pounds"] = "£",
        ["euro"] = "€", ["yen"] = "¥", ["sharp"] = "♯", ["flat"] = "♭", ["natural"] = "♮",
        ["triangle"] = "△", ["square"] = "□", ["blacksquare"] = "■", ["diamond"] = "⋄",
        ["clubsuit"] = "♣", ["diamondsuit"] = "♦", ["heartsuit"] = "♥", ["spadesuit"] = "♠",
        // Delimiters
        ["langle"] = "⟨", ["rangle"] = "⟩", ["lfloor"] = "⌊", ["rfloor"] = "⌋", ["lceil"] = "⌈",
        ["rceil"] = "⌉", ["lbrace"] = "{", ["rbrace"] = "}", ["lbrack"] = "[", ["rbrack"] = "]",
        ["vert"] = "|", ["Vert"] = "‖", ["lvert"] = "|", ["rvert"] = "|", ["lVert"] = "‖",
        ["rVert"] = "‖", ["backslash"] = "\\",
    };

    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh",
        "tanh", "coth", "log", "ln", "lg", "exp", "lim", "liminf", "limsup", "max", "min", "sup",
        "inf", "det", "dim", "ker", "deg", "gcd", "hom", "arg", "Pr",
    };

    /// <summary>
    /// Replaces every math span with a private marker so the Markdown parser never turns a
    /// <c>*</c> or a <c>_</c> inside the formula into emphasis. Code fences and code spans are
    /// copied untouched, and a lone <c>$</c> that looks like a price is left alone.
    /// </summary>
    public static string Protect(string source, List<Piece> pieces)
    {
        var text = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var output = new StringBuilder(text.Length);
        var inFence = false;
        var fenceChar = '\0';
        var fenceLength = 0;
        var lineStart = true;
        var i = 0;

        while (i < text.Length)
        {
            if (lineStart)
            {
                lineStart = false;
                var probe = i;
                while (probe < text.Length && (text[probe] == ' ' || text[probe] == '\t')) probe++;
                if (probe < text.Length && (text[probe] == '`' || text[probe] == '~'))
                {
                    var mark = text[probe];
                    var end = probe;
                    while (end < text.Length && text[end] == mark) end++;
                    if (end - probe >= 3)
                    {
                        if (!inFence) { inFence = true; fenceChar = mark; fenceLength = end - probe; }
                        else if (mark == fenceChar && end - probe >= fenceLength) inFence = false;

                        while (i < text.Length && text[i] != '\n') output.Append(text[i++]);
                        continue;
                    }
                }
            }

            var c = text[i];

            if (c == '\n') { output.Append(c); i++; lineStart = true; continue; }

            if (inFence) { output.Append(c); i++; continue; }

            if (c == '`')
            {
                var end = i;
                while (end < text.Length && text[end] == '`') end++;
                var run = end - i;
                output.Append(text, i, run);
                i = end;

                while (i < text.Length)
                {
                    if (text[i] == '`')
                    {
                        var close = i;
                        while (close < text.Length && text[close] == '`') close++;
                        if (close - i == run) { output.Append(text, i, close - i); i = close; break; }
                    }
                    output.Append(text[i++]);
                }
                continue;
            }

            if (c == '\\' && i + 1 < text.Length)
            {
                // Verbatim material keeps its dollar signs: copy it through untouched.
                if (TryRawBlock(text, i, out var rawEnd))
                {
                    output.Append(text, i, rawEnd - i);
                    i = rawEnd;
                    lineStart = rawEnd > 0 && text[rawEnd - 1] == '\n';
                    continue;
                }

                if (TryVerb(text, i, out var verbEnd))
                {
                    output.Append(text, i, verbEnd - i);
                    i = verbEnd;
                    continue;
                }

                // A second backslash makes \\[2mm] a line break, not the start of display math.
                var startsMath = i == 0 || text[i - 1] != '\\';
                if (startsMath && (text[i + 1] == '[' || text[i + 1] == '('))
                {
                    var close = text[i + 1] == '[' ? "\\]" : "\\)";
                    var end = text.IndexOf(close, i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        Add(pieces, output, text.Substring(i + 2, end - i - 2), text[i + 1] == '[');
                        i = end + 2;
                        continue;
                    }
                }
                else if (text[i + 1] == '$')
                {
                    output.Append('$');
                    i += 2;
                    continue;
                }
            }

            if (c == '$')
            {
                if (i + 1 < text.Length && text[i + 1] == '$')
                {
                    var end = text.IndexOf("$$", i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        Add(pieces, output, text.Substring(i + 2, end - i - 2), true);
                        i = end + 2;
                        continue;
                    }
                }
                else
                {
                    var end = FindInlineDollar(text, i + 1);
                    if (end > i)
                    {
                        Add(pieces, output, text.Substring(i + 1, end - i - 1), false);
                        i = end + 1;
                        continue;
                    }
                }
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

    /// <summary>Renders one formula inline; used for TeX commands that hold math, such as \ensuremath.</summary>
    public static void AppendMath(InlineCollection target, string latex, double size) => Render(target, latex, size);

    /// <summary>The Unicode text for a symbol command such as <c>\times</c> or <c>\alpha</c>.</summary>
    public static bool TrySymbol(string name, out string symbol)
    {
        if (Symbols.TryGetValue(name, out var value))
        {
            symbol = value;
            return true;
        }

        symbol = "";
        return false;
    }

    /// <summary>Appends a literal, turning any math marker back into formatted inlines.</summary>
    public static void AppendLiteral(InlineCollection target, string text, double size, List<Piece> pieces)
    {
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf(MarkOpen, i);
            if (open < 0) { AddText(target, text[i..]); return; }

            var close = text.IndexOf(MarkClose, open + 1);
            if (close < 0) { AddText(target, text[i..]); return; }

            if (open > i) AddText(target, text[i..open]);

            if (int.TryParse(text[(open + 1)..close], out var index) && index >= 0 && index < pieces.Count)
                Render(target, pieces[index].Latex, size);

            i = close + 1;
        }
    }

    public static bool TryBlockPiece(string text, List<Piece> pieces, out Piece piece)
    {
        piece = null!;
        var trimmed = text.Trim();
        if (trimmed.Length < 3 || trimmed[0] != MarkOpen || trimmed[^1] != MarkClose) return false;
        if (!int.TryParse(trimmed[1..^1], out var index) || index < 0 || index >= pieces.Count) return false;
        if (!pieces[index].Display) return false;
        piece = pieces[index];
        return true;
    }

    /// <summary>A centred, slightly larger line for display math.</summary>
    public static TextBlock DisplayBlock(Piece piece, double size)
    {
        var block = new TextBlock
        {
            FontFamily = Theme.Font,
            FontSize = size * 1.15,
            Foreground = Theme.Ink,
            FlowDirection = FlowDirection.LeftToRight,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 12),
        };
        Render(block.Inlines, piece.Latex, size * 1.15);
        return block;
    }

    private static void Add(List<Piece> pieces, StringBuilder output, string latex, bool display)
    {
        if (latex.Trim().Length == 0) return;
        output.Append(MarkOpen).Append(pieces.Count).Append(MarkClose);
        pieces.Add(new Piece(latex, display));
    }

    private static int FindInlineDollar(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\n') return -1;
            if (text[i] == '\\') { i++; continue; }
            if (text[i] != '$') continue;

            var body = text[start..i];
            if (body.Length == 0 || char.IsWhiteSpace(body[0]) || char.IsWhiteSpace(body[^1])) continue;
            if (body.All(ch => char.IsDigit(ch) || ch is '.' or ',')) continue;
            return i;
        }
        return -1;
    }

    private static readonly string[] RawEnvironments =
        ["verbatim", "verbatim*", "lstlisting", "minted", "alltt"];

    /// <summary>Matches <c>\begin{verbatim}</c> and friends so their dollar signs stay literal.</summary>
    private static bool TryRawBlock(string text, int start, out int end)
    {
        end = start;
        foreach (var environment in RawEnvironments)
        {
            var open = "\\begin{" + environment + "}";
            if (!text.AsSpan(start).StartsWith(open)) continue;

            var close = "\\end{" + environment + "}";
            var at = text.IndexOf(close, start + open.Length, StringComparison.Ordinal);
            end = at < 0 ? text.Length : at + close.Length;
            return true;
        }

        return false;
    }

    /// <summary>Matches <c>\verb|...|</c> and <c>\verb*|...|</c>.</summary>
    private static bool TryVerb(string text, int start, out int end)
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

    private static void AddText(InlineCollection target, string text)
    {
        if (text.Length > 0) target.Add(new Run(text));
    }

    private static void Render(InlineCollection target, string latex, double size)
    {
        var reader = new Reader(latex);
        Parse(target, reader, size, stopAtBrace: false);
    }

    private static void Parse(InlineCollection target, Reader reader, double size, bool stopAtBrace)
    {
        while (!reader.AtEnd)
        {
            var c = reader.Peek();

            if (stopAtBrace && c == '}') { reader.Next(); return; }

            switch (c)
            {
                case '\\': Command(target, reader, size); continue;
                case '{': reader.Next(); Parse(target, reader, size, stopAtBrace: true); continue;
                case '}': reader.Next(); continue;
                case '^': reader.Next(); Script(target, reader, size, superscript: true); continue;
                case '_': reader.Next(); Script(target, reader, size, superscript: false); continue;
                case '&': reader.Next(); target.Add(new Run("  ")); continue;
            }

            if (char.IsWhiteSpace(c))
            {
                reader.Next();
                if (target.LastInline is not Run { Text: " " }) target.Add(new Run(" "));
                continue;
            }

            reader.Next();
            target.Add(new Run(Character(c)));
        }
    }

    private static string Character(char c) => c switch
    {
        '-' => "−",
        '*' => "∗",
        '\'' => "′",
        _ => c.ToString(),
    };

    private static void Script(InlineCollection target, Reader reader, double size, bool superscript)
    {
        var span = new Span
        {
            BaselineAlignment = superscript ? BaselineAlignment.Superscript : BaselineAlignment.Subscript,
            FontSize = size * 0.72,
        };
        Group(span.Inlines, reader, size * 0.9);
        if (span.Inlines.Count > 0) target.Add(span);
    }

    private static void Command(InlineCollection target, Reader reader, double size)
    {
        reader.Next();

        if (reader.AtEnd) { target.Add(new Run("\\")); return; }

        var first = reader.Next();
        if (!char.IsLetter(first))
        {
            switch (first)
            {
                case ',': target.Add(new Run("\u2009")); return;
                case ';': case ':': case '>': target.Add(new Run("\u2005")); return;
                case '!': return;
                case ' ': target.Add(new Run(" ")); return;
                case '\\': target.Add(new LineBreak()); return;
                case '{': target.Add(new Run("{")); return;
                case '}': target.Add(new Run("}")); return;
                case '&': target.Add(new Run("&")); return;
                case '%': target.Add(new Run("%")); return;
                case '#': target.Add(new Run("#")); return;
                case '$': target.Add(new Run("$")); return;
                case '_': target.Add(new Run("_")); return;
                case '|': target.Add(new Run("‖")); return;
                default: target.Add(new Run(first.ToString())); return;
            }
        }

        var name = new StringBuilder().Append(first);
        while (!reader.AtEnd && char.IsLetter(reader.Peek())) name.Append(reader.Next());
        var command = name.ToString();

        switch (command)
        {
            case "frac": case "dfrac": case "tfrac":
                target.Add(Fraction(GroupText(reader, size), GroupText(reader, size), size));
                return;
            case "sqrt":
                Root(target, reader, size);
                return;
            case "text": case "textrm": case "textnormal": case "mathrm": case "operatorname": case "mbox":
                Upright(target, reader, size, FontWeights.Normal, FontStyles.Normal);
                return;
            case "mathbf": case "textbf": case "boldsymbol": case "bm": case "pmb":
                Upright(target, reader, size, FontWeights.Bold, FontStyles.Normal);
                return;
            case "mathit": case "textit": case "emph": case "mathcal": case "mathscr":
                Upright(target, reader, size, FontWeights.Normal, FontStyles.Italic);
                return;
            case "mathbb": case "mathfrak": case "mathsf": case "mathtt": case "mathnormal":
                Upright(target, reader, size, FontWeights.Normal, FontStyles.Normal);
                return;
            case "overline": case "bar":
                Overline(target, reader, size);
                return;
            case "underline":
                Underline(target, reader, size);
                return;
            case "hat": case "widehat":
                Accent(target, reader, size, '\u0302');
                return;
            case "tilde": case "widetilde":
                Accent(target, reader, size, '\u0303');
                return;
            case "vec":
                Accent(target, reader, size, '\u20D7');
                return;
            case "dot":
                Accent(target, reader, size, '\u0307');
                return;
            case "ddot":
                Accent(target, reader, size, '\u0308');
                return;
            case "left": case "right": case "middle":
            case "big": case "Big": case "bigg": case "Bigg":
            case "bigl": case "bigr": case "Bigl": case "Bigr": case "biggl": case "biggr":
                SkipDelimiter(target, reader, size);
                return;
            case "begin": case "end":
                SkipBraced(reader);
                return;
            case "quad": target.Add(new Run("\u2003")); return;
            case "qquad": target.Add(new Run("\u2003\u2003")); return;
            case "enspace": target.Add(new Run("\u2002")); return;
            case "thinspace": target.Add(new Run("\u2009")); return;
            case "negthinspace": return;
            case "displaystyle": case "textstyle": case "scriptstyle": case "scriptscriptstyle":
            case "limits": case "nolimits": case "nonumber": case "notag":
                return;
            case "mod": case "bmod":
                target.Add(new Run(" mod "));
                return;
            case "pmod":
                target.Add(new Run(" (mod "));
                target.Add(new Run(GroupText(reader, size) + ")"));
                return;
            case "not":
                target.Add(new Run("¬"));
                return;
            case "hline": case "cline":
                SkipBraced(reader);
                return;
        }

        if (Symbols.TryGetValue(command, out var symbol)) { target.Add(new Run(symbol)); return; }
        if (Functions.Contains(command)) { target.Add(new Run(command) { FontStyle = FontStyles.Normal }); return; }

        // Unknown: keep the name so the reader still sees what was written.
        target.Add(new Run(command));
    }

    private static void Root(InlineCollection target, Reader reader, double size)
    {
        reader.SkipSpaces();
        string? index = null;
        if (reader.Peek() == '[')
        {
            reader.Next();
            var builder = new StringBuilder();
            while (!reader.AtEnd && reader.Peek() != ']') builder.Append(reader.Next());
            if (!reader.AtEnd) reader.Next();
            index = builder.ToString();
        }

        var radicand = GroupText(reader, size);
        target.Add(new Run(index is { Length: > 0 } ? index + "√" : "√"));
        target.Add(new Run(radicand) { TextDecorations = new TextDecorationCollection { new TextDecoration { Location = TextDecorationLocation.OverLine } } });
    }

    private static void Upright(InlineCollection target, Reader reader, double size, FontWeight weight, FontStyle style)
    {
        var span = new Span { FontWeight = weight, FontStyle = style };
        Group(span.Inlines, reader, size);
        if (span.Inlines.Count > 0) target.Add(span);
    }

    private static void Overline(InlineCollection target, Reader reader, double size)
    {
        var span = new Span { TextDecorations = new TextDecorationCollection { new TextDecoration { Location = TextDecorationLocation.OverLine } } };
        Group(span.Inlines, reader, size);
        if (span.Inlines.Count > 0) target.Add(span);
    }

    private static void Underline(InlineCollection target, Reader reader, double size)
    {
        var span = new Span { TextDecorations = TextDecorations.Underline };
        Group(span.Inlines, reader, size);
        if (span.Inlines.Count > 0) target.Add(span);
    }

    private static void Accent(InlineCollection target, Reader reader, double size, char mark)
    {
        var text = GroupText(reader, size);
        if (text.Length == 0) return;
        target.Add(new Run(string.Concat(text.Select(ch => ch + mark.ToString()))));
    }

    private static void SkipDelimiter(InlineCollection target, Reader reader, double size)
    {
        reader.SkipSpaces();
        if (reader.AtEnd) return;

        if (reader.Peek() == '\\') { Command(target, reader, size); return; }

        var c = reader.Next();
        if (c is '(' or ')' or '[' or ']' or '|' or '/' or '<' or '>' or '{' or '}') target.Add(new Run(c.ToString()));
    }

    private static void SkipBraced(Reader reader)
    {
        reader.SkipSpaces();
        if (reader.Peek() != '{') return;
        reader.Next();
        var depth = 1;
        while (!reader.AtEnd && depth > 0)
        {
            var c = reader.Next();
            if (c == '{') depth++;
            else if (c == '}') depth--;
        }
    }

    private static void Group(InlineCollection target, Reader reader, double size)
    {
        reader.SkipSpaces();
        if (reader.AtEnd) return;

        if (reader.Peek() == '{')
        {
            reader.Next();
            Parse(target, reader, size, stopAtBrace: true);
            return;
        }

        if (reader.Peek() == '\\') { Command(target, reader, size); return; }

        target.Add(new Run(Character(reader.Next())));
    }

    private static string GroupText(Reader reader, double size)
    {
        var span = new Span();
        Group(span.Inlines, reader, size);
        return FlowKit.PlainTextOf(span.Inlines);
    }

    private static InlineUIContainer Fraction(string numerator, string denominator, double size)
    {
        var top = new TextBlock
        {
            Text = numerator.Length > 0 ? numerator : " ",
            FontSize = size * 0.85,
            Foreground = Theme.Ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
        };

        var bottom = new TextBlock
        {
            Text = denominator.Length > 0 ? denominator : " ",
            FontSize = size * 0.85,
            Foreground = Theme.Ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
        stack.Children.Add(top);
        stack.Children.Add(new Border { Height = 1, Background = Theme.Ink, Margin = new Thickness(0, 1, 0, 1), MinWidth = 12 });
        stack.Children.Add(bottom);

        return new InlineUIContainer(stack) { BaselineAlignment = BaselineAlignment.Center };
    }

    private sealed class Reader(string text)
    {
        private readonly string _text = text;
        private int _index;

        public bool AtEnd => _index >= _text.Length;

        public char Peek() => _index < _text.Length ? _text[_index] : '\0';

        public char Next() => _index < _text.Length ? _text[_index++] : '\0';

        public void SkipSpaces()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
        }
    }
}
