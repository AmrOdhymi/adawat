using System.Windows.Interop;
using Adawat.Core;

namespace Adawat.App;

public static class GlobalHotkeys
{
    private static HwndSource? _source;
    private const int HotkeyId = 0xAD01;
    private const int QuickWheelHotkeyId = 0xAD02;

    public static void Start()
    {
        if (_source != null) return;
        var parameters = new HwndSourceParameters("Adawat.HotkeyListener")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = IntPtr.Zero,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(HwndHook);
        Native.AddClipboardFormatListener(_source.Handle);
        Register();
    }

    public static void Update()
    {
        if (_source == null) return;
        Native.UnregisterHotKey(_source.Handle, HotkeyId);
        Native.UnregisterHotKey(_source.Handle, QuickWheelHotkeyId);
        Register();
    }

    private static void Register()
    {
        if (_source == null) return;
        RegisterOne(Settings.Current.FloatHotkey, HotkeyId);
        RegisterOne(Settings.Current.QuickWheelHotkey, QuickWheelHotkeyId);
    }

    private static void RegisterOne(string shortcut, int id)
    {
        if (_source == null || string.IsNullOrWhiteSpace(shortcut) || shortcut.Equals("None", StringComparison.OrdinalIgnoreCase)) return;

        ParseShortcut(shortcut, out var mod, out var vk);
        if (vk != 0)
        {
            Native.RegisterHotKey(_source.Handle, id, mod | Native.MOD_NOREPEAT, vk);
        }
    }

    private static void ParseShortcut(string shortcut, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= Native.MOD_CONTROL;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= Native.MOD_SHIFT;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= Native.MOD_ALT;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= Native.MOD_WIN;
            else if (part.Length == 1)
            {
                var ch = char.ToUpperInvariant(part[0]);
                if (ch >= 'A' && ch <= 'Z') vk = (uint)ch;
                else if (ch >= '0' && ch <= '9') vk = (uint)ch;
            }
            else if (part.Equals("Space", StringComparison.OrdinalIgnoreCase)) vk = 0x20;
        }
    }

    private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == HotkeyId)
            {
                Floats.ToggleOrShow();
                handled = true;
            }
            else if (id == QuickWheelHotkeyId)
            {
                Wheel.QuickWheel.ToggleOrShow();
                handled = true;
            }
        }
        else if (msg == Native.WM_CLIPBOARDUPDATE)
        {
            ClipboardHistory.OnClipboardUpdated();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public static void Stop()
    {
        if (_source != null)
        {
            Native.RemoveClipboardFormatListener(_source.Handle);
            Native.UnregisterHotKey(_source.Handle, HotkeyId);
            Native.UnregisterHotKey(_source.Handle, QuickWheelHotkeyId);
            _source.RemoveHook(HwndHook);
            _source.Dispose();
            _source = null;
        }
    }
}
