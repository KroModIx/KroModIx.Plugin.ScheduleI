using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

public sealed class NexusView : UserControl
{
    public NexusView()
    {
        var refreshBtn = new Button { Content = Strings.T("btn.refresh") };
        refreshBtn.Bind(Button.CommandProperty,
            new Binding(nameof(NexusViewModel.LoadFirstPageCommand)));

        var search = new TextBox
        {
            PlaceholderText = Strings.T("placeholder.search_nexus"),
            Width = 300,
        };
        search.Bind(TextBox.TextProperty,
            new Binding(nameof(NexusViewModel.SearchQuery)) { Mode = BindingMode.TwoWay });
        search.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && (s as TextBox)?.DataContext is NexusViewModel vm)
                vm.SearchCommand.Execute(null);
        };

        var searchBtn = new Button { Content = Strings.T("btn.search") };
        searchBtn.Classes.Add("accent");
        searchBtn.Bind(Button.CommandProperty, new Binding(nameof(NexusViewModel.SearchCommand)));

        var sortCombo = new ComboBox { Width = 180 };
        sortCombo.Bind(ComboBox.ItemsSourceProperty, new Binding(nameof(NexusViewModel.SortOptions)));
        sortCombo.Bind(ComboBox.SelectedItemProperty,
            new Binding(nameof(NexusViewModel.SelectedSort)) { Mode = BindingMode.TwoWay });
        sortCombo.ItemTemplate = new FuncDataTemplate<NexusSortOption>((o, _) =>
            o is null ? null : new TextBlock { Text = o.Label }, true);

        var catCombo = new ComboBox { Width = 180 };
        catCombo.Bind(ComboBox.ItemsSourceProperty, new Binding(nameof(NexusViewModel.Categories)));
        catCombo.Bind(ComboBox.SelectedItemProperty,
            new Binding(nameof(NexusViewModel.SelectedCategory)) { Mode = BindingMode.TwoWay });
        catCombo.ItemTemplate = new FuncDataTemplate<string>((c, _) => new TextBlock
        {
            Text = string.IsNullOrEmpty(c) ? Strings.T("filter.all_categories") : c,
        }, true);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { search, searchBtn, sortCombo, catCombo, refreshBtn },
        };

        var status = new TextBlock();
        status.Classes.Add("muted");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(NexusViewModel.StatusText)));
        var coverProgress = new TextBlock { Margin = new Thickness(12, 0, 0, 0) };
        coverProgress.Classes.Add("muted");
        coverProgress.Bind(TextBlock.TextProperty, new Binding(nameof(NexusViewModel.CoverProgressText)));
        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8),
            Children = { status, coverProgress },
        };

        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single,
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(NexusViewModel.Rows)));
        list.ItemTemplate = new FuncDataTemplate<NexusRow>((r, _) => r is null ? null : BuildRowCard(), true);
        // Doppelklick auf eine Row oeffnet Detail-Dialog — Kernprinzip
        // „Row-Interaction" aus dem KroModIx-Plugin-Skill.
        list.DoubleTapped += (_, _) =>
        {
            if (list.DataContext is NexusViewModel vm && list.SelectedItem is NexusRow row)
                vm.ShowDetailCommand.Execute(row);
        };

        var loadMore = new Button { Content = Strings.T("btn.load_more") };
        loadMore.HorizontalAlignment = HorizontalAlignment.Center;
        loadMore.Margin = new Thickness(0, 12, 0, 12);
        loadMore.Classes.Add("accent");
        loadMore.Bind(Button.CommandProperty, new Binding(nameof(NexusViewModel.LoadMoreCommand)));
        loadMore.Bind(Button.IsVisibleProperty, new Binding(nameof(NexusViewModel.HasMore)));

        var content = new StackPanel { Children = { list, loadMore } };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };

        Content = new DockPanel
        {
            Margin = new Thickness(16, 12),
            Children = { WithDock(toolbar, Dock.Top), WithDock(statusRow, Dock.Top), scroll },
        };
    }

    private static Control BuildRowCard()
    {
        var coverFrame = new Border
        {
            Width = 140, Height = 90, CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
        };
        var panel = new Panel();
        var fallback = new TextBlock
        {
            Text = "🌐", FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        fallback.Classes.Add("muted");
        fallback.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(NexusRow.NoCover)));
        panel.Children.Add(fallback);
        var img = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        img.Bind(Image.SourceProperty, new Binding(nameof(NexusRow.Cover)));
        img.Bind(Image.IsVisibleProperty, new Binding(nameof(NexusRow.HasCover)));
        panel.Children.Add(img);
        coverFrame.Child = panel;

        var title = new TextBlock { FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(NexusRow.Name)));

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        void AddMuted(string p) { var t = new TextBlock(); t.Classes.Add("muted"); t.Bind(TextBlock.TextProperty, new Binding(p)); meta.Children.Add(t); }
        void AddSep() { var s = new TextBlock { Text = "·" }; s.Classes.Add("muted"); meta.Children.Add(s); }
        AddMuted(nameof(NexusRow.Author)); AddSep();
        AddMuted(nameof(NexusRow.VersionDisplay)); AddSep();
        AddMuted(nameof(NexusRow.UpdatedText)); AddSep();
        AddMuted(nameof(NexusRow.EndorsementsText));

        var summary = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 40,
        };
        summary.Classes.Add("secondary");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(NexusRow.Summary)));

        var textStack = new StackPanel
        {
            Spacing = 4, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { title, meta, summary },
        };

        var download = new Button { Content = Strings.T("btn.download") };
        download.Classes.Add("accent");
        download.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + nameof(NexusViewModel.DownloadCommand),
        });
        download.Bind(Button.CommandParameterProperty, new Binding("."));
        download.Bind(Button.IsEnabledProperty, new Binding(nameof(NexusRow.IsPremium)));
        ToolTip.SetTip(download, Strings.T("tooltip.premium_download"));

        var openBtn = new Button { Content = Strings.T("btn.open_nexus") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + nameof(NexusViewModel.OpenInBrowserCommand),
        });
        openBtn.Bind(Button.CommandParameterProperty, new Binding("."));

        var detailBtn = new Button { Content = Strings.T("btn.details") };
        detailBtn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + nameof(NexusViewModel.ShowDetailCommand),
        });
        detailBtn.Bind(Button.CommandParameterProperty, new Binding("."));

        var actions = new StackPanel
        {
            Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
            Children = { download, detailBtn, openBtn },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(coverFrame);
        grid.Children.Add(textStack);
        grid.Children.Add(actions);

        var card = new Border { Margin = new Thickness(0, 0, 0, 8), Child = grid };
        card.Classes.Add("card");
        return card;
    }

    private static Control WithDock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }
}
