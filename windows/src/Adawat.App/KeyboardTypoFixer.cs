using System.Text;
using Adawat.Core;

namespace Adawat.App;

/// <summary>
/// Converts text mistyped in the wrong keyboard layout (English layout typing Arabic, or Arabic layout typing English).
/// Detects the language automatically and inverts the characters according to the standard 101/102 Arabic keyboard mapping.
/// </summary>
public static class KeyboardTypoFixer
{
    private static readonly Dictionary<char, string> EnToAr = new()
    {
        // Row 1
        ['`'] = "ذ", ['~'] = "ّ",
        // Row 2
        ['q'] = "ض", ['Q'] = "َ",
        ['w'] = "ص", ['W'] = "ً",
        ['e'] = "ث", ['E'] = "ُ",
        ['r'] = "ق", ['R'] = "ٌ",
        ['t'] = "ف", ['T'] = "لإ",
        ['y'] = "غ", ['Y'] = "إ",
        ['u'] = "ع", ['U'] = "‘",
        ['i'] = "ه", ['I'] = "÷",
        ['o'] = "خ", ['O'] = "×",
        ['p'] = "ح", ['P'] = "؛",
        ['['] = "ج", ['{'] = "<",
        [']'] = "د", ['}'] = ">",
        // Row 3
        ['a'] = "ش", ['A'] = "ِ",
        ['s'] = "س", ['S'] = "ٍ",
        ['d'] = "ي", ['D'] = "]",
        ['f'] = "ب", ['F'] = "[",
        ['g'] = "ل", ['G'] = "لأ",
        ['h'] = "ا", ['H'] = "أ",
        ['j'] = "ت", ['J'] = "ـ",
        ['k'] = "ن", ['K'] = "،",
        ['l'] = "م", ['L'] = "/",
        [';'] = "ك", [':'] = ":",
        ['\''] = "ط", ['"'] = "\"",
        // Row 4
        ['z'] = "ئ", ['Z'] = "~",
        ['x'] = "ء", ['X'] = "ْ",
        ['c'] = "ؤ", ['C'] = "}",
        ['v'] = "ر", ['V'] = "{",
        ['b'] = "لا", ['B'] = "لآ",
        ['n'] = "ى", ['N'] = "آ",
        ['m'] = "ة", ['M'] = "’",
        [','] = "و", ['<'] = ",",
        ['.'] = "ز", ['>'] = ".",
        ['/'] = "ظ", ['?'] = "؟",
    };

    private static readonly Dictionary<string, string> ArToEnLigatures = new()
    {
        ["لا"] = "b", ["\uFEFB"] = "b",
        ["لآ"] = "B", ["\uFEF5"] = "B",
        ["لأ"] = "G", ["\uFEF7"] = "G",
        ["لإ"] = "T", ["\uFEF9"] = "T",
    };

    private static readonly Dictionary<char, char> ArToEn = new()
    {
        // Row 1
        ['ذ'] = '`', ['ّ'] = '~',
        // Row 2
        ['ض'] = 'q', ['َ'] = 'Q',
        ['ص'] = 'w', ['ً'] = 'W',
        ['ث'] = 'e', ['ُ'] = 'E',
        ['ق'] = 'r', ['ٌ'] = 'R',
        ['ف'] = 't',
        ['غ'] = 'y', ['إ'] = 'Y',
        ['ع'] = 'u', ['‘'] = 'U',
        ['ه'] = 'i', ['÷'] = 'I',
        ['خ'] = 'o', ['×'] = 'O',
        ['ح'] = 'p', ['؛'] = 'P',
        ['ج'] = '[', ['<'] = '{',
        ['د'] = ']', ['>'] = '}',
        // Row 3
        ['ش'] = 'a', ['ِ'] = 'A',
        ['س'] = 's', ['ٍ'] = 'S',
        ['ي'] = 'd',
        ['ب'] = 'f',
        ['ل'] = 'g',
        ['ا'] = 'h', ['أ'] = 'H',
        ['ت'] = 'j', ['ـ'] = 'J',
        ['ن'] = 'k', ['،'] = 'K',
        ['م'] = 'l',
        ['ك'] = ';',
        ['ط'] = '\'',
        // Row 4
        ['ئ'] = 'z',
        ['ء'] = 'x', ['ْ'] = 'X',
        ['ؤ'] = 'c',
        ['ر'] = 'v',
        ['ى'] = 'n', ['آ'] = 'N',
        ['ة'] = 'm', ['’'] = 'M',
        ['و'] = ',',
        ['ز'] = '.',
        ['ظ'] = '/', ['؟'] = '?',
    };

    /// <summary>
    /// Detects whether the text is predominantly English or Arabic, and converts it to the opposite layout.
    /// </summary>
    public static string Convert(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var arabicCount = 0;
        var englishCount = 0;

        foreach (var ch in text)
        {
            if (ch is >= '\u0600' and <= '\u06FF') arabicCount++;
            else if (ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')) englishCount++;
        }

        return arabicCount >= englishCount && arabicCount > 0
            ? ConvertArabicToEnglish(text)
            : ConvertEnglishToArabic(text);
    }

    public static string ConvertEnglishToArabic(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            if (EnToAr.TryGetValue(ch, out var ar)) sb.Append(ar);
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    public static string ConvertArabicToEnglish(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            // Check 2-char ligature first
            if (i + 1 < text.Length)
            {
                var pair = text.Substring(i, 2);
                if (ArToEnLigatures.TryGetValue(pair, out var enLig))
                {
                    sb.Append(enLig);
                    i++;
                    continue;
                }
            }

            var single = text.Substring(i, 1);
            if (ArToEnLigatures.TryGetValue(single, out var lig))
            {
                sb.Append(lig);
                continue;
            }

            var ch = text[i];
            if (ArToEn.TryGetValue(ch, out var en)) sb.Append(en);
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reads text from the clipboard, converts it, copies it back, and displays a HUD notification.
    /// </summary>
    public static void FixClipboard()
    {
        var text = Adawat.Core.Clipboard.Text();
        if (string.IsNullOrWhiteSpace(text))
        {
            Hud.Shared.ShowMessage(L10n.T("Clipboard is empty"), Hud.Style.Failure);
            return;
        }

        var converted = Convert(text);
        Adawat.Core.Clipboard.CopyText(converted);
        Sounds.Tick();

        var preview = converted.Length > 45 ? converted[..45] + "…" : converted;
        Hud.Shared.ShowMessage($"{L10n.T("Fixed & Copied")}: {preview}", Hud.Style.Success);
    }
}
