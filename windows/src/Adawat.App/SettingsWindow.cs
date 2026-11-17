using System.Windows;
using System.Windows.Controls;
using Adawat.Core;
using Microsoft.Win32;

namespace Adawat.App;

/// <summary>Keys, where results go, compression defaults and startup.</summary>
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
        var window = new ToolWindow("Adawat Settings", 460);
        var stack = new StackPanel();
        void Section(string title) { stack.Children.Add(UI.Text(title, 12, FontWeights.SemiBold, Theme.Muted).Pad(0, 6)); }
        void Save() => settings.Save();
        (ModifierKey, string)[] keys = [(ModifierKey.Shift, "Shift"), (ModifierKey.Ctrl, "Ctrl"), (ModifierKey.Alt, "Alt"), (ModifierKey.None, "None")];

        Section("Shortcuts");
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
        stack.Children.Add(UI.Row("Open Float shelf", UI.Picker(hotkeys, settings.FloatHotkey, k => { settings.FloatHotkey = k; Save(); GlobalHotkeys.Update(); })));
        stack.Children.Add(UI.Row("Quick tools wheel", UI.Picker(quickHotkeys, settings.QuickWheelHotkey, k => { settings.QuickWheelHotkey = k; Save(); GlobalHotkeys.Update(); })));
        stack.Children.Add(UI.Row("Convert wheel", UI.Picker(keys, settings.ConvertKey, k => { settings.ConvertKey = k; Save(); })));
        stack.Children.Add(UI.Row("Tools wheel", UI.Picker(keys, settings.ToolsKey, k => { settings.ToolsKey = k; Save(); })));
        stack.Children.Add(UI.Row("Actions wheel", UI.Picker(keys, settings.ActionsKey, k => { settings.ActionsKey = k; Save(); })));
        stack.Children.Add(UI.Caption("Drag files anywhere, then press the key to open its wheel right away."));
        stack.Children.Add(UI.Divider());

        Section("Quick Wheel Tools");
        (string Id, string Name)[] quickTools = [
            ("screenshot", "Screenshot"),
            ("bluetooth", "Bluetooth Transfer"),
            ("make_qr", "Create QR Code"),
            ("read_qr", "Read QR Code"),
            ("keyboard_swap", "Fix Keyboard Layout (Ar/En)"),
            ("launcher", "Quick Launcher"),
            ("notepad", "Notepad"),
            ("clipboard", "Clipboard History"),
            ("screen_text", "Extract Text (OCR)"),
        ];
        foreach (var (id, name) in quickTools)
        {
            stack.Children.Add(UI.Check(name, settings.IsQuickToolEnabled(id), v => { settings.SetQuickToolEnabled(id, v); Save(); }).Pad(0, 3));
        }

        var ocrLangs = new List<(string Value, string Title)> { ("auto", "Auto (All)") };
        foreach (var l in ScreenTextExtractor.GetInstalledLanguages())
        {
            ocrLangs.Add((l.Tag, l.DisplayName));
        }
        stack.Children.Add(UI.Row("OCR Language", UI.Picker(ocrLangs, settings.OcrLanguage ?? "auto", v => { settings.OcrLanguage = v; Save(); })).Pad(0, 4));
        stack.Children.Add(UI.Divider());

        Section("Results");
        stack.Children.Add(UI.Row("Save results", UI.Picker([(OutputLocation.NextToOriginal, "Next to the original"), (OutputLocation.Downloads, "Downloads folder"), (OutputLocation.Desktop, "Desktop")],
                                                             settings.OutputLocation, v => { settings.OutputLocation = v; Save(); })));
        stack.Children.Add(UI.Check("Show results in a Float", settings.ShowResultsInFloat, v => { settings.ShowResultsInFloat = v; Save(); }).Pad(0, 4));
        stack.Children.Add(UI.Divider());

        Section("Sounds");
        stack.Children.Add(UI.Check("Play application sounds (ticks, notifications, and chimes)", settings.PlaySounds, v => { settings.PlaySounds = v; Save(); }).Pad(0, 4));
        stack.Children.Add(UI.Caption("Uncheck to mute all sounds across the entire app."));
        stack.Children.Add(UI.Divider());

        Section("Compression");
        stack.Children.Add(UI.Row("Default preset", UI.Segmented([(CompressPreset.Light, "Light"), (CompressPreset.Balanced, "Balanced"), (CompressPreset.Strong, "Strong")],
                                                                 settings.CompressPreset, v => { settings.CompressPreset = v; Save(); })));
        stack.Children.Add(UI.Row("Video", UI.Segmented([(VideoQuality.High, "High quality"), (VideoQuality.Balanced, "Balanced"), (VideoQuality.Small, "Smallest file")],
                                                        settings.VideoQuality, v => { settings.VideoQuality = v; Save(); })));
        stack.Children.Add(UI.Divider());

        Section("General");
        stack.Children.Add(UI.Check("Start Adawat when Windows starts", Startup.IsEnabled, Startup.Set).Pad(0, 4));
        var ffmpeg = ProcessRunner.Find("ffmpeg");
        stack.Children.Add(UI.Caption(ffmpeg != null ? $"ffmpeg: {ffmpeg}" : "ffmpeg is not installed. Install it with: winget install ffmpeg").Pad(0, 8));

        window.Body = stack;
        window.Leading(UI.Button("Restore Defaults", () =>
        {
            var defaults = new Settings();
            settings.ConvertKey = defaults.ConvertKey; settings.ToolsKey = defaults.ToolsKey; settings.ActionsKey = defaults.ActionsKey;
            settings.OutputLocation = defaults.OutputLocation; settings.ShowResultsInFloat = defaults.ShowResultsInFloat; settings.PlaySounds = defaults.PlaySounds;
            settings.CompressPreset = defaults.CompressPreset; settings.VideoQuality = defaults.VideoQuality;
            Save();
            // Reopen after this window has fully closed; reopening inside the click handler races the close.
            window.Closed += (_, _) => Present();
            window.CloseOnce();
        }));
        window.Trailing(UI.Button("Close", window.CloseOnce, primary: true));
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
