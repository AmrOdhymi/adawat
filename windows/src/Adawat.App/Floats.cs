using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Adawat.Core;

namespace Adawat.App;

/// <summary>Every open Float.</summary>
public static class Floats
{
    private static readonly List<FloatWindow> Open = [];

    public static bool Any => Open.Count > 0;

    public static void Create(IReadOnlyList<string> files)
    {
        var window = new FloatWindow(files);
        window.Closed += (_, _) => Open.Remove(window);
        Open.Add(window);
        window.Show();
    }

    /// <summary>Results go into the most recent Float, or a new one.</summary>
    public static void ShowResults(IReadOnlyList<string> files)
    {
        if (Open.LastOrDefault() is { } last) { last.Add(files); last.Activate(); }
        else Create(files);
    }

    public static void CloseAll()
    {
        foreach (var window in Open.ToList()) window.Close();
    }
}

/// <summary>
/// A small floating shelf for files: drop files on it to keep them at hand, drag them out to any app, double-click to open,
/// and pin it to the edge of the screen. Closing it never deletes the files.
/// </summary>
public sealed class FloatWindow : Window
{
    private readonly ObservableCollection<string> _files = [];
    private readonly WrapPanel _grid = new() { Margin = new Thickness(8) };
    private readonly TextBlock _count = UI.Text("", 11, FontWeights.SemiBold, Theme.Muted);
    private readonly TextBlock _empty = UI.Text("Drop files here", 12, color: Theme.Muted);
    private readonly HashSet<string> _selected = [];
    private readonly Dictionary<string, ImageSource?> _thumbCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Border> _tileElements = new(StringComparer.OrdinalIgnoreCase);
    private Point? _pressAt;
    private string? _pressed;
    private bool _docked;
    private Rect _undocked;

    public FloatWindow(IReadOnlyList<string> files)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Width = 330;
        Height = 270;
        MinWidth = 260;
        MinHeight = 160;
        FlowDirection = Theme.Direction;
        FontFamily = Theme.Font;
        Left = -10000;
        Top = -10000;

        var header = new DockPanel { Margin = new Thickness(12, 8, 8, 4), LastChildFill = false };
        var title = UI.Text("Float", 11, FontWeights.Bold, Theme.Muted);
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        _count.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(_count, Dock.Left);
        header.Children.Add(_count);

