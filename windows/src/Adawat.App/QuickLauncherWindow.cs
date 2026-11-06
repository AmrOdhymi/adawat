using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Adawat.Core;
using Microsoft.Win32;

namespace Adawat.App;

public sealed class ShortcutEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string Type { get; set; } = "App"; // "App", "Folder", "Web"
}

/// <summary>
/// A centered launcher window for applications, folders, and web links (similar to PowerToys Run),
/// with real-time search, execution, and adding/removing custom shortcuts.
/// </summary>
public static class QuickLauncherWindow
{
    private static ToolWindow? _open;
    private static readonly string FilePath = Path.Combine(Settings.Folder, "shortcuts.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Present()
    {
        if (_open != null) { _open.Activate(); return; }
        _open = Make();
        _open.Closed += (_, _) => _open = null;
        _open.Present();
    }

    public static ToolWindow Make()
    {
        var window = new ToolWindow("Quick Launcher", 560);
        var shortcuts = LoadShortcuts();

        var root = new StackPanel();

        // ── Search & Action Bar ──
        var searchBox = new TextBox
        {
            Height = 36,
            FontSize = 13.5,
            Padding = new Thickness(10, 4, 10, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = Theme.Font,
            FlowDirection = Theme.Direction,
        };

        // ── Shortcuts Container ──
        var itemsPanel = new StackPanel();

        void RefreshList(string filter = "")
        {
            itemsPanel.Children.Clear();
            var query = filter.Trim();
            var matches = string.IsNullOrEmpty(query)
                ? shortcuts
                : shortcuts.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                    || s.Target.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                var empty = UI.Caption("No matching shortcuts found.");
                empty.Margin = new Thickness(12, 24, 12, 24);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                itemsPanel.Children.Add(empty);
                return;
            }

            foreach (var item in matches)
            {
                itemsPanel.Children.Add(CreateShortcutCard(item, window, () =>
                {
                    shortcuts.Remove(item);
                    SaveShortcuts(shortcuts);
                    RefreshList(searchBox.Text);
                }));
            }
        }

        var addBtn = UI.Button("Add Shortcut", () => ShowAddDialog(window, () => RefreshList(searchBox.Text)), primary: false, icon: "");
        addBtn.Margin = new Thickness(8, 0, 0, 0);

        var topRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(addBtn, Dock.Right);
        topRow.Children.Add(addBtn);
        topRow.Children.Add(searchBox);
        root.Children.Add(topRow);

        var scroll = new ScrollViewer
        {
            Content = itemsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
            MinHeight = 160,
        };
        root.Children.Add(scroll);

        searchBox.TextChanged += (_, _) => RefreshList(searchBox.Text);

        searchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                var query = searchBox.Text.Trim();
                var first = shortcuts.FirstOrDefault(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                                       || s.Target.Contains(query, StringComparison.OrdinalIgnoreCase));
                if (first != null)
                {
                    Launch(first, window);
                }
                else if (!string.IsNullOrEmpty(query))
                {
                    // If user typed a direct URL or path, run it directly
                    Launch(new ShortcutEntry { Name = query, Target = query, Type = DetectType(query) }, window);
                }
                e.Handled = true;
            }
        };

        RefreshList();
        window.Body = root;

        window.Trailing(UI.Button("Close", window.Close, primary: true));

