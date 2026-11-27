using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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

    public static FlowDocument BuildFlowDocument(string title, string text)
    {
        if (TexFlow.LooksLikeLatex(text)) return TexFlow.FromLatex(text);
        if (LooksLikeHtml(text))
            return DocFlow.FromDoc(Adawat.Core.Documents.HtmlReader.Read(text, title));
        return MarkdownFlow.LooksLikeMarkdown(text)
            ? MarkdownFlow.FromMarkdown(text)
            : MarkdownFlow.FromPlain(text);
    }

    private static bool LooksLikeHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return true;

        // Markdown can carry an HTML sample in a fence; it stays Markdown.
        if (MarkdownFlow.LooksLikeMarkdown(text)) return false;

        if (text.Contains("<body", StringComparison.OrdinalIgnoreCase)) return true;

        var blocks = 0;
        foreach (var tag in new[] { "<p>", "<p ", "<div", "<h1", "<h2", "<h3", "<ul", "<ol", "<table", "<blockquote" })
            if (text.Contains(tag, StringComparison.OrdinalIgnoreCase)) blocks++;
        return blocks >= 2;
    }

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

        bool? picked;
        using (WindowLifecycle.HoldAutoClose(owner)) picked = dialog.ShowDialog(owner);
        if (picked != true) return;

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
        private readonly ProgressBar _loading;
        private readonly TextBlock _zoom;
        private readonly Border _findBar;
        private readonly TextBox _findBox;
        private readonly TextBlock _findStatus;
        private readonly string _heading;
        private string _source = "";
        private TextRange? _findRange;

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
                IsSelectionEnabled = true,
            };
            TextOptions.SetTextFormattingMode(_viewer, TextFormattingMode.Display);
            _viewer.PreviewMouseWheel += (_, e) =>
            {
                if (Scroller() is not { } scroller) return;
                scroller.ScrollToVerticalOffset(scroller.VerticalOffset - e.Delta * 0.8);
                e.Handled = true;
            };
            _viewer.PreviewKeyDown += OnViewerKeyDown;
            PreviewKeyDown += OnViewerKeyDown;

            _zoom = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                Foreground = Theme.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 42,
                TextAlignment = TextAlignment.Center,
            };

            var findIcon = UI.Icon("\uE721", 12);
            findIcon.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Muted");
            findIcon.Margin = new Thickness(0, 0, 8, 0);

            _findBox = new TextBox
            {
                Width = 220,
                MinHeight = 26,
                FontSize = 12.5,
                FontFamily = Theme.Font,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 8, 0),
            };
            _findBox.SetResourceReference(TextBox.BackgroundProperty, "Theme.Field");
            _findBox.SetResourceReference(TextBox.ForegroundProperty, "Theme.Ink");
            _findBox.SetResourceReference(TextBox.BorderBrushProperty, "Theme.LineStrong");
            _findBox.PreviewKeyDown += OnFindKeyDown;

            _findStatus = new TextBlock
            {
                Text = "",
                FontSize = 12,
                MinWidth = 80,
                MaxWidth = 180,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 6, 0),
            };
            _findStatus.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Muted");
            _findBox.TextChanged += (_, _) =>
            {
                _findStatus.Text = "";
                ClearFind();
            };

            var previousMatch = UI.IconButton("\uE70E", () => FindNext(false), "Previous match", 24);
            previousMatch.Margin = new Thickness(2, 0, 2, 0);
            var nextMatch = UI.IconButton("\uE70D", () => FindNext(true), "Next match", 24);
            nextMatch.Margin = new Thickness(2, 0, 2, 0);
            var closeFind = UI.IconButton("\uE711", CloseFind, "Close find", 24);

            var findPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            findPanel.Children.Add(findIcon);
            findPanel.Children.Add(_findBox);
            findPanel.Children.Add(_findStatus);
            findPanel.Children.Add(previousMatch);
            findPanel.Children.Add(nextMatch);
            findPanel.Children.Add(closeFind);

            _findBar = new Border
            {
                Visibility = Visibility.Collapsed,
                Padding = new Thickness(10, 6, 6, 6),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1),
                Child = findPanel,
            }.Radius("Theme.RadiusSmall");
            _findBar.SetResourceReference(Border.BackgroundProperty, "Theme.Surface2");
            _findBar.SetResourceReference(Border.BorderBrushProperty, "Theme.Line");

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_findBar, 0);
            Grid.SetRow(_viewer, 1);
            content.Children.Add(_findBar);
            content.Children.Add(_viewer);

            _loading = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 3,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Visible,
            };
            Grid.SetRow(_loading, 1);
            content.Children.Add(_loading);

            Body = content;
            BuildLeading();
            Trailing(
                UI.Button("Find", ToggleFind, primary: false),
                UI.Button("Copy", Copy, primary: false),
                UI.Button("Export...", () => Export(_heading, _source, this), primary: false),
                UI.Button("Close", CloseOnce, primary: true));

            // Build the document after the window is visible: doing it in the constructor blocked
            // the caller and the pointer until a long note was fully measured and laid out.
            _source = text;
            Loaded += async (_, _) =>
            {
                if (_viewer.Document != null) return;
                ScrollToTop();
                await ShowTextAsync(text);
            };
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

        private async Task ShowTextAsync(string text)
        {
            _source = text;
            ClearFind();
            _loading.Visibility = Visibility.Visible;
            try
            {
                var flow = await BuildFlowAsync(text);
                if (WindowLifecycle.IsClosingOrClosed(this)) return;
                _viewer.Document = flow;
            }
            catch (Exception ex)
            {
                Log.Write($"Preview render error: {ex}");
                if (WindowLifecycle.IsClosingOrClosed(this)) return;
                _viewer.Document = MarkdownFlow.FromPlain(text);
            }
            _viewer.Zoom = 100;
            _zoom.Text = "100%";
            _findStatus.Text = "";
            _loading.Visibility = Visibility.Collapsed;
            ScrollToTop();
            BuildLeading();
        }

        private async Task<FlowDocument> BuildFlowAsync(string text)
        {
            if (TexFlow.LooksLikeLatex(text))
                return TexFlow.FromLatex(text, null, OpenTarget);
            if (LooksLikeHtml(text))
            {
                var doc = await Task.Run(() => Adawat.Core.Documents.HtmlReader.Read(text, _heading)).ConfigureAwait(true);
                return await DocFlow.FromDocAsync(doc, null, OpenTarget, 13, yieldForUi: true).ConfigureAwait(true);
            }
            return MarkdownFlow.LooksLikeMarkdown(text)
                ? await MarkdownFlow.FromMarkdownAsync(text, null, OpenTarget, 13, yieldForUi: true).ConfigureAwait(true)
                : await MarkdownFlow.FromPlainAsync(text, null, 13, yieldForUi: true).ConfigureAwait(true);
        }

        /// <summary>FlowDocumentScrollViewer has no ScrollToHome, so the template's scroller is used.</summary>
        private void ScrollToTop()
        {
            if (Scroller() is { } scroller)
                scroller.ScrollToTop();
            else
                _viewer.Document?.Blocks.FirstBlock?.BringIntoView();
        }

        private ScrollViewer? Scroller()
        {
            _viewer.ApplyTemplate();
            return _viewer.Template?.FindName("PART_ContentHost", _viewer) as ScrollViewer;
        }

        private void ToggleFind()
        {
            _findBar.Visibility = Visibility.Visible;
            _findBox.Focus();
            _findBox.SelectAll();
        }

        private void CloseFind()
        {
            _findBar.Visibility = Visibility.Collapsed;
            _findStatus.Text = "";
            ClearFind();
        }

        private void FindNext(bool forward)
        {
            var query = _findBox.Text;
            if (query.Trim().Length == 0)
            {
                ClearFind();
                _findStatus.Text = "";
                return;
            }

            ClearFind();
            if (_viewer.Document is not { } document) return;

            var content = new TextRange(document.ContentStart, document.ContentEnd).Text;
            if (string.IsNullOrEmpty(content))
            {
                _findStatus.Text = L10n.T("No matches");
                return;
            }

            var compare = StringComparison.OrdinalIgnoreCase;
            var start = CurrentFlowOffset(document, forward);
            int at = -1;
            if (forward)
            {
                at = content.IndexOf(query, Math.Clamp(start, 0, content.Length), compare);
                if (at < 0 && start > 0) at = content.IndexOf(query, 0, compare);
            }
            else
            {
                var from = Math.Clamp(start - 1, 0, Math.Max(0, content.Length - 1));
                if (content.Length > 0) at = content.LastIndexOf(query, from, compare);
                if (at < 0 && start < content.Length) at = content.LastIndexOf(query, content.Length - 1, compare);
            }

            if (at < 0)
            {
                _findStatus.Text = L10n.T("No matches");
                return;
            }

            var pointer = GetTextPointerAtOffset(document.ContentStart, at);
            var end = GetTextPointerAtOffset(pointer ?? document.ContentStart, query.Length);
            if (pointer == null || end == null)
            {
                _findStatus.Text = L10n.T("No matches");
                return;
            }

            var range = new TextRange(pointer, end);
            range.ApplyPropertyValue(TextElement.BackgroundProperty, Theme.AccentSoft);
            _viewer.Focus();
            range.Select(pointer, end);
            pointer.Paragraph?.BringIntoView();
            _findRange = range;
            _findStatus.Text = L10n.T("Match");
        }

        private void ClearFind()
        {
            if (_findRange == null) return;
            try
            {
                _findRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            }
            catch { }
            _findRange = null;
        }

        private int CurrentFlowOffset(FlowDocument document, bool forward)
        {
            var selection = _viewer.Selection;
            if (selection != null && !selection.IsEmpty)
            {
                var p = forward ? selection.End : selection.Start;
                return Math.Max(0, new TextRange(document.ContentStart, p).Text.Length);
            }
            return 0;
        }

        private static TextPointer? GetTextPointerAtOffset(TextPointer start, int offset)
        {
            var navigator = start;
            var remaining = Math.Max(0, offset);
            while (navigator != null)
            {
                if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var text = navigator.GetTextInRun(LogicalDirection.Forward);
                    if (remaining <= text.Length)
                        return navigator.GetPositionAtOffset(remaining, LogicalDirection.Forward);
                    remaining -= text.Length;
                    navigator = navigator.GetPositionAtOffset(text.Length, LogicalDirection.Forward);
                }
                else
                {
                    var next = navigator.GetNextContextPosition(LogicalDirection.Forward);
                    if (next == null) break;
                    navigator = next;
                }
            }
            return navigator;
        }

        private void OnFindKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindNext((Keyboard.Modifiers & ModifierKeys.Shift) == 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseFind();
                e.Handled = true;
            }
        }

        private void OnViewerKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.F)
            {
                ToggleFind();
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && _findBar.Visibility == Visibility.Visible)
            {
                FindNext((Keyboard.Modifiers & ModifierKeys.Shift) == 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _findBar.Visibility == Visibility.Visible)
            {
                CloseFind();
                e.Handled = true;
            }
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
