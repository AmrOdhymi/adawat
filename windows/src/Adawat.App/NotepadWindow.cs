using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Adawat.Core;
using Microsoft.Win32;

namespace Adawat.App;

public sealed class NoteEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// A fast notepad tool supporting pinning to top, searching, copying, editing,
/// expanding in a dedicated view, and exporting as .txt or .md.
/// </summary>
public static class NotepadWindow
{
    private static ToolWindow? _open;
    private static readonly string FilePath = Path.Combine(Settings.Folder, "notes.json");
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
        var window = new ToolWindow("Notepad", 580);
        var notes = LoadNotes();

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
                ? notes
                : notes.Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || n.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            // Order: Pinned first, then newest updated
            var sorted = matches.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt).ToList();

            if (sorted.Count == 0)
            {
                var empty = UI.Caption(string.IsNullOrEmpty(query) ? "No notes yet. Click 'New Note' to create one." : "No matching notes found.");
                empty.Margin = new Thickness(12, 28, 12, 28);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                itemsPanel.Children.Add(empty);
                return;
            }

            foreach (var note in sorted)
            {
                itemsPanel.Children.Add(CreateNoteCard(note, window,
                    onEdit: () => OpenEditor(window, note, () => { notes = LoadNotes(); RefreshList(searchBox.Text); }),
                    onDelete: () =>
                    {
                        notes.Remove(note);
                        SaveNotes(notes);
                        RefreshList(searchBox.Text);
                    },
                    onTogglePin: () =>
                    {
                        note.IsPinned = !note.IsPinned;
                        note.UpdatedAt = DateTime.Now;
                        SaveNotes(notes);
                        RefreshList(searchBox.Text);
                    }));
            }
        }

        var newBtn = UI.Button("New Note", () => OpenEditor(window, null, () => { notes = LoadNotes(); RefreshList(searchBox.Text); }), primary: true, icon: "");
        newBtn.Margin = new Thickness(8, 0, 0, 0);

        var topRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(newBtn, Dock.Right);
        topRow.Children.Add(newBtn);
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

        RefreshList();
        window.Body = root;

        window.Trailing(UI.Button("Close", window.Close, primary: false));
        window.Loaded += (_, _) => searchBox.Focus();
        return window;
    }

    private static Border CreateNoteCard(NoteEntry note, Window owner, Action onEdit, Action onDelete, Action onTogglePin)
    {
        var row = new DockPanel { LastChildFill = true };

        // Pin button on right
        var pinIcon = note.IsPinned ? "" : ""; // Pin glyphs
        var pinBtn = UI.IconButton(pinIcon, onTogglePin, note.IsPinned ? "Unpin" : "Pin to top", 26);
        DockPanel.SetDock(pinBtn, Dock.Right);
        row.Children.Add(pinBtn);

        // Copy button
        var copyBtn = UI.IconButton("", () =>
        {
            var text = string.IsNullOrWhiteSpace(note.Title) ? note.Content : $"{note.Title}\n\n{note.Content}";
            Adawat.Core.Clipboard.CopyText(text);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Note copied to clipboard"), Hud.Style.Success);
        }, "Copy Note", 26);
        copyBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(copyBtn, Dock.Right);
        row.Children.Add(copyBtn);

        // Preview Markdown button
        var previewBtn = UI.IconButton("", () => MarkdownViewer.OpenPreview(string.IsNullOrWhiteSpace(note.Title) ? "Note" : note.Title, note.Content, owner), "Preview Markdown", 26);
        previewBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(previewBtn, Dock.Right);
        row.Children.Add(previewBtn);

        // Export button
        var exportBtn = UI.IconButton("", () => ExportNote(note, owner), "Export", 26);
        exportBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(exportBtn, Dock.Right);
        row.Children.Add(exportBtn);

        // Delete button
        var delBtn = UI.IconButton("", onDelete, "Delete", 26);
        delBtn.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(delBtn, Dock.Right);
        row.Children.Add(delBtn);

        // Pin badge if pinned
        if (note.IsPinned)
        {
            var pinBadge = UI.Icon("", 13, Theme.Accent);
            pinBadge.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(pinBadge, Dock.Left);
            row.Children.Add(pinBadge);
        }

        // Details
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var isArabic = (note.Title + " " + note.Content).Any(c => c >= 0x0600 && c <= 0x06FF);
        var displayTitle = string.IsNullOrWhiteSpace(note.Title) ? L10n.T("Untitled Note") : note.Title;
        var titleBlock = UI.Text(displayTitle, 13, FontWeights.SemiBold, Theme.Ink);
        titleBlock.MaxWidth = 340;
        titleBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        titleBlock.FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var snippet = note.Content.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        var snippetBlock = UI.Text(string.IsNullOrEmpty(snippet) ? "..." : snippet, 11, color: Theme.Muted);
        snippetBlock.MaxWidth = 340;
        snippetBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        snippetBlock.FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        info.Children.Add(titleBlock);
        info.Children.Add(snippetBlock);
        row.Children.Add(info);

        var card = new Border
        {
            Child = row,
            Background = note.IsPinned ? Theme.AccentSoft : Theme.ButtonFill,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 3, 0, 3),
            Cursor = Cursors.Hand,
        };

        card.MouseEnter += (_, _) => card.Background = Theme.ButtonPressed;
        card.MouseLeave += (_, _) => card.Background = note.IsPinned ? Theme.AccentSoft : Theme.ButtonFill;
        card.MouseLeftButtonUp += (_, _) => onEdit();

        return card;
    }

    private static void OpenEditor(Window owner, NoteEntry? note, Action onSaved)
    {
        var isNew = note == null;
        var current = note ?? new NoteEntry();

        var editor = new ToolWindow(isNew ? "New Note" : "Edit Note", 620) { Owner = owner };
        var stack = new StackPanel();

        var isInitialArabic = (current.Title + " " + current.Content).Any(c => c >= 0x0600 && c <= 0x06FF);
        var initialDir = isInitialArabic ? FlowDirection.RightToLeft : (L10n.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);

        var titleBox = new TextBox
        {
            Text = current.Title,
            Height = 32,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = Theme.Font,
            FlowDirection = initialDir,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(UI.Row("Title", titleBox));

        var contentBox = new TextBox
        {
            Text = current.Content,
            Height = 280,
            FontSize = 12.5,
            Padding = new Thickness(8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = Theme.Font,
            FlowDirection = initialDir,
            Margin = new Thickness(0, 4, 0, 8)
        };
        stack.Children.Add(contentBox);

        void AutoDetectDirection(TextBox box)
        {
            var first = box.Text.FirstOrDefault(char.IsLetter);
            if (first != default)
            {
                box.FlowDirection = (first >= 0x0600 && first <= 0x06FF) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }
        }

        titleBox.TextChanged += (_, _) => AutoDetectDirection(titleBox);
        contentBox.TextChanged += (_, _) => AutoDetectDirection(contentBox);

        editor.Body = stack;

        var dirBtn = UI.Button("RTL / LTR", () =>
        {
            var next = contentBox.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            contentBox.FlowDirection = next;
            titleBox.FlowDirection = next;
        }, primary: false, icon: "");

        editor.Leading(
            UI.Button("Cancel", editor.Close),
            dirBtn,
            UI.Button("Copy", () =>
            {
                var text = string.IsNullOrWhiteSpace(titleBox.Text) ? contentBox.Text : $"{titleBox.Text}\n\n{contentBox.Text}";
                Adawat.Core.Clipboard.CopyText(text);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Note copied to clipboard"), Hud.Style.Success);
            }, primary: false, icon: ""),
            UI.Button("Preview...", () =>
            {
                var title = string.IsNullOrWhiteSpace(titleBox.Text) ? "Note Preview" : titleBox.Text;
                MarkdownViewer.OpenPreview(title, contentBox.Text, editor);
            }, primary: false, icon: ""),
            UI.Button("Export...", () =>
            {
                var temp = new NoteEntry { Title = titleBox.Text, Content = contentBox.Text };
                ExportNote(temp, editor);
            }, primary: false, icon: "")
        );

        editor.Trailing(UI.Button("Save", () =>
        {
            current.Title = titleBox.Text.Trim();
            current.Content = contentBox.Text;
            current.UpdatedAt = DateTime.Now;

            var all = LoadNotes();
            if (isNew)
            {
                all.Add(current);
            }
            else
            {
                var index = all.FindIndex(n => n.Id == current.Id);
                if (index >= 0) all[index] = current;
                else all.Add(current);
            }

            SaveNotes(all);
            Sounds.Tick();
            editor.Close();
            onSaved();
        }, primary: true));

        editor.Present();
        editor.Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(titleBox.Text)) titleBox.Focus();
            else contentBox.Focus();
        };
    }

    private static void ExportNote(NoteEntry note, Window owner)
    {
        var defaultName = string.IsNullOrWhiteSpace(note.Title) ? "Note" : note.Title;
        // Clean invalid filename characters
        foreach (var c in Path.GetInvalidFileNameChars()) defaultName = defaultName.Replace(c, '_');

        var dlg = new SaveFileDialog
        {
            Title = L10n.T("Export Note"),
            FileName = defaultName,
            Filter = "Markdown Document (*.md)|*.md|Text Document (*.txt)|*.txt",
            DefaultExt = ".md"
        };

        if (dlg.ShowDialog(owner) == true)
        {
            try
            {
                var content = dlg.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(note.Title)
                    ? $"# {note.Title}\n\n{note.Content}"
                    : (string.IsNullOrWhiteSpace(note.Title) ? note.Content : $"{note.Title}\n\n{note.Content}");

                File.WriteAllText(dlg.FileName, content, System.Text.Encoding.UTF8);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Note exported successfully"), Hud.Style.Success);
            }
            catch (Exception ex)
            {
                Log.Write($"Export note error: {ex}");
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        }
    }

    private static List<NoteEntry> LoadNotes()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<NoteEntry>>(File.ReadAllText(FilePath), JsonOptions);
                if (list != null) return list;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to load notes: {ex}");
        }

        return [];
    }

    private static void SaveNotes(List<NoteEntry> list)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to save notes: {ex}");
        }
    }
}
