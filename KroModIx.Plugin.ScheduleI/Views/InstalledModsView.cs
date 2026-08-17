using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Installiert-Tab: Toolbar + Bootstrap-Panel (wenn MelonLoader fehlt)
/// + Row-Liste. v0.6: Row-Layout gleich zu Nexus/Downloads (Cover 140x90
/// + Titel/Autor/Version/Status/Summary + Details/Toggle/Uninstall).
/// Doppelklick oeffnet das gleiche Detail-Window wie im Nexus-Tab wenn
/// eine Nexus-ModId im InstallManifest hinterlegt ist.</summary>
public sealed class InstalledModsView : UserControl
{
    public InstalledModsView()
    {
        // ---- MelonLoader-Bootstrap-Panel (nur sichtbar wenn NeedsMelonLoaderBootstrap) ----
        var bootstrapPanel = new Border
        {
            Padding = new Thickness(20),
            Margin = new Thickness(0, 12, 0, 12),
            CornerRadius = new CornerRadius(8),
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
        };
        var bootstrapStack = new StackPanel { Spacing = 10 };
        var bootstrapTitle = new TextBlock
        {
            Text = "⚙  MelonLoader nicht installiert",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
        };
        bootstrapStack.Children.Add(bootstrapTitle);
        var bootstrapBody = new TextBlock { TextWrapping = TextWrapping.Wrap };
        bootstrapBody.Classes.Add("muted");
        bootstrapBody.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.StatusText)));
        bootstrapStack.Children.Add(bootstrapBody);
        var bootstrapBtn = new Button
        {
            Content = Strings.T("btn.install_melonloader"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
        };
        bootstrapBtn.Classes.Add("accent");
        bootstrapBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.InstallMelonLoaderCommand)));
        bootstrapStack.Children.Add(bootstrapBtn);
        bootstrapPanel.Child = bootstrapStack;
        bootstrapPanel.Bind(Border.IsVisibleProperty,
            new Binding(nameof(InstalledModsViewModel.NeedsMelonLoaderBootstrap)));

        // ---- Normal-Mode: Toolbar + Filter + Liste (sichtbar wenn MelonLoader da) ----
        var refreshBtn = new Button { Content = Strings.T("btn.refresh") };
        refreshBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.RefreshCommand)));

        var openBtn = new Button { Content = Strings.T("btn.open_folder") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.OpenPluginsFolderCommand)));

        var enableAllBtn = new Button { Content = Strings.T("btn.enable_all") };
        enableAllBtn.Classes.Add("ghost");
        enableAllBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.EnableAllCommand)));

        var disableAllBtn = new Button { Content = Strings.T("btn.disable_all") };
        disableAllBtn.Classes.Add("ghost");
        disableAllBtn.Bind(Button.CommandProperty,
            new Binding(nameof(InstalledModsViewModel.DisableAllCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { refreshBtn, openBtn, enableAllBtn, disableAllBtn },
        };
        toolbar.Bind(StackPanel.IsVisibleProperty,
            new Binding(nameof(InstalledModsViewModel.MelonLoaderInstalled)));

        var status = new TextBlock { Margin = new Thickness(0, 0, 0, 4) };
        status.Classes.Add("muted");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.StatusText)));
        status.Bind(TextBlock.IsVisibleProperty,
            new Binding(nameof(InstalledModsViewModel.MelonLoaderInstalled)));

        var filter = new TextBox
        {
            PlaceholderText = Strings.T("placeholder.search"),
            Margin = new Thickness(0, 0, 0, 8),
        };
        filter.Bind(TextBox.TextProperty,
            new Binding(nameof(InstalledModsViewModel.FilterText)) { Mode = BindingMode.TwoWay });
        filter.Bind(TextBox.IsVisibleProperty,
            new Binding(nameof(InstalledModsViewModel.MelonLoaderInstalled)));

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty,
            new Binding(nameof(InstalledModsViewModel.Rows)));
        list.ItemTemplate = new FuncDataTemplate<ModRow>((row, _) =>
            row is null ? null : BuildRowCard(), true);
        list.Bind(ListBox.IsVisibleProperty,
            new Binding(nameof(InstalledModsViewModel.MelonLoaderInstalled)));
        list.DoubleTapped += (_, _) =>
        {
            if (list.DataContext is InstalledModsViewModel vm && list.SelectedItem is ModRow row)
                vm.ShowDetailCommand.Execute(row);
        };

        Content = new DockPanel
        {
            Margin = new Thickness(16, 12),
            Children =
            {
                WithDock(bootstrapPanel, Dock.Top),
                WithDock(toolbar, Dock.Top),
                WithDock(status, Dock.Top),
                WithDock(filter, Dock.Top),
                list,
            },
        };
    }

    private static Control BuildRowCard()
    {
        var coverFrame = new Border
        {
            Width = 140, Height = 90, CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var panel = new Panel();
        var fallback = new TextBlock
        {
            FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fallback.Classes.Add("muted");
        fallback.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.TypeIcon)));
        fallback.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(ModRow.NoCover)));
        panel.Children.Add(fallback);
        var img = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        img.Bind(Image.SourceProperty, new Binding(nameof(ModRow.Cover)));
        img.Bind(Image.IsVisibleProperty, new Binding(nameof(ModRow.HasCover)));
        panel.Children.Add(img);
        coverFrame.Child = panel;

        var name = new TextBlock
        {
            FontWeight = FontWeight.SemiBold, FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        name.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.DisplayName)));

        var subtitle = new TextBlock { FontSize = 11 };
        subtitle.Classes.Add("muted");
        subtitle.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.SubtitleText)));

        var summary = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        };
        summary.Classes.Add("secondary");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.NexusSummary)));
        summary.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(ModRow.HasSummary)));

        var status = new TextBlock
        {
            FontSize = 10, FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 2, 0, 0),
        };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.StatusLabel)));

        var titleColumn = new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { name, subtitle, summary, status },
        };

        var toggleBtn = new Button();
        toggleBtn.Bind(Button.ContentProperty, new Binding(nameof(ModRow.ToggleButtonLabel)));
        BindRowCmd(toggleBtn, nameof(InstalledModsViewModel.ToggleEnabledCommand));

        var detailBtn = new Button { Content = Strings.T("btn.details") };
        BindRowCmd(detailBtn, nameof(InstalledModsViewModel.ShowDetailCommand));
        detailBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(ModRow.HasNexusMatch)));

        var uninstallBtn = new Button { Content = Strings.T("btn.uninstall") };
        uninstallBtn.Classes.Add("danger");
        BindRowCmd(uninstallBtn, nameof(InstalledModsViewModel.UninstallCommand));

        var actions = new StackPanel
        {
            Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { toggleBtn, detailBtn, uninstallBtn },
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(12, 8),
        };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(titleColumn, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(coverFrame);
        grid.Children.Add(titleColumn);
        grid.Children.Add(actions);

        var card = new Border { Margin = new Thickness(0, 0, 0, 6), Child = grid };
        card.Classes.Add("card");
        return card;
    }

    private static void BindRowCmd(Button btn, string cmd)
    {
        btn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource
            { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + cmd,
        });
        btn.Bind(Button.CommandParameterProperty, new Binding("."));
    }

    private static Control WithDock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }
}
