using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Adawat.Core;

namespace Adawat.App;

/// <summary>
/// Full-screen transparent overlay that shows detected text regions from OCR.
/// Each region is a clickable highlighted rectangle — clicking copies its text.
/// The user can also drag to manually select a region for OCR on images.
/// Escape or right-click dismisses.
/// </summary>
public sealed class TextOverlayWindow : Window
{
    private readonly BitmapSource _snapshot;
    private readonly Canvas _canvas;
    private readonly List<ScreenTextExtractor.TextRegion> _regions = [];
    private readonly List<Border> _regionBoxes = [];
    private readonly Border _manualSelection;
    private Point? _dragStart;
    private Rect _dragRect;
    private Border? _hoveredBox;

    // Pixel-to-DIP scaling
    private double _scaleX = 1, _scaleY = 1;

    // Toolbar elements tracked for updates
    private readonly TextBlock _spinnerIcon;
    private readonly TextBlock _statusLabel;
    private readonly Button _copyAllBtn;
    private string _selectedLanguage;

    public TextOverlayWindow(BitmapSource snapshot)
    {
        _snapshot = snapshot;
        _selectedLanguage = Settings.Current.OcrLanguage ?? "auto";

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };

        // Background: screenshot, slightly dimmed
        var bg = new Image
        {
            Source = snapshot,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
            Stretch = Stretch.Fill,
            Opacity = 0.85,
            IsHitTestVisible = false,
        };
        _canvas.Children.Add(bg);

