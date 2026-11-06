using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Adawat.Core;
using Microsoft.Win32;

namespace Adawat.App;

/// <summary>
/// A lightweight native WPF Markdown viewer for notes and clipboard snippets,
/// supporting headers, bold, italics, code blocks, lists, blockquotes, and export.
/// </summary>
public static class MarkdownViewer
{
    public static void OpenPreview(string title, string markdown, Window? owner = null)
    {
        var window = new ToolWindow($"{L10n.T("Preview")}: {title}", 640) { Owner = owner };
        var doc = BuildFlowDocument(title, markdown);

        var viewer = new FlowDocumentScrollViewer
        {
            Document = doc,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 420,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        window.Body = viewer;

        Button? dirBtn = null;
        void UpdateDirBtn()
        {
            if (dirBtn == null) return;
            var isRtl = doc.FlowDirection == FlowDirection.RightToLeft;
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(UI.Icon("", 12, Theme.Ink));
            panel.Children.Add(new Border { Width = 6 });
            panel.Children.Add(UI.Text(isRtl ? "LTR" : "RTL", 13, FontWeights.Medium, Theme.Ink));
            dirBtn.Content = panel;
        }

        dirBtn = UI.Button(doc.FlowDirection == FlowDirection.RightToLeft ? "LTR" : "RTL", () =>
        {
            doc.FlowDirection = doc.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            UpdateDirBtn();
        }, primary: false, icon: "");

        window.Leading(
            UI.Button("Copy Text", () =>
            {
                Adawat.Core.Clipboard.CopyText(markdown);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
            }, primary: false, icon: ""),
            dirBtn,
            UI.Button("Export...", () => Export(title, markdown, window), primary: false, icon: "")
        );

        window.Trailing(UI.Button("Close", window.Close, primary: true));
        window.Present();
    }

    public static FlowDocument BuildFlowDocument(string title, string markdown)
    {
        var isArabic = markdown.Any(c => c >= 0x0600 && c <= 0x06FF);
        var doc = new FlowDocument
        {
            FontFamily = Theme.Font,
            FontSize = 13,
            Foreground = Theme.Ink,
            PagePadding = new Thickness(16),
            FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        };

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var inCodeBlock = false;
        var codeBuilder = new System.Text.StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    inCodeBlock = false;
                    doc.Blocks.Add(CreateCodeBlock(codeBuilder.ToString()));
                    codeBuilder.Clear();
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBuilder.AppendLine(line);
                continue;
            }

            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // Headers
            if (trimmed.StartsWith("### "))
            {
                var p = new Paragraph { Margin = new Thickness(0, 10, 0, 4) };
                p.Inlines.Add(new Run(trimmed[4..]) { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Theme.Ink });
                doc.Blocks.Add(p);
            }
            else if (trimmed.StartsWith("## "))
            {
                var p = new Paragraph { Margin = new Thickness(0, 12, 0, 4) };
                p.Inlines.Add(new Run(trimmed[3..]) { FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Theme.Ink });
                doc.Blocks.Add(p);
            }
            else if (trimmed.StartsWith("# "))
            {
                var p = new Paragraph { Margin = new Thickness(0, 14, 0, 6) };
                p.Inlines.Add(new Run(trimmed[2..]) { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Theme.Accent });
                doc.Blocks.Add(p);
            }
            // Blockquotes
            else if (trimmed.StartsWith("> "))
            {
                var p = new Paragraph { Margin = new Thickness(12, 4, 12, 4), FontStyle = FontStyles.Italic, Foreground = Theme.Muted };
                p.Inlines.Add(new Run("▎ ") { Foreground = Theme.Accent, FontStyle = FontStyles.Normal });
                AddFormattedInlines(p.Inlines, trimmed[2..]);
                doc.Blocks.Add(p);
            }
            // Unordered list
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                var p = new Paragraph { Margin = new Thickness(16, 2, 0, 2) };
                p.Inlines.Add(new Run("•  ") { FontWeight = FontWeights.Bold, Foreground = Theme.Accent });
                AddFormattedInlines(p.Inlines, trimmed[2..]);
                doc.Blocks.Add(p);
            }
            // Numbered list
            else if (Regex.IsMatch(trimmed, @"^\d+\.\s"))
            {
                var match = Regex.Match(trimmed, @"^(\d+\.)\s+(.*)");
                var p = new Paragraph { Margin = new Thickness(16, 2, 0, 2) };
                p.Inlines.Add(new Run(match.Groups[1].Value + "  ") { FontWeight = FontWeights.SemiBold });
                AddFormattedInlines(p.Inlines, match.Groups[2].Value);
                doc.Blocks.Add(p);
            }
            // Normal paragraph
            else
            {
                var p = new Paragraph { Margin = new Thickness(0, 4, 0, 4), LineHeight = 20 };
                AddFormattedInlines(p.Inlines, line);
                doc.Blocks.Add(p);
            }
        }

        if (inCodeBlock && codeBuilder.Length > 0)
        {
            doc.Blocks.Add(CreateCodeBlock(codeBuilder.ToString()));
        }

        return doc;
    }

    private static Block CreateCodeBlock(string code)
    {
        var textBlock = new TextBlock
        {
            Text = code.TrimEnd(),
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 12,
            Foreground = Theme.Ink,
            TextWrapping = TextWrapping.Wrap,
        };

        var border = new Border
        {
            Child = textBlock,
            Background = Theme.ButtonFill,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 6, 0, 6),
        };

        return new BlockUIContainer(border);
    }

    private static void AddFormattedInlines(InlineCollection inlines, string text)
    {
        // Simple tokenization for bold (** or __), italic (* or _), and code (`)
        var pattern = @"(`[^`]+`|\*\*[^*]+\*\*|__[^_]+__|\*[^*]+\*|_[^_]+_)";
        var parts = Regex.Split(text, pattern);

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            if (part.StartsWith("`") && part.EndsWith("`") && part.Length >= 2)
            {
                inlines.Add(new Run(part[1..^1])
                {
                    FontFamily = new FontFamily("Consolas, monospace"),
                    FontSize = 11.5,
                    Background = Theme.ButtonFill,
                    Foreground = Theme.Accent
                });
            }
            else if ((part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4) ||
                     (part.StartsWith("__") && part.EndsWith("__") && part.Length >= 4))
            {
                inlines.Add(new Bold(new Run(part[2..^2])));
            }
            else if ((part.StartsWith("*") && part.EndsWith("*") && part.Length >= 2) ||
                     (part.StartsWith("_") && part.EndsWith("_") && part.Length >= 2))
            {
                inlines.Add(new Italic(new Run(part[1..^1])));
            }
            else
            {
                inlines.Add(new Run(part));
            }
        }
    }

    public static void Export(string title, string content, Window owner)
    {
        var defaultName = string.IsNullOrWhiteSpace(title) ? "Document" : title;
        foreach (var c in Path.GetInvalidFileNameChars()) defaultName = defaultName.Replace(c, '_');

        var dlg = new SaveFileDialog
        {
            Title = L10n.T("Export Document"),
            FileName = defaultName,
            Filter = "Markdown Document (*.md)|*.md|Text Document (*.txt)|*.txt",
            DefaultExt = ".md"
        };

        if (dlg.ShowDialog(owner) == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, content, System.Text.Encoding.UTF8);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Exported successfully"), Hud.Style.Success);
            }
            catch (Exception ex)
            {
                Log.Write($"Export error: {ex}");
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        }
    }
}
