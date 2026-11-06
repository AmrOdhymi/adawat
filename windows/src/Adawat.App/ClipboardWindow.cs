using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Adawat.Core;

namespace Adawat.App;

public sealed class ClipboardEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public bool IsPinned { get; set; }
    public DateTime CopiedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Service managing persistent infinite clipboard history and live updates.
/// </summary>
public static class ClipboardHistory
{
    private static readonly string FilePath = Path.Combine(Settings.Folder, "clipboard.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static bool _suppressUpdate;

    public static event Action? Changed;

    public static void SuppressNext() => _suppressUpdate = true;

    public static void OnClipboardUpdated()
    {
        if (_suppressUpdate)
        {
            _suppressUpdate = false;
            return;
        }

        // Small delay so the copying application can completely release the clipboard lock
        Task.Delay(60).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    var text = Adawat.Core.Clipboard.Text();
                    if (string.IsNullOrWhiteSpace(text)) return;

                    var list = Load();
                    if (list.Count > 0 && list[0].Text == text) return;

                    var existing = list.FirstOrDefault(e => e.Text == text);
                    if (existing != null && !existing.IsPinned)
                    {
                        list.Remove(existing);
                    }

                    if (existing == null)
                    {
                        existing = new ClipboardEntry { Text = text, CopiedAt = DateTime.Now };
                    }
                    else
                    {
                        existing.CopiedAt = DateTime.Now;
                    }

                    list.Insert(0, existing);
                    Save(list);

                    Changed?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Write($"Clipboard history update error: {ex}");
                }
            });
        });
    }

    public static List<ClipboardEntry> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<ClipboardEntry>>(File.ReadAllText(FilePath), JsonOptions);
                if (list != null) return list;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to load clipboard history: {ex}");
        }
        return [];
    }

    public static void Save(List<ClipboardEntry> list)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to save clipboard history: {ex}");
        }
    }
}

/// <summary>
/// A full-featured infinite clipboard manager with pinning, editing, searching,
/// markdown preview, and exporting to .txt/.md.
/// </summary>
public static class ClipboardWindow
{
    private static ToolWindow? _open;

    public static void Present()
    {
        if (_open != null) { _open.Activate(); return; }
        _open = Make();
        _open.Closed += (_, _) => _open = null;
        _open.Present();
    }

