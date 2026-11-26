using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Adawat.App.Documents;

/// <summary>
/// The pieces the Markdown and document renderers share: the page, code blocks, rules, links and
/// pictures. Everything points at the live theme brushes, so a theme switch repaints an open
/// preview in place instead of leaving it in the old colours.
/// </summary>
internal static class FlowKit
{
    public static FlowDocument NewDocument(bool rightToLeft, double size = 13) => new()
    {
        FontFamily = Theme.Font,
        FontSize = size,
        Foreground = Theme.Ink,
        PagePadding = new Thickness(20, 16, 20, 20),
        FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
    };

    public static Block CodeBlock(string code, string? language, double size = 13)
    {
        var text = new TextBlock
        {
            Text = code.TrimEnd('\n', '\r'),
            FontFamily = Theme.Mono,
            FontSize = size * 0.92,
            Foreground = Theme.Ink,
            TextWrapping = TextWrapping.Wrap,
            FlowDirection = FlowDirection.LeftToRight,
            LineHeight = size * 1.45,
        };

        var stack = new StackPanel();
        if (!string.IsNullOrWhiteSpace(language))
        {
            stack.Children.Add(new TextBlock
            {
                Text = language.Trim().ToUpperInvariant(),
                FontFamily = Theme.Font,
                FontSize = size * 0.72,
                Foreground = Theme.Muted,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 6),
            });
        }
        stack.Children.Add(text);

        var border = new Border
        {
            Background = Theme.ButtonFill,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 4, 0, 12),
            Child = stack,
        }.Radius("Theme.RadiusSmall");

        return new BlockUIContainer(border);
    }

    public static Block Hairline(double top = 16, double bottom = 16) =>
        new BlockUIContainer(new Border { Height = 1, Background = Theme.Hairline, Margin = new Thickness(0, top, 0, bottom) });

    public static Paragraph Note(string text, double size = 13, bool italic = true) => new(new Run(text))
    {
        Foreground = Theme.Muted,
        FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
        FontSize = size,
        Margin = new Thickness(0, 4, 0, 6),
    };

    public static Hyperlink Link(string url, Action<string>? open, double size = 13)
    {
        var link = new Hyperlink
        {
            Foreground = Theme.Accent,
            Cursor = Cursors.Hand,
            ToolTip = url,
            FontSize = size,
        };
        if (open != null) link.Click += (_, _) => open(url);
        return link;
    }

    /// <summary>Loads a picture relative to the document's folder. Data URIs work without a folder.</summary>
    public static BitmapSource? LoadImage(string url, string? baseDir)
    {
        try
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = url.IndexOf(',');
                if (comma < 0 || !url[..comma].Contains("base64", StringComparison.OrdinalIgnoreCase)) return null;
                return FromBytes(Convert.FromBase64String(url[(comma + 1)..]));
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;

            if (baseDir == null) return null;

            var clean = Uri.UnescapeDataString(url.Split('#')[0].Split('?')[0]);
            if (clean.Length == 0) return null;

            var root = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.IsPathRooted(clean) ? clean : Path.Combine(baseDir, clean));

            // A document may only pull pictures from its own folder: no walking up to other files.
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            if (!File.Exists(path)) return null;

            using var stream = File.OpenRead(path);
            return FromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    public static Block ImageBlock(BitmapSource source, string alt)
    {
        var image = new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 720,
            ToolTip = alt,
        };

        return new BlockUIContainer(new Border
        {
            Child = image,
            Margin = new Thickness(0, 4, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
        }.Radius("Theme.RadiusSmall"));
    }

    public static string PlainTextOf(InlineCollection inlines)
    {
        var text = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run: text.Append(run.Text); break;
                case LineBreak: text.Append(' '); break;
                case Span span: text.Append(PlainTextOf(span.Inlines)); break;
            }
        }
        return text.ToString();
    }

    /// <summary>Readable text out of an HTML fragment, with scripts, styles and tags removed.</summary>
    public static string StripHtml(string html)
    {
        var text = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(p|div|li|h[1-6]|tr|blockquote)>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static BitmapSource? FromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return FromStream(stream);
    }

    private static BitmapSource? FromStream(Stream stream)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
