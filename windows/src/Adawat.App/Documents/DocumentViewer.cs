using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Adawat.Core;
using Adawat.Core.Pdf;
using Microsoft.Win32;

namespace Adawat.App.Documents;

/// <summary>
/// The read-only document window opened from the wheel. It keeps one tab per file, accepts more files
/// through Open or drag and drop, and remembers the tab list (path, scroll and zoom only) after it is
/// closed. Nothing here edits a file or keeps its contents alive when the tab is not being read.
/// </summary>
public static class DocumentViewer
{
    private static ViewerWindow? _open;

    public static Window? Make(IReadOnlyList<string> files)
    {
        if (_open != null && !WindowLifecycle.IsClosingOrClosed(_open))
        {
            if (files.Count > 0) _open.AddFiles(files);
            return _open;
        }

        var window = new ViewerWindow(files);
        window.Closed += (_, _) => { if (ReferenceEquals(_open, window)) _open = null; };
        _open = window;
        return window;
    }

    public static void Present(IReadOnlyList<string>? files = null)
    {
        if (Make(files ?? []) is not ViewerWindow window) return;
        window.Present();
    }

    /// <summary>Opens the saved tabs, or the file picker when there is nothing remembered yet.</summary>
    public static void PickOrPresent()
    {
        if (_open != null && !WindowLifecycle.IsClosingOrClosed(_open))
        {
            _open.Present();
            return;
        }

        var (tabs, _) = PreviewTabsStore.Load();
        if (tabs.Count > 0) Present();
        else Pick();
    }

    public static void Pick()
    {
        var dialog = new OpenFileDialog
        {
            Title = L10n.T("Open Document"),
            Filter = "Documents|*.md;*.markdown;*.txt;*.html;*.htm;*.rtf;*.docx;*.doc;*.odt;*.epub;*.tex;*.latex;*.pdf|" +
                     "Markdown (*.md)|*.md;*.markdown|Text (*.txt)|*.txt|PDF (*.pdf)|*.pdf|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true,
        };

        if (dialog.ShowDialog() == true) Present(dialog.FileNames);
    }

    private sealed class Tab
    {
        public Tab(string path) => Path = path;

        public string Path { get; }
        public string Text { get; set; } = "";
        public double Scroll { get; set; }
        public double Zoom { get; set; } = 100;
        public FrameworkElement? View { get; set; }
        public PdfView? Pdf { get; set; }
        public Func<ScrollViewer?>? FindScroller { get; set; }
        public int BuildToken { get; set; }
    }

    private sealed class ViewerWindow : ToolWindow
    {
        private const int KeepViews = 3;

        private readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly ScrollViewer _tabScroll;
        private readonly ContentControl _host = new()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        private readonly Border _dropHint;
        private readonly TextBlock _zoomLabel;
        private readonly TextBlock _pageCount;
        private readonly TextBox _pageBox;
        private readonly StackPanel _pageNav;
        private FrameworkElement _emptyView;
        private readonly Button _copyButton;
        private readonly Button _exportButton;
        private readonly Button _openAppButton;
        private readonly List<Tab> _tabs = [];
        private readonly List<Tab> _recent = [];
        private Tab? _active;
        private PdfView? _pageOwner;
        private int _token;

        public ViewerWindow(IReadOnlyList<string> files) : base("Documents", 980)
        {
            SizeToContent = SizeToContent.Manual;
            Width = 980;
            Height = 720;
            MinWidth = 640;
            MinHeight = 420;
            MaxWidth = 1600;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = true;
            AllowDrop = true;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 44,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                UseAeroCaptionButtons = false,
            });

            _tabScroll = new ScrollViewer
            {
                Content = _tabStrip,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                MaxHeight = 40,
                Padding = new Thickness(0, 0, 0, 0),
            };

