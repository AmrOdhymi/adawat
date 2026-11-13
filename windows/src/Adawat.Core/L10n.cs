using System.Globalization;

namespace Adawat.Core;

/// <summary>Text helpers for the English interface. Arabic content in files, documents and pictures is still supported.</summary>
public static class L10n
{
    public static string T(string english) => english;

    /// <summary>Upper-cased for wheel labels.</summary>
    public static string Upper(string text) => text.ToUpper(CultureInfo.InvariantCulture);
}
