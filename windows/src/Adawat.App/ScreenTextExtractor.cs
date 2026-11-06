using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Storage.Streams;
using WinRtOcr = Windows.Media.Ocr;
using WinRtImaging = Windows.Graphics.Imaging;

namespace Adawat.App;

/// <summary>
/// Uses Windows.Media.Ocr (WinRT) to detect text in a screen capture.
/// Corrects the native Windows OCR RTL bug so Arabic sentences read in proper right-to-left word order.
/// Supports selecting specific installed language packs or auto-detection.
/// </summary>
public static class ScreenTextExtractor
{
    public readonly record struct TextRegion(Rect Bounds, string Text);
    public readonly record struct OcrLanguageInfo(string Tag, string DisplayName);

    /// <summary>Returns all installed OCR language packs on this Windows system.</summary>
    public static List<OcrLanguageInfo> GetInstalledLanguages()
    {
        var list = new List<OcrLanguageInfo>();
        try
        {
            foreach (var lang in WinRtOcr.OcrEngine.AvailableRecognizerLanguages)
            {
                var name = !string.IsNullOrWhiteSpace(lang.NativeName) ? lang.NativeName : lang.DisplayName;
                list.Add(new OcrLanguageInfo(lang.LanguageTag, $"{name} ({lang.LanguageTag})"));
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to enumerate OCR languages: {ex.Message}");
        }
        return list;
    }

    /// <summary>Whether Arabic OCR language pack is installed.</summary>
    public static bool IsArabicAvailable =>
        WinRtOcr.OcrEngine.AvailableRecognizerLanguages.Any(
            l => l.LanguageTag.StartsWith("ar", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Runs OCR on <paramref name="bitmap"/>.
    /// If <paramref name="languageTag"/> is specified and not "auto", uses that language pack only.
    /// Otherwise uses installed languages with Arabic prioritized first.
    /// </summary>
    public static async Task<List<TextRegion>> ExtractAsync(BitmapSource bitmap, string? languageTag = null)
    {
        var regions = new List<TextRegion>();

        // Convert WPF BitmapSource → WinRT SoftwareBitmap via PNG round-trip
        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        encoder.Save(ms);
        ms.Position = 0;

        var ras = new InMemoryRandomAccessStream();
        var writer = new DataWriter(ras.GetOutputStreamAt(0));
        writer.WriteBytes(ms.ToArray());
        await writer.StoreAsync();
        await writer.FlushAsync();
        ras.Seek(0);

        var decoder = await WinRtImaging.BitmapDecoder.CreateAsync(ras);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            WinRtImaging.BitmapPixelFormat.Bgra8,
            WinRtImaging.BitmapAlphaMode.Premultiplied);

        var available = WinRtOcr.OcrEngine.AvailableRecognizerLanguages;

        if (available.Count == 0)
        {
            var fallback = WinRtOcr.OcrEngine.TryCreateFromUserProfileLanguages();
            if (fallback != null)
                AddRecognizedLines(regions, await fallback.RecognizeAsync(softwareBitmap));
        }
        else if (!string.IsNullOrEmpty(languageTag) && !languageTag.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // User requested a specific language
            var targetLang = available.FirstOrDefault(l =>
                string.Equals(l.LanguageTag, languageTag, StringComparison.OrdinalIgnoreCase) ||
                l.LanguageTag.StartsWith(languageTag, StringComparison.OrdinalIgnoreCase));

            if (targetLang != null)
            {
                var engine = WinRtOcr.OcrEngine.TryCreateFromLanguage(targetLang);
                if (engine != null)
                {
                    var result = await engine.RecognizeAsync(softwareBitmap);
                    AddRecognizedLines(regions, result);
                }
            }
        }
        else
        {
            // Auto mode: sort Arabic/RTL first so Arabic text isn't mangled by LTR engines
            var sorted = available
                .OrderByDescending(l => l.LanguageTag.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? 2
                                      : l.Script.Equals("Arab", StringComparison.OrdinalIgnoreCase) ? 1
                                      : 0)
                .ToList();

            foreach (var lang in sorted)
            {
                var engine = WinRtOcr.OcrEngine.TryCreateFromLanguage(lang);
                if (engine == null) continue;

                try
                {
                    var result = await engine.RecognizeAsync(softwareBitmap);
                    AddRecognizedLines(regions, result);
                }
                catch (Exception ex)
                {
                    Log.Write($"OCR failed for '{lang.LanguageTag}': {ex.Message}");
                }
            }
        }

        softwareBitmap.Dispose();
        ras.Dispose();

        // Sort lines top-to-bottom for reading order
        const double rowThreshold = 14;
        regions.Sort((a, b) =>
        {
            var dy = a.Bounds.Y - b.Bounds.Y;
            if (Math.Abs(dy) > rowThreshold) return dy < 0 ? -1 : 1;
            // On same visual line: RTL ordering (rightmost box first if Arabic exists)
            bool isAr = ContainsArabic(a.Text) || ContainsArabic(b.Text);
            return isAr ? b.Bounds.X.CompareTo(a.Bounds.X) : a.Bounds.X.CompareTo(b.Bounds.X);
        });

        return regions;
    }

    /// <summary>Runs OCR on a cropped sub-region of a bitmap (for manual drag-to-select).</summary>
    public static async Task<string> ExtractFromRegionAsync(BitmapSource bitmap, Int32Rect crop, string? languageTag = null)
    {
        var cropped = new CroppedBitmap(bitmap, crop);
        cropped.Freeze();
        var regions = await ExtractAsync(cropped, languageTag);
        return string.Join(Environment.NewLine, regions.Select(r => r.Text));
    }

    // ── helpers ──

    private static void AddRecognizedLines(List<TextRegion> regions, WinRtOcr.OcrResult result)
    {
        foreach (var line in result.Lines)
        {
            var box = LineBounds(line);
            if (box.IsEmpty) continue;

            // Only add if not overlapping >50% with an existing region
            if (regions.Any(r => OverlapRatio(r.Bounds, box) > 0.5)) continue;

            var correctedText = GetCorrectedLineText(line);
            if (!string.IsNullOrWhiteSpace(correctedText))
                regions.Add(new TextRegion(box, correctedText));
        }
    }

    /// <summary>
    /// Corrects the native Windows OCR defect where RTL (Arabic) lines are returned
    /// with words concatenated left-to-right (e.g. "المعلومات وتكنولوجيا علوم").
    /// Correctly orders Arabic words from right-to-left while preserving English/number runs.
    /// </summary>
    public static string GetCorrectedLineText(WinRtOcr.OcrLine line)
    {
        if (line.Words.Count == 0) return line.Text ?? string.Empty;
        if (line.Words.Count == 1) return line.Words[0].Text ?? string.Empty;

        bool hasArabic = line.Words.Any(w => ContainsArabic(w.Text));
        if (!hasArabic)
        {
            // Pure LTR (English / Latin): sort words left to right
            var ltr = line.Words.OrderBy(w => w.BoundingRect.X);
            return string.Join(" ", ltr.Select(w => w.Text));
        }

        // Line contains Arabic text:
        // Group words into runs of Arabic vs Latin/Neutral
        var runs = new List<WordRun>();
        WordRun? current = null;

        foreach (var word in line.Words)
        {
            var kind = GetWordKind(word.Text);
            bool isAr = kind == WordKind.Arabic || (kind == WordKind.Neutral && (current?.IsArabic ?? true));

            if (current == null || current.IsArabic != isAr)
            {
                current = new WordRun(isAr);
                runs.Add(current);
            }
            current.Words.Add(word);
        }

        // Within runs:
        // Arabic runs: sort Right-to-Left (highest X to lowest X)
        // Latin runs: sort Left-to-Right (lowest X to highest X)
        foreach (var run in runs)
        {
            if (run.IsArabic)
                run.Words.Sort((a, b) => b.BoundingRect.X.CompareTo(a.BoundingRect.X));
            else
                run.Words.Sort((a, b) => a.BoundingRect.X.CompareTo(b.BoundingRect.X));
        }

        // Overall line reading order for Arabic is Right-to-Left:
        // Runs appearing physically further right on screen come first
        runs.Sort((a, b) => b.CenterX.CompareTo(a.CenterX));

        var tokens = runs.SelectMany(r => r.Words.Select(w => w.Text));
        return string.Join(" ", tokens);
    }

    private enum WordKind { Arabic, Latin, Neutral }

    private static WordKind GetWordKind(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return WordKind.Neutral;
        bool hasAr = false, hasLat = false;
        foreach (var c in text)
        {
            if (IsArabicChar(c)) hasAr = true;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) hasLat = true;
        }
        if (hasAr) return WordKind.Arabic;
        if (hasLat) return WordKind.Latin;
        return WordKind.Neutral;
    }

    private sealed class WordRun
    {
        public bool IsArabic { get; }
        public List<WinRtOcr.OcrWord> Words { get; } = [];
        public WordRun(bool isArabic) => IsArabic = isArabic;

        public double CenterX => Words.Count == 0 ? 0 : Words.Average(w => w.BoundingRect.X + w.BoundingRect.Width / 2.0);
    }

    public static bool ContainsArabic(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var c in text)
        {
            if (IsArabicChar(c)) return true;
        }
        return false;
    }