        // Dark overlay tint
        var dimRect = new System.Windows.Shapes.Rectangle
        {
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
            Fill = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)),
            IsHitTestVisible = false,
        };
        _canvas.Children.Add(dimRect);

        // Manual selection rectangle (hidden until drag)
        _manualSelection = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xA5, 0x00)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            CornerRadius = new CornerRadius(3),
        };
        _canvas.Children.Add(_manualSelection);

        // Build toolbar pill
        _spinnerIcon = new TextBlock
        {
            Text = "\uE117", FontFamily = Theme.Icons, FontSize = 13,
            Foreground = Theme.Accent, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 6, 6, 6),
        };
        _statusLabel = UI.Text("Scanning for text…", 11.5, FontWeights.Medium, Theme.Ink);
        _statusLabel.Margin = new Thickness(4, 6, 10, 6);
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;

        _copyAllBtn = UI.Button("Copy All", CopyAll, primary: true, icon: "\uE8C8");
        _copyAllBtn.Margin = new Thickness(4, 2, 4, 2);
        _copyAllBtn.IsEnabled = false;

        var closeBtn = UI.Button("Close", Close);
        closeBtn.Margin = new Thickness(4, 2, 4, 2);

        // Language Picker
        var installed = ScreenTextExtractor.GetInstalledLanguages();
        var langOptions = new List<(string Value, string Title)>
        {
            ("auto", L10n.T("Auto (All)"))
        };
        foreach (var l in installed)
        {
            langOptions.Add((l.Tag, l.DisplayName));
        }

        var langPicker = UI.Picker(langOptions, _selectedLanguage, selected =>
        {
            if (_selectedLanguage == selected) return;
            _selectedLanguage = selected;
            Settings.Current.OcrLanguage = selected;
            Settings.Current.Save();
            _ = RunOcr();
        });
        langPicker.Margin = new Thickness(6, 2, 6, 2);
        langPicker.MinWidth = 120;
        langPicker.Cursor = Cursors.Hand;

        var toolbarPanel = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Arrow };
        toolbarPanel.Children.Add(_spinnerIcon);
        toolbarPanel.Children.Add(_statusLabel);
        toolbarPanel.Children.Add(langPicker);
        toolbarPanel.Children.Add(_copyAllBtn);
        toolbarPanel.Children.Add(closeBtn);

        var toolbar = new Border
        {
            Child = toolbarPanel,
            Background = new SolidColorBrush(Color.FromArgb(0xF4, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Effect = new DropShadowEffect
            {
                BlurRadius = 16, ShadowDepth = 4, Direction = 270, Opacity = 0.22,
                Color = Color.FromRgb(0x20, 0x20, 0x24),
            },
            Tag = "toolbar",
        };
        _canvas.Children.Add(toolbar);

        Content = _canvas;

        Loaded += async (_, _) =>
        {
            _scaleX = ActualWidth / _snapshot.PixelWidth;
            _scaleY = ActualHeight / _snapshot.PixelHeight;

            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(toolbar, (ActualWidth - toolbar.DesiredSize.Width) / 2);
            Canvas.SetTop(toolbar, 18);

            await RunOcr();
        };

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        MouseRightButtonUp += (_, _) => Close();
    }

    // ── OCR ──

    private async Task RunOcr()
    {
        Dispatcher.Invoke(() =>
        {
            _spinnerIcon.Visibility = Visibility.Visible;
            _statusLabel.Text = L10n.T("Scanning for text…");
            _copyAllBtn.IsEnabled = false;
            RepositionToolbar();

            foreach (var box in _regionBoxes)
            {
                _canvas.Children.Remove(box);
            }
            _regionBoxes.Clear();
            _regions.Clear();
        });

        try
        {
            var results = await ScreenTextExtractor.ExtractAsync(_snapshot, _selectedLanguage);
            _regions.AddRange(results);

            Dispatcher.Invoke(() =>
            {
                foreach (var region in _regions) AddRegionBox(region);
                UpdateToolbar();

                // Warn if Arabic OCR isn't installed and user wants Arabic/Auto
                if (!ScreenTextExtractor.IsArabicAvailable && (_selectedLanguage == "auto" || _selectedLanguage.StartsWith("ar")))
                {
                    _statusLabel.Text += "  ⚠ " + L10n.T("Arabic OCR not installed");
                    _statusLabel.ToolTip = L10n.T("To enable Arabic text detection, go to Windows Settings → Time & Language → Language & Region → Add Arabic, then install the OCR feature.");
                    RepositionToolbar();
                }
            });
        }
        catch (Exception ex)
        {
            Log.Write($"Screen text OCR error: {ex}");
            Dispatcher.Invoke(() =>
            {
                _spinnerIcon.Visibility = Visibility.Collapsed;
                _statusLabel.Text = L10n.T("OCR failed. Drag to select text manually.");
            });
        }
    }

    // ── Region boxes ──

    private void AddRegionBox(ScreenTextExtractor.TextRegion region)
    {
        var dipRect = new Rect(
            region.Bounds.X * _scaleX,
            region.Bounds.Y * _scaleY,
            region.Bounds.Width * _scaleX,
            region.Bounds.Height * _scaleY);

        var accentFill = new SolidColorBrush(Color.FromArgb(0x30, 0x5B, 0x47, 0xE6));
        var hoverFill = new SolidColorBrush(Color.FromArgb(0x55, 0x5B, 0x47, 0xE6));
        var borderBrush = new SolidColorBrush(Color.FromArgb(0x90, 0x5B, 0x47, 0xE6));

        var box = new Border
        {
            Width = Math.Max(dipRect.Width, 10),
            Height = Math.Max(dipRect.Height, 6),
            Background = accentFill,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(3),
            Cursor = Cursors.Hand,
            ToolTip = new ToolTip
            {
                Content = new TextBlock
                {
                    Text = region.Text.Length > 120 ? region.Text[..120] + "…" : region.Text,
                    FontFamily = Theme.Font, FontSize = 12,
                    TextWrapping = TextWrapping.Wrap, MaxWidth = 360,
                    FlowDirection = IsArabic(region.Text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                },
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
            },
            Tag = region.Text,
        };

        box.MouseEnter += (_, _) =>
        {
            box.Background = hoverFill;
            box.BorderBrush = Theme.Accent;
            box.BorderThickness = new Thickness(2);
            box.Effect = new DropShadowEffect { Color = Theme.AccentColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.4 };
            _hoveredBox = box;
        };
        box.MouseLeave += (_, _) =>
        {
            box.Background = accentFill;
            box.BorderBrush = borderBrush;
            box.BorderThickness = new Thickness(1.5);
            box.Effect = null;
            if (_hoveredBox == box) _hoveredBox = null;
        };
        box.MouseLeftButtonUp += (_, e) =>
        {
            Adawat.Core.Clipboard.CopyText((string)box.Tag);
            Sounds.Tick();
            ShowCopiedFeedback(box);
            e.Handled = true;
        };

        Canvas.SetLeft(box, dipRect.X);
        Canvas.SetTop(box, dipRect.Y);
        _canvas.Children.Add(box);
        _regionBoxes.Add(box);
    }

    private void ShowCopiedFeedback(Border box)
    {
        var feedbackPanel = new StackPanel { Orientation = Orientation.Horizontal };
        feedbackPanel.Children.Add(UI.Icon("\uE73E", 12, Brushes.White));
        feedbackPanel.Children.Add(new Border { Width = 4 });
        feedbackPanel.Children.Add(UI.Text("Copied!", 11, FontWeights.SemiBold, Brushes.White));

        var feedback = new Border
        {
            Child = feedbackPanel,
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x34, 0xA8, 0x53)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.2 },
        };

        Canvas.SetLeft(feedback, Canvas.GetLeft(box));
        Canvas.SetTop(feedback, Canvas.GetTop(box) - 32);
        _canvas.Children.Add(feedback);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) => _canvas.Children.Remove(feedback);
            feedback.BeginAnimation(OpacityProperty, fade);
        };
        timer.Start();
    }

    // ── Toolbar ──

    private void UpdateToolbar()
    {
        _spinnerIcon.Visibility = Visibility.Collapsed;
        _statusLabel.Text = _regions.Count > 0
            ? string.Format(L10n.T("Found {0} text regions — click to copy"), _regions.Count)
            : L10n.T("No text found. Drag to select a region manually.");
        _copyAllBtn.IsEnabled = _regions.Count > 0;

        RepositionToolbar();
    }

    private void RepositionToolbar()
    {
        var toolbar = _canvas.Children.OfType<Border>().FirstOrDefault(b => b.Tag as string == "toolbar");
        if (toolbar == null) return;
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(toolbar, (ActualWidth - toolbar.DesiredSize.Width) / 2);
    }

    private void CopyAll()
    {
        if (_regions.Count == 0) return;
        Adawat.Core.Clipboard.CopyText(string.Join(Environment.NewLine, _regions.Select(r => r.Text)));
        Sounds.Tick();
        Hud.Shared.ShowMessage(L10n.T("All text copied to clipboard"), Hud.Style.Success);
    }

    // ── Manual Selection (drag-to-OCR) ──

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (_hoveredBox != null) return; // let region box handle it

        _dragStart = e.GetPosition(_canvas);
        _manualSelection.Visibility = Visibility.Visible;
        Canvas.SetLeft(_manualSelection, _dragStart.Value.X);
        Canvas.SetTop(_manualSelection, _dragStart.Value.Y);
        _manualSelection.Width = 0;
        _manualSelection.Height = 0;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart == null) return;
        var cur = e.GetPosition(_canvas);
        var x = Math.Min(_dragStart.Value.X, cur.X);
        var y = Math.Min(_dragStart.Value.Y, cur.Y);
        var w = Math.Abs(cur.X - _dragStart.Value.X);
        var h = Math.Abs(cur.Y - _dragStart.Value.Y);

        _dragRect = new Rect(x, y, w, h);
        Canvas.SetLeft(_manualSelection, x);
        Canvas.SetTop(_manualSelection, y);
        _manualSelection.Width = w;
        _manualSelection.Height = h;
    }

    private async void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart == null) return;
        ReleaseMouseCapture();
        _dragStart = null;

        if (_dragRect.Width > 10 && _dragRect.Height > 10)
        {
            // Convert DIPs back to pixel coords in the snapshot
            var px = Math.Clamp((int)(_dragRect.X / _scaleX), 0, _snapshot.PixelWidth - 1);
            var py = Math.Clamp((int)(_dragRect.Y / _scaleY), 0, _snapshot.PixelHeight - 1);
            var pw = Math.Clamp((int)(_dragRect.Width / _scaleX), 1, _snapshot.PixelWidth - px);
            var ph = Math.Clamp((int)(_dragRect.Height / _scaleY), 1, _snapshot.PixelHeight - py);

            try
            {
                _spinnerIcon.Visibility = Visibility.Visible;
                _statusLabel.Text = L10n.T("Scanning selected region…");
                RepositionToolbar();

                var text = await ScreenTextExtractor.ExtractFromRegionAsync(_snapshot, new Int32Rect(px, py, pw, ph), _selectedLanguage);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    Adawat.Core.Clipboard.CopyText(text);
                    Sounds.Tick();
                    Hud.Shared.ShowMessage(L10n.T("Text copied to clipboard"), Hud.Style.Success);

                    var region = new ScreenTextExtractor.TextRegion(new Rect(px, py, pw, ph), text);
                    _regions.Add(region);
                    AddRegionBox(region);
                }
                else
                {
                    Hud.Shared.ShowMessage(L10n.T("No text found in selected region"), Hud.Style.Failure);
                }

                UpdateToolbar();
            }
            catch (Exception ex)
            {
                Log.Write($"Manual OCR error: {ex}");
                Hud.Shared.ShowMessage(L10n.T("Failed to extract text"), Hud.Style.Failure);
                UpdateToolbar();
            }
        }

        _manualSelection.Visibility = Visibility.Collapsed;
        _dragRect = Rect.Empty;
    }

    // ── Entry point ──

    /// <summary>Captures the screen, opens the overlay, runs OCR. Called from the Quick Wheel.</summary>
    public static void Launch()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Close the wheel first so it doesn't appear in the capture
            Wheel.QuickWheel.Close();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var snapshot = ScreenCapture.CaptureVirtualScreen();
                if (snapshot == null)
                {
                    Hud.Shared.ShowMessage(L10n.T("Could not capture screen"), Hud.Style.Failure);
                    return;
                }
                var overlay = new TextOverlayWindow(snapshot);
                overlay.Show();
                overlay.Activate();
            };
            timer.Start();
        });
    }

    private static bool IsArabic(string text) => text.Any(c => c >= 0x0600 && c <= 0x06FF);
}

