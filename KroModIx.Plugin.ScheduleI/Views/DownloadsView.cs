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

/// <summary>v0.6: Row-Layout gleich zum Nexus-Katalog (Cover 140x90 +
/// Titel/Autor/Version/Datum + Summary + Details/Install/Delete-Buttons).
/// Doppelklick oeffnet das gleiche Detail-Window. Kernprinzipien 6/7
/// aus dem KroModIx-Plugin-Skill.</summary>
public sealed class DownloadsView : UserControl
{
    public DownloadsView()
    {
        var refreshBtn = new Button { Content = Strings.T("btn.refresh") };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.RefreshCommand)));

        var openBtn = new Button { Content = Strings.T("btn.open_downloads_folder") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.OpenDownloadsFolderCommand)));

        var installAllBtn = new Button { Content = Strings.T("btn.install_all") };
        installAllBtn.Classes.Add("accent");
        installAllBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.InstallAllCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { refreshBtn, openBtn, installAllBtn },
        };

        var status = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
        status.Classes.Add("muted");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadsViewModel.StatusText)));

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(DownloadsViewModel.Rows)));
        list.ItemTemplate = new FuncDataTemplate<DownloadRow>((r, _) => r is null ? null : BuildRowCard(), true);
        list.DoubleTapped += (_, _) =>
        {
            if (list.DataContext is DownloadsViewModel vm && list.SelectedItem is DownloadRow row)
                vm.ShowDetailCommand.Execute(row);
        };

        Content = new DockPanel
        {
            Margin = new Thickness(16, 12),
            Children = { WithDock(toolbar, Dock.Top), WithDock(status, Dock.Top), list },
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
            Text = "📦", FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fallback.Classes.Add("muted");
        fallback.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(DownloadRow.NoCover)));
        panel.Children.Add(fallback);
        var img = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        img.Bind(Image.SourceProperty, new Binding(nameof(DownloadRow.Cover)));
        img.Bind(Image.IsVisibleProperty, new Binding(nameof(DownloadRow.HasCover)));
        panel.Children.Add(img);
        coverFrame.Child = panel;

        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.DisplayName)));

        var subtitle = new TextBlock();
        subtitle.Classes.Add("muted");
        subtitle.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.SubtitleText)));

        var summary = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 40,
        };
        summary.Classes.Add("secondary");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.NexusSummary)));
        summary.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(DownloadRow.HasSummary)));

        var filename = new TextBlock
        {
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
        };
        filename.Classes.Add("muted");
        filename.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadRow.FileName)));

        var textStack = new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { title, subtitle, summary, filename },
        };

        var installBtn = new Button { Content = Strings.T("btn.install") };
        installBtn.Classes.Add("accent");
        BindRowCmd(installBtn, nameof(DownloadsViewModel.InstallRowCommand));

        var detailBtn = new Button { Content = Strings.T("btn.details") };
        BindRowCmd(detailBtn, nameof(DownloadsViewModel.ShowDetailCommand));
        detailBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(DownloadRow.HasNexusMatch)));

        var deleteBtn = new Button { Content = Strings.T("btn.delete_file") };
        deleteBtn.Classes.Add("danger");
        BindRowCmd(deleteBtn, nameof(DownloadsViewModel.DeleteRowCommand));

        var actions = new StackPanel
        {
            Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { installBtn, detailBtn, deleteBtn },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 8) };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(coverFrame);
        grid.Children.Add(textStack);
        grid.Children.Add(actions);
        var card = new Border { Margin = new Thickness(0, 0, 0, 6), Child = grid };
        card.Classes.Add("card");
        return card;
    }

    private static void BindRowCmd(Button btn, string cmd)
    {
        btn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + cmd,
        });
        btn.Bind(Button.CommandParameterProperty, new Binding("."));
    }

    private static Control WithDock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }
}
