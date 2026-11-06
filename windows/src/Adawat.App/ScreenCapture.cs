using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Adawat.Core;

namespace Adawat.App;

public static class ScreenCapture
{
    /// <summary>Captures the entire virtual desktop across all monitors into a BitmapSource.</summary>
    public static BitmapSource? CaptureVirtualScreen()
    {
        try
        {
            var left = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
            var top = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
            var width = Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN);
            var height = Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                width = (int)SystemParameters.PrimaryScreenWidth;
                height = (int)SystemParameters.PrimaryScreenHeight;
                left = 0;
                top = 0;
            }

            var hdcSrc = Native.GetDC(IntPtr.Zero);
            var hdcDest = Native.CreateCompatibleDC(hdcSrc);
            var hBitmap = Native.CreateCompatibleBitmap(hdcSrc, width, height);
            var hOld = Native.SelectObject(hdcDest, hBitmap);

            Native.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, left, top, Native.SRCCOPY | Native.CAPTUREBLT);
            Native.SelectObject(hdcDest, hOld);

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            Native.DeleteObject(hBitmap);
            Native.DeleteDC(hdcDest);
            Native.ReleaseDC(IntPtr.Zero, hdcSrc);

            return source;
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to capture screen: {ex.Message}");
            return null;
        }
    }

    /// <summary>Saves the bitmap as PNG to Downloads folder, copies it to clipboard, and alerts the user.</summary>
    public static string? SaveScreenshot(BitmapSource bitmap)
    {
        try
        {
            var folder = Output.Downloads;
            Directory.CreateDirectory(folder);
            var target = Output.Unique(System.IO.Path.Combine(folder, $"Screenshot {DateTime.Now:yyyy-MM-dd HHmmss}.png"));

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(target))
            {
                encoder.Save(stream);
            }

            try
            {
                System.Windows.Clipboard.SetImage(bitmap);
            }
            catch { }

            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Screenshot saved to Downloads"), Hud.Style.Success);

            if (Settings.Current.ShowResultsInFloat)
            {
                Floats.ShowResults([target]);
            }

            return target;
        }
        catch (Exception ex)
        {
            Log.Write($"Save screenshot error: {ex}");
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            return null;
        }
    }

    /// <summary>
    /// Launches the modern interactive snipping overlay.
    /// If onCaptured is provided, it receives the captured bitmap (e.g. for QR scanning).
    /// Otherwise, it saves the screenshot to the Downloads folder.
    /// </summary>
    public static void StartSnipping(Action<BitmapSource>? onCaptured = null, string? prompt = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Close any open wheel first
            Wheel.QuickWheel.Close();

            // Give a tiny moment for wheels/popups to fade before grabbing the screen
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var snapshot = CaptureVirtualScreen();
                if (snapshot == null)
                {
                    Hud.Shared.ShowMessage(L10n.T("Could not capture screen"), Hud.Style.Failure);
                    return;
                }

                var overlay = new SnippingOverlayWindow(snapshot, onCaptured ?? (img => SaveScreenshot(img)), prompt);
                overlay.Show();
            };
            timer.Start();
        });
    }
}

/// <summary>
/// Full-screen transparent overlay for selecting a region of the screen or capturing full screen.
/// </summary>
public sealed class SnippingOverlayWindow : Window
{
    private readonly BitmapSource _snapshot;
    private readonly Action<BitmapSource> _onCaptured;
    private readonly string? _prompt;
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly System.Windows.Shapes.Path _dimMask = new();
    private readonly Border _selectionBorder = new();
    private Point? _startPoint;
    private Rect _selectedRect;

