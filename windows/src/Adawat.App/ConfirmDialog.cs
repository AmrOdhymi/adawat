using System.Windows;
using System.Windows.Controls;

namespace Adawat.App;

/// <summary>A small themed yes/no dialog for destructive actions such as deleting a note.</summary>
public static class ConfirmDialog
{
    /// <summary>
    /// Shows a modal confirmation owned by <paramref name="owner"/>. Returns true only when the user
    /// picks the confirm button; Escape, Cancel and the close button all return false.
    /// </summary>
    public static bool Ask(Window? owner, string title, string message, string confirmLabel = "Delete")
    {
        var dialog = new ToolWindow(title, 460)
        {
            Owner = owner,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
        };

        var stack = new StackPanel();
        stack.Children.Add(UI.Text(message, 13, wrap: true));
        dialog.Body = stack;

        var confirmed = false;
        var cancel = UI.Button("Cancel", dialog.CloseOnce);
        dialog.Leading(cancel);
        dialog.Trailing(UI.Button(confirmLabel, () =>
        {
            confirmed = true;
            dialog.CloseOnce();
        }, primary: true));
        // Start on Cancel so Enter never deletes by accident.
        dialog.Loaded += (_, _) => cancel.Focus();

        // The list closes on deactivation; hold it while the owned dialog takes focus so the caller
        // still has a live window to refresh after the answer.
        var hold = owner is null ? null : WindowLifecycle.HoldAutoClose(owner);
        try { dialog.ShowDialog(); }
        finally { hold?.Dispose(); }

        return confirmed;
    }
}
