using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Adawat.Core;
using Microsoft.Win32;

namespace Adawat.App;

/// <summary>Appearance, keys, where results go, compression defaults and startup, split into tabs
/// so the window never grows past the screen.</summary>
public static class SettingsWindow
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
        var settings = Settings.Current;
        var window = new ToolWindow("Adawat Settings", 820);
        (ModifierKey, string)[] keys = [(ModifierKey.Shift, "Shift"), (ModifierKey.Ctrl, "Ctrl"), (ModifierKey.Alt, "Alt"), (ModifierKey.None, "None")];

        void Save() => settings.Save();

        // Only the titles live in the array. The pages are built by a switch inside Show, because
        // converting the builder methods to delegates would make the compiler capture pages/nav/host
        // before they are assigned (CS0165).
        var pages = new (string Title, string Glyph)[]
        {
            ("Appearance", "\uE790"),
            ("Shortcuts", "\uE765"),
            ("Tools", "\uE90F"),
            ("Results", "\uE8A5"),
            ("Compression", "\uE7C3"),
            ("General", "\uE713"),
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nav = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        var host = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MinHeight = 360,
            MaxHeight = 468,
            Padding = new Thickness(2, 0, 10, 0),
        };
        Grid.SetColumn(host, 1);
        root.Children.Add(nav);
        root.Children.Add(host);

        var items = new List<(Border Box, TextBlock Label, TextBlock Glyph)>();
        var active = 0;

        void Show(int index)
        {
            active = Math.Clamp(index, 0, pages.Length - 1);
            host.Content = active switch
            {
                0 => Appearance(),
                1 => Shortcuts(),
                2 => Tools(),
                3 => Results(),
                4 => Compression(),
                _ => General(),
            };
            host.ScrollToTop();
        }

        void PaintNav()
        {
            for (var i = 0; i < items.Count; i++)
            {
                var on = i == active;
                items[i].Box.Background = on ? Theme.AccentSoft : Brushes.Transparent;
                items[i].Label.Foreground = on ? Theme.Accent : Theme.Ink;
                items[i].Label.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                items[i].Glyph.Foreground = on ? Theme.Accent : Theme.Muted;
            }
        }

        void BuildNav()
        {
            nav.Children.Clear();
            items.Clear();
            for (var i = 0; i < pages.Length; i++)
            {
                var index = i;
                var glyph = UI.Icon(pages[i].Glyph, 14, Theme.Muted);
                var label = UI.Text(pages[i].Title, 12.5, FontWeights.Normal, Theme.Ink);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(glyph);
                row.Children.Add(new Border { Width = 10 });
                row.Children.Add(label);
                var box = new Border
                {
                    Child = row, Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent,
                }.Radius("Theme.RadiusSmall");
                box.MouseLeftButtonUp += (_, _) => { Show(index); PaintNav(); };
                nav.Children.Add(box);
                items.Add((box, label, glyph));
            }
        }

        static void Section(StackPanel stack, string title)
        {
            var head = UI.Text(title, 12, FontWeights.SemiBold, Theme.Muted);
            head.Margin = new Thickness(0, stack.Children.Count == 0 ? 0 : 18, 0, 10);
            stack.Children.Add(head);
        }

        StackPanel Appearance()
        {
            var stack = new StackPanel();
            Section(stack, "Theme");
            stack.Children.Add(UI.Row("Theme", UI.Segmented([
                (AppTheme.PaperInk, "Paper & Ink"),
                (AppTheme.GraphitePro, "Graphite Pro")
            ], settings.Theme, v => { settings.Theme = v; ApplyAppearance(); })));
            stack.Children.Add(UI.Row("Mode", UI.Segmented([
                (AppThemeMode.Light, "Light"),
                (AppThemeMode.Dark, "Dark")
            ], settings.ThemeMode, v => { settings.ThemeMode = v; ApplyAppearance(); })));
            stack.Children.Add(UI.Caption("Paper & Ink is warm and quiet. Graphite Pro is dense and technical. Each keeps its own Light and Dark palette.").Pad(0, 8));
            return stack;
        }

        StackPanel Shortcuts()
        {
            var stack = new StackPanel();
            (string, string)[] hotkeys = [
                ("Ctrl+Shift+F", "Ctrl + Shift + F"),
                ("Ctrl+Alt+F", "Ctrl + Alt + F"),
                ("Ctrl+Shift+Space", "Ctrl + Shift + Space"),
                ("Alt+Shift+F", "Alt + Shift + F"),
                ("None", "None")
            ];
            (string, string)[] quickHotkeys = [
                ("Ctrl+Space", "Ctrl + Space"),
                ("Ctrl+Shift+Space", "Ctrl + Shift + Space"),
                ("Alt+Space", "Alt + Space"),
                ("Ctrl+Alt+Space", "Ctrl + Alt + Space"),
                ("None", "None")
            ];
            Section(stack, "Global shortcuts");
            stack.Children.Add(UI.Row("Open Float shelf", UI.Picker(hotkeys, settings.FloatHotkey, k => { settings.FloatHotkey = k; Save(); GlobalHotkeys.Update(); })));
            stack.Children.Add(UI.Row("Quick tools wheel", UI.Picker(quickHotkeys, settings.QuickWheelHotkey, k => { settings.QuickWheelHotkey = k; Save(); GlobalHotkeys.Update(); })));
            stack.Children.Add(UI.Caption("Drag files anywhere, then press the key to open its wheel right away.").Pad(0, 8));

            Section(stack, "Wheel modifiers");
            stack.Children.Add(UI.Row("Convert wheel", UI.Picker(keys, settings.ConvertKey, k => { settings.ConvertKey = k; Save(); })));
            stack.Children.Add(UI.Row("Tools wheel", UI.Picker(keys, settings.ToolsKey, k => { settings.ToolsKey = k; Save(); })));
            stack.Children.Add(UI.Row("Actions wheel", UI.Picker(keys, settings.ActionsKey, k => { settings.ActionsKey = k; Save(); })));
            stack.Children.Add(UI.Caption("Hold a modifier while dropping files to open that wheel directly.").Pad(0, 8));
            return stack;
        }

        StackPanel Tools()
        {
            var stack = new StackPanel();
            Section(stack, "Quick Wheel");
            (string Id, string Name)[] quickTools = [
                ("screenshot", "Screenshot"),
                ("bluetooth", "Bluetooth Transfer"),
                ("make_qr", "Create QR Code"),
                ("read_qr", "Read QR Code"),
                ("keyboard_swap", "Fix Keyboard Layout (Ar/En)"),
                ("launcher", "Quick Launcher"),
                ("notepad", "Notepad"),
                ("clipboard", "Clipboard History"),
                ("documents", "Document Viewer"),
                ("screen_text", "Extract Text (OCR)"),
            ];
            foreach (var (id, name) in quickTools)
            {
                stack.Children.Add(UI.Check(name, settings.IsQuickToolEnabled(id), v => { settings.SetQuickToolEnabled(id, v); Save(); }).Pad(0, 3));
            }

            Section(stack, "Text extraction");
            var ocrLangs = new List<(string Value, string Title)> { ("auto", "Auto (All)") };
            foreach (var l in ScreenTextExtractor.GetInstalledLanguages())
            {
                ocrLangs.Add((l.Tag, l.DisplayName));
            }
            stack.Children.Add(UI.Row("OCR Language", UI.Picker(ocrLangs, settings.OcrLanguage ?? "auto", v => { settings.OcrLanguage = v; Save(); })));
            return stack;
        }

        StackPanel Results()
        {
            var stack = new StackPanel();
            Section(stack, "Output");
            stack.Children.Add(UI.Row("Save results", UI.Picker([(OutputLocation.NextToOriginal, "Next to the original"), (OutputLocation.Downloads, "Downloads folder"), (OutputLocation.Desktop, "Desktop")],
                                                                 settings.OutputLocation, v => { settings.OutputLocation = v; Save(); })));
            stack.Children.Add(UI.Check("Show results in a Float", settings.ShowResultsInFloat, v => { settings.ShowResultsInFloat = v; Save(); }).Pad(0, 6));

            Section(stack, "Sounds");
            stack.Children.Add(UI.Check("Play application sounds (ticks, notifications, and chimes)", settings.PlaySounds, v => { settings.PlaySounds = v; Save(); }).Pad(0, 6));
            stack.Children.Add(UI.Caption("Uncheck to mute all sounds across the entire app."));
            return stack;
        }

        StackPanel Compression()
        {
            var stack = new StackPanel();
            Section(stack, "Defaults");
            stack.Children.Add(UI.Row("Default preset", UI.Segmented([(CompressPreset.Light, "Light"), (CompressPreset.Balanced, "Balanced"), (CompressPreset.Strong, "Strong")],
                                                                     settings.CompressPreset, v => { settings.CompressPreset = v; Save(); })));
            stack.Children.Add(UI.Row("Video", UI.Segmented([(VideoQuality.High, "High quality"), (VideoQuality.Balanced, "Balanced"), (VideoQuality.Small, "Smallest file")],
                                                            settings.VideoQuality, v => { settings.VideoQuality = v; Save(); })));
            stack.Children.Add(UI.Caption("These presets fill in the compression dialogs; you can still change them per file.").Pad(0, 8));
            return stack;
        }

        StackPanel General()
        {
            var stack = new StackPanel();
            Section(stack, "Startup");
            stack.Children.Add(UI.Check("Start Adawat when Windows starts", Startup.IsEnabled, Startup.Set).Pad(0, 6));

            Section(stack, "Clipboard");
            stack.Children.Add(UI.Row("Text history limit", UI.Picker([(100, "100 items"), (500, "500 items"), (1000, "1000 items"), (0, "Unlimited")], settings.ClipboardTextLimit, v => { settings.ClipboardTextLimit = v; Save(); ClipboardHistory.Save(ClipboardHistory.Load()); })));
            stack.Children.Add(UI.Row("Image history limit", UI.Picker([(10, "10 images"), (30, "30 images"), (50, "50 images")], settings.ClipboardImageLimit, v => { settings.ClipboardImageLimit = v; Save(); ClipboardHistory.Save(ClipboardHistory.Load()); })));
            stack.Children.Add(UI.Caption("Pinned items are kept. Lower limits remove the oldest non-pinned entries.").Pad(0, 4));

            Section(stack, "Deleting");
            stack.Children.Add(UI.Check("Confirm before deleting clipboard entries", settings.ConfirmClipboardDelete, v => { settings.ConfirmClipboardDelete = v; Save(); }).Pad(0, 3));
            stack.Children.Add(UI.Check("Confirm before deleting notes", settings.ConfirmNoteDelete, v => { settings.ConfirmNoteDelete = v; Save(); }).Pad(0, 3));
            stack.Children.Add(UI.Caption("Uncheck for direct delete with no question."));

            Section(stack, "Tools");
            var ffmpeg = ProcessRunner.Find("ffmpeg");
            stack.Children.Add(UI.Caption(ffmpeg != null ? $"ffmpeg: {ffmpeg}" : "ffmpeg is not installed. Install it with: winget install ffmpeg"));
            return stack;
        }

        void BuildFooter()
        {
            window.ClearLeading();
            window.ClearTrailing();
            window.Leading(UI.Button("Restore Defaults", () =>
            {
                var defaults = new Settings();
                settings.Theme = defaults.Theme; settings.ThemeMode = defaults.ThemeMode;
                settings.ConvertKey = defaults.ConvertKey; settings.ToolsKey = defaults.ToolsKey; settings.ActionsKey = defaults.ActionsKey;
                settings.OutputLocation = defaults.OutputLocation; settings.ShowResultsInFloat = defaults.ShowResultsInFloat; settings.PlaySounds = defaults.PlaySounds;
                settings.ConfirmClipboardDelete = defaults.ConfirmClipboardDelete; settings.ConfirmNoteDelete = defaults.ConfirmNoteDelete;
                settings.ClipboardTextLimit = defaults.ClipboardTextLimit; settings.ClipboardImageLimit = defaults.ClipboardImageLimit;
                settings.CompressPreset = defaults.CompressPreset; settings.VideoQuality = defaults.VideoQuality;
                Theme.Apply(settings.Theme, settings.ThemeMode);
                UI.InstallThemeResources();
                UI.RefreshOpenWindows();
                Save();
                // Reopen after this window has fully closed; reopening inside the click handler races the close.
                window.Closed += (_, _) => Present();
                window.CloseOnce();
            }));
            window.Trailing(UI.Button("Close", window.CloseOnce, primary: true));
        }

        void ApplyAppearance()
        {
            Save();
            Theme.Apply(settings.Theme, settings.ThemeMode);
            UI.InstallThemeResources();
            UI.RefreshOpenWindows();
            BuildNav();
            PaintNav();
            Show(active);
            BuildFooter();
        }

        BuildNav();
        Show(0);
        PaintNav();
        BuildFooter();
        window.Body = root;
        return window;
    }
}

/// <summary>Starting with Windows, through the per-user Run key (no administrator rights needed).</summary>
public static class Startup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled => Registry.CurrentUser.OpenSubKey(Key)?.GetValue("Adawat") != null;

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("Adawat", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("Adawat", false);
    }
}
