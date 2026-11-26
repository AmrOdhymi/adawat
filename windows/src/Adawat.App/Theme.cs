using System.Windows;
using System.Windows.Media;
using Adawat.Core;

namespace Adawat.App;

/// <summary>
/// The colours, type and corner radii shared by every window. Both approved directions keep
/// their exact preview palettes; switching theme or mode only changes the mutable brushes, so
/// controls that captured a brush reference update in place.
/// </summary>
public static class Theme
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.PaperInk;
    public static AppThemeMode CurrentMode { get; private set; } = AppThemeMode.Light;
    public static bool IsGraphite => CurrentTheme == AppTheme.GraphitePro;

    public static event Action? Changed;

    public static Color AccentColor { get; private set; } = Color.FromRgb(0x9A, 0x3B, 0x2C);
    public static Color ShadowColor { get; private set; } = Colors.Black;

    // Live brushes stay outside Application.Resources: WPF seals every Freezable added to the
    // application dictionary, and a sealed brush can no longer change colour. Publish() below
    // stores frozen snapshots instead, so these instances remain mutable.
    public static SolidColorBrush Bg { get; private set; } = new();
    public static SolidColorBrush Surface { get; private set; } = new();
    public static SolidColorBrush Surface2 { get; private set; } = new();
    public static SolidColorBrush Field { get; private set; } = new();
    public static SolidColorBrush Ink { get; private set; } = new();
    public static SolidColorBrush Muted { get; private set; } = new();
    public static SolidColorBrush Line { get; private set; } = new();
    public static SolidColorBrush LineStrong { get; private set; } = new();
    public static SolidColorBrush Accent { get; private set; } = new();
    public static SolidColorBrush Accent2 { get; private set; } = new();
    public static SolidColorBrush AccentSoft { get; private set; } = new();
    public static SolidColorBrush AccentHover { get; private set; } = new();
    public static SolidColorBrush AccentPressed { get; private set; } = new();
    public static SolidColorBrush OnAccent { get; private set; } = new();
    public static SolidColorBrush Danger { get; private set; } = new();
    public static SolidColorBrush DangerSoft { get; private set; } = new();
    public static SolidColorBrush Ok { get; private set; } = new();
    public static SolidColorBrush PetalFill { get; private set; } = new();
    public static SolidColorBrush PetalHover { get; private set; } = new();
    public static SolidColorBrush PetalStroke { get; private set; } = new();
    public static SolidColorBrush Hub { get; private set; } = new();
    public static SolidColorBrush Disc { get; private set; } = new();
    public static SolidColorBrush ChipBg { get; private set; } = new();
    public static SolidColorBrush HudBg { get; private set; } = new();
    public static SolidColorBrush OnHud { get; private set; } = new();
    public static SolidColorBrush HudMuted { get; private set; } = new();
    public static SolidColorBrush Card { get; private set; } = new();
    public static SolidColorBrush Hairline { get; private set; } = new();
    public static SolidColorBrush ButtonFill { get; private set; } = new();
    public static SolidColorBrush ButtonHover { get; private set; } = new();
    public static SolidColorBrush ButtonPressed { get; private set; } = new();
    public static SolidColorBrush QrInk { get; private set; } = new();
    public static SolidColorBrush Mint { get; private set; } = new();

    public static FontFamily Font { get; private set; } = new("Segoe UI Variable Text, Segoe UI, Tahoma");
    public static FontFamily Display { get; private set; } = new("Segoe UI Variable Display, Segoe UI, Tahoma");
    public static FontFamily Mono { get; private set; } = new("Cascadia Mono, Consolas, Courier New");
    public static FontFamily LabelFont { get; private set; } = new("Segoe UI Variable Text, Segoe UI, Tahoma");
    public static FontFamily Icons { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static FlowDirection Direction => FlowDirection.LeftToRight;

    // Paper keeps the tight corners from its preview. Graphite Pro is deliberately rounder,
    // matching the softer directions without changing a single colour token.
    public static CornerRadius SmallRadius => new(IsGraphite ? 6 : 2);
    public static CornerRadius MediumRadius => new(IsGraphite ? 14 : 3);
    public static CornerRadius LargeRadius => new(IsGraphite ? 18 : 4);
    public static CornerRadius XlRadius => new(IsGraphite ? 20 : 6);
    public static CornerRadius CardRadius => new(IsGraphite ? 14 : 3);
    public static CornerRadius ControlRadius => new(IsGraphite ? 10 : 2);
    public static CornerRadius PillRadius => new(IsGraphite ? 999 : 2);
    public static double SmallRadiusValue => IsGraphite ? 6 : 2;
    public static double MediumRadiusValue => IsGraphite ? 14 : 3;
    public static double LargeRadiusValue => IsGraphite ? 18 : 4;
    public static double CardRadiusValue => IsGraphite ? 14 : 3;
    public static double ControlRadiusValue => IsGraphite ? 10 : 2;

    public static double R(double paper, double graphite) => IsGraphite ? graphite : paper;

    public static void ApplyFromSettings() => Apply(Settings.Current.Theme, Settings.Current.ThemeMode);

    /// <summary>
    /// Publishes frozen snapshots of the live brushes under the <c>Theme.*</c> keys. WPF seals
    /// every Freezable added to <see cref="Application.Resources"/>, so publishing the live
    /// instances would freeze them and the next theme change would throw. Each call replaces the
    /// snapshots, which is what makes DynamicResource consumers recolour immediately.
    /// </summary>
    public static void Publish()
    {
        if (Application.Current is not { } app) return;
        var r = app.Resources;
        r["Theme.Bg"] = Snapshot(Bg);
        r["Theme.Surface"] = Snapshot(Surface);
        r["Theme.Surface2"] = Snapshot(Surface2);
        r["Theme.Field"] = Snapshot(Field);
        r["Theme.Ink"] = Snapshot(Ink);
        r["Theme.Muted"] = Snapshot(Muted);
        r["Theme.Line"] = Snapshot(Line);
        r["Theme.LineStrong"] = Snapshot(LineStrong);
        r["Theme.Accent"] = Snapshot(Accent);
        r["Theme.Accent2"] = Snapshot(Accent2);
        r["Theme.AccentSoft"] = Snapshot(AccentSoft);
        r["Theme.AccentHover"] = Snapshot(AccentHover);
        r["Theme.AccentPressed"] = Snapshot(AccentPressed);
        r["Theme.OnAccent"] = Snapshot(OnAccent);
        r["Theme.Danger"] = Snapshot(Danger);
        r["Theme.DangerSoft"] = Snapshot(DangerSoft);
        r["Theme.Ok"] = Snapshot(Ok);
        r["Theme.PetalFill"] = Snapshot(PetalFill);
        r["Theme.PetalStroke"] = Snapshot(PetalStroke);
        r["Theme.Hub"] = Snapshot(Hub);
        r["Theme.Disc"] = Snapshot(Disc);
        r["Theme.ChipBg"] = Snapshot(ChipBg);
        r["Theme.HudBg"] = Snapshot(HudBg);
        r["Theme.OnHud"] = Snapshot(OnHud);
        r["Theme.HudMuted"] = Snapshot(HudMuted);
        r["Theme.Card"] = Snapshot(Card);
        r["Theme.Hairline"] = Snapshot(Hairline);
        r["Theme.ButtonFill"] = Snapshot(ButtonFill);
        r["Theme.ButtonHover"] = Snapshot(ButtonHover);
        r["Theme.ButtonPressed"] = Snapshot(ButtonPressed);
        r["Theme.QrInk"] = Snapshot(QrInk);
        r["Theme.Mint"] = Snapshot(Mint);
        r["Theme.Font"] = Font;
        r["Theme.Display"] = Display;
        r["Theme.LabelFont"] = LabelFont;

        // CornerRadius is a struct, not a Freezable, so it can live in the application dictionary
        // safely. Controls point at these keys with SetResourceReference, which makes an open
        // window pick up the new corners on a theme switch instead of keeping its build-time value.
        r["Theme.RadiusSmall"] = SmallRadius;
        r["Theme.RadiusMedium"] = MediumRadius;
        r["Theme.RadiusLarge"] = LargeRadius;
        r["Theme.RadiusXl"] = XlRadius;
        r["Theme.RadiusCard"] = CardRadius;
        r["Theme.RadiusControl"] = ControlRadius;
        r["Theme.RadiusPill"] = PillRadius;
    }

    /// <summary>A frozen copy for application resources; the live brush stays mutable.</summary>
    private static SolidColorBrush Snapshot(SolidColorBrush brush)
    {
        var copy = brush.Clone();
        copy.Freeze();
        return copy;
    }

    public static void Apply(AppTheme theme, AppThemeMode mode)
    {
        var palette = PaletteFor(theme, mode);
        CurrentTheme = theme;
        CurrentMode = mode;

        AccentColor = palette.Accent;
        ShadowColor = Colors.Black;

        Bg = Set(Bg, palette.Bg);
        Surface = Set(Surface, palette.Surface);
        Surface2 = Set(Surface2, palette.Surface2);
        Field = Set(Field, palette.Field);
        Ink = Set(Ink, palette.Text);
        Muted = Set(Muted, palette.Muted);
        Line = Set(Line, palette.Line);
        LineStrong = Set(LineStrong, palette.LineStrong);
        Accent = Set(Accent, palette.Accent);
        Accent2 = Set(Accent2, palette.Accent2);
        AccentSoft = Set(AccentSoft, palette.AccentSoft);
        AccentHover = Set(AccentHover, palette.Accent2);
        AccentPressed = Set(AccentPressed, Shade(palette.Accent, 0.82));
        OnAccent = Set(OnAccent, palette.OnAccent);
        Danger = Set(Danger, palette.Danger);
        DangerSoft = Set(DangerSoft, WithAlpha(palette.Danger, 0x24));
        Ok = Set(Ok, palette.Ok);
        PetalFill = Set(PetalFill, palette.Petal);
        PetalHover = Set(PetalHover, palette.PetalHover);
        PetalStroke = Set(PetalStroke, palette.LineStrong);
        Hub = Set(Hub, palette.Hub);
        Disc = Set(Disc, WithAlpha(palette.Hub, 0xE8));
        ChipBg = Set(ChipBg, palette.ChipBg);
        HudBg = Set(HudBg, palette.HudBg);
        OnHud = Set(OnHud, Color.FromRgb(0xF2, 0xF5, 0xF9));
        HudMuted = Set(HudMuted, Color.FromArgb(0xA6, 0xF2, 0xF5, 0xF9));
        Card = Set(Card, WithAlpha(palette.Text, 0x0B));
        Hairline = Set(Hairline, palette.Line);
        ButtonFill = Set(ButtonFill, WithAlpha(palette.Text, 0x14));
        ButtonHover = Set(ButtonHover, WithAlpha(palette.Text, 0x1C));
        ButtonPressed = Set(ButtonPressed, WithAlpha(palette.Text, 0x24));
        QrInk = Set(QrInk, palette.QrInk);
        Mint = Set(Mint, palette.Accent2);

        if (theme == AppTheme.PaperInk)
        {
            Font = new FontFamily("Segoe UI, Tahoma");
            Display = new FontFamily("Georgia, Times New Roman, serif");
            LabelFont = Font;
        }
        else
        {
            Font = new FontFamily("Segoe UI Variable Text, Segoe UI, Tahoma");
            Display = new FontFamily("Segoe UI Variable Display, Segoe UI, Tahoma");
            LabelFont = Mono;
        }

        Changed?.Invoke();
        Publish();
    }

    private static SolidColorBrush Set(SolidColorBrush brush, Color color)
    {
        // Defensive: a frozen brush cannot be mutated. Replace it so a theme change can never
        // abort halfway, even if some future code freezes a live brush.
        if (brush.IsFrozen) return new SolidColorBrush(color);
        if (brush.Color != color) brush.Color = color;
        return brush;
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Shade(Color color, double factor) => Color.FromRgb(
        (byte)Math.Clamp(color.R * factor, 0, 255),
        (byte)Math.Clamp(color.G * factor, 0, 255),
        (byte)Math.Clamp(color.B * factor, 0, 255));

    private static Palette PaletteFor(AppTheme theme, AppThemeMode mode) =>
        theme == AppTheme.GraphitePro
            ? mode == AppThemeMode.Dark ? GraphiteDark : GraphiteLight
            : mode == AppThemeMode.Dark ? PaperDark : PaperLight;

    private sealed record Palette(
        Color Bg, Color Surface, Color Surface2, Color Field, Color Text, Color Muted,
        Color Line, Color LineStrong, Color Accent, Color Accent2, Color AccentSoft, Color OnAccent,
        Color Danger, Color Ok, Color Petal, Color PetalHover, Color Hub, Color HudBg, Color ChipBg, Color QrInk);

    private static readonly Palette PaperLight = new(
        Bg: Rgb(0xEF, 0xE8, 0xD9), Surface: Rgb(0xFB, 0xF7, 0xEE), Surface2: Rgb(0xF6, 0xF0, 0xE2),
        Field: Rgb(0xF7, 0xF2, 0xE6), Text: Rgb(0x24, 0x1F, 0x18), Muted: Rgb(0x6A, 0x61, 0x54),
        Line: Rgb(0xDD, 0xD2, 0xBD), LineStrong: Rgb(0xC6, 0xB8, 0x9E),
        Accent: Rgb(0x9A, 0x3B, 0x2C), Accent2: Rgb(0xB4, 0x5A, 0x3F), AccentSoft: Rgb(0xF0, 0xDE, 0xD2),
        OnAccent: Rgb(0xFF, 0xFA, 0xF2), Danger: Rgb(0x8C, 0x2F, 0x24), Ok: Rgb(0x4F, 0x6B, 0x3A),
        Petal: Rgb(0xF2, 0xEA, 0xD9), PetalHover: Rgb(0xE9, 0xDC, 0xC3), Hub: Rgb(0xFF, 0xFD, 0xF7),
        HudBg: Rgb(0x2B, 0x25, 0x1D), ChipBg: Rgb(0xF2, 0xEC, 0xDF), QrInk: Rgb(0x24, 0x1F, 0x18));

    private static readonly Palette PaperDark = new(
        Bg: Rgb(0x1C, 0x19, 0x15), Surface: Rgb(0x26, 0x22, 0x1C), Surface2: Rgb(0x2D, 0x28, 0x21),
        Field: Rgb(0x33, 0x2D, 0x25), Text: Rgb(0xEC, 0xE3, 0xD3), Muted: Rgb(0xB1, 0xA6, 0x91),
        Line: Rgb(0x45, 0x3D, 0x32), LineStrong: Rgb(0x5C, 0x52, 0x43),
        Accent: Rgb(0xD9, 0x81, 0x64), Accent2: Rgb(0xE5, 0xA1, 0x84), AccentSoft: Rgb(0x3B, 0x2A, 0x23),
        OnAccent: Rgb(0x1D, 0x13, 0x0F), Danger: Rgb(0xE8, 0x8A, 0x76), Ok: Rgb(0x9F, 0xBF, 0x7D),
        Petal: Rgb(0x30, 0x2A, 0x22), PetalHover: Rgb(0x3D, 0x35, 0x29), Hub: Rgb(0x22, 0x1E, 0x19),
        HudBg: Rgb(0x12, 0x10, 0x0D), ChipBg: Rgb(0x30, 0x2A, 0x22), QrInk: Rgb(0x24, 0x1F, 0x18));

    private static readonly Palette GraphiteDark = new(
        Bg: Rgb(0x0E, 0x10, 0x12), Surface: Rgb(0x16, 0x19, 0x1C), Surface2: Rgb(0x1B, 0x1F, 0x23),
        Field: Rgb(0x1E, 0x23, 0x28), Text: Rgb(0xE9, 0xEC, 0xEF), Muted: Rgb(0x8D, 0x99, 0xA4),
        Line: Rgb(0x2A, 0x30, 0x36), LineStrong: Rgb(0x3D, 0x45, 0x4E),
        Accent: Rgb(0xE2, 0xA3, 0x3F), Accent2: Rgb(0xF0, 0xBD, 0x66), AccentSoft: Rgb(0x32, 0x28, 0x0F),
        OnAccent: Rgb(0x17, 0x12, 0x07), Danger: Rgb(0xE0, 0x62, 0x52), Ok: Rgb(0x55, 0xC0, 0x8A),
        Petal: Rgb(0x1D, 0x22, 0x27), PetalHover: Rgb(0x28, 0x31, 0x3A), Hub: Rgb(0x11, 0x14, 0x17),
        HudBg: Rgb(0x08, 0x0A, 0x0B), ChipBg: Rgb(0x20, 0x25, 0x2A), QrInk: Rgb(0x11, 0x14, 0x17));

    private static readonly Palette GraphiteLight = new(
        Bg: Rgb(0xF1, 0xF2, 0xF4), Surface: Rgb(0xFF, 0xFF, 0xFF), Surface2: Rgb(0xF6, 0xF7, 0xF9),
        Field: Rgb(0xF4, 0xF5, 0xF7), Text: Rgb(0x14, 0x17, 0x1A), Muted: Rgb(0x5C, 0x68, 0x73),
        Line: Rgb(0xDD, 0xE0, 0xE4), LineStrong: Rgb(0xC3, 0xC9, 0xD0),
        Accent: Rgb(0x9A, 0x62, 0x0F), Accent2: Rgb(0xC9, 0x8A, 0x2A), AccentSoft: Rgb(0xF6, 0xEA, 0xD2),
        OnAccent: Rgb(0xFF, 0xFF, 0xFF), Danger: Rgb(0xB3, 0x35, 0x2A), Ok: Rgb(0x23, 0x7A, 0x52),
        Petal: Rgb(0xEE, 0xF0, 0xF3), PetalHover: Rgb(0xE2, 0xE6, 0xEA), Hub: Rgb(0xFF, 0xFF, 0xFF),
        HudBg: Rgb(0x16, 0x19, 0x1C), ChipBg: Rgb(0xF0, 0xF2, 0xF4), QrInk: Rgb(0x11, 0x14, 0x17));

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
