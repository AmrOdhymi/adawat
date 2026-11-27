using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Adawat.Core;
using Adawat.Core.Pdf;

namespace Adawat.App.Documents;

/// <summary>
/// Read-only PDF pages. Every page is a light placeholder until it scrolls into view; only then is it
/// rendered from the file (which is opened and closed for that one page). Rendered pages are kept in an
/// LRU cache that never grows past 256 MB: once the cache is full the least recently read page is
/// forgotten first, while its placeholder, the scroll position and the file path stay.
/// Each page also carries an invisible word layer for selecting, copying and searching its text.
/// </summary>
internal sealed class PdfView : Grid
{
    private const long BudgetBytes = 256L * 1024 * 1024;
    private const double PointsToDip = 96.0 / 72.0;
    private const int MaxRendersPerScan = 3;
    private const int MaxTextPages = 40;

    private static readonly Brush SelectionBrush = Frozen(Color.FromArgb(0x55, 0x2F, 0x6F, 0xB5));
    private static readonly Brush HitBrush = Frozen(Color.FromArgb(0x66, 0xFF, 0xC1, 0x07));
    private static readonly Brush ActiveHitBrush = Frozen(Color.FromArgb(0xAA, 0xFF, 0x8F, 0x00));
    private static readonly Brush HitSurface = Frozen(Color.FromArgb(1, 0xFF, 0xFF, 0xFF));

    private sealed class PageSlot
    {
        public int Index;
        public double WidthPoints;
        public double HeightPoints;
        public double Top;
        public double Height;
        public double ContentWidth;
        public double ContentHeight;
        public Border Host = null!;
        public Image Picture = null!;
        public WordLayer Layer = null!;
        public PdfOps.PdfPageText? Text;
        public bool TextPending;
        public bool TextFailed;
        public bool Pending;
        public bool Failed;
    }

    private sealed class Cached
    {
        public BitmapSource Image = null!;
        public long Bytes;
        public DateTime Used;
    }

    /// <summary>The invisible text layer: it draws only the selection and search marks, so a page with
    /// thousands of words costs one element, not one element per word.</summary>
    private sealed class WordLayer : FrameworkElement
    {
        private readonly PdfView _owner;

        public WordLayer(PdfView owner, PageSlot slot)
        {
            _owner = owner;
            Slot = slot;
            ClipToBounds = true;
            Focusable = false;
            SnapsToDevicePixels = true;
            Cursor = Cursors.Arrow;
        }

        public PageSlot Slot { get; }

        /// <summary>The word nearest <paramref name="point"/>, or -1 when the point is in the margin.</summary>
        public int HitTestWord(Point point)
        {
            if (Slot.Text is not { } text || text.Words.Count == 0) return -1;
            if (RenderSize.Width <= 0 || RenderSize.Height <= 0) return -1;

            var scaleX = RenderSize.Width / Math.Max(1, text.Width);
            var scaleY = RenderSize.Height / Math.Max(1, text.Height);
            var x = point.X / scaleX;
            var y = point.Y / scaleY;

            var best = -1;
            var bestDistance = double.MaxValue;
            for (var index = 0; index < text.Words.Count; index++)
            {
                var word = text.Words[index];
                var dx = x < word.Left ? word.Left - x : x > word.Left + word.Width ? x - (word.Left + word.Width) : 0;
                var dy = y < word.Top ? word.Top - y : y > word.Top + word.Height ? y - (word.Top + word.Height) : 0;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }

            if (best < 0) return -1;
            var limit = Math.Max(6, text.Words[best].Height) * 0.75;
            return bestDistance <= limit * limit ? best : -1;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(HitSurface, null, new Rect(RenderSize));
            _owner.DrawMarks(dc, Slot, RenderSize);
        }
    }

    private readonly string _path;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _panel;
    private readonly DispatcherTimer _scanTimer;
    private readonly List<PageSlot> _slots = [];
    private readonly Dictionary<int, Cached> _cache = [];
    private readonly HashSet<int> _visible = [];
    private readonly SemaphoreSlim _renderGate = new(1);
    private readonly CancellationTokenSource _cancel = new();
    private double _zoom;
    private long _cachedBytes;
    private int _generation;
    private int _lastPage = 1;
    private bool _disposed;
    private PageSlot? _anchorSlot;
    private int _anchorWord = -1;
    private PageSlot? _focusSlot;
    private int _focusWord = -1;
    private bool _selecting;
    private string _searchQuery = "";
    private int _searchPage = -1;
    private List<PdfOps.PdfSearchHit> _searchHits = [];
    private int _searchIndex = -1;
    private CancellationTokenSource? _searchCancel;

