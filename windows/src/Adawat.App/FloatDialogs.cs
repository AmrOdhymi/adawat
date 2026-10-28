using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Adawat.Core;
using Adawat.Core.Images;
using Adawat.Core.Media;

namespace Adawat.App;

/// <summary>Dialogs for Float Shelf: Rename (Single & Batch) and Properties.</summary>
public static class FloatDialogs
{
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double val = bytes;
        int i = 0;
        while (val >= 1024 && i < units.Length - 1) { val /= 1024; i++; }
        return $"{val:0.##} {units[i]} ({bytes:N0} bytes)";
    }

    /// <summary>Shows a rename dialog for a single file or opens batch rename for multiple files.</summary>
    public static void ShowRename(Window owner, IReadOnlyList<string> targets, Action<Dictionary<string, string>> onRenamed)
    {
        if (targets.Count == 0) return;
        if (targets.Count == 1)
        {
            var singleWin = new SingleRenameWindow(owner, targets[0], onRenamed);
            singleWin.ShowDialog();
        }
        else
        {
            var batchWin = new BatchRenameWindow(owner, targets, onRenamed);
            batchWin.ShowDialog();
        }
    }

    /// <summary>Shows the properties window for selected or all files.</summary>
    public static void ShowProperties(Window owner, IReadOnlyList<string> targets)
    {
        if (targets.Count == 0) return;
        var win = new PropertiesWindow(owner, targets);
        win.ShowDialog();
    }
}

/// <summary>Rename dialog for a single file.</summary>
internal sealed class SingleRenameWindow : Window
{
    private readonly string _target;
    private readonly Action<Dictionary<string, string>> _onRenamed;
    private readonly TextBox _input;
    private readonly string _dir;
    private readonly string _ext;

    public SingleRenameWindow(Window owner, string target, Action<Dictionary<string, string>> onRenamed)
    {
        Owner = owner;
        _target = target;
        _onRenamed = onRenamed;
        _dir = Path.GetDirectoryName(target) ?? "";
        var isDir = Directory.Exists(target);
        _ext = isDir ? "" : Path.GetExtension(target);
        var baseName = isDir ? Path.GetFileName(target.TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(target);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = Theme.Font;
        FlowDirection = Theme.Direction;

        var stack = new StackPanel { Margin = new Thickness(20), Width = 340 };
        var title = UI.Text("Rename", 14, FontWeights.Bold, Theme.Ink);
        var oldLabel = UI.Text($"Original: {Path.GetFileName(target.TrimEnd('\\', '/'))}", 11.5, color: Theme.Muted);
        oldLabel.Margin = new Thickness(0, 4, 0, 14);

        _input = new TextBox
        {
            Text = baseName,
            FontSize = 13,
            FontFamily = Theme.Font,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16),
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = UI.Button("Cancel", Close);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        var rename = UI.Button("Rename", ApplyRename, primary: true);

        buttons.Children.Add(cancel);
        buttons.Children.Add(rename);

        stack.Children.Add(title);
        stack.Children.Add(oldLabel);
        stack.Children.Add(_input);
        stack.Children.Add(buttons);

        Content = new Border
        {
            Child = stack,
            CornerRadius = new CornerRadius(14),
            Background = Brushes.White,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.2 },
        };

        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };

        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) ApplyRename();
            if (e.Key == Key.Escape) Close();
        };
    }

    private void ApplyRename()
    {
        var newBase = _input.Text.Trim();
        if (string.IsNullOrWhiteSpace(newBase)) return;

        var newName = newBase + _ext;
        var newPath = Path.Combine(_dir, newName);

        if (string.Equals(_target, newPath, StringComparison.OrdinalIgnoreCase))
        {
            Close();
            return;
        }

        try
        {
            if (Directory.Exists(_target)) Directory.Move(_target, newPath);
            else if (File.Exists(_target)) File.Move(_target, newPath);

            _onRenamed(new Dictionary<string, string> { [_target] = newPath });
            Hud.Shared.ShowMessage($"Renamed to {newName}", Hud.Style.Success);
            Close();
        }
        catch (Exception ex)
        {
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
        }
    }
}