        window.Loaded += (_, _) => searchBox.Focus();
        return window;
    }

    private static Border CreateShortcutCard(ShortcutEntry item, Window window, Action onDelete)
    {
        var row = new DockPanel { LastChildFill = true };

        var iconGlyph = item.Type switch
        {
            "Folder" => Catalog.Icons.Folder,
            "Web" => "",
            _ => ""
        };

        var icon = UI.Icon(iconGlyph, 18, Theme.Accent);
        icon.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);

        var delBtn = UI.IconButton("", onDelete, "Delete", 26);
        DockPanel.SetDock(delBtn, Dock.Right);
        row.Children.Add(delBtn);

        var launchBtn = UI.IconButton("", () => Launch(item, window), "Launch", 26);
        launchBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(launchBtn, Dock.Right);
        row.Children.Add(launchBtn);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = UI.Text(item.Name, 13, FontWeights.SemiBold, Theme.Ink);
        var target = UI.Text(item.Target, 11, color: Theme.Muted);
        target.MaxWidth = 360;
        target.TextTrimming = TextTrimming.CharacterEllipsis;
        info.Children.Add(name);
        info.Children.Add(target);
        row.Children.Add(info);

        var card = new Border
        {
            Child = row,
            Background = Theme.ButtonFill,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 3, 0, 3),
            Cursor = Cursors.Hand,
        };

        card.MouseEnter += (_, _) => card.Background = Theme.ButtonPressed;
        card.MouseLeave += (_, _) => card.Background = Theme.ButtonFill;
        card.MouseLeftButtonUp += (_, _) => Launch(item, window);

        return card;
    }

    private static void Launch(ShortcutEntry item, Window window)
    {
        try
        {
            var target = item.Target;
            if (item.Type == "Web" && !target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                target = "https://" + target;
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            Sounds.Tick();
            window.Close();
        }
        catch (Exception ex)
        {
            Log.Write($"Launcher execution error: {ex}");
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
        }
    }

    private static void ShowAddDialog(Window owner, Action onAdded)
    {
        var dialog = new ToolWindow("Add Shortcut", 460) { Owner = owner };
        var stack = new StackPanel();

        var nameBox = new TextBox { Height = 28, Margin = new Thickness(0, 4, 0, 8), VerticalContentAlignment = VerticalAlignment.Center };
        stack.Children.Add(UI.Row("Name", nameBox));

        var targetBox = new TextBox { Height = 28, Margin = new Thickness(0, 4, 0, 8), VerticalContentAlignment = VerticalAlignment.Center };
        var browseRow = new DockPanel { LastChildFill = true };

        var browseFile = UI.Button("Browse File...", () =>
        {
            var dlg = new OpenFileDialog { Title = L10n.T("Select application or file") };
            if (dlg.ShowDialog() == true)
            {
                targetBox.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                    nameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        });
        browseFile.Margin = new Thickness(6, 0, 0, 0);

        var browseFolder = UI.Button("Browse Folder...", () =>
        {
            var dlg = new OpenFolderDialog { Title = L10n.T("Select folder") };
            if (dlg.ShowDialog() == true)
            {
                targetBox.Text = dlg.FolderName;
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                    nameBox.Text = Path.GetFileName(dlg.FolderName);
            }
        });
        browseFolder.Margin = new Thickness(6, 0, 0, 0);

        DockPanel.SetDock(browseFolder, Dock.Right);
        DockPanel.SetDock(browseFile, Dock.Right);
        browseRow.Children.Add(browseFolder);
        browseRow.Children.Add(browseFile);
        browseRow.Children.Add(targetBox);

        stack.Children.Add(UI.Row("Target / URL", browseRow));

        dialog.Body = stack;

        dialog.Leading(UI.Button("Cancel", dialog.Close));
        dialog.Trailing(UI.Button("Add", () =>
        {
            var name = nameBox.Text.Trim();
            var target = targetBox.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(target))
            {
                Hud.Shared.ShowMessage(L10n.T("Please fill both name and target"), Hud.Style.Failure);
                return;
            }

            var type = DetectType(target);
            var shortcuts = LoadShortcuts();
            shortcuts.Add(new ShortcutEntry { Name = name, Target = target, Type = type });
            SaveShortcuts(shortcuts);

            dialog.Close();
            onAdded();
        }, primary: true));

        dialog.Present();
    }

    private static string DetectType(string target)
    {
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return "Web";

        if (Directory.Exists(target)) return "Folder";
        return "App";
    }

    private static List<ShortcutEntry> LoadShortcuts()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<ShortcutEntry>>(File.ReadAllText(FilePath), JsonOptions);
                if (list != null && list.Count > 0) return list;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to load shortcuts: {ex}");
        }

        // Default initial shortcuts
        var defaults = new List<ShortcutEntry>
        {
            new() { Name = "File Explorer", Target = "explorer.exe", Type = "App" },
            new() { Name = "Downloads", Target = Output.Downloads, Type = "Folder" },
            new() { Name = "Desktop", Target = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Type = "Folder" },
            new() { Name = "Google", Target = "https://www.google.com", Type = "Web" },
            new() { Name = "Windows Settings", Target = "ms-settings:", Type = "App" },
        };
        SaveShortcuts(defaults);
        return defaults;
    }

    private static void SaveShortcuts(List<ShortcutEntry> list)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to save shortcuts: {ex}");
        }
    }
}