    public PdfView(string path, IReadOnlyList<(double Width, double Height)> sizes, double zoom = 100)
    {
        _path = path;
        _zoom = Math.Clamp(zoom, 40, 250);
        ClipToBounds = true;
        SetResourceReference(Panel.BackgroundProperty, "Theme.Surface2");

        _scanTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(90),
        };
        _scanTimer.Tick += (_, _) =>
        {
            _scanTimer.Stop();
            Scan();
        };

        _panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(16, 14, 16, 18) };

        for (var index = 0; index < sizes.Count; index++)
        {
            var slot = new PageSlot
            {
                Index = index,
                WidthPoints = sizes[index].Width,
                HeightPoints = sizes[index].Height,
            };
            slot.Picture = new Image { Stretch = Stretch.Fill, SnapsToDevicePixels = true, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(slot.Picture, BitmapScalingMode.Linear);
            slot.Layer = new WordLayer(this, slot);
            var page = new Grid();
            page.Children.Add(slot.Picture);
            page.Children.Add(slot.Layer);
            slot.Host = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = page,
            };
            slot.Host.SetResourceReference(Border.BorderBrushProperty, "Theme.Line");
            slot.Host.BorderThickness = new Thickness(1);
            _slots.Add(slot);
            _panel.Children.Add(slot.Host);
        }

        _scroll = new ScrollViewer
        {
            Content = _panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
        };
        _scroll.ScrollChanged += (_, _) =>
        {
            QueueVisible();
            NotifyPage();
        };
        _scroll.SizeChanged += (_, _) => QueueVisible();
        Children.Add(_scroll);

        var menu = UI.Menu();
        var copy = UI.MenuEntry("Copy", CopySelection);
        menu.Items.Add(copy);
        ContextMenu = menu;
        ContextMenuOpening += (_, _) => copy.IsEnabled = HasSelection;

        PreviewMouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        LostMouseCapture += (_, _) => _selecting = false;

        ApplySizes();
        Loaded += (_, _) => QueueVisible();
    }

    public ScrollViewer Scroller => _scroll;

    public double Zoom => _zoom;

    /// <summary>How far down the document the reader is, 0 to 1.</summary>
    public double Ratio => _scroll.ScrollableHeight > 0
        ? Math.Clamp(_scroll.VerticalOffset / _scroll.ScrollableHeight, 0, 1)
        : 0;

    public int PageCount => _slots.Count;

    /// <summary>The 1-based page nearest the reading position, for the page box.</summary>
    public int CurrentPage
    {
        get
        {
            if (_slots.Count == 0) return 0;

            var y = _scroll.VerticalOffset + Math.Max(1, _scroll.ViewportHeight) * 0.35;
            var low = 0;
            var high = _slots.Count - 1;
            var page = 0;
            while (low <= high)
            {
                var middle = (low + high) / 2;
                if (_slots[middle].Top <= y)
                {
                    page = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return page + 1;
        }
    }

    /// <summary>Raised with the 1-based page number whenever the reading position changes page.</summary>
    public event Action<int>? PageChanged;

    /// <summary>Raised with the page number being scanned by a search.</summary>
    public event Action<int>? SearchProgress;

    public void GoToPage(int page)
    {
        if (_slots.Count == 0) return;

        page = Math.Clamp(page, 1, _slots.Count);
        _scroll.ScrollToVerticalOffset(Math.Max(0, _slots[page - 1].Top));
        QueueVisible();
        NotifyPage();
    }

    /// <summary>The zoom that makes the current page as wide as the viewport.</summary>
    public double FitWidthZoom
    {
        get
        {
            if (_slots.Count == 0) return _zoom;

            var slot = _slots[Math.Clamp(CurrentPage - 1, 0, _slots.Count - 1)];
            var width = Math.Max(1, slot.WidthPoints * PointsToDip);
            return Math.Clamp(Math.Max(80, _scroll.ViewportWidth - 42) / width * 100, 40, 250);
        }
    }

    /// <summary>The zoom that fits the whole current page inside the viewport.</summary>
    public double FitPageZoom
    {
        get
        {
            if (_slots.Count == 0) return _zoom;

            var slot = _slots[Math.Clamp(CurrentPage - 1, 0, _slots.Count - 1)];
            var width = Math.Max(1, slot.WidthPoints * PointsToDip);
            var height = Math.Max(1, slot.HeightPoints * PointsToDip);
            var availableWidth = Math.Max(80, _scroll.ViewportWidth - 42);
            var availableHeight = Math.Max(80, _scroll.ViewportHeight - 32);
            return Math.Clamp(Math.Min(availableWidth / width, availableHeight / height) * 100, 40, 250);
        }
    }

    private void NotifyPage()
    {
        if (_disposed || _slots.Count == 0) return;

        var page = CurrentPage;
        if (page == _lastPage) return;

        _lastPage = page;
        PageChanged?.Invoke(page);
    }

    public void SetZoom(double zoom)
    {
        zoom = Math.Clamp(zoom, 40, 250);
        if (Math.Abs(zoom - _zoom) < 0.5) return;

        var ratio = Ratio;
        _zoom = zoom;
        _generation++;
        ClearCache();
        ApplySizes();
        UpdateLayout();
        ScrollToRatio(ratio);
        QueueVisible();
        NotifyPage();
    }

    public void ScrollToRatio(double ratio)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed) return;
            _scroll.ScrollToVerticalOffset(Math.Clamp(ratio, 0, 1) * Math.Max(0, _scroll.ScrollableHeight));
        }), DispatcherPriority.Loaded);
    }

    private void ApplySizes()
    {
        // The panel starts 14 DIPs down, so the scroll offsets include that top margin.
        double top = 14;
        foreach (var slot in _slots)
        {
            var width = slot.WidthPoints * PointsToDip * _zoom / 100;
            var height = slot.HeightPoints * PointsToDip * _zoom / 100;
            slot.Host.Width = width;
            slot.Host.Height = height;
            slot.ContentWidth = Math.Max(1, width - 2);
            slot.ContentHeight = Math.Max(1, height - 2);
            slot.Top = top;
            slot.Height = height;
            top += height + slot.Host.Margin.Top + slot.Host.Margin.Bottom;
            if (slot.Text != null && _visible.Contains(slot.Index)) slot.Layer.InvalidateVisual();
        }
    }

    /// <summary>Restarts the scan timer. Rendering and text extraction wait until scrolling pauses,
    /// so the wheel and scrollbar stay smooth.</summary>
    private void QueueVisible()
    {
        if (_disposed) return;
        _scanTimer.Stop();
        _scanTimer.Start();
    }

    private void Scan()
    {
        if (_disposed) return;

        var top = _scroll.VerticalOffset;
        var viewport = Math.Max(1, _scroll.ViewportHeight);
        var bottom = top + viewport;
        _visible.Clear();

        var candidates = new List<(PageSlot Slot, double Distance)>();
        foreach (var slot in _slots)
        {
            var near = slot.Top + slot.Height >= top - viewport && slot.Top <= bottom + viewport;
            if (!near) continue;

            var visible = slot.Top + slot.Height > top && slot.Top < bottom;
            if (visible)
            {
                _visible.Add(slot.Index);
                if (slot.Picture.Source == null && !slot.Pending && !slot.Failed)
                    candidates.Add((slot, 0));
                continue;
            }

            if (slot.Picture.Source == null && !slot.Pending && !slot.Failed)
                candidates.Add((slot, Math.Min(Math.Abs(slot.Top - bottom), Math.Abs(top - (slot.Top + slot.Height)))));
        }

        foreach (var (slot, _) in candidates.OrderBy(item => item.Distance).Take(MaxRendersPerScan))
            _ = RenderAsync(slot, _generation);

        foreach (var slot in _slots)
        {
            if (!_visible.Contains(slot.Index)) continue;
            EnsureTextLayer(slot);
            slot.Layer.InvalidateVisual();
        }

        TrimCache();
        TrimWords();
    }

    private async Task RenderAsync(PageSlot slot, int generation)
    {
        slot.Pending = true;
        var acquired = false;
        try
        {
            await _renderGate.WaitAsync(_cancel.Token);
            acquired = true;
            if (_disposed || generation != _generation) return;

            var dpi = (int)Math.Clamp(96 * _zoom / 100, 72, 250);
            var bytes = await Task.Run(() => PdfOps.RenderPagePng(_path, slot.Index, dpi), _cancel.Token);
            if (_disposed || generation != _generation) return;

            if (Decode(bytes) is not { } image)
            {
                slot.Failed = true;
                return;
            }

            slot.Picture.Source = image;

            var size = (long)image.PixelWidth * image.PixelHeight * 4;
            if (_cache.TryGetValue(slot.Index, out var old)) _cachedBytes -= old.Bytes;
            _cache[slot.Index] = new Cached { Image = image, Bytes = size, Used = DateTime.UtcNow };
            _cachedBytes += size;
            TrimCache();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            slot.Failed = true;
            Log.Write($"PDF page render failed ({_path} #{slot.Index + 1}): {ex.Message}");
        }
        finally
        {
            slot.Pending = false;
            if (acquired) _renderGate.Release();
            // A zoom change invalidated this render; ask for a fresh scan so the page is not left blank.
            if (!_disposed && generation != _generation) QueueVisible();
        }
    }

    private void EnsureTextLayer(PageSlot slot)
    {
        if (_disposed) return;
        if (slot.Text != null)
        {
            if (slot.Text.Words.Count > 0) slot.Layer.Cursor = Cursors.IBeam;
            return;
        }
        if (slot.TextPending || slot.TextFailed) return;

        slot.TextPending = true;
        _ = LoadTextAsync(slot);
    }

    private async Task LoadTextAsync(PageSlot slot)
    {
        try
        {
            var text = await Task.Run(() => PdfOps.PageWords(_path, slot.Index), _cancel.Token);
            if (_disposed) return;

            slot.Text = text;
            slot.TextFailed = false;
            slot.Layer.Cursor = text.Words.Count > 0 ? Cursors.IBeam : Cursors.Arrow;
            slot.Layer.InvalidateVisual();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            slot.TextFailed = true;
            slot.Layer.Cursor = Cursors.Arrow;
            Log.Write($"PDF text layer failed ({_path} #{slot.Index + 1}): {ex.Message}");
        }
        finally
        {
            slot.TextPending = false;
        }
    }

    private void TrimWords()
    {
        var loaded = _slots.Where(slot => slot.Text != null).ToList();
        if (loaded.Count <= MaxTextPages) return;

        var keep = new HashSet<int>(_visible) { _searchPage };
        if (_anchorSlot != null && _focusSlot != null)
        {
            var (start, end) = OrderedSelection();
            for (var page = start.Page; page <= end.Page; page++) keep.Add(page);
        }

        foreach (var slot in loaded.Where(slot => !keep.Contains(slot.Index)).Take(loaded.Count - MaxTextPages))
        {
            slot.Text = null;
            slot.TextFailed = false;
            slot.Layer.Cursor = Cursors.Arrow;
            slot.Layer.InvalidateVisual();
        }
    }

    private void TrimCache()
    {
        if (_cachedBytes <= BudgetBytes) return;

        foreach (var entry in _cache.Where(pair => !_visible.Contains(pair.Key))
                                    .OrderBy(pair => pair.Value.Used)
                                    .ToList())
        {
            Evict(entry.Key);
            if (_cachedBytes <= BudgetBytes) break;
        }
    }

    private void Evict(int index)
    {
        if (!_cache.Remove(index, out var cached)) return;
        _cachedBytes -= cached.Bytes;

        var slot = _slots.FirstOrDefault(item => item.Index == index);
        if (slot != null && ReferenceEquals(slot.Picture.Source, cached.Image)) slot.Picture.Source = null;
    }

    private void ClearCache()
    {
        foreach (var slot in _slots)
        {
            slot.Picture.Source = null;
            slot.Failed = false;
        }
        _cache.Clear();
        _cachedBytes = 0;
    }

    private static BitmapSource? Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Log.Write($"PDF page decode failed: {ex.Message}");
            return null;
        }
    }

    // MARK: Selection

    public bool HasSelection => _anchorSlot != null && _focusSlot != null;

    /// <summary>The selected words in reading order, one line per line and a blank line between pages.</summary>
    public string SelectedText
    {
        get
        {
            if (_anchorSlot == null || _focusSlot == null) return "";

            var (start, end) = OrderedSelection();
            var text = new StringBuilder();
            var firstPage = true;
            for (var page = start.Page; page <= end.Page; page++)
            {
                var slot = _slots[page];
                if (slot.Text is not { } pageText || pageText.Words.Count == 0) continue;

                var from = page == start.Page ? start.Word : 0;
                var to = page == end.Page ? end.Word : pageText.Words.Count - 1;
                if (from > to) (from, to) = (to, from);
                from = Math.Clamp(from, 0, pageText.Words.Count - 1);
                to = Math.Clamp(to, 0, pageText.Words.Count - 1);

                if (!firstPage) text.Append("\n\n");
                firstPage = false;

                var line = -1;
                for (var index = from; index <= to; index++)
                {
                    var word = pageText.Words[index];
                    if (line >= 0) text.Append(word.Line == line ? ' ' : '\n');
                    text.Append(word.Text);
                    line = word.Line;
                }
            }

            return text.ToString().Trim();
        }
    }

    public void CopySelection()
    {
        var text = SelectedText;
        if (text.Length == 0) return;

        Adawat.Core.Clipboard.CopyText(text);
        Sounds.Tick();
        Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
    }

    public void ClearSelection()
    {
        _anchorSlot = null;
        _focusSlot = null;
        _selecting = false;
        InvalidateMarks();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not WordLayer layer) return;

        var word = layer.HitTestWord(e.GetPosition(layer));
        if (word < 0)
        {
            ClearSelection();
            e.Handled = true;
            return;
        }

        _anchorSlot = layer.Slot;
        _anchorWord = word;
        _focusSlot = layer.Slot;
        _focusWord = word;
        _selecting = CaptureMouse();
        InvalidateMarks();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_selecting) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _selecting = false;
            ReleaseMouseCapture();
            return;
        }

        if (InputHitTest(e.GetPosition(this)) is not WordLayer layer) return;
        var word = layer.HitTestWord(e.GetPosition(layer));
        if (word < 0) return;
        if (ReferenceEquals(layer.Slot, _focusSlot) && word == _focusWord) return;

        _focusSlot = layer.Slot;
        _focusWord = word;
        InvalidateMarks();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting) return;
        _selecting = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private ((int Page, int Word) Start, (int Page, int Word) End) OrderedSelection()
    {
        var a = (_anchorSlot!.Index, _anchorWord);
        var b = (_focusSlot!.Index, _focusWord);
        return a.CompareTo(b) <= 0 ? (a, b) : (b, a);
    }

    private bool InSelection(int page)
    {
        if (_anchorSlot == null || _focusSlot == null) return false;
        var (start, end) = OrderedSelection();
        return page >= start.Page && page <= end.Page;
    }

    private void InvalidateMarks()
    {
        foreach (var slot in _slots)
        {
            if (slot.Text == null) continue;
            if (_visible.Contains(slot.Index) || slot.Index == _searchPage || InSelection(slot.Index))
                slot.Layer.InvalidateVisual();
        }
    }

    private void DrawMarks(DrawingContext dc, PageSlot slot, Size size)
    {
        if (slot.Text is not { } text || text.Words.Count == 0 || size.Width <= 0 || size.Height <= 0) return;

        var scaleX = size.Width / Math.Max(1, text.Width);
        var scaleY = size.Height / Math.Max(1, text.Height);

        if (_searchPage == slot.Index && _searchHits.Count > 0)
        {
            for (var hitIndex = 0; hitIndex < _searchHits.Count; hitIndex++)
            {
                var hit = _searchHits[hitIndex];
                var brush = hitIndex == _searchIndex ? ActiveHitBrush : HitBrush;
                for (var index = hit.FirstWord; index <= hit.LastWord && index < text.Words.Count; index++)
                    DrawWord(dc, text.Words[index], scaleX, scaleY, brush);
            }
        }

        if (_anchorSlot == null || _focusSlot == null) return;

        var (start, end) = OrderedSelection();
        if (slot.Index < start.Page || slot.Index > end.Page) return;

        var from = slot.Index == start.Page ? start.Word : 0;
        var to = slot.Index == end.Page ? end.Word : text.Words.Count - 1;
        if (from > to) (from, to) = (to, from);
        from = Math.Clamp(from, 0, text.Words.Count - 1);
        to = Math.Clamp(to, 0, text.Words.Count - 1);
        for (var index = from; index <= to; index++)
            DrawWord(dc, text.Words[index], scaleX, scaleY, SelectionBrush);
    }

    private static void DrawWord(DrawingContext dc, PdfOps.PdfTextWord word, double scaleX, double scaleY, Brush brush)
    {
        var rect = new Rect(word.Left * scaleX, word.Top * scaleY,
                            Math.Max(1, word.Width * scaleX), Math.Max(1, word.Height * scaleY));
        dc.DrawRectangle(brush, null, rect);
    }

    // MARK: Search

    /// <summary>Finds the next or previous match, moving within the current page first and then wrapping
    /// around the document. Returns the text for the find bar.</summary>
    public async Task<string> FindAsync(string query, bool forward)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            ClearSearch();
            return "";
        }
        if (PageCount == 0) return L10n.T("No text found");

        _searchCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _searchCancel = cancel;

        try
        {
            if (!string.Equals(query, _searchQuery, StringComparison.Ordinal))
            {
                _searchQuery = query;
                ResetHits();
            }

            if (_searchPage >= 0 && _searchHits.Count > 0)
            {
                var next = _searchIndex + (forward ? 1 : -1);
                if (next >= 0 && next < _searchHits.Count)
                {
                    _searchIndex = next;
                    ShowHit();
                    return Status();
                }
            }

            var start = _searchPage >= 0
                ? (_searchPage + (forward ? 1 : -1) + PageCount) % PageCount
                : Math.Clamp(CurrentPage - 1, 0, PageCount - 1);

            var progress = new Progress<int>(page =>
            {
                if (!cancel.IsCancellationRequested) SearchProgress?.Invoke(page);
            });

            var result = await PdfOps.FindPageAsync(_path, query, start, forward, progress, cancel.Token);
            if (_disposed || cancel.IsCancellationRequested) return "";

            if (result == null)
            {
                ResetHits();
                return L10n.T("No matches");
            }

            _searchPage = result.Page;
            _searchHits = result.Hits.ToList();
            _searchIndex = forward ? 0 : _searchHits.Count - 1;

            var slot = _slots[result.Page];
            if (slot.Text == null)
            {
                slot.Text = result.Text;
                slot.TextFailed = false;
                slot.Layer.Cursor = result.Text.Words.Count > 0 ? Cursors.IBeam : Cursors.Arrow;
            }

            ShowHit();
            return Status();
        }
        catch (OperationCanceledException)
        {
            return "";
        }
        catch (Exception ex)
        {
            Log.Write($"PDF search failed ({_path}): {ex.Message}");
            return L10n.T("No text found");
        }
        finally
        {
            if (ReferenceEquals(_searchCancel, cancel)) _searchCancel = null;
        }
    }

    public void ClearSearch()
    {
        _searchCancel?.Cancel();
        _searchCancel = null;
        _searchQuery = "";
        ResetHits();
    }

    private void ResetHits()
    {
        _searchPage = -1;
        _searchHits.Clear();
        _searchIndex = -1;
        InvalidateMarks();
    }

    private void ShowHit()
    {
        if (_searchPage < 0 || _searchIndex < 0 || _searchIndex >= _searchHits.Count) return;

        var slot = _slots[_searchPage];
        if (slot.Text is not { } text || text.Words.Count == 0) return;

        var hit = _searchHits[_searchIndex];
        var word = text.Words[Math.Clamp(hit.FirstWord, 0, text.Words.Count - 1)];
        var height = slot.ContentHeight > 0 ? slot.ContentHeight : slot.Height;
        var y = slot.Top + word.Top * (height / Math.Max(1, text.Height));
        _scroll.ScrollToVerticalOffset(Math.Max(0, y - Math.Max(1, _scroll.ViewportHeight) * 0.3));
        InvalidateMarks();
        QueueVisible();
    }

    private string Status()
    {
        if (_searchPage < 0 || _searchIndex < 0 || _searchHits.Count == 0) return "";
        return $"{L10n.T("Page")} {_searchPage + 1} · {_searchIndex + 1}/{_searchHits.Count}";
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scanTimer.Stop();
        _searchCancel?.Cancel();
        _cancel.Cancel();
        ClearCache();
        foreach (var slot in _slots)
        {
            slot.Text = null;
            slot.TextPending = false;
        }
    }
}
