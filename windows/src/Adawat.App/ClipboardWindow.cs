using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Adawat.Core;
using Adawat.App.Documents;
using Microsoft.Win32;

namespace Adawat.App;

public sealed class ClipboardEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "Text"; // "Text" or "Image"
    public string Text { get; set; } = "";
    public string? ImagePath { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CopiedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Service managing persistent infinite text clipboard history and the last 10 captured images.
/// </summary>
public static class ClipboardHistory
{
    public static readonly string ImagesFolder = Path.Combine(Settings.Folder, "clipboard_images");
    private static readonly string FilePath = Path.Combine(Settings.Folder, "clipboard.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static bool _suppressUpdate;
    public const int MaxImages = 10;

    public static event Action? Changed;

    public static void SuppressNext() => _suppressUpdate = true;

    public static void OnClipboardUpdated()
    {
        if (_suppressUpdate)
        {
            _suppressUpdate = false;
            return;
        }

        Task.Delay(80).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsImage())
                    {
                        var img = System.Windows.Clipboard.GetImage();
                        if (img != null)
                        {
                            ProcessNewImage(img);
                            return;
                        }
                    }

                    if (System.Windows.Clipboard.ContainsText())
                    {
                        var text = System.Windows.Clipboard.GetText();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            ProcessNewText(text);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Write($"Clipboard history update error: {ex}");
                }
            });
        });
    }

    private static void ProcessNewImage(BitmapSource img)
    {
        try
        {
            Directory.CreateDirectory(ImagesFolder);
            var list = Load();

            // Ignore identical dimensions captured within 3 seconds
            var first = list.FirstOrDefault();
            if (first != null && first.Type == "Image" &&
                first.ImageWidth == img.PixelWidth &&
                first.ImageHeight == img.PixelHeight &&
                (DateTime.Now - first.CopiedAt).TotalSeconds < 3)
            {
                return;
            }

            var entryId = Guid.NewGuid().ToString("N");
            var filePath = Path.Combine(ImagesFolder, $"{entryId}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(filePath))
            {
                encoder.Save(fs);
            }

            var entry = new ClipboardEntry
            {
                Id = entryId,
                Type = "Image",
                Text = $"Image ({img.PixelWidth} × {img.PixelHeight})",
                ImagePath = filePath,
                ImageWidth = img.PixelWidth,
                ImageHeight = img.PixelHeight,
                CopiedAt = DateTime.Now
            };

            list.Insert(0, entry);
            EnforceImageLimit(list);

            Save(list);
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Write($"Save clipboard image error: {ex}");
        }
    }

    private static void ProcessNewText(string text)
    {
        var list = Load();
        if (list.Count > 0 && list[0].Type == "Text" && list[0].Text == text) return;

        var existing = list.FirstOrDefault(e => e.Type == "Text" && e.Text == text);
        if (existing != null && !existing.IsPinned)
        {
            list.Remove(existing);
        }

        if (existing == null)
        {
            existing = new ClipboardEntry { Type = "Text", Text = text, CopiedAt = DateTime.Now };
        }
        else
        {
            existing.CopiedAt = DateTime.Now;
        }

        list.Insert(0, existing);
        Save(list);
        Changed?.Invoke();
    }

    /// <summary>
    /// Caps images at 10 items (deleting older files from disk).
    /// Texts are never removed by this cap (they remain infinite).
    /// </summary>
    private static void EnforceImageLimit(List<ClipboardEntry> list)
    {
        var images = list.Where(e => e.Type == "Image").ToList();
        if (images.Count > MaxImages)
        {
            var excess = images.Count - MaxImages;
            var toRemove = images
                .Where(e => !e.IsPinned)
                .OrderBy(e => e.CopiedAt)
                .Take(excess)
                .ToList();

            if (toRemove.Count < excess)
            {
                var remaining = excess - toRemove.Count;
                var olderPinned = images.Except(toRemove).OrderBy(e => e.CopiedAt).Take(remaining);
                toRemove.AddRange(olderPinned);
            }

            foreach (var old in toRemove)
            {
                list.Remove(old);
                try
                {
                    if (!string.IsNullOrEmpty(old.ImagePath) && File.Exists(old.ImagePath))
                        File.Delete(old.ImagePath);
                }
                catch { }
            }
        }
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
/// A full-featured clipboard manager supporting infinite texts and the last 10 images,
/// with filtering, previewing, pinning, and editing.
/// </summary>
public static class ClipboardWindow
{
    private static ToolWindow? _open;

    public static void Present()
    {
        if (_open != null) { WindowLifecycle.ActivateIfOpen(_open); return; }
        _open = Make();
        _open.Closed += (_, _) => _open = null;
        _open.Present();
    }

    public static ToolWindow Make()
    {
        var window = new ToolWindow("Clipboard History", 600);
        var entries = ClipboardHistory.Load();
        var currentFilterType = "All"; // "All", "Text", "Image"
        var itemsPanel = new StackPanel();
        var visibleCards = new List<(string Id, Border Card, Action Open)>();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        string? primaryId = null;

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

        List<ClipboardEntry> SelectedEntriesInView()
        {
            var order = visibleCards.Select((v, i) => (v.Id, i)).ToDictionary(x => x.Id, x => x.i);
            return entries.Where(e => selectedIds.Contains(e.Id))
                .OrderBy(e => order.TryGetValue(e.Id, out var i) ? i : int.MaxValue)
                .ToList();
        }

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

        var countLabel = UI.Caption("");
        countLabel.HorizontalAlignment = HorizontalAlignment.Right;
        countLabel.VerticalAlignment = VerticalAlignment.Center;
        countLabel.Margin = new Thickness(12, 0, 0, 0);

        Border CreateEntryCard(ClipboardEntry item, Window owner, Action onCopy, Action onTogglePin, Action onDelete, Action onEdit)
        {
            var row = new DockPanel { LastChildFill = true };

            // Pin badge
            if (item.IsPinned)
            {
                var pinBadge = UI.Icon("", 13, Theme.Accent);
                pinBadge.Margin = new Thickness(0, 0, 8, 0);
                DockPanel.SetDock(pinBadge, Dock.Left);
                row.Children.Add(pinBadge);
            }

            if (item.Type == "Image")
            {
                // Image Thumbnail on the left
                var thumb = LoadBitmapImage(item.ImagePath ?? "", decodeHeight: 80);
                FrameworkElement imageDisplay;
                if (thumb != null)
                {
                    imageDisplay = new Border
                    {
                        ClipToBounds = true,
                        Width = 56,
                        Height = 44,
                        Background = Theme.Card,
                        Margin = new Thickness(0, 0, 10, 0),
                        Child = new Image
                        {
                            Source = thumb,
                            Stretch = Stretch.UniformToFill
                        }
                    }.Radius("Theme.RadiusSmall");
                }
                else
                {
                    imageDisplay = new Border
                    {
                        Width = 56,
                        Height = 44,
                        Background = Theme.ButtonFill,
                        Margin = new Thickness(0, 0, 10, 0),
                        Child = UI.Icon(Catalog.Icons.Photo, 18, Theme.Muted)
                    }.Radius("Theme.RadiusSmall");
                }
                DockPanel.SetDock(imageDisplay, Dock.Left);
                row.Children.Add(imageDisplay);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleText = !string.IsNullOrEmpty(item.Text) ? item.Text : $"Image ({item.ImageWidth} × {item.ImageHeight} px)";
                var titleBlock = UI.Text(titleText, 12.5, FontWeights.SemiBold, Theme.Ink);
                var meta = UI.Text($"{item.CopiedAt:g} • PNG image", 10.5, color: Theme.Muted);
                info.Children.Add(titleBlock);
                info.Children.Add(meta);
                row.Children.Add(info);
            }
            else
            {
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
            }

            var card = new Border
            {
                Child = row,
                Background = item.IsPinned ? Theme.AccentSoft : Theme.ButtonFill,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 4, 0, 4),
                Cursor = Cursors.Hand,
                Tag = (item.Id, item.IsPinned),
            }.Radius("Theme.RadiusCard");
            PaintCard(card, item.Id, item.IsPinned);

            card.MouseEnter += (_, _) => { if (!IsSelected(item.Id)) card.Background = Theme.ButtonHover; };
            card.MouseLeave += (_, _) => PaintCard(card, item.Id, item.IsPinned);
            card.MouseLeftButtonUp += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ToggleSelect(item.Id);
                else SelectOnly(item.Id);
            };
            card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { if (item.Type == "Image") OpenImagePreview(owner, item); else onEdit(); e.Handled = true; } };
            card.PreviewMouseRightButtonDown += (_, _) => { if (!IsSelected(item.Id)) SelectOnly(item.Id); };

            card.ContextMenu = UI.Menu();
            card.ContextMenuOpening += (_, _) =>
            {
                card.ContextMenu = BuildContextMenu(item, owner, onCopy, onTogglePin, onDelete, onEdit,
                    targets: () => { var sel = SelectedEntriesInView(); return sel.Count > 0 && sel.Any(e => e.Id == item.Id) ? sel : [item]; },
                    deleteMany: t => { DeleteEntries(t); },
                    togglePinMany: t => { TogglePinEntries(t); },
                    copyMany: t => { CopyEntries(t); });
            };

            return card;
        }

        void RefreshList(string filter = "")
        {
            itemsPanel.Children.Clear();
            visibleCards.Clear();
            var query = filter.Trim();

            var matches = entries.AsEnumerable();
            if (currentFilterType == "Text")
                matches = matches.Where(e => e.Type != "Image");
            else if (currentFilterType == "Image")
                matches = matches.Where(e => e.Type == "Image");

            if (!string.IsNullOrEmpty(query))
            {
                matches = matches.Where(e => e.Text.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            // Pinned first, then newest copied
            var sorted = matches.OrderByDescending(e => e.IsPinned).ThenByDescending(e => e.CopiedAt).ToList();

            // Drop selections no longer visible, so actions never target hidden cards.
            var visibleIds = sorted.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            selectedIds.RemoveWhere(id => !visibleIds.Contains(id));
            if (primaryId != null && !selectedIds.Contains(primaryId)) primaryId = selectedIds.FirstOrDefault();

            var imgCount = entries.Count(e => e.Type == "Image");
            countLabel.Text = $"{sorted.Count} item(s) • {imgCount}/10 images";

            if (sorted.Count == 0)
            {
                var emptyMsg = string.IsNullOrEmpty(query)
                    ? (currentFilterType == "Image" ? "No images copied yet. Copy any image to see it here." : "Clipboard history is empty. Copy text or images anywhere to see them here.")
                    : "No matching items found.";
                var empty = UI.Caption(emptyMsg);
                empty.Margin = new Thickness(12, 28, 12, 28);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                itemsPanel.Children.Add(empty);
                return;
            }

            foreach (var item in sorted)
            {
                Action edit = () => OpenEditDialog(window, item, () =>
                {
                    ClipboardHistory.Save(entries);
                    RefreshList(searchBox.Text);
                });

                var card = CreateEntryCard(item, window,
                    onCopy: () => CopyItem(item),
                    onTogglePin: () =>
                    {
                        item.IsPinned = !item.IsPinned;
                        ClipboardHistory.Save(entries);
                        RefreshList(searchBox.Text);
                    },
                    onDelete: () =>
                    {
                        if (Settings.Current.ConfirmClipboardDelete &&
                            !ConfirmDialog.Ask(window, "Delete Clipboard Entry",
                                "Delete this clipboard entry permanently? This cannot be undone."))
                            return;

                        // The confirm dialog re-activates the list, and Activated reloads entries
                        // from disk. Reload here too and remove by Id so the card's captured item
                        // still matches; removing by reference would silently leave the entry.
                        entries = ClipboardHistory.Load();
                        if (entries.RemoveAll(e => e.Id == item.Id) == 0)
                        {
                            RefreshList(searchBox.Text);
                            return;
                        }

                        if (item.Type == "Image" && !string.IsNullOrEmpty(item.ImagePath))
                        {
                            try { File.Delete(item.ImagePath); } catch { }
                        }
                        ClipboardHistory.Save(entries);
                        RefreshList(searchBox.Text);
                    },
                    onEdit: edit);

                itemsPanel.Children.Add(card);
                visibleCards.Add((item.Id, card,
                    item.Type == "Image" ? () => OpenImagePreview(window, item) : edit));
            }
        }

        void MoveSelection(int delta)
        {
            if (visibleCards.Count == 0) return;
            var index = primaryId == null ? -1 : visibleCards.FindIndex(v => v.Id == primaryId);
            if (index < 0 && delta < 0) index = visibleCards.Count;
            var next = index + delta;
            if (next < 0 || next >= visibleCards.Count) return;
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

        void DeleteEntries(List<ClipboardEntry> targets)
        {
            if (targets.Count == 0) return;
            var msg = targets.Count == 1
                ? "Delete this clipboard entry permanently? This cannot be undone."
                : $"Delete {targets.Count} selected clipboard entries permanently? This cannot be undone.";
            if (Settings.Current.ConfirmClipboardDelete &&
                !ConfirmDialog.Ask(window, "Delete Clipboard Entry", msg))
                return;
            entries = ClipboardHistory.Load();
            var ids = targets.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            var toDelete = entries.Where(e => ids.Contains(e.Id)).ToList();
            if (toDelete.Count == 0) { RefreshList(searchBox.Text); return; }
            foreach (var item in toDelete)
            {
                if (item.Type == "Image" && !string.IsNullOrEmpty(item.ImagePath))
                {
                    try { File.Delete(item.ImagePath); } catch { }
                }
            }
            entries.RemoveAll(e => ids.Contains(e.Id));
            foreach (var id in ids) selectedIds.Remove(id);
            primaryId = selectedIds.FirstOrDefault();
            ClipboardHistory.Save(entries);
            RefreshList(searchBox.Text);
        }

        void DeleteSelected() => DeleteEntries(SelectedEntriesInView());

        void TogglePinEntries(List<ClipboardEntry> targets)
        {
            if (targets.Count == 0) return;
            var pin = targets.Any(x => !x.IsPinned);
            entries = ClipboardHistory.Load();
            var ids = targets.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var e in entries) if (ids.Contains(e.Id)) e.IsPinned = pin;
            ClipboardHistory.Save(entries);
            RefreshList(searchBox.Text);
        }

        void CopyEntries(List<ClipboardEntry> targets)
        {
            if (targets.Count == 0) return;
            var texts = targets.Where(x => x.Type != "Image").Select(x => x.Text).ToList();
            if (texts.Count == targets.Count)
            {
                Adawat.Core.Clipboard.CopyText(string.Join("\n\n", texts));
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T(targets.Count == 1 ? "Copied to clipboard" : $"{targets.Count} items copied"), Hud.Style.Success);
            }
            else if (targets.Count == 1) CopyItem(targets[0]);
            else
            {
                // Mixed selection: copy text parts only to avoid ambiguous image clipboard behavior.
                if (texts.Count > 0) { Adawat.Core.Clipboard.CopyText(string.Join("\n\n", texts)); Sounds.Tick(); Hud.Shared.ShowMessage(L10n.T($"{texts.Count} text items copied"), Hud.Style.Success); }
                else CopyItem(targets[0]);
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
            if (Settings.Current.ConfirmClipboardDelete &&
                !ConfirmDialog.Ask(window, "Clear Clipboard History",
                    "Clear all non-pinned clipboard entries? This cannot be undone.", "Clear"))
                return;

            var removed = entries.Where(e => !e.IsPinned).ToList();
            foreach (var r in removed)
            {
                if (r.Type == "Image" && !string.IsNullOrEmpty(r.ImagePath))
                {
                    try { File.Delete(r.ImagePath); } catch { }
                }
            }
            entries.RemoveAll(e => !e.IsPinned);
            ClipboardHistory.Save(entries);
            RefreshList(searchBox.Text);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Cleared non-pinned history"), Hud.Style.Success);
        }, primary: false, icon: "");
        clearBtn.Margin = new Thickness(8, 0, 0, 0);

        var topRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(clearBtn, Dock.Right);
        DockPanel.SetDock(addBtn, Dock.Right);
        topRow.Children.Add(clearBtn);
        topRow.Children.Add(addBtn);
        topRow.Children.Add(searchBox);
        root.Children.Add(topRow);

        // ── Filter Row (All / Texts / Images + Count) ──
        var filterRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(countLabel, Dock.Right);
        filterRow.Children.Add(countLabel);

        var filterTab = UI.Segmented([
            ("All", "All"),
            ("Text", "Texts"),
            ("Image", "Images (10 max)")
        ], currentFilterType, selected =>
        {
            currentFilterType = selected;
            RefreshList(searchBox.Text);
        });
        DockPanel.SetDock(filterTab, Dock.Left);
        filterRow.Children.Add(filterTab);
        root.Children.Add(filterRow);

        var scroll = new ScrollViewer
        {
            Content = itemsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 390,
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

        window.Closed += (_, _) =>
        {
            ClipboardHistory.Changed -= OnLiveUpdate;
        };

        RefreshList();
        window.Body = root;

        window.Trailing(UI.Button("Close", window.CloseOnce, primary: false));

        // Up/Down move the selection through the visible cards and Enter opens it.
        // Buttons keep their own keyboard activation.
        window.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.FocusedElement is TextBox) return;
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

        // Closing an edit dialog or its preview can make Windows minimize this list as a side effect.
        WindowLifecycle.KeepRestored(window);

        window.Loaded += (_, _) => searchBox.Focus();
        return window;
    }

    private static void CopyItem(ClipboardEntry item)
    {
        ClipboardHistory.SuppressNext();
        if (item.Type == "Image" && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
        {
            try
            {
                var fullImg = LoadBitmapImage(item.ImagePath, decodeHeight: 0);
                if (fullImg != null)
                {
                    System.Windows.Clipboard.SetImage(fullImg);
                    Sounds.Tick();
                    Hud.Shared.ShowMessage(L10n.T("Image copied to clipboard"), Hud.Style.Success);
                }
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to copy image to clipboard: {ex}");
            }
        }
        else
        {
            Adawat.Core.Clipboard.CopyText(item.Text);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
        }
    }



    public static BitmapImage? LoadBitmapImage(string path, int decodeHeight = 80)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            if (decodeHeight > 0) bmp.DecodePixelHeight = decodeHeight;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static void OpenImagePreview(Window owner, ClipboardEntry item)
    {
        if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath)) return;

        var dialog = new ToolWindow("Image Preview", 620) { Owner = owner };
        var stack = new StackPanel();

        var fullImg = LoadBitmapImage(item.ImagePath, decodeHeight: 0);
        var imgView = new Image
        {
            Source = fullImg,
            Stretch = Stretch.Uniform,
            MaxHeight = 440,
            MaxWidth = 580,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var border = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 4, 0, 10),
            Child = imgView
        }.Radius("Theme.RadiusMedium");
        stack.Children.Add(border);

        dialog.Body = stack;
        dialog.Leading(UI.Button("Save As...", () => SaveImageAs(item, dialog), primary: false, icon: ""));
        dialog.Trailing(UI.Button("Copy Image", () =>
        {
            ClipboardHistory.SuppressNext();
            if (fullImg != null) System.Windows.Clipboard.SetImage(fullImg);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Image copied to clipboard"), Hud.Style.Success);
            dialog.Close();
        }, primary: true, icon: ""));

        dialog.Present();
    }

    private static void SaveImageAs(ClipboardEntry item, Window owner)
    {
        if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath)) return;
        var dlg = new SaveFileDialog
        {
            Title = L10n.T("Save Image"),
            Filter = "PNG Image (*.png)|*.png|All Files (*.*)|*.*",
            FileName = $"Clipboard_{item.CopiedAt:yyyyMMdd_HHmmss}.png"
        };
        bool? picked;
        using (WindowLifecycle.HoldAutoClose(owner)) picked = dlg.ShowDialog(owner);
        if (picked == true)
        {
            try
            {
                File.Copy(item.ImagePath, dlg.FileName, overwrite: true);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Image saved successfully"), Hud.Style.Success);
            }
            catch (Exception ex)
            {
                Log.Write($"Save image error: {ex}");
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        }
    }

    private static void ExportText(Window owner, string text, string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        var dlg = new SaveFileDialog
        {
            Title = L10n.T("Export Text"),
            FileName = string.IsNullOrWhiteSpace(name) ? "Clipboard" : name,
            Filter = "Markdown Document (*.md)|*.md|Text Document (*.txt)|*.txt",
            DefaultExt = ".md",
        };

        bool? picked;
        using (WindowLifecycle.HoldAutoClose(owner)) picked = dlg.ShowDialog(owner);
        if (picked != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, text, System.Text.Encoding.UTF8);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Exported successfully"), Hud.Style.Success);
        }
        catch (Exception ex)
        {
            Log.Write($"Export clipboard text error: {ex}");
            Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
        }
    }

    private static void OpenAddDialog(Window owner, Action onAdded)
    {
        var dialog = new ToolWindow("Add to Clipboard", 620) { Owner = owner };
        var stack = new StackPanel();

        var initialDir = FlowDirection.LeftToRight;

        var textBox = new TextBox
        {
            Height = 220,
            FontSize = 13,
            Padding = new Thickness(8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
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

        void ImportText()
        {
            var dlg = new OpenFileDialog
            {
                Title = L10n.T("Import Text"),
                Filter = "Text and Markdown (*.txt;*.md)|*.txt;*.md|All Files (*.*)|*.*",
            };

            // The list closes itself on deactivation and a native picker is not an owned window.
            bool? picked;
            using (WindowLifecycle.HoldAutoClose(dialog)) picked = dlg.ShowDialog(dialog);
            if (picked != true) return;

            try
            {
                textBox.Text = File.ReadAllText(dlg.FileName);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Text imported"), Hud.Style.Success);
            }
            catch (Exception ex)
            {
                Log.Write($"Import clipboard text error: {ex}");
                Hud.Shared.ShowMessage(ex.Message, Hud.Style.Failure);
            }
        }

        dialog.Body = stack;
        dialog.Leading(
            UI.Button("Cancel", dialog.Close),
            UI.Button("RTL / LTR", () =>
            {
                textBox.FlowDirection = textBox.FlowDirection == FlowDirection.RightToLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }, primary: false, icon: ""),
            UI.Button("Import...", ImportText, primary: false, icon: Catalog.Icons.Folder),
            UI.Button("Preview...", () => MarkdownViewer.OpenPreview("Clipboard Preview", textBox.Text, dialog), primary: false, icon: "\uE946")
        );
        dialog.Trailing(UI.Button("Add", () =>
        {
            var text = textBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            var list = ClipboardHistory.Load();
            list.Insert(0, new ClipboardEntry { Type = "Text", Text = text, CopiedAt = DateTime.Now });
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
        // 620 wide like the note editor: both footers now carry the same six buttons, and 540
        // pushed Copy/Export past the edge of the window.
        var dialog = new ToolWindow("Edit Clipboard Entry", 620) { Owner = owner };
        var stack = new StackPanel();

        var isInitialArabic = entry.Text.Any(c => c >= 0x0600 && c <= 0x06FF);
        var initialDir = isInitialArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var textBox = new TextBox
        {
            Text = entry.Text,
            Height = 240,
            FontSize = 13,
            Padding = new Thickness(8),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
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
            UI.Button("Copy", () =>
            {
                Adawat.Core.Clipboard.CopyText(textBox.Text);
                Sounds.Tick();
                Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
            }, primary: false, icon: "\uE8C8"),
            UI.Button("Export...", () => ExportText(dialog, textBox.Text, $"Clipboard_{entry.CopiedAt:yyyyMMdd_HHmmss}"), primary: false, icon: "\uE8A5"),
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

    private static ContextMenu BuildContextMenu(ClipboardEntry item, Window owner, Action onCopy, Action onTogglePin, Action onDelete, Action onEdit,
        Func<List<ClipboardEntry>> targets, Action<List<ClipboardEntry>> deleteMany, Action<List<ClipboardEntry>> togglePinMany, Action<List<ClipboardEntry>> copyMany)
    {
        var menu = UI.Menu();
        var sel = targets();
        var multi = sel.Count > 1;
        menu.Items.Add(UI.MenuEntry(multi ? $"Copy ({sel.Count})" : "Copy", () =>
        {
            var t = targets();
            if (t.Count <= 1) { onCopy(); return; }
            copyMany(t);
        }, ""));
        menu.Items.Add(UI.MenuEntry(item.IsPinned ? "Unpin" : "Pin to top", () =>
        {
            var t = targets();
            if (t.Count <= 1) { onTogglePin(); return; }
            togglePinMany(t);
        }, item.IsPinned ? "" : ""));
        if (item.Type == "Image")
        {
            menu.Items.Add(UI.MenuEntry("Preview image", () => OpenImagePreview(owner, item), "", enabled: !multi));
            menu.Items.Add(UI.MenuEntry("Save image as...", () => SaveImageAs(item, owner), "", enabled: !multi));
        }
        else
        {
            menu.Items.Add(UI.MenuEntry("Open in viewer", () => MarkdownViewer.OpenPreview("Clipboard Preview", item.Text, owner), "", enabled: !multi));
            menu.Items.Add(UI.MenuEntry("Edit details", onEdit, "", enabled: !multi));
            menu.Items.Add(UI.MenuEntry("Export...", () => ExportText(owner, item.Text, $"Clipboard_{item.CopiedAt:yyyyMMdd_HHmmss}"), "\uE8A5", enabled: !multi));
        }
        menu.Items.Add(UI.Separator());
        menu.Items.Add(UI.MenuEntry(multi ? $"Delete ({sel.Count})" : "Delete", () =>
        {
            var t = targets();
            if (t.Count <= 1) { onDelete(); return; }
            deleteMany(t);
        }, ""));
        return menu;
    }
}
