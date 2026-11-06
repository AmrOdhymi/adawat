using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Adawat.Core;
using Adawat.App;

namespace Adawat.App.Wheel;

public sealed record QuickToolItem(string Id, string Title, string Icon, Action Action);

public static class QuickWheel
{
    private static QuickWheelWindow? _instance;

    public static void ToggleOrShow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _instance ??= new QuickWheelWindow();
            if (_instance.IsWheelShown)
            {
                _instance.HideWheel();
            }
            else
            {
                if (Native.GetCursorPos(out var pt))
                    _instance.ShowWheel(new Point(pt.X, pt.Y));
                else
                    _instance.ShowWheel(new Point(SystemParameters.PrimaryScreenWidth / 2, SystemParameters.PrimaryScreenHeight / 2));
            }
        });
    }

    public static void Close() => _instance?.HideWheel();
}

/// <summary>
/// A standalone radial wheel invoked by a global hotkey (e.g. Ctrl+Space) at the current mouse position.
/// Does not require dragging files; items are selected by clicking with the mouse.
/// </summary>
public sealed class QuickWheelWindow : Window
{
    private readonly Grid _root = new() { Width = WheelGeometry.Size, Height = WheelGeometry.Size, Background = Brushes.Transparent };
    private readonly Canvas _petals = new() { Width = WheelGeometry.Size, Height = WheelGeometry.Size, IsHitTestVisible = false };
    private readonly TextBlock _center = new() { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Black, MaxWidth = 72 };
    private readonly ScaleTransform _scale = new(0.9, 0.9, WheelGeometry.Size / 2, WheelGeometry.Size / 2);
    private List<QuickToolItem> _items = [];
    private int? _hovered;
    private bool _shown;

    public bool IsWheelShown => _shown;

    public QuickWheelWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        FlowDirection = FlowDirection.LeftToRight;

