using System.IO;
using System.Text.Json;
using Adawat.Core;

namespace Adawat.App.Documents;

/// <summary>One remembered tab: the file on disk, its scroll ratio and its zoom.</summary>
internal sealed class PreviewTabState
{
    public PreviewTabState() { }

    public string Path { get; set; } = "";
    public double Scroll { get; set; }
    public double Zoom { get; set; } = 100;
}

/// <summary>
/// Remembers the open document tabs without keeping their contents. Only the path, the scroll ratio
/// and the zoom are written to <c>%APPDATA%\Adawat\preview-tabs.json</c>, so closing the viewer (or
/// the app) loses no tab but also holds no document in memory.
/// </summary>
internal static class PreviewTabsStore
{
    private const int MaxTabs = 20;
    private static readonly string FilePath = System.IO.Path.Combine(Settings.Folder, "preview-tabs.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static (List<PreviewTabState> Tabs, string? Active) Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return ([], null);
            var saved = JsonSerializer.Deserialize<SavedTabs>(File.ReadAllText(FilePath), Json);
            if (saved?.Tabs == null) return ([], null);
            var tabs = saved.Tabs
                .Where(tab => tab != null && !string.IsNullOrWhiteSpace(tab.Path) && File.Exists(tab.Path))
                .Take(MaxTabs)
                .ToList();
            return (tabs, saved.Active);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return ([], null);
        }
    }

    public static void Save(IEnumerable<PreviewTabState> tabs, string? active)
    {
        try
        {
            var list = tabs
                .Where(tab => File.Exists(tab.Path))
                .Take(MaxTabs)
                .ToList();
            Directory.CreateDirectory(Settings.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SavedTabs { Tabs = list, Active = active }, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Preview tabs save error: {e.Message}");
        }
    }

    private sealed class SavedTabs
    {
        public SavedTabs() { }

        public List<PreviewTabState> Tabs { get; set; } = [];
        public string? Active { get; set; }
    }
}