    public static ToolWindow Make()
    {
        var window = new ToolWindow("Clipboard History", 580);
        var entries = ClipboardHistory.Load();

        var root = new StackPanel();

        // ── Search & Actions Bar ──
        var searchBox = new TextBox
        {
            Height = 36,
            FontSize = 13.5,
            Padding = new Thickness(10, 4, 10, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = Theme.Font,
            FlowDirection = Theme.Direction,
        };

        var itemsPanel = new StackPanel();

        void RefreshList(string filter = "")
        {
            itemsPanel.Children.Clear();
            var query = filter.Trim();
            var matches = string.IsNullOrEmpty(query)
                ? entries
                : entries.Where(e => e.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            // Pinned first, then newest copied
            var sorted = matches.OrderByDescending(e => e.IsPinned).ThenByDescending(e => e.CopiedAt).ToList();

            if (sorted.Count == 0)
            {
                var empty = UI.Caption(string.IsNullOrEmpty(query) ? "Clipboard history is empty. Copy text anywhere to see it here." : "No matching items found.");
                empty.Margin = new Thickness(12, 28, 12, 28);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                itemsPanel.Children.Add(empty);
                return;
            }

            foreach (var item in sorted)
            {
                itemsPanel.Children.Add(CreateEntryCard(item, window,
                    onCopy: () =>
                    {
                        ClipboardHistory.SuppressNext();
                        Adawat.Core.Clipboard.CopyText(item.Text);
                        Sounds.Tick();
                        Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
                    },
                    onTogglePin: () =>
                    {
                        item.IsPinned = !item.IsPinned;
                        ClipboardHistory.Save(entries);
                        RefreshList(searchBox.Text);
                    },
                    onDelete: () =>
                    {
                        entries.Remove(item);
                        ClipboardHistory.Save(entries);
                        RefreshList(searchBox.Text);
                    },
                    onEdit: () => OpenEditDialog(window, item, () =>
                    {
                        ClipboardHistory.Save(entries);
                        RefreshList(searchBox.Text);
                    })));
            }
        }

        var addBtn = UI.Button("Add", () => OpenAddDialog(window, () =>
        {
            entries = ClipboardHistory.Load();
            RefreshList(searchBox.Text);
        }), primary: false, icon: "");
        addBtn.Margin = new Thickness(8, 0, 0, 0);

        var clearBtn = UI.Button("Clear History", () =>
        {
            entries.RemoveAll(e => !e.IsPinned);
            ClipboardHistory.Save(entries);
            RefreshList(searchBox.Text);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Cleared non-pinned history"), Hud.Style.Success);
        }, primary: false, icon: "");
        clearBtn.Margin = new Thickness(6, 0, 0, 0);

        var topRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(clearBtn, Dock.Right);
        DockPanel.SetDock(addBtn, Dock.Right);
        topRow.Children.Add(clearBtn);
        topRow.Children.Add(addBtn);
        topRow.Children.Add(searchBox);
        root.Children.Add(topRow);

        var scroll = new ScrollViewer
        {
            Content = itemsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
            MinHeight = 180,
        };
        root.Children.Add(scroll);

        searchBox.TextChanged += (_, _) => RefreshList(searchBox.Text);

        void OnLiveUpdate()
        {
            entries = ClipboardHistory.Load();
            RefreshList(searchBox.Text);
        }

        ClipboardHistory.Changed += OnLiveUpdate;

        window.Activated += (_, _) =>
        {
            entries = ClipboardHistory.Load();
            RefreshList(searchBox.Text);
        };

        window.SourceInitialized += (_, _) =>
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            Native.AddClipboardFormatListener(helper.Handle);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == Native.WM_CLIPBOARDUPDATE)
                {
                    ClipboardHistory.OnClipboardUpdated();
                }
                return IntPtr.Zero;
            });
        };

        window.Closed += (_, _) =>
        {
            ClipboardHistory.Changed -= OnLiveUpdate;
            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
                Native.RemoveClipboardFormatListener(helper.Handle);
        };

        RefreshList();
        window.Body = root;

        window.Trailing(UI.Button("Close", window.Close, primary: false));
        window.Loaded += (_, _) => searchBox.Focus();
        return window;
    }

    private static Border CreateEntryCard(ClipboardEntry item, Window owner, Action onCopy, Action onTogglePin, Action onDelete, Action onEdit)
    {
        var row = new DockPanel { LastChildFill = true };

        // Right Action Buttons
        var pinIcon = item.IsPinned ? "" : "";
        var pinBtn = UI.IconButton(pinIcon, onTogglePin, item.IsPinned ? "Unpin" : "Pin to top", 26);
        DockPanel.SetDock(pinBtn, Dock.Right);
        row.Children.Add(pinBtn);

        var copyBtn = UI.IconButton("", onCopy, "Copy", 26);
        copyBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(copyBtn, Dock.Right);
        row.Children.Add(copyBtn);

        var previewBtn = UI.IconButton("", () => MarkdownViewer.OpenPreview("Clipboard Preview", item.Text, owner), "Preview Markdown", 26);
        previewBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(previewBtn, Dock.Right);
        row.Children.Add(previewBtn);

        var exportBtn = UI.IconButton("", () => MarkdownViewer.Export("Clipboard Entry", item.Text, owner), "Export (.txt / .md)", 26);
        exportBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(exportBtn, Dock.Right);
        row.Children.Add(exportBtn);

        var editBtn = UI.IconButton("", onEdit, "Edit", 26);
        editBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(editBtn, Dock.Right);
        row.Children.Add(editBtn);

        var delBtn = UI.IconButton("", onDelete, "Delete", 26);
        delBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(delBtn, Dock.Right);
        row.Children.Add(delBtn);

        // Pin badge
        if (item.IsPinned)
        {
            var pinBadge = UI.Icon("", 13, Theme.Accent);
            pinBadge.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(pinBadge, Dock.Left);
            row.Children.Add(pinBadge);
        }

        // Details
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var isArabic = item.Text.Any(c => c >= 0x0600 && c <= 0x06FF);
        var textPreview = item.Text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        var textBlock = UI.Text(string.IsNullOrEmpty(textPreview) ? "..." : textPreview, 12.5, FontWeights.Medium, Theme.Ink);
        textBlock.MaxWidth = 270;
        textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        textBlock.FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var meta = UI.Text($"{item.CopiedAt:g} • {item.Text.Length} chars", 10.5, color: Theme.Muted);
        info.Children.Add(textBlock);
        info.Children.Add(meta);
        row.Children.Add(info);

        var card = new Border
        {
            Child = row,
            Background = item.IsPinned ? Theme.AccentSoft : Theme.ButtonFill,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 3, 0, 3),
            Cursor = Cursors.Hand,
        };

        card.MouseEnter += (_, _) => card.Background = Theme.ButtonPressed;
        card.MouseLeave += (_, _) => card.Background = item.IsPinned ? Theme.AccentSoft : Theme.ButtonFill;
        card.MouseLeftButtonUp += (_, _) => onCopy();

        return card;
    }

    private static void OpenAddDialog(Window owner, Action onAdded)
    {
        var dialog = new ToolWindow("Add to Clipboard", 520) { Owner = owner };
        var stack = new StackPanel();

        var initialDir = L10n.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var textBox = new TextBox
        {
            Height = 220,
            FontSize = 13,
            Padding = new Thickness(8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = Theme.Font,
            FlowDirection = initialDir,
            Margin = new Thickness(0, 4, 0, 8)
        };

        textBox.TextChanged += (_, _) =>
        {
            var first = textBox.Text.FirstOrDefault(char.IsLetter);
            if (first != default)
            {
                textBox.FlowDirection = (first >= 0x0600 && first <= 0x06FF) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }
        };

        stack.Children.Add(textBox);

        dialog.Body = stack;
        dialog.Leading(
            UI.Button("Cancel", dialog.Close),
            UI.Button("RTL / LTR", () =>
            {
                textBox.FlowDirection = textBox.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }, primary: false, icon: "")
        );
        dialog.Trailing(UI.Button("Add", () =>
        {
            var text = textBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            var list = ClipboardHistory.Load();
            list.Insert(0, new ClipboardEntry { Text = text, CopiedAt = DateTime.Now });
            ClipboardHistory.Save(list);
            Sounds.Tick();
            dialog.Close();
            onAdded();
        }, primary: true));

        dialog.Present();
        dialog.Loaded += (_, _) => textBox.Focus();
    }

    private static void OpenEditDialog(Window owner, ClipboardEntry entry, Action onSaved)
    {
        var dialog = new ToolWindow("Edit Clipboard Entry", 540) { Owner = owner };
        var stack = new StackPanel();

        var isInitialArabic = entry.Text.Any(c => c >= 0x0600 && c <= 0x06FF);
        var initialDir = isInitialArabic ? FlowDirection.RightToLeft : (L10n.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);

        var textBox = new TextBox
        {
            Text = entry.Text,
            Height = 240,
            FontSize = 13,
            Padding = new Thickness(8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = Theme.Font,
            FlowDirection = initialDir,
            Margin = new Thickness(0, 4, 0, 8)
        };

        textBox.TextChanged += (_, _) =>
        {
            var first = textBox.Text.FirstOrDefault(char.IsLetter);
            if (first != default)
            {
                textBox.FlowDirection = (first >= 0x0600 && first <= 0x06FF) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }
        };

        stack.Children.Add(textBox);

        dialog.Body = stack;
        dialog.Leading(
            UI.Button("Cancel", dialog.Close),
            UI.Button("RTL / LTR", () =>
            {
                textBox.FlowDirection = textBox.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }, primary: false, icon: ""),
            UI.Button("Preview...", () => MarkdownViewer.OpenPreview("Edit Preview", textBox.Text, dialog), primary: false, icon: "")
        );

        dialog.Trailing(UI.Button("Save", () =>
        {
            entry.Text = textBox.Text;
            dialog.Close();
            onSaved();
            Sounds.Tick();
        }, primary: true));

        dialog.Present();
        dialog.Loaded += (_, _) => textBox.Focus();
    }
}