        var disc = new Ellipse
        {
            Width = (WheelGeometry.OuterRadius + 6.5) * 2, Height = (WheelGeometry.OuterRadius + 6.5) * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1,
            Fill = Theme.Disc, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 5, Direction = 270, Opacity = 0.18, Color = Color.FromRgb(0x26, 0x34, 0x38) },
        };
        var hole = new EllipseGeometry(new Point(WheelGeometry.Size / 2, WheelGeometry.Size / 2), WheelGeometry.InnerRadius - 6, WheelGeometry.InnerRadius - 6);
        disc.OpacityMask = new DrawingBrush(new GeometryDrawing(Brushes.Black, null,
            new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(WheelGeometry.Size / 2, WheelGeometry.Size / 2), WheelGeometry.OuterRadius + 6.5, WheelGeometry.OuterRadius + 6.5), hole)))
        { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(WheelGeometry.Size / 2 - WheelGeometry.OuterRadius - 6.5, WheelGeometry.Size / 2 - WheelGeometry.OuterRadius - 6.5, (WheelGeometry.OuterRadius + 6.5) * 2, (WheelGeometry.OuterRadius + 6.5) * 2) };

        var centerPill = new Border
        {
            Child = _center, Background = new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF)), CornerRadius = new CornerRadius(24),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6), MinHeight = 32, MinWidth = 70, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Direction = 270, Opacity = 0.1 },
        };
        _center.VerticalAlignment = VerticalAlignment.Center;

        _root.Children.Add(disc);
        _root.Children.Add(_petals);
        _root.Children.Add(centerPill);
        _root.RenderTransform = _scale;

        _root.MouseMove += (_, e) =>
        {
            var p = e.GetPosition(_root);
            var index = _items.Count > 0 ? WheelGeometry.Index(p, _items.Count) : null;
            SetHovered(index);
        };

        _root.MouseLeave += (_, _) => SetHovered(null);

        _root.MouseLeftButtonUp += (_, _) =>
        {
            if (_hovered is { } index && index < _items.Count)
            {
                var action = _items[index].Action;
                HideWheel();
                Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                HideWheel();
            }
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HideWheel();
                e.Handled = true;
            }
        };

        Deactivated += (_, _) => HideWheel();

        Content = _root;
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle);
    }

    public void ShowWheel(Point screenPixels)
    {
        _items = LoadActiveItems();
        _hovered = null;

        if (!_shown)
        {
            Opacity = 0;
            if (!IsLoaded) { Left = -10000; Top = -10000; base.Show(); }
            Place(screenPixels);
            base.Show();
            Activate();
            _shown = true;

            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            var grow = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            };
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
        else
        {
            Place(screenPixels);
        }

        Draw();
        Sounds.Tick();
    }

    public void HideWheel()
    {
        if (!_shown) return;
        _shown = false;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
        fade.Completed += (_, _) => { if (!_shown) base.Hide(); };
        BeginAnimation(OpacityProperty, fade);
    }

    private void Place(Point screenPixels)
    {
        var source = PresentationSource.FromVisual(this);
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var scale = toDevice.M11 <= 0 ? 1 : toDevice.M11;
        var screen = Native.WorkArea((int)screenPixels.X, (int)screenPixels.Y);
        var size = WheelGeometry.Size * scale;
        var x = Math.Clamp(screenPixels.X - size / 2, screen.Left, screen.Right - size);
        var y = Math.Clamp(screenPixels.Y - size / 2, screen.Top, screen.Bottom - size);
        Left = x / scale;
        Top = y / scale;
    }

    private void SetHovered(int? index)
    {
        if (index == _hovered) return;
        _hovered = index;
        Draw();
        if (index != null) Sounds.Tick();
    }

    private void Draw()
    {
        _petals.Children.Clear();
        var count = _items.Count;
        for (var index = 0; index < count; index++)
        {
            var hovered = index == _hovered;
            var item = _items[index];
            var petal = new System.Windows.Shapes.Path
            {
                Data = WheelGeometry.Petal(index, count),
                Fill = hovered ? Theme.Accent : Theme.PetalFill,
                Stroke = hovered ? new SolidColorBrush(Color.FromArgb(0x70, 0x3E, 0x2E, 0xAA)) : Theme.PetalStroke,
                StrokeThickness = 0.75,
            };
            var group = new Canvas();
            group.Children.Add(petal);
            var label = RenderLabel(item, hovered, count);
            var position = WheelGeometry.Position(index, count);
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, position.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, position.Y - label.DesiredSize.Height / 2);
            group.Children.Add(label);

            if (hovered && count > 1)
            {
                var angle = -Math.PI / 2 + (double)index / count * 2 * Math.PI;
                group.RenderTransform = new TranslateTransform(Math.Cos(angle) * 5, Math.Sin(angle) * 5);
                group.Effect = new DropShadowEffect { Color = Theme.AccentColor, BlurRadius = 12, ShadowDepth = 3, Direction = 270, Opacity = 0.35 };
            }
            else
            {
                group.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 3, Direction = 270, Opacity = 0.12, Color = Color.FromRgb(0x26, 0x34, 0x38) };
            }
            _petals.Children.Add(group);
        }

        var current = _hovered is { } h && h < count ? _items[h] : null;
        _center.Text = current != null ? current.Title : (count == 0 ? L10n.Upper(L10n.T("NO OPTIONS")) : L10n.Upper(L10n.T("Tools")));
        _center.FontFamily = Theme.Font;
        _center.FontSize = current == null ? 11 : 10;
        _center.FontWeight = FontWeights.SemiBold;
    }

    private static FrameworkElement RenderLabel(QuickToolItem item, bool hovered, int count)
    {
        var color = hovered ? Brushes.White : Brushes.Black;
        var stack = new StackPanel { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = item.Icon,
            FontFamily = Theme.Icons,
            FontSize = 16,
            Foreground = color,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontFamily = Theme.Font,
            FontWeight = FontWeights.SemiBold,
            FontSize = 9.5,
            Foreground = color,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
            MaxWidth = 66,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FlowDirection = Theme.Direction,
        });
        return stack;
    }

    private static List<QuickToolItem> LoadActiveItems()
    {
        var settings = Settings.Current;
        var all = new List<QuickToolItem>
        {
            new("screenshot", L10n.T("Screenshot"), Catalog.Icons.Camera, () => ScreenCapture.StartSnipping()),
            new("bluetooth", L10n.T("Bluetooth"), "\uE702", () => BluetoothSender.OpenBluetoothTransfer()),
            new("make_qr", L10n.T("Make QR"), Catalog.Icons.Qr, () => ToolWindows.Open("qr", [])),
            new("read_qr", L10n.T("Scan QR"), Catalog.Icons.Crop, () => QrScanner.StartScan()),
            new("keyboard_swap", L10n.T("Fix Layout"), Catalog.Icons.Text, () => KeyboardTypoFixer.FixClipboard()),
            new("launcher", L10n.T("Launcher"), "\uE71D", () => QuickLauncherWindow.Present()),
            new("notepad", L10n.T("Notepad"), Catalog.Icons.Edit, () => NotepadWindow.Present()),
            new("clipboard", L10n.T("Clipboard"), "\uE77F", () => ClipboardWindow.Present()),
            new("screen_text", L10n.T("Extract Text"), Catalog.Icons.Text, () => TextOverlayWindow.Launch()),
        };

        return all.Where(item => settings.IsQuickToolEnabled(item.Id)).ToList();
    }
}