/// <summary>Batch Rename Window with live preview and multiple renaming modes.</summary>
internal sealed class BatchRenameWindow : Window
{
    private readonly IReadOnlyList<string> _targets;
    private readonly Action<Dictionary<string, string>> _onRenamed;
    private readonly StackPanel _previewList = new();
    private readonly TextBox _findBox = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _replaceBox = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _prefixBox = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _suffixBox = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _numberBaseBox = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _startAtBox = new() { Text = "1", Width = 48, Padding = new Thickness(6, 4, 6, 4) };
    private readonly CheckBox _matchCaseCheck = new() { Content = "Match Case", Margin = new Thickness(4, 0, 0, 0) };
    private int _mode; // 0 = Find & Replace, 1 = Prefix/Suffix, 2 = Numbering, 3 = Case

    private readonly RadioButton _rbReplace = new() { Content = "Find & Replace", IsChecked = true, Margin = new Thickness(0, 0, 12, 0) };
    private readonly RadioButton _rbAdd = new() { Content = "Prefix / Suffix", Margin = new Thickness(0, 0, 12, 0) };
    private readonly RadioButton _rbNumber = new() { Content = "Numbering", Margin = new Thickness(0, 0, 12, 0) };
    private readonly RadioButton _rbCase = new() { Content = "Change Case" };
    private readonly ComboBox _caseCombo = new() { Items = { "lowercase", "UPPERCASE", "Title Case" }, SelectedIndex = 0, Width = 140 };

