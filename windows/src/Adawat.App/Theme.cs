using System.Windows;
using System.Windows.Media;

namespace Adawat.App;

/// <summary>Colours and type shared by every window, matching the Mac app: light surfaces, one violet accent.</summary>
public static class Theme
{
    public static readonly Color AccentColor = Color.FromRgb(0x5B, 0x47, 0xE6);
    public static readonly Brush Accent = Freeze(new SolidColorBrush(AccentColor));
    public static readonly Brush AccentSoft = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0x5B, 0x47, 0xE6)));
    public static readonly Brush Mint = Freeze(new SolidColorBrush(Color.FromRgb(0x7C, 0xF2, 0xC8)));
    public static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x2B, 0x29)));
    public static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75)));
    public static readonly Brush Surface = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFB)));
    public static readonly Brush Card = Freeze(new SolidColorBrush(Color.FromArgb(0x0B, 0, 0, 0)));
    public static readonly Brush Hairline = Freeze(new SolidColorBrush(Color.FromArgb(0x1A, 0, 0, 0)));
    public static readonly Brush ButtonFill = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0)));
    public static readonly Brush ButtonPressed = Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0, 0, 0)));
    public static readonly Brush PetalFill = Freeze(new SolidColorBrush(Color.FromArgb(0xF0, 0xF8, 0xF9, 0xFA)));
    public static readonly Brush PetalStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x5D, 0x66, 0x66)));
    public static readonly Brush Disc = Freeze(new SolidColorBrush(Color.FromArgb(0xB8, 0xEE, 0xF0, 0xF2)));

    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Tahoma");
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI, Tahoma");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static FlowDirection Direction => Core.L10n.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
