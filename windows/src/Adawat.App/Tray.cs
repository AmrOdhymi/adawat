using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Adawat.Core;
using H.NotifyIcon;

namespace Adawat.App;

/// <summary>The icon next to the clock and its menu.</summary>
public sealed class Tray : IDisposable
{
    private readonly TaskbarIcon _icon;
    private ContextMenu? _menu;

    public Tray()
    {
        var settings = Settings.Current;
        _icon = new TaskbarIcon
        {
            ToolTipText = L10n.T($"Drag files, then press {Key(settings.ConvertKey)}, {Key(settings.ToolsKey)} or {Key(settings.ActionsKey)}"),
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/Adawat.ico")),
            NoLeftClickDelay = true,
        };
        // Both clicks open our own menu. Leaving ContextMenu on the icon would send right-click
        // through the library's internal popup path, which is where the menu lost its text colours.
        _icon.TrayLeftMouseUp += (_, _) => OpenMenu();
        _icon.TrayRightMouseUp += (_, _) => OpenMenu();
        Theme.Changed += ResetMenu;
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void OpenMenu()
    {
        var menu = _menu ??= Menu();
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    /// <summary>Drops the cached menu so the next open builds it from the current theme resources.</summary>
    private void ResetMenu()
    {
        if (_menu is null) return;
        _menu.IsOpen = false;
        _menu = null;
    }

    public static string Key(ModifierKey key) => key switch { ModifierKey.Shift => "Shift", ModifierKey.Ctrl => "Ctrl", ModifierKey.Alt => "Alt", _ => "—" };

    private static ContextMenu Menu()
    {
        var menu = UI.Menu();
        MenuItem Item(string title, Action action, string? icon = null) => UI.MenuEntry(title, action, icon);
        var hint = UI.MenuEntry("Adawat — drag files, then press a modifier key", enabled: false);
        menu.Items.Add(hint);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("New Float", () => Floats.Create([]), ""));
        var closeFloats = Item("Close All Floats", Floats.CloseAll, "");
        menu.Items.Add(closeFloats);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Welcome Guide…", WelcomeWindow.Present, ""));
        menu.Items.Add(Item("Settings…", SettingsWindow.Present, ""));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Exit Adawat", () => Application.Current.Shutdown(), ""));
        menu.Opened += (_, _) => closeFloats.IsEnabled = Floats.Any;
        return menu;
    }

    public void Dispose()
    {
        Theme.Changed -= ResetMenu;
        _icon.Dispose();
    }
}
