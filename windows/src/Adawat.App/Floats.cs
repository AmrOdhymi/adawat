using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    public static IReadOnlyList<FloatWindow> OpenWindows => Open;

    public static void Create(IReadOnlyList<string> files)
    {
        var window = new FloatWindow(files);
        window.Closed += (_, _) => Open.Remove(window);
        Open.Add(window);
        window.Show();
    }

    /// <summary>Always opens a new independent Float window.</summary>
    public static void CreateNew() => Create([]);

    public static void ToggleOrShow() => Create([]);

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
/// A floating shelf for files: drop files on it to keep them at hand, drag them out to any app, double-click to open.
/// Resizable from all corners and edges, natively draggable from the header, with Windows-style checkboxes.
/// </summary>
public sealed class FloatWindow : Window
{
    private readonly ObservableCollection<string> _files = [];
    private readonly WrapPanel _grid = new() { Margin = new Thickness(8) };
    private readonly TextBlock _count = UI.Text("", 11, FontWeights.SemiBold, Theme.Muted);
    private readonly TextBlock _empty = UI.Text("Drop files here", 12, color: Theme.Muted);
    private readonly HashSet<string> _selected = [];
    private readonly Dictionary<string, ImageSource?> _thumbCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Border Tile, Border CheckBorder, TextBlock CheckIcon)> _tileElements = new(StringComparer.OrdinalIgnoreCase);
    private Point? _pressAt;
    private string? _pressed;

    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    public FloatWindow(IReadOnlyList<string> files)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Width = 350;
        Height = 280;
        MinWidth = 220;
        MinHeight = 160;
        FlowDirection = Theme.Direction;
        FontFamily = Theme.Font;
        Left = -10000;
        Top = -10000;

        // Clean Header: Title + Count on Left, single Close button on Right
        var header = new DockPanel { Margin = new Thickness(12, 8, 8, 4), LastChildFill = false };
        var title = UI.Text("Float", 11, FontWeights.Bold, Theme.Muted);
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);
        _count.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(_count, Dock.Left);
        header.Children.Add(_count);

var closeBtn = UI.IconButton(
    "",
    Close,
    "Close Float without deleting files",
    24
);

closeBtn.Height = 25;
closeBtn.MinHeight = 25;
closeBtn.VerticalAlignment = VerticalAlignment.Center;
closeBtn.Padding = new Thickness(0);
closeBtn.Margin = new Thickness(4, 0, 0, 0);

