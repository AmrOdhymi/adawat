using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Adawat.Core.Pdf;

namespace Adawat.App.Documents;

/// <summary>
/// Read-only PDF pages. Every page is a light placeholder until it scrolls into view; only then is it
/// rendered from the file (which is opened and closed for that one page). Rendered pages are kept in an
/// LRU cache that never grows past 256 MB: once the cache is full the least recently read page is
/// forgotten first, while its placeholder, the scroll position and the file path stay.
/// </summary>
internal sealed class PdfView : Grid
{
    private const long BudgetBytes = 256L * 1024 * 1024;
    private const double PointsToDip = 96.0 / 72.0;
    private const int MaxRendersPerScan = 6;

    private sealed class PageSlot
    {
        public int Index;
        public double WidthPoints;
        public double HeightPoints;
        public double Top;
        public double Height;
        public Border Host = null!;
        public Image Picture = null!;
        public bool Pending;
        public bool Failed;
    }

    private sealed class Cached
    {
        public BitmapSource Image = null!;
        public long Bytes;
        public DateTime Used;
    }

    private readonly string _path;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _panel;
    private readonly List<PageSlot> _slots = [];
    private readonly Dictionary<int, Cached> _cache = [];
    private readonly HashSet<int> _visible = [];
    private readonly SemaphoreSlim _renderGate = new(1);
    private readonly CancellationTokenSource _cancel = new();
    private double _zoom;
    private long _cachedBytes;
    private int _generation;
    private int _lastPage = 1;
    private bool _queued;
    private bool _disposed;

    public PdfView(string path, IReadOnlyList<(double Width, double Height)> sizes, double zoom = 100)
    {
        _path = path;
        _zoom = Math.Clamp(zoom, 40, 250);
        ClipToBounds = true;
        SetResourceReference(Panel.BackgroundProperty, "Theme.Surface2");

        _panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(16, 14, 16, 18) };

        for (var index = 0; index < sizes.Count; index++)
        {
            var slot = new PageSlot
            {
                Index = index,
                WidthPoints = sizes[index].Width,
                HeightPoints = sizes[index].Height,
            };
            slot.Picture = new Image { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
            RenderOptions.SetBitmapScalingMode(slot.Picture, BitmapScalingMode.HighQuality);
            slot.Host = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = slot.Picture,
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
            var page = 1;
            foreach (var slot in _slots)
            {
                if (slot.Top > y) break;
                page = slot.Index + 1;
            }

            return page;
        }
    }

    /// <summary>Raised with the 1-based page number whenever the reading position changes page.</summary>
    public event Action<int>? PageChanged;

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
            slot.Top = top;
            slot.Height = height;
            top += height + slot.Host.Margin.Top + slot.Host.Margin.Bottom;
        }
    }

    private void QueueVisible()
    {
        if (_disposed || !IsLoaded || _queued) return;
        _queued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _queued = false;
            Scan();
        }), DispatcherPriority.Background);
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

        TrimCache();
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancel.Cancel();
        ClearCache();
    }
}