            _zoomLabel = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                Foreground = Theme.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 42,
                TextAlignment = TextAlignment.Center,
            };

            _pageBox = new TextBox
            {
                Width = 46,
                MinHeight = 24,
                FontSize = 12,
                FontFamily = Theme.Font,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 4, 0),
            };
            _pageBox.SetResourceReference(TextBox.BackgroundProperty, "Theme.Field");
            _pageBox.SetResourceReference(TextBox.ForegroundProperty, "Theme.Ink");
            _pageBox.SetResourceReference(TextBox.BorderBrushProperty, "Theme.LineStrong");
            _pageBox.KeyDown += OnPageBoxKeyDown;
            _pageBox.LostKeyboardFocus += (_, _) =>
            {
                if (_active?.Pdf is { } pdf) SyncPageBox(pdf);
            };

            _pageCount = new TextBlock
            {
                Text = "/ 0",
                FontSize = 12,
                Foreground = Theme.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };

            var pageLabel = UI.Text("Page", 12, color: Theme.Muted);
            pageLabel.VerticalAlignment = VerticalAlignment.Center;
            pageLabel.Margin = new Thickness(0, 0, 6, 0);

            _pageNav = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _pageNav.Children.Add(pageLabel);
            _pageNav.Children.Add(_pageBox);
            _pageNav.Children.Add(_pageCount);

            var openButton = UI.IconButton("\uE710", Pick, "Open document", 26);
            openButton.Margin = new Thickness(6, 0, 0, 0);
            var tabBar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(openButton, Dock.Right);
            tabBar.Children.Add(openButton);
            tabBar.Children.Add(_tabScroll);

            _emptyView = BuildEmpty();

            _dropHint = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Accent,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(6),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = L10n.T("Drop documents to open them in tabs"),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Theme.Accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }.Radius("Theme.RadiusLarge");

            var content = new Grid();
            content.Children.Add(_host);
            content.Children.Add(_dropHint);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(tabBar, 0);
            Grid.SetRow(content, 1);
            root.Children.Add(tabBar);
            root.Children.Add(content);
            Body = root;

            BuildLeading();
            _copyButton = UI.Button("Copy", Copy, primary: false);
            _exportButton = UI.Button("Export...", ExportActive, primary: false);
            _openAppButton = UI.Button("Open in app", OpenInDefaultApp, primary: false);
            _openAppButton.Visibility = Visibility.Collapsed;
            Trailing(
                _openAppButton,
                _copyButton,
                _exportButton,
                UI.Button("Close", CloseOnce, primary: true));

            DragOver += OnDragOver;
            DragLeave += (_, _) => _dropHint.Visibility = Visibility.Collapsed;
            Drop += OnDrop;
            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += (_, _) => Restore(files);
            Closed += (_, _) =>
            {
                _token++;
                SaveState();
                Theme.Changed -= OnThemeChanged;
            };
            Theme.Changed += OnThemeChanged;

            SetEmpty();
        }

        public void AddFiles(IEnumerable<string> files)
        {
            var added = new List<Tab>();
            foreach (var file in files)
            {
                if (!File.Exists(file) || !FileKinds.CanPreview(file)) continue;
                var full = Path.GetFullPath(file);
                var existing = _tabs.FirstOrDefault(tab => string.Equals(tab.Path, full, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    added.Add(existing);
                    continue;
                }

                var tab = new Tab(full);
                _tabs.Add(tab);
                added.Add(tab);
            }

            if (added.Count == 0) return;
            RebuildTabs();
            Activate(added[0]);
            SaveState();
        }

        private void Restore(IReadOnlyList<string> files)
        {
            var (saved, activePath) = PreviewTabsStore.Load();
            foreach (var state in saved)
            {
                var full = Path.GetFullPath(state.Path);
                if (_tabs.Any(tab => string.Equals(tab.Path, full, StringComparison.OrdinalIgnoreCase))) continue;
                _tabs.Add(new Tab(full)
                {
                    Scroll = Math.Clamp(state.Scroll, 0, 1),
                    Zoom = Math.Clamp(state.Zoom, 40, 250),
                });
            }

            if (files.Count > 0)
            {
                AddFiles(files);
                return;
            }

            RebuildTabs();
            if (_tabs.Count == 0)
            {
                SetEmpty();
                return;
            }

            Activate(_tabs.FirstOrDefault(tab => string.Equals(tab.Path, activePath, StringComparison.OrdinalIgnoreCase)) ?? _tabs[0]);
        }

        private FrameworkElement BuildEmpty()
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(UI.Icon(Catalog.Icons.Document, 34, Theme.Muted));
            stack.Children.Add(new TextBlock
            {
                Text = L10n.T("No documents open"),
                FontFamily = Theme.Display,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Ink,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4),
            });
            stack.Children.Add(new TextBlock
            {
                Text = L10n.T("Open a document or drop files here."),
                FontSize = 12,
                Foreground = Theme.Muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14),
            });
            var open = UI.Button("Open...", Pick, primary: true, icon: Catalog.Icons.Document);
            open.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(open);
            return stack;
        }

        private void SetEmpty()
        {
            _host.Content = _emptyView;
            UpdateChrome();
        }

        private void BuildLeading()
        {
            ClearLeading();
            Leading(
                UI.Button("Open...", Pick, primary: false, icon: Catalog.Icons.Document),
                UI.Button("-", () => ZoomBy(-10), primary: false),
                _zoomLabel,
                UI.Button("+", () => ZoomBy(10), primary: false));

            if (_active?.Pdf is { } pdf)
            {
                Leading(
                    UI.Button("Fit", () => ApplyPdfZoom(pdf.FitWidthZoom), primary: false),
                    UI.Button("Page", () => ApplyPdfZoom(pdf.FitPageZoom), primary: false),
                    _pageNav);
                SyncPageBox(pdf);
                return;
            }

            if (_active == null || _active.View is FlowDocumentScrollViewer)
                Leading(UI.Button(IsActiveRightToLeft ? "LTR" : "RTL", ToggleDirection, primary: false));
        }

        private bool IsActiveRightToLeft =>
            _active?.View is FlowDocumentScrollViewer { Document: { } document } && document.FlowDirection == FlowDirection.RightToLeft;

        private void ZoomBy(double delta)
        {
            if (_active == null) return;

            if (_active.Pdf is { } pdf)
            {
                ApplyPdfZoom(pdf.Zoom + delta);
                return;
            }

            if (_active.View is FlowDocumentScrollViewer viewer)
            {
                viewer.Zoom = Math.Clamp(viewer.Zoom + delta, viewer.MinZoom, viewer.MaxZoom);
                _active.Zoom = viewer.Zoom;
            }
            else
            {
                return;
            }

            _zoomLabel.Text = $"{_active.Zoom:0}%";
            SaveState();
        }

        private void ApplyPdfZoom(double zoom)
        {
            if (_active?.Pdf is not { } pdf) return;

            zoom = Math.Clamp(zoom, 40, 250);
            pdf.SetZoom(zoom);
            _active.Zoom = zoom;
            _zoomLabel.Text = $"{zoom:0}%";
            SyncPageBox(pdf);
            SaveState();
        }

        private void SyncPageBox(PdfView pdf)
        {
            _pageCount.Text = $"/ {pdf.PageCount}";
            if (_pageOwner != pdf || !_pageBox.IsKeyboardFocusWithin)
            {
                _pageBox.Text = pdf.CurrentPage.ToString(CultureInfo.InvariantCulture);
                _pageOwner = pdf;
            }
        }

        private void OnPageBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (_active?.Pdf is not { } pdf) return;

            if (e.Key == Key.Enter)
            {
                if (int.TryParse(_pageBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page))
                    pdf.GoToPage(page);
                SyncPageBox(pdf);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SyncPageBox(pdf);
                e.Handled = true;
            }
        }

        private void OpenInDefaultApp()
        {
            if (_active == null) return;

            try
            {
                Process.Start(new ProcessStartInfo(_active.Path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Write($"Document viewer open-in-app error: {ex}");
                Hud.Shared.ShowMessage(L10n.T("Could not open the file."), Hud.Style.Failure);
            }
        }

        private void ToggleDirection()
        {
            if (_active?.View is FlowDocumentScrollViewer { Document: { } document })
                document.FlowDirection = document.FlowDirection == FlowDirection.RightToLeft
                    ? FlowDirection.LeftToRight
                    : FlowDirection.RightToLeft;
            BuildLeading();
        }

        private void Activate(Tab tab)
        {
            if (!_tabs.Contains(tab)) return;

            if (_active == tab)
            {
                if (tab.View != null) ShowView(tab);
                return;
            }

            CaptureActive();
            _active = tab;
            _recent.Remove(tab);
            _recent.Insert(0, tab);
            RebuildTabs();
            UpdateChrome();
            SaveState();

            if (tab.View != null)
            {
                ShowView(tab);
                return;
            }

            ShowLoading();
            var token = ++_token;
            tab.BuildToken = token;
            _ = BuildAsync(tab, token);
        }

        private void ShowView(Tab tab)
        {
            _host.Content = tab.View;
            RestoreScroll(tab);
            UpdateChrome();
            TrimViews();

            if (tab.Pdf is { } pdf)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_active == tab && !WindowLifecycle.IsClosingOrClosed(this)) pdf.Scroller.Focus();
                }), DispatcherPriority.Input);
        }

        private void ShowLoading()
        {
            _host.Content = new TextBlock
            {
                Text = L10n.T("Opening..."),
                FontSize = 12.5,
                Foreground = Theme.Muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private async Task BuildAsync(Tab tab, int token)
        {
            FrameworkElement view;
            string text;
            try
            {
                (view, text) = await RenderAsync(tab.Path, tab.Zoom);
            }
            catch (Exception ex)
            {
                Log.Write($"Document viewer error ({tab.Path}): {ex}");
                if (token != _token || token != tab.BuildToken || WindowLifecycle.IsClosingOrClosed(this)) return;
                view = MakeFlowView(ErrorFlow(ex.Message), tab.Zoom);
                text = "";
            }

            if (token != _token || token != tab.BuildToken || WindowLifecycle.IsClosingOrClosed(this))
            {
                (view as PdfView)?.Dispose();
                return;
            }

            tab.Text = text;
            tab.View = view;

            if (view is PdfView pdf)
            {
                tab.Pdf = pdf;
                tab.FindScroller = () => pdf.Scroller;
                pdf.PageChanged += _ =>
                {
                    if (_active == tab) SyncPageBox(pdf);
                };
                pdf.Scroller.ScrollChanged += (_, _) =>
                {
                    if (_active != tab) return;
                    if (pdf.Scroller.ScrollableHeight > 0)
                        tab.Scroll = pdf.Scroller.VerticalOffset / pdf.Scroller.ScrollableHeight;
                };
            }
            else if (view is FlowDocumentScrollViewer flow)
            {
                tab.FindScroller = () => Scroller(flow);
                flow.Loaded += (_, _) =>
                {
                    if (tab.View != flow) return;
                    if (Scroller(flow) is not { } scroller) return;
                    scroller.ScrollChanged += (_, _) =>
                    {
                        if (_active != tab) return;
                        if (scroller.ScrollableHeight > 0)
                            tab.Scroll = scroller.VerticalOffset / scroller.ScrollableHeight;
                    };
                    scroller.ScrollToVerticalOffset(tab.Scroll * Math.Max(0, scroller.ScrollableHeight));
                };
            }

            if (_active == tab)
            {
                ShowView(tab);
            }
            else
            {
                TrimViews();
            }
        }

        private static FlowDocumentScrollViewer MakeFlowView(FlowDocument flow, double zoom) => new()
        {
            Document = flow,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsToolBarVisible = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinZoom = 60,
            MaxZoom = 250,
            ZoomIncrement = 10,
            Zoom = Math.Clamp(zoom, 60, 250),
        };

        private async Task<(FrameworkElement View, string Text)> RenderAsync(string path, double zoom)
        {
            var extension = FileKinds.Ext(path);
            var folder = Path.GetDirectoryName(path);

            if (extension == "pdf")
            {
                try
                {
                    var sizes = await Task.Run(() => PdfOps.PageSizes(path));
                    return (new PdfView(path, sizes, zoom), "");
                }
                catch (Exception ex) when (IsProtected(ex))
                {
                    return (MakeFlowView(ProtectedFlow(), zoom), "");
                }
            }

            if (extension is "md" or "markdown" or "txt")
            {
                var text = Adawat.Core.Documents.DocumentOps.ReadText(path);
                var flow = MarkdownFlow.LooksLikeMarkdown(text)
                    ? MarkdownFlow.FromMarkdown(text, folder, OpenTarget)
                    : MarkdownFlow.FromPlain(text, folder);
                return (MakeFlowView(flow, zoom), text);
            }

            if (extension is "tex" or "latex")
            {
                var source = Adawat.Core.Documents.DocumentOps.ReadText(path);
                var flow = TexFlow.FromLatex(source, folder, OpenTarget);
                var plain = new TextRange(flow.ContentStart, flow.ContentEnd).Text;
                return (MakeFlowView(flow, zoom), plain);
            }

            var document = await Adawat.Core.Documents.DocumentOps.Load(path);
            return (MakeFlowView(DocFlow.FromDoc(document, folder, OpenTarget), zoom), document.PlainText);
        }

        private static FlowDocument ErrorFlow(string message)
        {
            var flow = FlowKit.NewDocument(false);
            flow.Blocks.Add(new Paragraph(new Run(L10n.T("This file could not be opened.")))
            {
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
            });
            flow.Blocks.Add(new Paragraph(new Run(message)) { Foreground = Theme.Muted, LineHeight = 20 });
            return flow;
        }

        private static bool IsProtected(Exception ex) =>
            ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase);

        /// <summary>Shown instead of pages when the PDF needs a password the viewer cannot ask for.</summary>
        private static FlowDocument ProtectedFlow()
        {
            var flow = FlowKit.NewDocument(false);
            flow.Blocks.Add(new Paragraph(new Run(L10n.T("This PDF is password-protected.")))
            {
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
            });
            flow.Blocks.Add(new Paragraph(new Run(L10n.T("Use \"Open in app\" to enter the password.")))
            {
                Foreground = Theme.Muted,
                LineHeight = 20,
            });
            return flow;
        }

        private void RestoreScroll(Tab tab)
        {
            if (tab.Pdf is { } pdf)
            {
                pdf.ScrollToRatio(tab.Scroll);
                return;
            }

            if (tab.View is not FrameworkElement element) return;

            void Apply()
            {
                if (tab.FindScroller?.Invoke() is not { } scroller) return;
                scroller.ScrollToVerticalOffset(tab.Scroll * Math.Max(0, scroller.ScrollableHeight));
            }

            element.ApplyTemplate();
            Apply();
            element.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Loaded);
        }

        private void CaptureActive() => CaptureScroll(_active);

        private static void CaptureScroll(Tab? tab)
        {
            if (tab?.FindScroller?.Invoke() is not { } scroller) return;
            if (scroller.ScrollableHeight > 0) tab.Scroll = scroller.VerticalOffset / scroller.ScrollableHeight;
        }

        private static ScrollViewer? Scroller(FlowDocumentScrollViewer viewer)
        {
            viewer.ApplyTemplate();
            return viewer.Template?.FindName("PART_ContentHost", viewer) as ScrollViewer;
        }

        private void TrimViews()
        {
            var keep = new HashSet<Tab>(_recent.Take(KeepViews));
            if (_active != null) keep.Add(_active);
            foreach (var tab in _tabs)
            {
                if (tab.View == null) continue;

                // PDF pages are the largest data the viewer holds; only the visible PDF keeps its cache.
                if (tab.Pdf != null && tab != _active)
                {
                    CaptureScroll(tab);
                    tab.Pdf.Dispose();
                    tab.Pdf = null;
                    tab.View = null;
                    tab.FindScroller = null;
                    continue;
                }

                if (keep.Contains(tab)) continue;
                CaptureScroll(tab);
                tab.View = null;
                tab.FindScroller = null;
            }
        }

        private void RebuildTabs()
        {
            _tabStrip.Children.Clear();
            foreach (var tab in _tabs) _tabStrip.Children.Add(TabChip(tab));
        }

        private FrameworkElement TabChip(Tab tab)
        {
            var active = tab == _active;
            var label = new TextBlock
            {
                Text = Path.GetFileName(tab.Path),
                FontSize = 12,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = active ? Theme.Ink : Theme.Muted,
                MaxWidth = 170,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var close = UI.IconButton("\uE711", () => CloseTab(tab), "Close tab", 18);
            close.Margin = new Thickness(4, 0, 0, 0);

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(label);
            content.Children.Add(close);

            var chip = new Border
            {
                Child = content,
                Padding = new Thickness(10, 3, 3, 3),
                Margin = new Thickness(0, 0, 4, 0),
                Background = active ? Theme.Card : Brushes.Transparent,
                BorderBrush = active ? Theme.LineStrong : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = tab.Path,
            }.Radius("Theme.RadiusSmall");

            chip.MouseLeftButtonUp += (_, e) =>
            {
                Activate(tab);
                e.Handled = true;
            };
            chip.MouseDown += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                CloseTab(tab);
                e.Handled = true;
            };

            return chip;
        }

        private void CloseTab(Tab tab)
        {
            var index = _tabs.IndexOf(tab);
            if (index < 0) return;

            CaptureScroll(tab);
            _tabs.RemoveAt(index);
            _recent.Remove(tab);
            tab.BuildToken = 0;
            tab.Pdf?.Dispose();
            tab.Pdf = null;
            tab.View = null;
            tab.FindScroller = null;

            if (_active == tab)
            {
                _active = null;
                _host.Content = null;
                if (_tabs.Count > 0) Activate(_tabs[Math.Clamp(index, 0, _tabs.Count - 1)]);
                else SetEmpty();
            }

            RebuildTabs();
            SaveState();
            UpdateChrome();
        }

        private void Cycle(int step)
        {
            if (_tabs.Count < 2) return;
            var index = _active == null ? -1 : _tabs.IndexOf(_active);
            var next = (index + step + _tabs.Count) % _tabs.Count;
            Activate(_tabs[next]);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                // A PDF page has no focusable content, so the viewer moves its scroller itself.
                if (_active?.Pdf is { } pdf && !_pageBox.IsKeyboardFocusWithin)
                {
                    switch (e.Key)
                    {
                        case Key.PageDown:
                            pdf.Scroller.PageDown();
                            e.Handled = true;
                            break;
                        case Key.PageUp:
                            pdf.Scroller.PageUp();
                            e.Handled = true;
                            break;
                        case Key.Home:
                            pdf.GoToPage(1);
                            e.Handled = true;
                            break;
                        case Key.End:
                            pdf.GoToPage(pdf.PageCount);
                            e.Handled = true;
                            break;
                    }
                }

                return;
            }

            switch (e.Key)
            {
                case Key.W:
                    if (_active != null) CloseTab(_active);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    Cycle((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1);
                    e.Handled = true;
                    break;
                case Key.O:
                    Pick();
                    e.Handled = true;
                    break;
                case Key.G:
                    if (_active?.Pdf != null)
                    {
                        _pageBox.Focus();
                        _pageBox.SelectAll();
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void Pick()
        {
            var dialog = new OpenFileDialog
            {
                Title = L10n.T("Open Document"),
                Filter = "Documents|*.md;*.markdown;*.txt;*.html;*.htm;*.rtf;*.docx;*.doc;*.odt;*.epub;*.tex;*.latex;*.pdf|" +
                         "Markdown (*.md)|*.md;*.markdown|Text (*.txt)|*.txt|PDF (*.pdf)|*.pdf|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = true,
            };

            if (dialog.ShowDialog(this) == true) AddFiles(dialog.FileNames);
        }

        private void Copy()
        {
            if (_active == null || _active.Pdf != null) return;
            Adawat.Core.Clipboard.CopyText(_active.Text);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
        }

        private void ExportActive()
        {
            if (_active == null || _active.Pdf != null) return;
            MarkdownViewer.Export(Path.GetFileNameWithoutExtension(_active.Path), _active.Text, this);
        }

        private void OpenTarget(string url)
        {
            try
            {
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    return;
                }

                if (_active == null) return;
                var folder = Path.GetDirectoryName(_active.Path);
                if (folder == null) return;

                var clean = Uri.UnescapeDataString(url.Split('#')[0]);
                if (clean.Length == 0) return;

                var target = Path.GetFullPath(Path.Combine(folder, clean));
                if (!File.Exists(target)) return;

                if (FileKinds.CanPreview(target)) AddFiles([target]);
                else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Write($"Document viewer link error: {ex}");
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            var supported = TryFiles(e.Data, out _);
            e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
            _dropHint.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            _dropHint.Visibility = Visibility.Collapsed;
            if (!TryFiles(e.Data, out var files)) return;
            e.Handled = true;
            AddFiles(files);
        }

        private static bool TryFiles(IDataObject data, out string[] files)
        {
            files = data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] dropped
                ? dropped.Where(File.Exists).Where(FileKinds.CanPreview).ToArray()
                : Array.Empty<string>();
            return files.Length > 0;
        }

        private void SaveState()
        {
            CaptureActive();
            PreviewTabsStore.Save(
                _tabs.Select(tab => new PreviewTabState { Path = tab.Path, Scroll = tab.Scroll, Zoom = tab.Zoom }),
                _active?.Path);
        }

        private void UpdateChrome()
        {
            var name = _active == null ? "Documents" : Path.GetFileName(_active.Path);
            Title = _active == null ? L10n.T("Documents") : $"{name} - Adawat";
            Header = name;
            _zoomLabel.Text = $"{(_active?.Zoom ?? 100):0}%";
            var isPdf = _active != null && FileKinds.Ext(_active.Path) == "pdf";
            var textTab = _active != null && !isPdf;
            _copyButton.IsEnabled = textTab;
            _exportButton.IsEnabled = textTab;
            _copyButton.Visibility = isPdf ? Visibility.Collapsed : Visibility.Visible;
            _exportButton.Visibility = isPdf ? Visibility.Collapsed : Visibility.Visible;
            _openAppButton.Visibility = isPdf ? Visibility.Visible : Visibility.Collapsed;
            if (_active?.Pdf is { } pdf) SyncPageBox(pdf);
            BuildLeading();
        }

        private void OnThemeChanged()
        {
            if (WindowLifecycle.IsClosingOrClosed(this)) return;

            foreach (var tab in _tabs)
            {
                if (tab.View is FlowDocumentScrollViewer { Document: { } document })
                    document.FontFamily = Theme.Font;
            }

            if (_active == null)
            {
                _emptyView = BuildEmpty();
                _host.Content = _emptyView;
            }

            RebuildTabs();
            UpdateChrome();
        }
    }
}
