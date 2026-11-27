using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adawat.Core;

public enum OutputLocation { NextToOriginal, Downloads, Desktop }

public enum CompressPreset { Light, Balanced, Strong }

public enum VideoQuality { High, Balanced, Small }

public enum ModifierKey { Shift, Ctrl, Alt, None }

public enum AppTheme { PaperInk, GraphitePro }

/// <summary>Light or dark palette. Named to avoid colliding with <c>System.Windows.ThemeMode</c>.</summary>
public enum AppThemeMode { Light, Dark }

/// <summary>Everything the user can change, saved as JSON in %APPDATA%\Adawat\settings.json.</summary>
public sealed class Settings
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Adawat");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Settings Current { get; } = Load();

    public AppTheme Theme { get; set; } = AppTheme.PaperInk;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Light;

    public ModifierKey ConvertKey { get; set; } = ModifierKey.Shift;
    public ModifierKey ToolsKey { get; set; } = ModifierKey.Ctrl;
    public ModifierKey ActionsKey { get; set; } = ModifierKey.Alt;
    public string FloatHotkey { get; set; } = "Ctrl+Shift+F";
    public string QuickWheelHotkey { get; set; } = "Ctrl+Space";
    public List<string> EnabledQuickTools { get; set; } = ["screenshot", "bluetooth", "make_qr", "read_qr", "keyboard_swap", "launcher", "notepad", "clipboard", "documents", "screen_text"];

    /// <summary>Bumped when a new quick-wheel tool must appear for existing settings files. Zero means
    /// the file predates the field, so the migration adds the tools introduced since.</summary>
    public int QuickToolsVersion { get; set; }
    public string OcrLanguage { get; set; } = "auto";

    public bool IsQuickToolEnabled(string id) => EnabledQuickTools == null || EnabledQuickTools.Contains(id);

    public void SetQuickToolEnabled(string id, bool enabled)
    {
        EnabledQuickTools ??= [];
        if (enabled && !EnabledQuickTools.Contains(id)) EnabledQuickTools.Add(id);
        else if (!enabled) EnabledQuickTools.Remove(id);
    }

    public OutputLocation OutputLocation { get; set; } = OutputLocation.NextToOriginal;
    public bool ShowResultsInFloat { get; set; }
    public bool PlaySounds { get; set; } = true;

    /// <summary>Ask before removing a clipboard entry (or clearing non-pinned history).</summary>
    public bool ConfirmClipboardDelete { get; set; } = true;

    /// <summary>Ask before removing a note.</summary>
    public bool ConfirmNoteDelete { get; set; } = true;
    public double ImageQuality { get; set; } = 0.85;
    public int ResizeMaxPixels { get; set; } = 2048;
    public VideoQuality VideoQuality { get; set; } = VideoQuality.Balanced;
    public CompressPreset CompressPreset { get; set; } = CompressPreset.Balanced;
    public int CompressImageMaxPixels { get; set; }
    public int CompressVideoMaxHeight { get; set; }
    public bool HasSeenWelcome { get; set; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (IOException) { }
    }

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new();
                settings.Migrate();
                return settings;
            }
        }
        catch (Exception e) when (e is IOException or JsonException) { }
        return new();
    }

    /// <summary>Adds tools introduced after the settings file was written, without re-enabling ones the user turned off.</summary>
    private void Migrate()
    {
        if (QuickToolsVersion >= 1) return;
        EnabledQuickTools ??= [];
        if (!EnabledQuickTools.Contains("documents")) EnabledQuickTools.Add("documents");
        QuickToolsVersion = 1;
        Save();
    }
}