        foreach (var (glyph, help, action) in new (string, string, Action)[]
        {
            ("", "Close Float without deleting files", Close),
            ("", "Attach to the screen edge", ToggleDock),
            ("", "Share", () => { if (Targets().Count > 0) ShareUI.Share(Targets()); }),
            ("", "Move files to folder", MoveTargets),
            ("", "Copy files to clipboard", CopyTargets),
        })
        {
            var button = UI.IconButton(glyph, action, help, 24);
            button.Margin = new Thickness(4, 0, 0, 0);
            DockPanel.SetDock(button, Dock.Right);
            header.Children.Add(button);
        }

        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 1 && e.OriginalSource is DependencyObject dep && !IsInsideButton(dep))
            {
                if (!_docked) DragMove();
            }
        };

        var scroll = new ScrollViewer { Content = _grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        var body = new Grid();
        body.Children.Add(_empty);
        body.Children.Add(scroll);
        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(body);

        Content = new Border
        {
            Margin = new Thickness(12), CornerRadius = new CornerRadius(16), Child = layout,
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFA, 0xFA, 0xFB)), BorderBrush = Theme.Hairline, BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.2 },
        };

        AllowDrop = true;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped) Add(dropped);
            e.Handled = true;
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete) Remove(Targets());
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _selected.UnionWith(_files);
                UpdateSelectionVisuals();
            }
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopyTargets();
            }
            if (e.Key == Key.Escape)
            {
                _selected.Clear();
                UpdateSelectionVisuals();
            }
        };

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            Native.RoundCorners(handle);

            var dpi = VisualTreeHelper.GetDpi(this);
            var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            Native.GetCursorPos(out var pt);
            var work = Native.WorkArea(pt.X, pt.Y);

            var curX = pt.X / scaleX;
            var curY = pt.Y / scaleY;
            var workLeft = work.Left / scaleX;
            var workTop = work.Top / scaleY;
            var workRight = work.Right / scaleX;
            var workBottom = work.Bottom / scaleY;

            Left = Math.Clamp(curX - Width / 2, workLeft, workRight - Width);
            Top = Math.Clamp(curY - 60, workTop, workBottom - Height);
        };

        Add(files);
    }

    private static bool IsInsideButton(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj is Button) return true;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return false;
    }

    public void Add(IEnumerable<string> files)
    {
        foreach (var file in files) if (!_files.Contains(file)) _files.Add(file);
        Draw();
    }

    private void Remove(IEnumerable<string> files)
    {
        foreach (var file in files.ToList())
        {
            _files.Remove(file);
            _selected.Remove(file);
            _thumbCache.Remove(file);
        }
        Draw();
    }

    /// <summary>The selection, or every file when nothing is selected.</summary>
    private List<string> Targets() => _selected.Count > 0 ? _files.Where(_selected.Contains).ToList() : [.. _files];

    private void CopyTargets()
    {
        var targets = Targets();
        if (targets.Count == 0) return;
        Core.Clipboard.CopyFiles(targets);
        Hud.Shared.ShowMessage(targets.Count == 1 ? "File copied" : $"{targets.Count} files copied", Hud.Style.Success);
    }

    private void MoveTargets()
    {
        var targets = Targets();
        if (targets.Count == 0) return;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L10n.T($"Choose where to move {(targets.Count == 1 ? Path.GetFileName(targets[0]) : $"{targets.Count} items")}")
        };
        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            var dest = dialog.FolderName;
            var moved = new List<string>();
            foreach (var item in targets)
            {
                try
                {
                    var name = Path.GetFileName(item.TrimEnd('\\', '/'));
                    if (string.IsNullOrEmpty(name)) name = "item";
                    var targetPath = Output.Unique(Path.Combine(dest, name));
                    if (Directory.Exists(item)) Directory.Move(item, targetPath);
                    else if (File.Exists(item)) File.Move(item, targetPath);
                    moved.Add(item);
                }
                catch (Exception ex)
                {
                    Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
                }
            }
            if (moved.Count > 0)
            {
                Remove(moved);
                Hud.Shared.ShowMessage(moved.Count == 1 ? $"Moved 1 item to {Path.GetFileName(dest)}" : $"Moved {moved.Count} items to {Path.GetFileName(dest)}", Hud.Style.Success);
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var (file, tile) in _tileElements)
        {
            var selected = _selected.Contains(file);
            tile.Background = selected ? Theme.AccentSoft : Brushes.Transparent;
        }
    }

    private void Draw()
    {
        _grid.Children.Clear();
        _tileElements.Clear();
        _count.Text = _files.Count > 0 ? (_files.Count == 1 ? L10n.T("1 file") : L10n.T($"{_files.Count} files")) : "";
        _empty.Visibility = _files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var file in _files)
        {
            var tile = Tile(file);
            _tileElements[file] = tile;
            _grid.Children.Add(tile);
        }
    }

    private Border Tile(string file)
    {
        var selected = _selected.Contains(file);
        if (!_thumbCache.TryGetValue(file, out var thumb))
        {
            thumb = ShellThumbnail.For(file, 128);
            _thumbCache[file] = thumb;
        }

        var image = new Image { Width = 64, Height = 64, Source = thumb, Stretch = Stretch.Uniform };
        var fileName = Path.GetFileName(file.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(fileName)) fileName = file;

        var name = new TextBlock
        {
            Text = fileName, FontFamily = Theme.Font, FontSize = 11, Foreground = Theme.Ink, TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 78, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), FlowDirection = FlowDirection.LeftToRight,
        };
        var stack = new StackPanel();
        stack.Children.Add(image);
        stack.Children.Add(name);

        var tile = new Border
        {
            Child = stack, Padding = new Thickness(6), Margin = new Thickness(3), CornerRadius = new CornerRadius(9), Width = 88,
            Background = selected ? Theme.AccentSoft : Brushes.Transparent, ToolTip = L10n.T($"{fileName}. Double-click to open; drag to carry the file."),
        };

        tile.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                try
                {
                    if (File.Exists(file) || Directory.Exists(file))
                        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                    else
                        Hud.Shared.ShowMessage("File not found", Hud.Style.Failure);
                }
                catch (Exception ex)
                {
                    Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
                }
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (!_selected.Remove(file)) _selected.Add(file);
                UpdateSelectionVisuals();
            }
            else if (!_selected.Contains(file))
            {
                _selected.Clear();
                _selected.Add(file);
                UpdateSelectionVisuals();
            }
            _pressAt = e.GetPosition(this);
            _pressed = file;
        };

        tile.MouseMove += (_, e) =>
        {
            if (_pressAt is not { } start || _pressed == null || e.LeftButton != MouseButtonState.Pressed) return;
            var now = e.GetPosition(this);
            if (Math.Abs(now.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _pressAt = null;
            var targets = Targets();
            var data = new DataObject(DataFormats.FileDrop, targets.ToArray());
            var effects = DragDrop.DoDragDrop(tile, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            if (effects == DragDropEffects.Move)
            {
                var missing = targets.Where(f => !File.Exists(f) && !Directory.Exists(f)).ToList();
                if (missing.Count > 0) Remove(missing);
            }
        };

        tile.MouseLeftButtonUp += (_, _) =>
        {
            if (_pressed == file && _pressAt != null)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 && _selected.Count > 1 && _selected.Contains(file))
                {
                    _selected.Clear();
                    _selected.Add(file);
                    UpdateSelectionVisuals();
                }
            }
            _pressAt = null;
            _pressed = null;
        };

        var menu = new ContextMenu { FlowDirection = Theme.Direction };
        void Item(string title, Action action) { var item = new MenuItem { Header = L10n.T(title) }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("Open", () =>
        {
            try
            {
                if (File.Exists(file) || Directory.Exists(file))
                    Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                else
                    Hud.Shared.ShowMessage("File not found", Hud.Style.Failure);
            }
            catch (Exception ex)
            {
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        });
        Item("Show in File Explorer", () => FileActions.ShowInExplorer(file));
        Item("Copy", CopyTargets);
        Item("Move to Folder...", MoveTargets);
        Item("Copy Path", () => Core.Clipboard.CopyText(file));
        Item("Remove from Float", () => Remove(Targets().Contains(file) ? Targets() : [file]));
        tile.ContextMenu = menu;
        return tile;
    }

    /// <summary>Pins the Float as a narrow strip on the right edge of the screen; again to put it back.</summary>
    private void ToggleDock()
    {
        if (_docked)
        {
            Left = _undocked.Left; Top = _undocked.Top; Width = _undocked.Width; Height = _undocked.Height;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            _grid.Margin = new Thickness(8);
            _docked = false;
            return;
        }

        _undocked = new Rect(Left, Top, Width, Height);
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        var centerPhysX = (int)((Left + Width / 2) * scaleX);
        var centerPhysY = (int)((Top + Height / 2) * scaleY);
        var work = Native.WorkArea(centerPhysX, centerPhysY);

        ResizeMode = ResizeMode.NoResize;
        Width = 165;
        _grid.Margin = new Thickness(4);
        Height = Math.Min(560, (work.Bottom - work.Top) / scaleY - 40);
        Left = work.Right / scaleX - Width + 4;
        Top = work.Top / scaleY + ((work.Bottom - work.Top) / scaleY - Height) / 2;
        _docked = true;
    }
}
