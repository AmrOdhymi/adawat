using System.Windows;

namespace Adawat.App;

/// <summary>
/// Tracks whether a window is closing or already closed, so show/close/activate calls made from
/// <c>Deactivated</c>, timers or async completions never run against a window WPF is tearing down.
/// </summary>
internal static class WindowLifecycle
{
    private sealed class State
    {
        public bool Closing;
        public bool Closed;
    }

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(WindowLifecycle), new PropertyMetadata(null));

    /// <summary>Subscribes to <c>Closing</c>/<c>Closed</c> once per window. Safe to call more than once.</summary>
    public static void Track(Window window)
    {
        if (window.GetValue(StateProperty) is State) return;
        var state = new State();
        window.SetValue(StateProperty, state);
        window.Closing += (_, _) => state.Closing = true;
        window.Closed += (_, _) => state.Closed = true;
    }

    /// <summary>True from the moment <c>Closing</c> is raised, and stays true after the window is closed.</summary>
    public static bool IsClosingOrClosed(Window window) =>
        window.GetValue(StateProperty) is State state && (state.Closing || state.Closed);

    /// <summary>A live, visible window that can still be shown or activated.</summary>
    public static bool IsOpen(Window window) => !IsClosingOrClosed(window) && window.IsVisible;

    /// <summary>Brings a live window to the front; does nothing once it started closing.</summary>
    public static void ActivateIfOpen(Window window)
    {
        if (!IsOpen(window)) return;
        try { window.Activate(); }
        catch (InvalidOperationException) { }
    }

    /// <summary>Whether a <c>Deactivated</c> handler may close the window: still visible, no owned dialogs, not closing.</summary>
    public static bool CanAutoClose(Window window) =>
        !IsClosingOrClosed(window) && window.IsVisible && window.OwnedWindows.Count == 0;

    /// <summary>Closes the window unless it is already closing.</summary>
    public static void CloseOnce(Window window)
    {
        if (IsClosingOrClosed(window)) return;
        try { window.Close(); }
        catch (InvalidOperationException) { }
    }

    /// <summary>Hides the window unless it is already closing.</summary>
    public static void HideOnce(Window window)
    {
        if (IsClosingOrClosed(window)) return;
        window.Hide();
    }
}