    public BatchRenameWindow(Window owner, IReadOnlyList<string> targets, Action<Dictionary<string, string>> onRenamed)
    {
        Owner = owner;
        _targets = targets;
        _onRenamed = onRenamed;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Width = 560;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = Theme.Font;
        FlowDirection = Theme.Direction;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16), LastChildFill = false };
        var title = UI.Text($"Batch Rename ({targets.Count} files)", 15, FontWeights.Bold, Theme.Ink);
        DockPanel.SetDock(title, Dock.Left);
        var closeBtn = UI.IconButton("", Close, "Close", 24);
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(title);
        header.Children.Add(closeBtn);

        // Mode selector
        var modePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        modePanel.Children.Add(_rbReplace);
        modePanel.Children.Add(_rbAdd);
        modePanel.Children.Add(_rbNumber);
        modePanel.Children.Add(_rbCase);

        // Inputs container
        var inputsArea = new Grid { Margin = new Thickness(0, 0, 0, 14) };

        // Find / Replace
        var gridReplace = new Grid();
        gridReplace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridReplace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var col1 = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
        col1.Children.Add(UI.Caption("Find"));
        col1.Children.Add(_findBox);
        var col2 = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        col2.Children.Add(UI.Caption("Replace with"));
        col2.Children.Add(_replaceBox);
        col2.Children.Add(_matchCaseCheck);
        Grid.SetColumn(col1, 0);
        Grid.SetColumn(col2, 1);
        gridReplace.Children.Add(col1);
        gridReplace.Children.Add(col2);

        // Prefix / Suffix
        var gridAdd = new Grid { Visibility = Visibility.Collapsed };
        gridAdd.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridAdd.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var pcol1 = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
        pcol1.Children.Add(UI.Caption("Prefix"));
        pcol1.Children.Add(_prefixBox);
        var pcol2 = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        pcol2.Children.Add(UI.Caption("Suffix"));
        pcol2.Children.Add(_suffixBox);
        Grid.SetColumn(pcol1, 0);
        Grid.SetColumn(pcol2, 1);
        gridAdd.Children.Add(pcol1);
        gridAdd.Children.Add(pcol2);

        // Numbering
        var gridNumber = new Grid { Visibility = Visibility.Collapsed };
        gridNumber.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        gridNumber.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var ncol1 = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
        ncol1.Children.Add(UI.Caption("Base Name"));
        ncol1.Children.Add(_numberBaseBox);
        var ncol2 = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        ncol2.Children.Add(UI.Caption("Start Number"));
        ncol2.Children.Add(_startAtBox);
        Grid.SetColumn(ncol1, 0);
        Grid.SetColumn(ncol2, 1);
        gridNumber.Children.Add(ncol1);
        gridNumber.Children.Add(ncol2);

        // Case
        var gridCase = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
        gridCase.Children.Add(UI.Caption("Format: ").Pad(0, 4));
        gridCase.Children.Add(_caseCombo);

        inputsArea.Children.Add(gridReplace);
        inputsArea.Children.Add(gridAdd);
        inputsArea.Children.Add(gridNumber);
        inputsArea.Children.Add(gridCase);

        void UpdateModeInputs()
        {
            gridReplace.Visibility = _mode == 0 ? Visibility.Visible : Visibility.Collapsed;
            gridAdd.Visibility = _mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            gridNumber.Visibility = _mode == 2 ? Visibility.Visible : Visibility.Collapsed;
            gridCase.Visibility = _mode == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        _rbReplace.Checked += (_, _) => { _mode = 0; UpdateModeInputs(); UpdatePreview(); };
        _rbAdd.Checked += (_, _) => { _mode = 1; UpdateModeInputs(); UpdatePreview(); };
        _rbNumber.Checked += (_, _) => { _mode = 2; UpdateModeInputs(); UpdatePreview(); };
        _rbCase.Checked += (_, _) => { _mode = 3; UpdateModeInputs(); UpdatePreview(); };

        // Preview Header
        var previewHeader = new DockPanel { Margin = new Thickness(6, 0, 6, 4) };
        var origHeader = UI.Text("Original", 11, FontWeights.SemiBold, Theme.Muted);
        var newHeader = UI.Text("New Name", 11, FontWeights.SemiBold, Theme.Muted);
        DockPanel.SetDock(origHeader, Dock.Left);
        DockPanel.SetDock(newHeader, Dock.Right);
        previewHeader.Children.Add(origHeader);
        previewHeader.Children.Add(newHeader);

        var previewScroll = new ScrollViewer
        {
            Content = _previewList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 220,
            Background = new SolidColorBrush(Color.FromArgb(0x50, 0xF0, 0xF2, 0xF5)),
        };

        var previewBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Child = previewScroll,
            Margin = new Thickness(0, 0, 0, 16),
        };

        // Actions
        var bottomBar = new DockPanel { LastChildFill = false };
        var cancelBtn = UI.Button("Cancel", Close);
        var applyBtn = UI.Button("Apply Rename", Apply, primary: true);
        DockPanel.SetDock(applyBtn, Dock.Right);
        DockPanel.SetDock(cancelBtn, Dock.Right);
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        bottomBar.Children.Add(applyBtn);
        bottomBar.Children.Add(cancelBtn);

        var layout = new StackPanel { Margin = new Thickness(22) };
        layout.Children.Add(header);
        layout.Children.Add(modePanel);
        layout.Children.Add(inputsArea);
        layout.Children.Add(previewHeader);
        layout.Children.Add(previewBorder);
        layout.Children.Add(bottomBar);

        Content = new Border
        {
            Child = layout,
            CornerRadius = new CornerRadius(16),
            Background = Brushes.White,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.2 },
        };

        // Event hooks for live preview
        _findBox.TextChanged += (_, _) => UpdatePreview();
        _replaceBox.TextChanged += (_, _) => UpdatePreview();
        _prefixBox.TextChanged += (_, _) => UpdatePreview();
        _suffixBox.TextChanged += (_, _) => UpdatePreview();
        _numberBaseBox.TextChanged += (_, _) => UpdatePreview();
        _startAtBox.TextChanged += (_, _) => UpdatePreview();
        _matchCaseCheck.Click += (_, _) => UpdatePreview();
        _caseCombo.SelectionChanged += (_, _) => UpdatePreview();

        UpdatePreview();
    }

    private Dictionary<string, string> ComputeNames()
    {
        var result = new Dictionary<string, string>();
        int.TryParse(_startAtBox.Text, out var startNum);
        if (startNum <= 0) startNum = 1;

        for (int i = 0; i < _targets.Count; i++)
        {
            var target = _targets[i];
            var dir = Path.GetDirectoryName(target) ?? "";
            var isDir = Directory.Exists(target);
            var ext = isDir ? "" : Path.GetExtension(target);
            var baseName = isDir ? Path.GetFileName(target.TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(target);

            string newBase = baseName;

            switch (_mode)
            {
                case 0: // Find & Replace
                    if (!string.IsNullOrEmpty(_findBox.Text))
                    {
                        var opt = _matchCaseCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
                        newBase = Regex.Replace(baseName, Regex.Escape(_findBox.Text), _replaceBox.Text ?? "", opt);
                    }
                    break;
                case 1: // Prefix / Suffix
                    newBase = $"{_prefixBox.Text}{baseName}{_suffixBox.Text}";
                    break;
                case 2: // Numbering
                    var b = string.IsNullOrWhiteSpace(_numberBaseBox.Text) ? baseName : _numberBaseBox.Text;
                    newBase = $"{b}_{startNum + i:D2}";
                    break;
                case 3: // Case
                    newBase = _caseCombo.SelectedIndex switch
                    {
                        0 => baseName.ToLowerInvariant(),
                        1 => baseName.ToUpperInvariant(),
                        _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(baseName.ToLowerInvariant())
                    };
                    break;
            }

            result[target] = Path.Combine(dir, newBase + ext);
        }
        return result;
    }

    private void UpdatePreview()
    {
        _previewList.Children.Clear();
        var map = ComputeNames();

        foreach (var (oldPath, newPath) in map)
        {
            var oldName = Path.GetFileName(oldPath.TrimEnd('\\', '/'));
            var newName = Path.GetFileName(newPath.TrimEnd('\\', '/'));
            var isChanged = !string.Equals(oldName, newName, StringComparison.Ordinal);

            var row = new DockPanel { Margin = new Thickness(8, 4, 8, 4) };
            var origText = UI.Text(oldName, 11.5, color: Theme.Ink);
            var newText = UI.Text(newName, 11.5, FontWeights.Medium, isChanged ? Theme.Accent : Theme.Muted);

            DockPanel.SetDock(origText, Dock.Left);
            DockPanel.SetDock(newText, Dock.Right);
            row.Children.Add(origText);
            row.Children.Add(newText);
            _previewList.Children.Add(row);
        }
    }

    private void Apply()
    {
        var map = ComputeNames();
        var renamed = new Dictionary<string, string>();

        // Rename via temporary names to avoid collision loops
        var tempMap = new Dictionary<string, string>();
        foreach (var (oldPath, newPath) in map)
        {
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) continue;
            var temp = oldPath + $".adawat_tmp_{Guid.NewGuid():N}";
            try
            {
                if (Directory.Exists(oldPath)) Directory.Move(oldPath, temp);
                else if (File.Exists(oldPath)) File.Move(oldPath, temp);
                tempMap[temp] = newPath;
            }
            catch { }
        }

        foreach (var (temp, newPath) in tempMap)
        {
            try
            {
                var finalTarget = Output.Unique(newPath);
                if (Directory.Exists(temp)) Directory.Move(temp, finalTarget);
                else if (File.Exists(temp)) File.Move(temp, finalTarget);
                var orig = map.First(kv => kv.Value == newPath).Key;
                renamed[orig] = finalTarget;
            }
            catch { }
        }

        if (renamed.Count > 0)
        {
            _onRenamed(renamed);
            Hud.Shared.ShowMessage($"Renamed {renamed.Count} files", Hud.Style.Success);
        }
        Close();
    }
}