    public SnippingOverlayWindow(BitmapSource snapshot, Action<BitmapSource> onCaptured, string? prompt = null)
    {
        _snapshot = snapshot;
        _onCaptured = onCaptured;
        _prompt = prompt;

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

        // Dim overlay background with exclusion for selection
        _dimMask.Fill = new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0));
        _canvas.Children.Add(_dimMask);

        // Selection highlight rectangle
        _selectionBorder.BorderBrush = Theme.Accent;
        _selectionBorder.BorderThickness = new Thickness(1.5);
        _selectionBorder.Background = new SolidColorBrush(Color.FromArgb(0x10, 0x5B, 0x47, 0xE6));
        _selectionBorder.Visibility = Visibility.Collapsed;
        _canvas.Children.Add(_selectionBorder);

        // Top Control Bar (Pill)
        var toolbar = BuildToolbar();
        _canvas.Children.Add(toolbar);

        Content = _canvas;

        Loaded += (_, _) => UpdateMask();
        SizeChanged += (_, _) =>
        {
            Canvas.SetLeft(toolbar, (ActualWidth - toolbar.DesiredSize.Width) / 2);
            Canvas.SetTop(toolbar, 24);
            UpdateMask();
        };

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key is Key.Enter or Key.Space)
            {
                CaptureFull();
            }
        };
    }

    private FrameworkElement BuildToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor = Cursors.Arrow,
            Margin = new Thickness(6, 4, 6, 4)
        };

        var label = UI.Text(_prompt ?? L10n.T("Select region or click Full Screen"), 11.5, FontWeights.Medium, Theme.Ink);
        label.Margin = new Thickness(10, 6, 12, 6);
        label.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(label);

        var fullBtn = UI.Button(L10n.T("Full Screen"), CaptureFull);
        fullBtn.Margin = new Thickness(4, 2, 4, 2);
        panel.Children.Add(fullBtn);

        var cancelBtn = UI.Button(L10n.T("Cancel"), Close);
        cancelBtn.Margin = new Thickness(4, 2, 4, 2);
        panel.Children.Add(cancelBtn);

        var pill = new Border
        {
            Child = panel,
            Background = new SolidColorBrush(Color.FromArgb(0xF4, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = 0.22,
                Color = Color.FromRgb(0x20, 0x20, 0x24)
            }
        };

        pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return pill;
    }

    private void CaptureFull()
    {
        Close();
        _onCaptured(_snapshot);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _startPoint = e.GetPosition(_canvas);
            _selectionBorder.Visibility = Visibility.Visible;
            Canvas.SetLeft(_selectionBorder, _startPoint.Value.X);
            Canvas.SetTop(_selectionBorder, _startPoint.Value.Y);
            _selectionBorder.Width = 0;
            _selectionBorder.Height = 0;
            CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_startPoint == null) return;

        var current = e.GetPosition(_canvas);
        var x = Math.Min(_startPoint.Value.X, current.X);
        var y = Math.Min(_startPoint.Value.Y, current.Y);
        var w = Math.Abs(current.X - _startPoint.Value.X);
        var h = Math.Abs(current.Y - _startPoint.Value.Y);

        _selectedRect = new Rect(x, y, w, h);

        Canvas.SetLeft(_selectionBorder, x);
        Canvas.SetTop(_selectionBorder, y);
        _selectionBorder.Width = w;
        _selectionBorder.Height = h;

        UpdateMask();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPoint == null) return;
        ReleaseMouseCapture();
        _startPoint = null;

        if (_selectedRect.Width > 5 && _selectedRect.Height > 5)
        {
            Close();

            // Convert canvas DIPs to snapshot pixels
            var scaleX = (double)_snapshot.PixelWidth / ActualWidth;
            var scaleY = (double)_snapshot.PixelHeight / ActualHeight;

            var px = Math.Clamp((int)(_selectedRect.X * scaleX), 0, _snapshot.PixelWidth - 1);
            var py = Math.Clamp((int)(_selectedRect.Y * scaleY), 0, _snapshot.PixelHeight - 1);
            var pw = Math.Clamp((int)(_selectedRect.Width * scaleX), 1, _snapshot.PixelWidth - px);
            var ph = Math.Clamp((int)(_selectedRect.Height * scaleY), 1, _snapshot.PixelHeight - py);

            try
            {
                var cropped = new CroppedBitmap(_snapshot, new Int32Rect(px, py, pw, ph));
                cropped.Freeze();
                _onCaptured(cropped);
            }
            catch (Exception ex)
            {
                Log.Write($"Crop error: {ex}");
                _onCaptured(_snapshot);
            }
        }
        else
        {
            _selectionBorder.Visibility = Visibility.Collapsed;
            _selectedRect = Rect.Empty;
            UpdateMask();
        }
    }

    private void UpdateMask()
    {
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        if (_selectedRect.Width > 0 && _selectedRect.Height > 0)
        {
            var selectGeo = new RectangleGeometry(_selectedRect);
            _dimMask.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, selectGeo);
        }
        else
        {
            _dimMask.Data = full;
        }
    }
}
