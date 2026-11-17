using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Adawat.Core;

namespace Adawat.App;

/// <summary>
/// Confirmation and preview dialog shown after taking a screenshot.
/// Allows user to Save, Copy to clipboard, Open in an image editor, or Cancel.
/// </summary>
public sealed class ScreenshotPreviewWindow : Window
{
    private readonly BitmapSource _bitmap;

    public static void Present(BitmapSource bitmap)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new ScreenshotPreviewWindow(bitmap);
            win.Show();
            win.Activate();
        });
    }

    public ScreenshotPreviewWindow(BitmapSource bitmap)
    {
        _bitmap = bitmap;
        WindowLifecycle.Track(this);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        MaxWidth = 840;
        MaxHeight = 640;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = Theme.Font;
        FlowDirection = Theme.Direction;

        var root = new Border
        {
            Background = Theme.Surface,
            CornerRadius = new CornerRadius(14),
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Margin = new Thickness(20),
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

        // ── Header (Title + Dimensions + Close button) ──
        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var title = UI.Text(L10n.T("Screenshot Captured"), 14, FontWeights.SemiBold, Theme.Ink);
        titleRow.Children.Add(title);

        var dimensions = UI.Text($"  •  {bitmap.PixelWidth} × {bitmap.PixelHeight} px", 12, FontWeights.Normal, Theme.Muted);
        dimensions.Margin = new Thickness(4, 0, 0, 0);
        titleRow.Children.Add(dimensions);

        header.Children.Add(titleRow);

        var closeBtn = UI.IconButton("", () => WindowLifecycle.CloseOnce(this), "Close", 24);
        Grid.SetColumn(closeBtn, 1);
        header.Children.Add(closeBtn);

        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };

        stack.Children.Add(header);

        // ── Image Preview Area ──
        var previewImage = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            MaxHeight = 420,
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0C, 0, 0, 0)),
            CornerRadius = new CornerRadius(8),
            BorderBrush = Theme.Hairline,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 14),
            Child = previewImage
        };
        stack.Children.Add(previewBorder);

        // ── Action Buttons ──
        var actionsGrid = new Grid();
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftActions = new StackPanel { Orientation = Orientation.Horizontal };
        var cancelBtn = UI.Button(L10n.T("Cancel"), () => WindowLifecycle.CloseOnce(this));
        cancelBtn.Margin = new Thickness(0, 0, 8, 0);
        leftActions.Children.Add(cancelBtn);
        actionsGrid.Children.Add(leftActions);

        var rightActions = new StackPanel { Orientation = Orientation.Horizontal };

        // Open in Editor
        var editBtn = UI.Button(L10n.T("Edit"), OnEdit, primary: false, icon: Catalog.Icons.Edit);
        editBtn.Margin = new Thickness(0, 0, 8, 0);
        rightActions.Children.Add(editBtn);

        // Copy to Clipboard
        var copyBtn = UI.Button(L10n.T("Copy"), OnCopy, primary: false, icon: Catalog.Icons.Copy);
        copyBtn.Margin = new Thickness(0, 0, 8, 0);
        rightActions.Children.Add(copyBtn);

        // Save to Disk
        var saveBtn = UI.Button(L10n.T("Save"), OnSave, primary: true, icon: "\uE74E");
        rightActions.Children.Add(saveBtn);

        Grid.SetColumn(rightActions, 2);
        actionsGrid.Children.Add(rightActions);

        stack.Children.Add(actionsGrid);

        root.Child = stack;
        Content = root;

        // Dismiss when user clicks outside
        Deactivated += (_, _) =>
        {
            if (!WindowLifecycle.CanAutoClose(this)) return;
            WindowLifecycle.CloseOnce(this);
        };

        // Keyboard shortcuts
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                WindowLifecycle.CloseOnce(this);
            }
            else if (e.Key == Key.Enter || (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
            {
                OnSave();
            }
            else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                OnCopy();
            }
            else if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                OnEdit();
            }
        };
    }

    private void OnSave()
    {
        WindowLifecycle.CloseOnce(this);
        ScreenCapture.SaveScreenshot(_bitmap);
    }

    private void OnCopy()
    {
        WindowLifecycle.CloseOnce(this);
        ScreenCapture.CopyToClipboard(_bitmap);
    }

    private void OnEdit()
    {
        WindowLifecycle.CloseOnce(this);
        ScreenCapture.OpenInEditor(_bitmap);
    }
}