/// <summary>Properties Window displaying size, metadata, and breakdowns.</summary>
internal sealed class PropertiesWindow : Window
{
    public PropertiesWindow(Window owner, IReadOnlyList<string> targets)
    {
        Owner = owner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = Theme.Font;
        FlowDirection = Theme.Direction;

        var stack = new StackPanel { Margin = new Thickness(22) };

        // Header
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16), LastChildFill = false };
        var title = UI.Text("Properties", 15, FontWeights.Bold, Theme.Ink);
        DockPanel.SetDock(title, Dock.Left);
        var closeBtn = UI.IconButton("", Close, "Close", 24);
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(title);
        header.Children.Add(closeBtn);
        stack.Children.Add(header);

        long totalBytes = 0;
        int fileCount = 0;
        int dirCount = 0;
        int images = 0, videos = 0, audio = 0, documents = 0, others = 0;

        foreach (var path in targets)
        {
            if (File.Exists(path))
            {
                fileCount++;
                var info = new FileInfo(path);
                totalBytes += info.Length;
                var ext = info.Extension.ToLowerInvariant().TrimStart('.');
                if (ImageFormat.All.Any(f => f.Ext == ext) || ext is "png" or "jpg" or "jpeg" or "webp" or "gif") images++;
                else if (MediaFormat.Video.Any(f => f.Ext == ext) || ext is "mp4" or "mkv" or "mov" or "avi") videos++;
                else if (MediaFormat.Audio.Any(f => f.Ext == ext) || ext is "mp3" or "wav" or "aac" or "m4a") audio++;
                else if (ext is "pdf" or "docx" or "doc" or "xlsx" or "pptx" or "txt" or "md") documents++;
                else others++;
            }
            else if (Directory.Exists(path))
            {
                dirCount++;
            }
        }

        void Row(string label, string value)
        {
            var p = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var l = UI.Text(label, 12, color: Theme.Muted);
            var v = UI.Text(value, 12, FontWeights.Medium, Theme.Ink);
            DockPanel.SetDock(l, Dock.Left);
            DockPanel.SetDock(v, Dock.Right);
            p.Children.Add(l);
            p.Children.Add(v);
            stack.Children.Add(p);
        }

        if (targets.Count == 1)
        {
            var path = targets[0];
            var name = Path.GetFileName(path.TrimEnd('\\', '/'));
            Row("Name", name);
            Row("Location", Path.GetDirectoryName(path) ?? "");

            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                Row("Size", FloatDialogs.FormatSize(fi.Length));
                Row("Type", fi.Extension.ToUpperInvariant() + " file");
                Row("Created", fi.CreationTime.ToString("g"));
                Row("Modified", fi.LastWriteTime.ToString("g"));

                // Image dimensions if image
                try
                {
                    if (images > 0)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path);
                        bitmap.EndInit();
                        Row("Dimensions", $"{bitmap.PixelWidth} × {bitmap.PixelHeight} pixels");
                    }
                }
                catch { }
            }
            else if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                Row("Type", "File folder");
                Row("Created", di.CreationTime.ToString("g"));
            }
        }
        else
        {
            Row("Selected Items", $"{targets.Count} items ({fileCount} files, {dirCount} folders)");
            Row("Total Size", FloatDialogs.FormatSize(totalBytes));
            stack.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
            Row("Images", $"{images}");
            Row("Videos", $"{videos}");
            Row("Audio", $"{audio}");
            Row("Documents", $"{documents}");
            Row("Others", $"{others}");
        }

        stack.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 14) });
        var okBtn = UI.Button("Close", Close, primary: true);
        okBtn.HorizontalAlignment = HorizontalAlignment.Right;
        stack.Children.Add(okBtn);

        Content = new Border
        {
            Child = stack,
            CornerRadius = new CornerRadius(16),
            Background = Brushes.White,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.2 },
        };
    }
}
