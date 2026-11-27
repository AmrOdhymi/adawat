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
        if (_open != null) { WindowLifecycle.ActivateIfOpen(_open); return; }
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
        var visibleCards = new List<(string Id, Border Card, Action Open)>();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        string? primaryId = null;
        const int pageSize = 20;
        var unpinnedPage = 1;
        var loadMoreBtn = UI.Button("Load more", () => { }, primary: false);
        loadMoreBtn.Margin = new Thickness(0, 8, 0, 0);
        loadMoreBtn.Visibility = Visibility.Collapsed;

        bool IsSelected(string id) => selectedIds.Contains(id);

        void PaintCard(Border card, string id, bool pinned)
        {
            var selected = IsSelected(id);
            card.Background = selected ? Theme.AccentSoft : (pinned ? Theme.AccentSoft : Theme.ButtonFill);
            card.BorderBrush = selected ? Theme.LineStrong : Brushes.Transparent;
        }

        void RefreshCardStyles()
        {
            foreach (var child in itemsPanel.Children)
            {
                if (child is not Border c || c.Tag is not ValueTuple<string, bool> tag) continue;
                PaintCard(c, tag.Item1, tag.Item2);
            }
        }

        void SelectOnly(string id)
        {
            selectedIds.Clear();
            selectedIds.Add(id);
            primaryId = id;
            RefreshCardStyles();
        }

        void ToggleSelect(string id)
        {
            if (!selectedIds.Add(id)) selectedIds.Remove(id);
            primaryId = selectedIds.Contains(id) ? id : selectedIds.FirstOrDefault();
            RefreshCardStyles();
        }

        List<NoteEntry> SelectedNotesInView()
        {
            var order = visibleCards.Select((v, i) => (v.Id, i)).ToDictionary(x => x.Id, x => x.i);
            return notes.Where(n => selectedIds.Contains(n.Id))
                .OrderBy(n => order.TryGetValue(n.Id, out var i) ? i : int.MaxValue)
                .ToList();
        }

        Border CreateNoteCard(NoteEntry note)
        {
            var row = new DockPanel { LastChildFill = true };

            if (note.IsPinned)
            {
                var pinBadge = UI.Icon("", 13, Theme.Accent);
                pinBadge.Margin = new Thickness(0, 0, 8, 0);
                DockPanel.SetDock(pinBadge, Dock.Left);
                row.Children.Add(pinBadge);
            }

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
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = Cursors.Hand,
                Tag = (note.Id, note.IsPinned),
            }.Radius("Theme.RadiusCard");
            PaintCard(card, note.Id, note.IsPinned);

            void Edit() => OpenEditor(window, note, () => { notes = LoadNotes(); RefreshList(searchBox.Text); });
            card.MouseEnter += (_, _) => { if (!IsSelected(note.Id)) card.Background = Theme.ButtonHover; };
            card.MouseLeave += (_, _) => PaintCard(card, note.Id, note.IsPinned);
            card.MouseLeftButtonUp += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ToggleSelect(note.Id);
                else SelectOnly(note.Id);
            };
            card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { Edit(); e.Handled = true; } };
            card.PreviewMouseRightButtonDown += (_, _) => { if (!IsSelected(note.Id)) SelectOnly(note.Id); };

            List<NoteEntry> Targets() { var sel = SelectedNotesInView(); return sel.Count > 0 && sel.Any(n => n.Id == note.Id) ? sel : [note]; }

            void CopyNotes(List<NoteEntry> targets)
            {
                var text = string.Join("\n\n---\n\n", targets.Select(n => string.IsNullOrWhiteSpace(n.Title) ? n.Content : $"{n.Title}\n\n{n.Content}"));
                Adawat.Core.Clipboard.CopyText(text);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T(targets.Count == 1 ? "Note copied to clipboard" : $"{targets.Count} notes copied"), Hud.Style.Success);
            }

            void TogglePin(List<NoteEntry> targets)
            {
                var pin = targets.Any(n => !n.IsPinned);
                foreach (var n in targets) { n.IsPinned = pin; n.UpdatedAt = DateTime.Now; }
                SaveNotes(notes);
                RefreshList(searchBox.Text);
            }

            void DeleteNotes(List<NoteEntry> targets)
            {
                var msg = targets.Count == 1
                    ? "Delete this note permanently? This cannot be undone."
                    : $"Delete {targets.Count} selected notes permanently? This cannot be undone.";
                if (Settings.Current.ConfirmNoteDelete &&
                    !ConfirmDialog.Ask(window, "Delete Note", msg))
                    return;
                var ids = targets.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
                notes.RemoveAll(n => ids.Contains(n.Id));
                foreach (var id in ids) selectedIds.Remove(id);
                primaryId = selectedIds.FirstOrDefault();
                SaveNotes(notes);
                RefreshList(searchBox.Text);
            }

            void ExportNotes(List<NoteEntry> targets)
            {
                foreach (var n in targets) ExportNote(n, window);
            }

            card.ContextMenu = UI.Menu();
            card.ContextMenuOpening += (_, _) =>
            {
                var t = Targets();
                var multi = t.Count > 1;
                var menu = UI.Menu();
                menu.Items.Add(UI.MenuEntry(multi ? $"Copy ({t.Count})" : "Copy", () => CopyNotes(Targets()), ""));
                menu.Items.Add(UI.MenuEntry(note.IsPinned ? "Unpin" : "Pin to top", () => TogglePin(Targets()), note.IsPinned ? "" : ""));
                menu.Items.Add(UI.MenuEntry("Open in viewer", () => MarkdownViewer.OpenPreview(string.IsNullOrWhiteSpace(note.Title) ? "Note" : note.Title, note.Content, window), "", enabled: !multi));
                menu.Items.Add(UI.MenuEntry("Edit details", Edit, "", enabled: !multi));
                menu.Items.Add(UI.MenuEntry("Export...", () => ExportNotes(Targets()), ""));
                menu.Items.Add(UI.Separator());
                menu.Items.Add(UI.MenuEntry(multi ? $"Delete ({t.Count})" : "Delete", () => DeleteNotes(Targets()), ""));
                card.ContextMenu = menu;
            };

            return card;
        }

        void RefreshList(string filter = "")
        {
            itemsPanel.Children.Clear();
            visibleCards.Clear();
            var query = filter.Trim();
            var matches = string.IsNullOrEmpty(query)
                ? notes
                : notes.Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || n.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            var sorted = matches.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt).ToList();
            var pinned = sorted.Where(n => n.IsPinned).ToList();
            var unpinned = sorted.Where(n => !n.IsPinned).ToList();
            var shownUnpinned = unpinned.Take(unpinnedPage * pageSize).ToList();
            var shown = pinned.Concat(shownUnpinned).ToList();
            var shownIds = shown.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
            selectedIds.RemoveWhere(id => !shownIds.Contains(id));
            if (primaryId != null && !selectedIds.Contains(primaryId)) primaryId = selectedIds.FirstOrDefault();
            var remaining = Math.Max(0, unpinned.Count - shownUnpinned.Count);

            loadMoreBtn.Content = remaining > 0 ? $"Load more ({remaining} remaining)" : "Load more";
            loadMoreBtn.Visibility = remaining > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (shown.Count == 0)
            {
                var empty = UI.Caption(string.IsNullOrEmpty(query) ? "No notes yet. Click 'New Note' to create one." : "No matching notes found.");
                empty.Margin = new Thickness(12, 28, 12, 28);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                itemsPanel.Children.Add(empty);
                if (remaining > 0) itemsPanel.Children.Add(loadMoreBtn);
                return;
            }

            foreach (var note in shown)
            {
                var card = CreateNoteCard(note);
                itemsPanel.Children.Add(card);
                visibleCards.Add((note.Id, card,
                    () => OpenEditor(window, note, () => { notes = LoadNotes(); RefreshList(searchBox.Text); })));
            }

            if (remaining > 0) itemsPanel.Children.Add(loadMoreBtn);
        }

        // Keyboard navigation over the cards currently visible after search and sorting.
        void MoveSelection(int delta)
        {
            if (visibleCards.Count == 0) return;
            var index = primaryId == null ? -1 : visibleCards.FindIndex(v => v.Id == primaryId);
            if (index < 0 && delta < 0) index = visibleCards.Count;
            var next = index + delta;
            if (next < 0 || next >= visibleCards.Count) return; // stop at the edges, no wrap
            var target = visibleCards[next];
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) SelectOnly(target.Id);
            else { primaryId = target.Id; if (!IsSelected(target.Id)) ToggleSelect(target.Id); RefreshCardStyles(); }
            target.Card.BringIntoView();
        }

        void OpenSelected()
        {
            if (primaryId == null) return;
            var index = visibleCards.FindIndex(v => v.Id == primaryId);
            if (index >= 0) visibleCards[index].Open();
        }

        void DeleteSelected()
        {
            var targets = SelectedNotesInView();
            if (targets.Count == 0) return;
            var msg = targets.Count == 1
                ? "Delete this note permanently? This cannot be undone."
                : $"Delete {targets.Count} selected notes permanently? This cannot be undone.";
            if (Settings.Current.ConfirmNoteDelete &&
                !ConfirmDialog.Ask(window, "Delete Note", msg))
                return;
            var ids = targets.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
            notes.RemoveAll(n => ids.Contains(n.Id));
            foreach (var id in ids) selectedIds.Remove(id);
            primaryId = selectedIds.FirstOrDefault();
            SaveNotes(notes);
            RefreshList(searchBox.Text);
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

        loadMoreBtn.Click += (_, _) => { unpinnedPage++; RefreshList(searchBox.Text); };
        searchBox.TextChanged += (_, _) => { unpinnedPage = 1; RefreshList(searchBox.Text); };

        RefreshList();
        window.Body = root;

        window.Trailing(UI.Button("Close", window.CloseOnce, primary: false));

        // Up/Down move the selection through the visible cards and Enter opens it.
        // Buttons keep their own keyboard activation.
        window.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.FocusedElement is TextBox) return;
            // Arrows never activate a focused button, so they stay available for the list.
            if (e.Key == Key.Enter && Keyboard.FocusedElement is Button) return;
            if (e.Key == Key.Down) { MoveSelection(1); e.Handled = true; }
            else if (e.Key == Key.Up) { MoveSelection(-1); e.Handled = true; }
            else if (e.Key == Key.Enter && primaryId != null) { OpenSelected(); e.Handled = true; }
            else if (e.Key == Key.Delete && selectedIds.Count > 0) { DeleteSelected(); e.Handled = true; }
        };

        // Auto-close when clicking outside, unless an open child dialog exists
        window.Deactivated += (_, _) =>
        {
            if (!WindowLifecycle.CanAutoClose(window)) return;
            window.CloseOnce();
        };

        // Closing a note or its preview can make Windows minimize this list as a side effect.
        WindowLifecycle.KeepRestored(window);

        window.Loaded += (_, _) => searchBox.Focus();
        return window;
    }



    private static void OpenEditor(Window owner, NoteEntry? note, Action onSaved)
    {
        var isNew = note == null;
        var current = note ?? new NoteEntry();

        // Same shape as the clipboard edit dialog: shown in the taskbar so clearing Owner on close is
        // a real Win32 detach. A ShowInTaskbar=false editor is re-parented by WPF to its hidden
        // window when Owner is cleared, which kept minimizing the notepad list after a preview closed.
        var editor = new ToolWindow(isNew ? "New Note" : "Edit Note", 620) { Owner = owner };
        var stack = new StackPanel();

        var isInitialArabic = (current.Title + " " + current.Content).Any(c => c >= 0x0600 && c <= 0x06FF);
        var initialDir = isInitialArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var titleBox = new TextBox
        {
            Text = current.Title,
            Height = 36,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = Theme.Font,
            FlowDirection = initialDir,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(UI.FieldRow("Title", titleBox, 60));

        var contentBox = new TextBox
        {
            Text = current.Content,
            Height = 280,
            FontSize = 12.5,
            Padding = new Thickness(8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
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

        void ImportNote()
        {
            var dlg = new OpenFileDialog
            {
                Title = L10n.T("Import Note"),
                Filter = "Text and Markdown (*.txt;*.md)|*.txt;*.md|All Files (*.*)|*.*",
            };
            bool? picked;
            using (WindowLifecycle.HoldAutoClose(editor)) picked = dlg.ShowDialog(editor);
            if (picked != true) return;
            try
            {
                contentBox.Text = File.ReadAllText(dlg.FileName);
                if (string.IsNullOrWhiteSpace(titleBox.Text))
                    titleBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Note imported"), Hud.Style.Success);
            }
            catch (Exception ex)
            {
                Log.Write($"Import note error: {ex}");
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        }

        void PreviewNote() =>
            MarkdownViewer.OpenPreview(string.IsNullOrWhiteSpace(titleBox.Text) ? "Note Preview" : titleBox.Text, contentBox.Text, editor);

        // New notes expose only what the add flow needs; Copy and Export stay on the edit dialog.
        if (isNew)
        {
            editor.Leading(
                UI.Button("Cancel", editor.CloseOnce),
                dirBtn,
                UI.Button("Import...", ImportNote, primary: false, icon: Catalog.Icons.Folder),
                UI.Button("Preview...", PreviewNote, primary: false, icon: "\uE946")
            );
        }
        else
        {
            editor.Leading(
                UI.Button("Cancel", editor.CloseOnce),
                dirBtn,
                UI.Button("Copy", () =>
                {
                    var text = string.IsNullOrWhiteSpace(titleBox.Text) ? contentBox.Text : $"{titleBox.Text}\n\n{contentBox.Text}";
                    Adawat.Core.Clipboard.CopyText(text);
                    Sounds.Tick();
                    Hud.Shared.ShowMessage(L10n.T("Note copied to clipboard"), Hud.Style.Success);
                }, primary: false, icon: "\uE8C8"),
                UI.Button("Preview...", PreviewNote, primary: false, icon: "\uE946"),
                UI.Button("Export...", () =>
                {
                    var temp = new NoteEntry { Title = titleBox.Text, Content = contentBox.Text };
                    ExportNote(temp, editor);
                }, primary: false, icon: "\uE8A5")
            );
        }

        editor.Trailing(UI.Button(isNew ? "Add" : "Save", () =>
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
            editor.CloseOnce();
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

        // The list closes itself on deactivation, and a native file dialog is not an owned WPF window,
        // so without this hold the list would vanish as soon as the picker takes focus.
        bool? picked;
        using (WindowLifecycle.HoldAutoClose(owner)) picked = dlg.ShowDialog(owner);

        if (picked == true)
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
