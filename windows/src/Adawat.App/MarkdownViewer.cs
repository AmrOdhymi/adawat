using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Adawat.App.Documents;
using Adawat.Core;
using Microsoft.Win32;
using DocumentViewer = Adawat.App.Documents.DocumentViewer;

namespace Adawat.App;

/// <summary>
/// The read-only preview shared by the clipboard and the notepad. It shows exactly the text it was
/// given, with Markdown drawn formatted, and there is no way to open another file from it. Tabs,
/// Open and drag and drop belong to <see cref="DocumentViewer"/>.
/// </summary>
public static class MarkdownViewer
{
    public static void OpenPreview(string title, string text, Window? owner = null) =>
        new PreviewWindow(title, text, owner).Present();

    /// <summary>Kept for callers that already have a path; documents with tabs open in the viewer.</summary>
    public static void OpenFile(string path, Window? owner = null) => DocumentViewer.Present([path]);

    public static FlowDocument BuildFlowDocument(string title, string text) =>
        MarkdownFlow.LooksLikeMarkdown(text)
            ? MarkdownFlow.FromMarkdown(text)
            : MarkdownFlow.FromPlain(text);

    public static void Export(string title, string content, Window owner)
    {
        var defaultName = string.IsNullOrWhiteSpace(title) ? "Document" : title;
        foreach (var c in Path.GetInvalidFileNameChars()) defaultName = defaultName.Replace(c, '_');

        var dialog = new SaveFileDialog
        {
            Title = L10n.T("Export Document"),
            FileName = defaultName,
            Filter = "Markdown Document (*.md)|*.md|Text Document (*.txt)|*.txt",
            DefaultExt = ".md",
        };

        if (dialog.ShowDialog(owner) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, content, System.Text.Encoding.UTF8);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Exported successfully"), Hud.Style.Success);
        }
        catch (Exception ex)
        {
            Log.Write($"Export error: {ex}");
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
        }
    }

    private sealed class PreviewWindow : ToolWindow
    {
        private readonly FlowDocumentScrollViewer _viewer;
        private readonly TextBlock _zoom;
        private readonly string _heading;
        private string _source = "";

        public PreviewWindow(string heading, string text, Window? owner) : base($"Preview: {heading}", 880)
        {
            _heading = heading;
            ShowInTaskbar = false;
            if (owner != null) Owner = owner;

            SizeToContent = SizeToContent.Manual;
            Height = 640;
            MinWidth = 500;
            MinHeight = 360;
            MaxWidth = 1280;
            ResizeMode = ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 44,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                UseAeroCaptionButtons = false,
            });

            _viewer = new FlowDocumentScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                IsToolBarVisible = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MinZoom = 60,
                MaxZoom = 250,
                ZoomIncrement = 10,
                Zoom = 100,
            };

            _zoom = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                Foreground = Theme.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 42,
                TextAlignment = TextAlignment.Center,
            };

            Body = _viewer;
            BuildLeading();
            Trailing(
                UI.Button("Copy", Copy, primary: false),
                UI.Button("Export...", () => Export(_heading, _source, this), primary: false),
                UI.Button("Close", CloseOnce, primary: true));

            Loaded += (_, _) => ScrollToTop();
            ShowText(text);
        }

        private void BuildLeading()
        {
            ClearLeading();
            var rightToLeft = _viewer.Document?.FlowDirection == FlowDirection.RightToLeft;
            Leading(
                UI.Button("-", () => ZoomBy(-10), primary: false),
                _zoom,
                UI.Button("+", () => ZoomBy(10), primary: false),
                UI.Button(rightToLeft ? "LTR" : "RTL", ToggleDirection, primary: false));
        }

        private void ZoomBy(double delta)
        {
            _viewer.Zoom = Math.Clamp(_viewer.Zoom + delta, _viewer.MinZoom, _viewer.MaxZoom);
            _zoom.Text = $"{_viewer.Zoom:0}%";
        }

        private void ToggleDirection()
        {
            if (_viewer.Document is { } document)
            {
                document.FlowDirection = document.FlowDirection == FlowDirection.RightToLeft
                    ? FlowDirection.LeftToRight
                    : FlowDirection.RightToLeft;
            }
            BuildLeading();
        }

        private void ShowText(string text)
        {
            _source = text;
            _viewer.Document = MarkdownFlow.LooksLikeMarkdown(text)
                ? MarkdownFlow.FromMarkdown(text, null, OpenTarget)
                : MarkdownFlow.FromPlain(text);
            _viewer.Zoom = 100;
            _zoom.Text = "100%";
            ScrollToTop();
            BuildLeading();
        }

        /// <summary>FlowDocumentScrollViewer has no ScrollToHome, so the template's scroller is used.</summary>
        private void ScrollToTop()
        {
            _viewer.ApplyTemplate();
            if (_viewer.Template?.FindName("PART_ContentHost", _viewer) is ScrollViewer scroller)
                scroller.ScrollToTop();
            else
                _viewer.Document?.Blocks.FirstBlock?.BringIntoView();
        }

        private void Copy()
        {
            Adawat.Core.Clipboard.CopyText(_source);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
        }

        /// <summary>Web links stay clickable; a link to another local document is ignored on purpose.</summary>
        private void OpenTarget(string url)
        {
            try
            {
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Log.Write($"Preview link error: {ex}");
            }
        }
    }
}
