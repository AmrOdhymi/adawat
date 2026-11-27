using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Adawat.Core;

namespace Adawat.App;

/// <summary>Small builders for the controls every window uses: soft grey buttons, rows with the
/// title at the start and the control at the end, segmented choices, and a violet accent.</summary>
public static class UI
{
    public static TextBlock Text(string text, double size = 13, FontWeight? weight = null, Brush? color = null, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = L10n.T(text), FontSize = size, FontWeight = weight ?? FontWeights.Normal, Foreground = color ?? Theme.Ink,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.FontFamilyProperty, "Theme.Font");
        return block;
    }

    public static TextBlock Caption(string text) => Text(text, 11.5, color: Theme.Muted, wrap: true);

    public static TextBlock Icon(string glyph, double size = 13, Brush? color = null) => new()
    {
        Text = glyph, FontFamily = Theme.Icons, FontSize = size, Foreground = color ?? Theme.Ink, VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>The soft grey tool button; <paramref name="primary"/> fills it with the accent.</summary>
    public static Button Button(string title, Action click, bool primary = false, string? icon = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (icon != null) { content.Children.Add(Icon(icon, 12, primary ? Theme.OnAccent : Theme.Ink)); content.Children.Add(new Border { Width = 6 }); }
        content.Children.Add(Text(title, 13, FontWeights.Medium, primary ? Theme.OnAccent : Theme.Ink));
        var button = new Button { Content = content, Style = ButtonStyle(primary), Cursor = Cursors.Hand };
        button.Click += (_, _) => click();
        return button;
    }

    public static Button IconButton(string glyph, Action click, string help, double size = 26)
    {
        var button = new Button
        {
            Content = Icon(glyph, 11), Width = size, Height = size, Style = ButtonStyle(false, round: true), ToolTip = L10n.T(help), Cursor = Cursors.Hand,
        };
        button.Click += (_, _) => click();
        return button;
    }

    private static readonly Dictionary<(bool, bool), Style> Styles = new();

    private static Style ButtonStyle(bool primary, bool round = false)
    {
        if (Styles.TryGetValue((primary, round), out var cached)) return cached;
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "Back");
        border.Radius(round ? "Theme.RadiusSmall" : "Theme.RadiusControl");
        border.SetResourceReference(Border.BackgroundProperty, primary ? "Theme.Accent" : "Theme.ButtonFill");
        border.SetValue(Border.PaddingProperty, round ? new Thickness(0) : new Thickness(14, 6, 14, 6));
        border.SetValue(FrameworkElement.MinHeightProperty, round ? 0.0 : 30.0);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, Res(primary ? "Theme.AccentPressed" : "Theme.ButtonPressed"), "Back"));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Res(primary ? "Theme.AccentHover" : "Theme.ButtonHover"), "Back"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
        template.Triggers.Add(hover);
        template.Triggers.Add(pressed);
        template.Triggers.Add(disabled);
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        Styles[(primary, round)] = style;
        return style;
    }

    /// <summary>A row: title at the start, control at the end.</summary>
    public static Grid Row(string title, UIElement control, double minHeight = 30)
    {
        var grid = new Grid { MinHeight = minHeight, Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Text(title);
        grid.Children.Add(label);
        Grid.SetColumn((UIElement)control, 2);
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>A form field row: label at the start, control stretches to fill all remaining width.</summary>
    public static Grid FieldRow(string title, FrameworkElement control, double labelWidth = 90)
    {
        var grid = new Grid { MinHeight = 32, Margin = new Thickness(0, 5, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var label = Text(title, 13, FontWeights.SemiBold, Theme.Ink);
        grid.Children.Add(label);
        Grid.SetColumn(control, 1);
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>A segmented control: a row of choices, one selected.</summary>
    public static Border Segmented<T>(IReadOnlyList<(T Value, string Title)> options, T selected, Action<T> changed)
    {
        var panel = new UniformGrid { Rows = 1, Columns = options.Count };
        var buttons = new List<Border>();
        void Select(int index)
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].Background = i == index ? Theme.Hub : Brushes.Transparent;
                buttons[i].Effect = i == index ? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Direction = 270, Opacity = 0.15 } : null;
                ((TextBlock)buttons[i].Child).FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }
        for (var index = 0; index < options.Count; index++)
        {
            var i = index;
            var cell = new Border { MinHeight = 30, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(4), Cursor = Cursors.Hand,
                                    Child = new TextBlock { Text = L10n.T(options[index].Title), FontFamily = Theme.Font, FontSize = 12.5, Foreground = Theme.Ink, HorizontalAlignment = HorizontalAlignment.Center } }.Radius("Theme.RadiusSmall");
            cell.MouseLeftButtonUp += (_, _) => { Select(i); changed(options[i].Value); };
            buttons.Add(cell);
            panel.Children.Add(cell);
        }
        Select(Math.Max(0, options.Select(o => o.Value).ToList().IndexOf(selected)));
        return new Border { Background = Theme.ButtonFill, Padding = new Thickness(3), Child = panel }.Radius("Theme.RadiusMedium");
    }

    public static CheckBox Check(string title, bool value, Action<bool> changed)
    {
        var box = new CheckBox { Content = Text(title), IsChecked = value, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Ink };
        box.Checked += (_, _) => changed(true);
        box.Unchecked += (_, _) => changed(false);
        return box;
    }

    /// <summary>A slider with its value written beside it and a reset arrow.</summary>
    public static StackPanel Slider(double min, double max, double value, Action<double> changed, Func<double, string>? format = null, double? reset = null, double width = 160, double step = 0)
    {
        var slider = new System.Windows.Controls.Slider { Minimum = min, Maximum = max, Value = value, Width = width, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
        if (step > 0) { slider.TickFrequency = step; slider.IsSnapToTickEnabled = true; }
        var label = Text(format?.Invoke(value) ?? Math.Round(value).ToString(), 12, color: Theme.Muted);
        label.MinWidth = 48;
        label.TextAlignment = TextAlignment.Right;
        slider.ValueChanged += (_, e) => { label.Text = format?.Invoke(e.NewValue) ?? Math.Round(e.NewValue).ToString(); changed(e.NewValue); };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(slider);
        row.Children.Add(label);
        if (reset is { } r) row.Children.Add(IconButton("", () => slider.Value = r, "Reset", 22));
        return row;
    }

    public static ComboBox Picker<T>(IReadOnlyList<(T Value, string Title)> options, T selected, Action<T> changed)
    {
        var box = new ComboBox { FontSize = 12.5, MinWidth = 120, VerticalAlignment = VerticalAlignment.Center, Background = Theme.Field, Foreground = Theme.Ink, BorderBrush = Theme.Line };
        foreach (var option in options) box.Items.Add(L10n.T(option.Title));
        box.SelectedIndex = Math.Max(0, options.Select(o => o.Value).ToList().IndexOf(selected));
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) changed(options[box.SelectedIndex].Value); };
        return box;
    }

    /// <summary>
    /// A context menu whose colours come from live theme resources. Building menus through this
    /// factory keeps every item legible after a theme switch, even for menus that stay open.
    /// </summary>
    public static ContextMenu Menu()
    {
        var menu = new ContextMenu { FlowDirection = Theme.Direction };
        menu.SetResourceReference(Control.ForegroundProperty, "Theme.Ink");
        menu.SetResourceReference(Control.FontFamilyProperty, "Theme.Font");
        menu.FontSize = 12.5;
        return menu;
    }

    /// <summary>A menu item that resolves its text and icon colour from live theme resources.</summary>
    public static MenuItem MenuEntry(string title, Action? click = null, string? icon = null, bool enabled = true)
    {
        var item = new MenuItem { Header = L10n.T(title), IsEnabled = enabled };
        item.SetResourceReference(Control.ForegroundProperty, "Theme.Ink");
        item.SetResourceReference(Control.FontFamilyProperty, "Theme.Font");
        if (icon is not null)
        {
            var glyph = Icon(icon, 14);
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Ink");
            item.Icon = glyph;
        }
        if (click is not null) item.Click += (_, _) => click();
        return item;
    }

    public static Separator Separator()
    {
        var sep = new Separator { Margin = new Thickness(4, 2, 4, 2) };
        sep.SetResourceReference(Control.BackgroundProperty, "Theme.Line");
        return sep;
    }

    /// <summary>Points a border at a live theme radius so it follows a theme switch.</summary>
    public static Border Radius(this Border border, string key)
    {
        border.SetResourceReference(Border.CornerRadiusProperty, key);
        return border;
    }

    /// <summary>The template counterpart of <see cref="Radius(Border, string)"/>.</summary>
    public static FrameworkElementFactory Radius(this FrameworkElementFactory factory, string key)
    {
        factory.SetResourceReference(Border.CornerRadiusProperty, key);
        return factory;
    }

    /// <summary>
    /// Installs the implicit styles for the stock WPF controls used by the app. The styles refer to
    /// the brushes by resource key, so a theme change recolours them in place: a brush instance that
    /// is stored inside a style is frozen by WPF, which made live switching throw.
    /// </summary>
    public static void InstallThemeResources()
    {
        Styles.Clear();
        if (Application.Current is not { } app) return;
        Theme.Publish();
        app.Resources[typeof(TextBox)] = TextBoxStyle();
        app.Resources[typeof(CheckBox)] = CheckBoxStyle();
        app.Resources[typeof(RadioButton)] = RadioButtonStyle();
        app.Resources[typeof(ComboBox)] = ComboBoxStyle();
        app.Resources[typeof(ComboBoxItem)] = ComboBoxItemStyle();
        app.Resources[typeof(ContextMenu)] = ContextMenuStyle();
        app.Resources[typeof(MenuItem)] = MenuItemStyle();
        app.Resources[typeof(Separator)] = SeparatorStyle();
        app.Resources[typeof(ToolTip)] = ToolTipStyle();
        app.Resources[typeof(ScrollBar)] = ScrollBarStyle();
    }

    /// <summary>A dynamic resource reference for the setters below.</summary>
    private static DynamicResourceExtension Res(string key) => new(key);

    /// <summary>Re-reads theme-dependent chrome on every open tool window after a live switch.</summary>
    public static void RefreshOpenWindows()
    {
        if (Application.Current is not { } app) return;
        foreach (var window in app.Windows.OfType<ToolWindow>()) window.RefreshTheme();

        // Elements that paint their own corners (waveforms, custom clips) read Theme.*RadiusValue
        // inside OnRender, which a resource reference cannot update. One pass over the live trees
        // makes those redraw with the new radius.
        foreach (Window window in app.Windows) InvalidateTree(window);
    }

    private static void InvalidateTree(DependencyObject node)
    {
        if (node is UIElement element) element.InvalidateVisual();
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++) InvalidateTree(VisualTreeHelper.GetChild(node, i));
    }

    private static Style TextBoxStyle()
    {
        var template = new ControlTemplate(typeof(TextBox));
        var border = new FrameworkElementFactory(typeof(Border), "Border");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.Radius("Theme.RadiusSmall");
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(ScrollViewer.FocusableProperty, false);
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, new TemplateBindingExtension(TextBox.VerticalScrollBarVisibilityProperty));
        border.AppendChild(host);
        template.VisualTree = border;

        // The content host is only as tall as its text when centred, which keeps single-line text in
        // the middle of a taller box. Multi-line editors set VerticalContentAlignment=Top and stay stretched.
        var centered = new Trigger { Property = Control.VerticalContentAlignmentProperty, Value = VerticalAlignment.Center };
        centered.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center, "PART_ContentHost"));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.LineStrong"), "Border"));
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Border"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(centered);
        template.Triggers.Add(hover);
        template.Triggers.Add(focus);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Res("Theme.Field")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Res("Theme.Line")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 4, 9, 4)));
        style.Setters.Add(new Setter(Control.MinHeightProperty, 32.0));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(TextBox.CaretBrushProperty, Res("Theme.Accent")));
        style.Setters.Add(new Setter(TextBox.SelectionBrushProperty, Res("Theme.AccentSoft")));
        style.Setters.Add(new Setter(TextBox.SelectionTextBrushProperty, Res("Theme.Ink")));
        return style;
    }

    private static Style CheckBoxStyle()
    {
        var template = new ControlTemplate(typeof(CheckBox));
        var dock = new FrameworkElementFactory(typeof(DockPanel));
        var box = new FrameworkElementFactory(typeof(Border), "Box");
        box.SetValue(FrameworkElement.WidthProperty, 16.0);
        box.SetValue(FrameworkElement.HeightProperty, 16.0);
        box.Radius("Theme.RadiusSmall");
        box.SetResourceReference(Border.BackgroundProperty, "Theme.Field");
        box.SetResourceReference(Border.BorderBrushProperty, "Theme.LineStrong");
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        box.SetValue(DockPanel.DockProperty, Dock.Left);
        var check = new FrameworkElementFactory(typeof(TextBlock), "Check");
        check.SetValue(TextBlock.TextProperty, "\uE73E");
        check.SetValue(TextBlock.FontFamilyProperty, Theme.Icons);
        check.SetValue(TextBlock.FontSizeProperty, 9.0);
        check.SetResourceReference(TextBlock.ForegroundProperty, "Theme.OnAccent");
        check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        box.AppendChild(check);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        dock.AppendChild(box);
        dock.AppendChild(content);
        template.VisualTree = dock;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Box"));
        var selected = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, Res("Theme.Accent"), "Box"));
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Box"));
        selected.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Check"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(hover);
        template.Triggers.Add(selected);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(CheckBox));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style RadioButtonStyle()
    {
        var template = new ControlTemplate(typeof(RadioButton));
        var dock = new FrameworkElementFactory(typeof(DockPanel));
        var box = new FrameworkElementFactory(typeof(Border), "Box");
        box.SetValue(FrameworkElement.WidthProperty, 16.0);
        box.SetValue(FrameworkElement.HeightProperty, 16.0);
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        box.SetResourceReference(Border.BackgroundProperty, "Theme.Field");
        box.SetResourceReference(Border.BorderBrushProperty, "Theme.LineStrong");
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        box.SetValue(DockPanel.DockProperty, Dock.Left);
        var dot = new FrameworkElementFactory(typeof(Border), "Dot");
        dot.SetValue(FrameworkElement.WidthProperty, 7.0);
        dot.SetValue(FrameworkElement.HeightProperty, 7.0);
        dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        dot.SetResourceReference(Border.BackgroundProperty, "Theme.Accent");
        dot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        dot.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        box.AppendChild(dot);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        dock.AppendChild(box);
        dock.AppendChild(content);
        template.VisualTree = dock;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Box"));
        var selected = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Box"));
        selected.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Dot"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(hover);
        template.Triggers.Add(selected);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(RadioButton));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        return style;
    }

    private static Style ComboBoxStyle()
    {
        var template = new ControlTemplate(typeof(ComboBox));
        var root = new FrameworkElementFactory(typeof(Grid));

        var back = new FrameworkElementFactory(typeof(Border), "Back");
        back.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        back.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        back.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        back.Radius("Theme.RadiusSmall");
        var inner = new FrameworkElementFactory(typeof(DockPanel));
        inner.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 0, 7, 0));
        var chevron = new FrameworkElementFactory(typeof(TextBlock));
        chevron.SetValue(TextBlock.TextProperty, "\uE70D");
        chevron.SetValue(TextBlock.FontFamilyProperty, Theme.Icons);
        chevron.SetValue(TextBlock.FontSizeProperty, 10.0);
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Muted");
        chevron.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        chevron.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        chevron.SetValue(DockPanel.DockProperty, Dock.Right);
        var selection = new FrameworkElementFactory(typeof(ContentPresenter));
        selection.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
        selection.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
        selection.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemStringFormatProperty));
        selection.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        selection.SetValue(UIElement.IsHitTestVisibleProperty, false);
        inner.AppendChild(chevron);
        inner.AppendChild(selection);
        back.AppendChild(inner);
        root.AppendChild(back);

        var toggle = new FrameworkElementFactory(typeof(ToggleButton), "Toggle");
        toggle.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        toggle.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        toggle.SetValue(Control.FocusableProperty, false);
        toggle.SetValue(Control.IsTabStopProperty, false);
        toggle.SetValue(Control.TemplateProperty, TransparentButtonTemplate());
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay });
        root.AppendChild(toggle);

        var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay });
        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
        popupBorder.SetResourceReference(Border.BorderBrushProperty, "Theme.Line");
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.Radius("Theme.RadiusSmall");
        popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 3, 0, 0));
        popupBorder.SetValue(Border.PaddingProperty, new Thickness(3));
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.SetValue(FrameworkElement.MaxHeightProperty, 240.0);
        var items = new FrameworkElementFactory(typeof(ItemsPresenter));
        scroll.AppendChild(items);
        popupBorder.AppendChild(scroll);
        popup.AppendChild(popupBorder);
        root.AppendChild(popup);
        template.VisualTree = root;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.LineStrong"), "Back"));
        var open = new Trigger { Property = ComboBox.IsDropDownOpenProperty, Value = true };
        open.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Back"));
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Res("Theme.Accent"), "Back"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(hover);
        template.Triggers.Add(open);
        template.Triggers.Add(focus);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(ComboBox));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Res("Theme.Field")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Res("Theme.Line")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(Control.MinHeightProperty, 32.0));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static ControlTemplate TransparentButtonTemplate()
    {
        var template = new ControlTemplate(typeof(ToggleButton));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        template.VisualTree = border;
        return template;
    }

    private static Style ComboBoxItemStyle()
    {
        var template = new ControlTemplate(typeof(ComboBoxItem));
        var border = new FrameworkElementFactory(typeof(Border), "Back");
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.Radius("Theme.RadiusSmall");
        border.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        border.AppendChild(presenter);
        template.VisualTree = border;

        var highlight = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlight.Setters.Add(new Setter(Border.BackgroundProperty, Res("Theme.ButtonFill"), "Back"));
        var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, Res("Theme.AccentSoft"), "Back"));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Accent")));
        template.Triggers.Add(highlight);
        template.Triggers.Add(selected);

        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        return style;
    }

    private static Style ContextMenuStyle()
    {
        var template = new ControlTemplate(typeof(ContextMenu));
        var border = new FrameworkElementFactory(typeof(Border), "Back");
        border.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
        border.SetResourceReference(Border.BorderBrushProperty, "Theme.Line");
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.Radius("Theme.RadiusMedium");
        border.SetValue(Border.PaddingProperty, new Thickness(4));
        var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        border.AppendChild(presenter);
        template.VisualTree = border;

        var style = new Style(typeof(ContextMenu));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(ContextMenu.HasDropShadowProperty, false));
        return style;
    }

    private static Style MenuItemStyle()
    {
        var template = new ControlTemplate(typeof(MenuItem));
        var border = new FrameworkElementFactory(typeof(Border), "Back");
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.Radius("Theme.RadiusSmall");
        border.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var icon = new FrameworkElementFactory(typeof(ContentPresenter), "Icon");
        icon.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.IconProperty));
        icon.SetValue(FrameworkElement.WidthProperty, 18.0);
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var header = new FrameworkElementFactory(typeof(ContentPresenter), "Header");
        header.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.HeaderProperty));
        header.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(MenuItem.HeaderTemplateProperty));
        header.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(MenuItem.HeaderStringFormatProperty));
        header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        panel.AppendChild(icon);
        panel.AppendChild(header);
        border.AppendChild(panel);
        template.VisualTree = border;

        var highlight = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        highlight.Setters.Add(new Setter(Border.BackgroundProperty, Res("Theme.AccentSoft"), "Back"));
        highlight.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Accent")));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(highlight);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        return style;
    }

    private static Style SeparatorStyle()
    {
        var template = new ControlTemplate(typeof(Separator));
        var line = new FrameworkElementFactory(typeof(Border));
        line.SetValue(FrameworkElement.HeightProperty, 1.0);
        line.SetResourceReference(Border.BackgroundProperty, "Theme.Line");
        line.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 6, 4, 6));
        template.VisualTree = line;
        var style = new Style(typeof(Separator));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static Style ToolTipStyle()
    {
        var template = new ControlTemplate(typeof(ToolTip));
        var border = new FrameworkElementFactory(typeof(Border), "Back");
        border.SetResourceReference(Border.BackgroundProperty, "Theme.Surface");
        border.SetResourceReference(Border.BorderBrushProperty, "Theme.Line");
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.Radius("Theme.RadiusSmall");
        border.SetValue(Border.PaddingProperty, new Thickness(7, 4, 7, 4));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        border.AppendChild(presenter);
        template.VisualTree = border;
        var style = new Style(typeof(ToolTip));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Res("Theme.Ink")));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Res("Theme.Font")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        return style;
    }

    /// <summary>A slim scrollbar that follows the theme: transparent track, rounded thumb, no arrows.</summary>
    private static Style ScrollBarStyle()
    {
        // Track is a FrameworkElement, not a panel, so its parts must be supplied through
        // Track.* property elements; a FrameworkElementFactory cannot append children to it.
        const string xaml = """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="{x:Type ScrollBar}">
                <Track x:Name="PART_Track" IsDirectionReversed="True" Margin="1">
                    <Track.DecreaseRepeatButton>
                        <RepeatButton x:Name="Decrease" Command="{x:Static ScrollBar.PageUpCommand}" Opacity="0" Focusable="False"/>
                    </Track.DecreaseRepeatButton>
                    <Track.Thumb>
                        <Thumb x:Name="Thumb" MinHeight="24" Cursor="Hand">
                            <Thumb.Template>
                                <ControlTemplate TargetType="{x:Type Thumb}">
                                    <Border x:Name="Bar" CornerRadius="3" Margin="2" Background="{DynamicResource Theme.LineStrong}"/>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property="IsMouseOver" Value="True">
                                            <Setter TargetName="Bar" Property="Background" Value="{DynamicResource Theme.Muted}"/>
                                        </Trigger>
                                        <Trigger Property="IsDragging" Value="True">
                                            <Setter TargetName="Bar" Property="Background" Value="{DynamicResource Theme.Accent}"/>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </Thumb.Template>
                        </Thumb>
                    </Track.Thumb>
                    <Track.IncreaseRepeatButton>
                        <RepeatButton x:Name="Increase" Command="{x:Static ScrollBar.PageDownCommand}" Opacity="0" Focusable="False"/>
                    </Track.IncreaseRepeatButton>
                </Track>
                <ControlTemplate.Triggers>
                    <Trigger Property="Orientation" Value="Horizontal">
                        <Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/>
                        <Setter TargetName="Decrease" Property="Command" Value="{x:Static ScrollBar.PageLeftCommand}"/>
                        <Setter TargetName="Increase" Property="Command" Value="{x:Static ScrollBar.PageRightCommand}"/>
                        <Setter TargetName="Thumb" Property="MinHeight" Value="0"/>
                        <Setter TargetName="Thumb" Property="MinWidth" Value="24"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
            """;

        var template = (ControlTemplate)XamlReader.Parse(xaml);
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
        style.Setters.Add(new Setter(Control.FocusableProperty, false));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        // A style trigger beats the style setter, so the horizontal bar swaps its width for a height.
        var horizontal = new Trigger { Property = ScrollBar.OrientationProperty, Value = Orientation.Horizontal };
        horizontal.Setters.Add(new Setter(FrameworkElement.WidthProperty, double.NaN));
        horizontal.Setters.Add(new Setter(FrameworkElement.HeightProperty, 10.0));
        style.Triggers.Add(horizontal);
        return style;
    }

    public static Border Divider() => new() { Height = 1, Background = Theme.Hairline, Margin = new Thickness(0, 8, 0, 8) };

    public static T Pad<T>(this T element, double all) where T : FrameworkElement { element.Margin = new Thickness(all); return element; }
    public static T Pad<T>(this T element, double horizontal, double vertical) where T : FrameworkElement { element.Margin = new Thickness(horizontal, vertical, horizontal, vertical); return element; }
}

