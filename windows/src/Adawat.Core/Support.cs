using System.Collections.Specialized;
using System.Globalization;
using System.Windows;

namespace Adawat.Core;

public static class Format
{
    /// <summary>"1.4 MB", in the style Windows shows sizes.</summary>
    public static string Bytes(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1) { value /= 1000; unit++; }
        return unit == 0 ? $"{bytes} {units[0]}" : value.ToString(value < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    /// <summary>"1:04.25"</summary>
    public static string Time(double seconds)
    {
        seconds = Math.Max(0, seconds);
        var minutes = (int)seconds / 60;
        return $"{minutes}:{seconds - minutes * 60:00.00}".Replace(',', '.');
    }

    public static double? ParseTime(string text)
    {
        double total = 0;
        foreach (var part in text.Trim().Split(':'))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
            total = total * 60 + value;
        }
        return total;
    }
}

/// <summary>The clipboard, from any thread: WPF needs a single-threaded apartment, so work hops onto one.</summary>
public static class Clipboard
{
    public static void CopyText(string text) => Sta(() => System.Windows.Clipboard.SetText(text));

    public static void CopyFiles(IEnumerable<string> paths)
    {
        var list = new StringCollection();
        list.AddRange(paths.ToArray());
        Sta(() => System.Windows.Clipboard.SetFileDropList(list));
    }

    public static void CopyImage(System.Windows.Media.Imaging.BitmapSource image) => Sta(() => System.Windows.Clipboard.SetImage(image));

    public static string? Text() { string? text = null; Sta(() => text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null); return text; }

    private static void Sta(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) { Retry(action); return; }
        var thread = new Thread(() => Retry(action));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    /// <summary>Another app may hold the clipboard for a moment.</summary>
    private static void Retry(Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { action(); return; }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 5) { Thread.Sleep(60); }
        }
    }
}