DockPanel.SetDock(closeBtn, Dock.Right);
header.Children.Add(closeBtn);

        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is DependencyObject dep && !IsInsideButton(dep))
            {
                try { DragMove(); } catch { }
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

        // Left-click on empty space deselects all files (scrollbar interaction is preserved)
        scroll.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (IsInsideScrollBar(dep) || IsInsideTile(dep) || IsInsideButton(dep))
                    return;

                if (_selected.Count > 0)
                {
                    _selected.Clear();
                    UpdateSelectionVisuals();
                }
            }
        };

        // Empty space context menu: acts on ALL files when right-clicking on empty space
        body.ContextMenuOpening += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (IsInsideScrollBar(dep) || IsInsideTile(dep))
                    return;

                if (_selected.Count > 0)
                {
                    _selected.Clear();
                    UpdateSelectionVisuals();
                }
            }
        };
        body.ContextMenu = MakeEmptySpaceMenu();

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
            if (e.Key == Key.F2)
            {
                RenameTargets();
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

            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);

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

            // Cascade position if other Float windows exist
            var previous = Floats.OpenWindows.LastOrDefault(w => w != this && w.IsLoaded);
            if (previous != null)
            {
                Left = Math.Clamp(previous.Left + 28, workLeft, workRight - Width);
                Top = Math.Clamp(previous.Top + 28, workTop, workBottom - Height);
            }
            else
            {
                Left = Math.Clamp(curX - Width / 2, workLeft, workRight - Width);
                Top = Math.Clamp(curY - 60, workTop, workBottom - Height);
            }
        };

        Add(files);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            var screenX = (short)(lParam.ToInt32() & 0xFFFF);
            var screenY = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
            var clientPt = PointFromScreen(new Point(screenX, screenY));

            const double inset = 12.0;       // Margin of outer Border (shadow)
            const double grip = 8.0;         // Thickness of the resize zone

            var w = ActualWidth;
            var h = ActualHeight;

            var innerLeft = inset;
            var innerTop = inset;
            var innerRight = w - inset;
            var innerBottom = h - inset;

            // Check if pointer is in the active window area
            if (clientPt.X >= innerLeft - grip && clientPt.X <= innerRight + grip &&
                clientPt.Y >= innerTop - grip && clientPt.Y <= innerBottom + grip)
            {
                bool isTop = clientPt.Y <= innerTop + grip;
                bool isBottom = clientPt.Y >= innerBottom - grip;
                bool isLeft = clientPt.X <= innerLeft + grip;
                bool isRight = clientPt.X >= innerRight - grip;

                // Corners (diagonal resizing)
                if (isTop && isLeft) { handled = true; return new IntPtr(HTTOPLEFT); }
                if (isTop && isRight) { handled = true; return new IntPtr(HTTOPRIGHT); }
                if (isBottom && isLeft) { handled = true; return new IntPtr(HTBOTTOMLEFT); }
                if (isBottom && isRight) { handled = true; return new IntPtr(HTBOTTOMRIGHT); }

                // Edges (horizontal / vertical resizing)
                if (isTop) { handled = true; return new IntPtr(HTTOP); }
                if (isBottom) { handled = true; return new IntPtr(HTBOTTOM); }
                if (isLeft) { handled = true; return new IntPtr(HTLEFT); }
                if (isRight) { handled = true; return new IntPtr(HTRIGHT); }

                // Header area: enables smooth native window dragging
                if (clientPt.Y <= innerTop + 36 && clientPt.X >= innerLeft + grip && clientPt.X <= innerRight - grip)
                {
                    var hitElement = InputHitTest(clientPt) as DependencyObject;
                    if (!IsInsideButton(hitElement))
                    {
                        handled = true;
                        return new IntPtr(HTCAPTION);
                    }
                }
            }
        }
        return IntPtr.Zero;
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

    private static bool IsInsideTile(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj is FrameworkElement fe && fe.Tag as string == "Tile") return true;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return false;
    }

    private static bool IsInsideScrollBar(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj is System.Windows.Controls.Primitives.ScrollBar ||
                obj is System.Windows.Controls.Primitives.Thumb ||
                obj is System.Windows.Controls.Primitives.Track ||
                obj is System.Windows.Controls.Primitives.RepeatButton)
                return true;
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
            _tileElements.Remove(file);
        }
        Draw();
    }

    /// <summary>The selection, or every file when nothing is selected.</summary>
    private List<string> Targets() => _selected.Count > 0 ? _files.Where(_selected.Contains).ToList() : [.. _files];

    private void CopyTargets(IReadOnlyList<string>? specificTargets = null)
    {
        var targets = specificTargets ?? Targets();
        if (targets.Count == 0) return;
        Core.Clipboard.CopyFiles(targets);
        Hud.Shared.ShowMessage(targets.Count == 1 ? "File copied" : $"{targets.Count} files copied", Hud.Style.Success);
    }

    private void MoveTargets(IReadOnlyList<string>? specificTargets = null)
    {
        var targets = specificTargets ?? Targets();
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

    private void RenameTargets(IReadOnlyList<string>? specificTargets = null)
    {
        var targets = specificTargets ?? Targets();
        if (targets.Count == 0) return;
        FloatDialogs.ShowRename(this, targets, map =>
        {
            foreach (var (oldPath, newPath) in map)
            {
                var idx = _files.IndexOf(oldPath);
                if (idx >= 0) _files[idx] = newPath;
                if (_selected.Remove(oldPath)) _selected.Add(newPath);
                if (_thumbCache.Remove(oldPath, out var thumb)) _thumbCache[newPath] = thumb;
            }
            Draw();
        });
    }

    private void ShowProperties(IReadOnlyList<string>? specificTargets = null)
    {
        var targets = specificTargets ?? Targets();
        if (targets.Count == 0) return;
        FloatDialogs.ShowProperties(this, targets);
    }

    private void SendToBluetooth(IReadOnlyList<string>? specificTargets = null)
    {
        var targets = specificTargets ?? Targets();
        if (targets.Count == 0) return;

        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (BluetoothSender.Send(handle, targets))
            {
                Hud.Shared.ShowMessage(L10n.T("Send to Bluetooth device"), Hud.Style.Success);
                return;
            }

            var data = new DataObject(DataFormats.FileDrop, targets.ToArray());
            System.Windows.Clipboard.SetDataObject(data, true);

            var sendToPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\SendTo");
            string? btShortcut = null;
            if (Directory.Exists(sendToPath))
            {
                btShortcut = Directory.GetFiles(sendToPath, "*Bluetooth*").FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(btShortcut) && File.Exists(btShortcut))
            {
                var args = string.Join(" ", targets.Select(t => $"\"{t}\""));
                Process.Start(new ProcessStartInfo
                {
                    FileName = btShortcut,
                    Arguments = args,
                    UseShellExecute = true
                });
                Hud.Shared.ShowMessage(L10n.T("Send to Bluetooth device"), Hud.Style.Success);
                return;
            }

            Process.Start(new ProcessStartInfo("fsquirt.exe", "-send") { UseShellExecute = true });
            Hud.Shared.ShowMessage(L10n.T("Send to Bluetooth device"), Hud.Style.Success);
        }
        catch (Exception ex)
        {
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
        }
    }

    private async void CompressToZip(IReadOnlyList<string>? specificTargets = null)
    {
        var targets = specificTargets ?? Targets();
        if (targets.Count == 0) return;

        try
        {
            var first = targets[0];
            var dir = Directory.Exists(first) ? Path.GetDirectoryName(first.TrimEnd('\\', '/')) : Path.GetDirectoryName(first);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var baseName = targets.Count == 1 ? Path.GetFileNameWithoutExtension(first.TrimEnd('\\', '/')) : "Archive";
            if (string.IsNullOrEmpty(baseName)) baseName = "Archive";
            var zipPath = Output.Unique(Path.Combine(dir, $"{baseName}.zip"));

            Hud.Shared.ShowMessage(L10n.T("Compressing..."), Hud.Style.Working);

            await Task.Run(() =>
            {
                using var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
                foreach (var item in targets)
                {
                    AddEntryToZip(zip, item, Path.GetFileName(item.TrimEnd('\\', '/')));
                }
            });

            Add([zipPath]);
            Hud.Shared.ShowMessage(L10n.T("ZIP archive created"), Hud.Style.Success);
        }
        catch (Exception ex)
        {
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
        }
    }

    private static void AddEntryToZip(System.IO.Compression.ZipArchive zip, string path, string entryName)
    {
        if (Directory.Exists(path))
        {
            var entries = Directory.EnumerateFileSystemEntries(path).ToList();
            if (entries.Count == 0)
            {
                zip.CreateEntry(entryName + "/");
                return;
            }
            foreach (var child in entries)
            {
                AddEntryToZip(zip, child, entryName + "/" + Path.GetFileName(child));
            }
            return;
        }

        if (File.Exists(path))
        {
            zip.CreateEntryFromFile(path, entryName, System.IO.Compression.CompressionLevel.Optimal);
        }
    }

    private void DeleteFromDisk(IReadOnlyList<string> targets)
    {
        if (targets.Count == 0) return;
        var message = targets.Count == 1
            ? L10n.T($"Are you sure you want to permanently delete '{Path.GetFileName(targets[0])}' from disk?")
            : L10n.T($"Are you sure you want to permanently delete all {targets.Count} files from disk?");
        var result = MessageBox.Show(this, message, L10n.T("Confirm Delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var deleted = new List<string>();
        foreach (var path in targets)
        {
            try
            {
                if (File.Exists(path)) { File.Delete(path); deleted.Add(path); }
                else if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); deleted.Add(path); }
            }
            catch (Exception ex)
            {
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        }
        if (deleted.Count > 0)
        {
            Remove(deleted);
            Hud.Shared.ShowMessage(deleted.Count == 1 ? "File deleted" : $"{deleted.Count} files deleted", Hud.Style.Success);
        }
    }

    private ContextMenu MakeEmptySpaceMenu()
    {
        var menu = new ContextMenu { FlowDirection = Theme.Direction };
        void Item(string title, Action action) { var item = new MenuItem { Header = L10n.T(title) }; item.Click += (_, _) => action(); menu.Items.Add(item); }

        Item("Copy All", () => CopyTargets([.. _files]));
        Item("Move All to Folder...", () => MoveTargets([.. _files]));
        Item("Compress All to ZIP", () => CompressToZip([.. _files]));
        Item("Share All", () => { if (_files.Count > 0) ShareUI.Share([.. _files]); });
        Item("Send All to Bluetooth device", () => SendToBluetooth([.. _files]));
        menu.Items.Add(new Separator());
        Item("Select All", () => { _selected.UnionWith(_files); UpdateSelectionVisuals(); });
        Item("Properties", () => ShowProperties([.. _files]));
        menu.Items.Add(new Separator());
        Item("Clear All from Float", () => Remove([.. _files]));
        Item("Delete All from Disk", () => DeleteFromDisk([.. _files]));
        return menu;
    }

    private void UpdateSelectionVisuals()
    {
        var anySelected = _selected.Count > 0;
        foreach (var (file, (tile, checkBorder, checkIcon)) in _tileElements)
        {
            var selected = _selected.Contains(file);
            tile.Background = selected ? Theme.AccentSoft : Brushes.Transparent;
            if (selected)
            {
                checkBorder.Visibility = Visibility.Visible;
                checkBorder.Background = Theme.Accent;
                checkBorder.BorderBrush = Theme.Accent;
                checkIcon.Visibility = Visibility.Visible;
            }
            else
            {
                checkBorder.Visibility = anySelected ? Visibility.Visible : (tile.IsMouseOver ? Visibility.Visible : Visibility.Collapsed);
                checkBorder.Background = new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF));
                checkBorder.BorderBrush = Theme.Hairline;
                checkIcon.Visibility = Visibility.Collapsed;
            }
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

        // Windows-style checkmark / checkbox in top-left
        var checkIcon = new TextBlock
        {
            Text = "✓",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = selected ? Visibility.Visible : Visibility.Collapsed,
        };

        var checkBorder = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = selected ? Theme.Accent : new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF)),
            BorderBrush = selected ? Theme.Accent : Theme.Hairline,
            BorderThickness = new Thickness(1.2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2, 2, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = L10n.T("Select item"),
            Child = checkIcon,
            Visibility = selected ? Visibility.Visible : Visibility.Collapsed,
        };

        checkBorder.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (!_selected.Remove(file))
            {
                _selected.Add(file);
            }
            UpdateSelectionVisuals();
        };

        // Close / remove button on top-right of tile
        var xButton = new Button
        {
            Content = new TextBlock { Text = "✕", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Theme.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 16,
            Height = 16,
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00)),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 2, 0),
            ToolTip = L10n.T("Remove from list"),
            Visibility = Visibility.Collapsed,
        };
        xButton.Click += (_, e) =>
        {
            e.Handled = true;
            Remove([file]);
        };

        var tileGrid = new Grid();
        tileGrid.Children.Add(stack);
        tileGrid.Children.Add(checkBorder);
        tileGrid.Children.Add(xButton);

        var tile = new Border
        {
            Tag = "Tile",
            Child = tileGrid, Padding = new Thickness(6), Margin = new Thickness(3), CornerRadius = new CornerRadius(9), Width = 88,
            Background = selected ? Theme.AccentSoft : Brushes.Transparent, ToolTip = L10n.T($"{fileName}. Double-click to open; drag to carry the file."),
        };

        _tileElements[file] = (tile, checkBorder, checkIcon);

        tile.MouseEnter += (_, _) =>
        {
            xButton.Visibility = Visibility.Visible;
            if (!_selected.Contains(file)) checkBorder.Visibility = Visibility.Visible;
        };

        tile.MouseLeave += (_, _) =>
        {
            xButton.Visibility = Visibility.Collapsed;
            if (!_selected.Contains(file) && _selected.Count == 0) checkBorder.Visibility = Visibility.Collapsed;
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

        // Right click on tile: if tile is not selected, select it
        tile.PreviewMouseRightButtonDown += (_, _) =>
        {
            if (!_selected.Contains(file) && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                _selected.Clear();
                _selected.Add(file);
                UpdateSelectionVisuals();
            }
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
        menu.Items.Add(new Separator());
        Item(Targets().Count > 1 ? "Batch Rename..." : "Rename...", () => RenameTargets(Targets()));
        Item(Targets().Count > 1 ? "Copy" : "Copy", () => CopyTargets(Targets()));
        Item(Targets().Count > 1 ? "Move to Folder..." : "Move to Folder...", () => MoveTargets(Targets()));
        Item("Compress to ZIP", () => CompressToZip(Targets()));
        Item("Share", () => ShareUI.Share(Targets()));
        Item("Send to Bluetooth device", () => SendToBluetooth(Targets()));
        menu.Items.Add(new Separator());
        Item("Properties", () => ShowProperties(Targets()));
        menu.Items.Add(new Separator());
        Item("Copy Path", () => Core.Clipboard.CopyText(string.Join(Environment.NewLine, Targets())));
        Item(Targets().Count > 1 ? "Remove from Float" : "Remove from Float", () => Remove(Targets()));
        Item(Targets().Count > 1 ? "Delete from Disk" : "Delete from Disk", () => DeleteFromDisk(Targets()));
        tile.ContextMenu = menu;
        return tile;
    }
}