/// <summary>
/// The frame every tool window shares: a round close button and centred title on top, the tool's controls in the middle,
/// and a bar of actions at the bottom (secondary at the start, primary at the end). Windows 11 draws the rounded corners and shadow.
/// </summary>
public class ToolWindow : Window
{
    private readonly ContentControl _body = new();
    private readonly DockPanel _footer = new() { LastChildFill = false, Margin = new Thickness(16, 0, 16, 0), Height = 52 };
    private readonly StackPanel _leading = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _trailing = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title;
    private readonly Border _line1;
    private readonly Border _line2;
    private readonly List<(Window Owner, WindowState State)> _owners = new();

    public ToolWindow(string title, double width)
    {
        Title = L10n.T(title);
        Width = width;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.Surface;
        FontFamily = Theme.Font;
        FlowDirection = Theme.Direction;
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/Adawat.ico"));
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 44, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0, 0, 0, 1), UseAeroCaptionButtons = false });
        var header = new Grid { Height = 44 };
        _title = new TextBlock { Text = Title, FontFamily = Theme.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Theme.Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(_title);
        WindowLifecycle.Track(this);
        // Closing an owned window can make Windows minimize the window that opened it — and, when
        // the child had a child of its own, the owner's owner too. The usual workaround is to clear
        // Owner, but WPF silently re-parents a ShowInTaskbar=false window to an internal hidden
        // window instead of removing the owner, so the minimize survives. Detach at the Win32 level
        // and keep the owner chain so a late minimize is put back.
        Loaded += (_, _) => CaptureOwners();
        Closing += (_, _) => DetachOwnerForClose();
        Closed += (_, _) =>
        {
            RestoreOwners();
            DeferRestore(DispatcherPriority.Background);
            GuardOwners();
        };
        var close = UI.IconButton("", CloseOnce, "Close", 24);
        close.HorizontalAlignment = HorizontalAlignment.Left;
        close.Margin = new Thickness(12, 0, 0, 0);
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        header.Children.Add(close);
        DockPanel.SetDock(_leading, Dock.Left);
        DockPanel.SetDock(_trailing, Dock.Right);
        _footer.Children.Add(_leading);
        _footer.Children.Add(_trailing);
        var stack = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);
        _line1 = new Border { Height = 1, Background = Theme.Hairline };
        DockPanel.SetDock(_line1, Dock.Top);
        stack.Children.Add(_line1);
        DockPanel.SetDock(_footer, Dock.Bottom);
        stack.Children.Add(_footer);
        _line2 = new Border { Height = 1, Background = Theme.Hairline };
        DockPanel.SetDock(_line2, Dock.Bottom);
        stack.Children.Add(_line2);
        _body.Margin = new Thickness(16);
        stack.Children.Add(_body);
        Content = stack;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) CloseOnce(); };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            Native.RoundCorners(handle, !Theme.IsGraphite);
            Native.DarkTitleBar(handle, Theme.CurrentMode == AppThemeMode.Dark);
        };
    }

    public UIElement Body { set => _body.Content = value; }

    /// <summary>The centred caption in the custom title bar, for windows that rename themselves.</summary>
    public string Header { set => _title.Text = value; }

    public void Leading(params UIElement[] items) { foreach (var item in items) { if (item is FrameworkElement f) f.Margin = new Thickness(0, 0, 8, 0); _leading.Children.Add(item); } }

    public void Trailing(params UIElement[] items) { foreach (var item in items) { if (item is FrameworkElement f) f.Margin = new Thickness(8, 0, 0, 0); _trailing.Children.Add(item); } }

    public void ClearLeading() => _leading.Children.Clear();

    public void ClearTrailing() => _trailing.Children.Clear();

    /// <summary>Re-reads theme-dependent chrome after a live theme or mode change.</summary>
    public void RefreshTheme()
    {
        Background = Theme.Surface;
        FontFamily = Theme.Font;
        _title.FontFamily = Theme.Display;
        _title.Foreground = Theme.Ink;
        _line1.Background = Theme.Hairline;
        _line2.Background = Theme.Hairline;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            Native.RoundCorners(handle, !Theme.IsGraphite);
            Native.DarkTitleBar(handle, Theme.CurrentMode == AppThemeMode.Dark);
        }
    }

    /// <summary>Closes the window once; safe to call from a <c>Deactivated</c> handler or a second click.</summary>
    public void CloseOnce() => WindowLifecycle.CloseOnce(this);

    /// <summary>
    /// Removes this window's owner before it is destroyed, so the OS does not minimize the window
    /// that opened it. <c>Owner = null</c> only works for a window shown in the taskbar; for
    /// <c>ShowInTaskbar = false</c> WPF re-parents to an internal hidden window instead, which keeps
    /// the minimize cascade alive. Hiding first avoids a taskbar button flash while the owner is gone.
    /// </summary>
    private void DetachOwnerForClose()
    {
        if (Owner == null) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        if (!ShowInTaskbar) Native.ShowWindow(handle, Native.SW_HIDE);
        Native.DetachOwner(handle);
        if (!ShowInTaskbar) return;
        try { Owner = null; }
        catch (InvalidOperationException) { }
    }

    /// <summary>Remembers the visible owners and their state, so closing this window can undo a minimize.</summary>
    private void CaptureOwners()
    {
        _owners.Clear();
        var owner = Owner;
        for (var depth = 0; owner != null && depth < 8; depth++)
        {
            _owners.Add((owner, owner.WindowState));
            owner = owner.Owner;
        }
    }

    /// <summary>Restores any owner that the OS minimized as this window closed.</summary>
    private void RestoreOwners()
    {
        foreach (var (owner, state) in _owners)
        {
            if (state == WindowState.Minimized) continue;
            if (!WindowLifecycle.IsOpen(owner)) continue;
            if (owner.WindowState != WindowState.Minimized) continue;
            try
            {
                owner.WindowState = state;
                owner.Activate();
                Log.Write($"Owner restored after close: '{Title}' put '{owner.Title}' back to {state}");
            }
            catch (InvalidOperationException) { }
        }
    }

    private void DeferRestore(DispatcherPriority priority)
    {
        try
        {
            Dispatcher.BeginInvoke(priority, new Action(RestoreOwners));
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>Re-checks for a short moment, in case the OS minimizes an owner after the close returns.</summary>
    private void GuardOwners()
    {
        if (_owners.Count == 0) return;
        var checks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            RestoreOwners();
            if (++checks >= 3) timer.Stop();
        };
        timer.Start();
    }

    /// <summary>Shows the window in front, as tool windows open from a drop while another app has focus.</summary>
    public void Present()
    {
        if (WindowLifecycle.IsClosingOrClosed(this)) return;
        WindowLifecycle.BringToFront(this);
        Topmost = true;
        Topmost = false;
        Focus();
    }
}
