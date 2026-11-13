using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Adawat.Core;
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
        if (_open != null) { _open.Activate(); return; }
        _open = Make();
        _open.Closed += (_, _) => _open = null;
        _open.Present();
    }

    public static ToolWindow Make()
    {
        var window = new ToolWindow("Clipboard History", 600);
        var entries = ClipboardHistory.Load();
        var currentFilterType = "All"; // "All", "Text", "Image"

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
        var countLabel = UI.Caption("");
        countLabel.HorizontalAlignment = HorizontalAlignment.Right;
        countLabel.VerticalAlignment = VerticalAlignment.Center;

        void RefreshList(string filter = "")
        {
            itemsPanel.Children.Clear();
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
                itemsPanel.Children.Add(CreateEntryCard(item, window,
                    onCopy: () => CopyItem(item),
                    onTogglePin: () =>
                    {
                        item.IsPinned = !item.IsPinned;
                        ClipboardHistory.Save(entries);
                        RefreshList(searchBox.Text);
                    },
                    onDelete: () =>
                    {
                        if (item.Type == "Image" && !string.IsNullOrEmpty(item.ImagePath))
                        {
                            try { File.Delete(item.ImagePath); } catch { }
                        }
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
        clearBtn.Margin = new Thickness(6, 0, 0, 0);

        var topRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(clearBtn, Dock.Right);
        DockPanel.SetDock(addBtn, Dock.Right);
        topRow.Children.Add(clearBtn);
        topRow.Children.Add(addBtn);
        topRow.Children.Add(searchBox);
        root.Children.Add(topRow);

        // ── Filter Row (All / Texts / Images + Count) ──
        var filterRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
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

        window.Trailing(UI.Button("Close", window.Close, primary: false));

        // Auto-close when clicking outside, unless an open child dialog exists
        window.Deactivated += (_, _) =>
        {
            if (window.OwnedWindows.Count > 0) return;
            window.Close();
        };

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

        if (item.Type == "Image")
        {
            // Preview full image
            var previewBtn = UI.IconButton("", () => OpenImagePreview(owner, item), "Preview Image", 26);
            previewBtn.Margin = new Thickness(0, 0, 4, 0);
            DockPanel.SetDock(previewBtn, Dock.Right);
            row.Children.Add(previewBtn);

            // Save As...
            var saveBtn = UI.IconButton("", () => SaveImageAs(item, owner), "Save Image As...", 26);
            saveBtn.Margin = new Thickness(0, 0, 4, 0);
            DockPanel.SetDock(saveBtn, Dock.Right);
            row.Children.Add(saveBtn);
        }
        else
        {
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
        }

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

        if (item.Type == "Image")
        {
            // Image Thumbnail on the left
            var thumb = LoadBitmapImage(item.ImagePath ?? "", decodeHeight: 80);
            FrameworkElement imageDisplay;
            if (thumb != null)
            {
                imageDisplay = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    ClipToBounds = true,
                    Width = 56,
                    Height = 44,
                    Background = new SolidColorBrush(Color.FromArgb(0x10, 0, 0, 0)),
                    Margin = new Thickness(0, 0, 10, 0),
                    Child = new Image
                    {
                        Source = thumb,
                        Stretch = Stretch.UniformToFill
                    }
                };
            }
            else
            {
                imageDisplay = new Border
                {
                    Width = 56,
                    Height = 44,
                    Background = Theme.ButtonFill,
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 10, 0),
                    Child = UI.Icon(Catalog.Icons.Photo, 18, Theme.Muted)
                };
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
            Background = new SolidColorBrush(Color.FromArgb(0x0C, 0, 0, 0)),
            CornerRadius = new CornerRadius(8),
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 4, 0, 10),
            Child = imgView
        };
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
        if (dlg.ShowDialog(owner) == true)
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
