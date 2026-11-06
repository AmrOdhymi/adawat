using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Adawat.Core;

namespace Adawat.App;

/// <summary>
/// A sleek popup dialog showing the decoded QR code result.
/// Closes automatically when clicking outside or pressing Escape or clicking X.
/// </summary>
public sealed class QrResultWindow : Window
{
    public static void ShowResult(string text)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new QrResultWindow(text);
            win.Show();
            win.Activate();
        });
    }

    public QrResultWindow(string text)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        FontFamily = Theme.Font;
        FlowDirection = Theme.Direction;

        var isArabic = text.Any(c => c >= 0x0600 && c <= 0x06FF);

        var root = new Border
        {
            Background = Theme.Surface,
            CornerRadius = new CornerRadius(14),
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 6,
                Direction = 270,
                Opacity = 0.25,
                Color = Color.FromRgb(0x20, 0x20, 0x24)
            }
        };

        var stack = new StackPanel();

        // ── Header ──
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
        var closeBtn = UI.IconButton("", Close, "Close", 24);
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(UI.Icon(Catalog.Icons.Qr, 16, Theme.Accent));
        titleRow.Children.Add(new Border { Width = 8 });
        titleRow.Children.Add(UI.Text(L10n.T("Scanned QR Code"), 14, FontWeights.SemiBold, Theme.Ink));
        header.Children.Add(titleRow);
        stack.Children.Add(header);

        // ── Text Box ──
        var textBox = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 220,
            MinHeight = 60,
            Padding = new Thickness(10, 8, 10, 8),
            FontSize = 13,
            FontFamily = Theme.Font,
            FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            Background = Theme.ButtonFill,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(textBox);

        // ── Actions ──
        var actions = new DockPanel { LastChildFill = false };
        var isUrl = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);

        if (isUrl)
        {
            var url = text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + text : text;
            var openBtn = UI.Button("Open Link", () =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
                Close();
            }, primary: true, icon: "");
            DockPanel.SetDock(openBtn, Dock.Left);
            actions.Children.Add(openBtn);
        }

        var copyBtn = UI.Button("Copy", () =>
        {
            Adawat.Core.Clipboard.CopyText(text);
            Sounds.Tick();
            Hud.Shared.ShowMessage(L10n.T("Copied to clipboard"), Hud.Style.Success);
            Close();
        }, primary: !isUrl, icon: "");
        copyBtn.Margin = new Thickness(isUrl ? 8 : 0, 0, 0, 0);
        DockPanel.SetDock(copyBtn, Dock.Left);
        actions.Children.Add(copyBtn);

        var dismissBtn = UI.Button("Dismiss", Close, primary: false);
        DockPanel.SetDock(dismissBtn, Dock.Right);
        actions.Children.Add(dismissBtn);

        stack.Children.Add(actions);
        root.Child = stack;
        Content = root;

        // Dismiss when user clicks outside
        Deactivated += (_, _) => Close();

        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.Enter)
            {
                Close();
                e.Handled = true;
            }
        };

        SourceInitialized += (_, _) => Native.RoundCorners(new WindowInteropHelper(this).Handle);
    }
}