    public static bool IsArabicChar(char c) =>
        (c >= 0x0600 && c <= 0x06FF) || // Arabic standard
        (c >= 0x0750 && c <= 0x077F) || // Arabic Supplement
        (c >= 0x08A0 && c <= 0x08FF) || // Arabic Extended-A
        (c >= 0xFB50 && c <= 0xFDFF) || // Arabic Presentation Forms-A
        (c >= 0xFE70 && c <= 0xFEFF);   // Arabic Presentation Forms-B

    private static Rect LineBounds(WinRtOcr.OcrLine line)
    {
        if (line.Words.Count == 0) return Rect.Empty;
        double left = double.MaxValue, top = double.MaxValue, right = 0, bottom = 0;
        foreach (var w in line.Words)
        {
            var b = w.BoundingRect;
            if (b.X < left) left = b.X;
            if (b.Y < top) top = b.Y;
            if (b.X + b.Width > right) right = b.X + b.Width;
            if (b.Y + b.Height > bottom) bottom = b.Y + b.Height;
        }
        return new Rect(left, top, right - left, bottom - top);
    }

    private static double OverlapRatio(Rect a, Rect b)
    {
        var intersect = Rect.Intersect(a, b);
        if (intersect.IsEmpty) return 0;
        var intersectArea = intersect.Width * intersect.Height;
        var smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return smallerArea > 0 ? intersectArea / smallerArea : 0;
    }
}
