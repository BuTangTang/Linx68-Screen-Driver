using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.App.ViewModels;

public sealed partial class ThemeCardViewModel(
    ThemeDefinition definition,
    ImageSource? preview) : ObservableObject
{
    public ThemeDefinition Definition { get; } = definition;

    public string Id => Definition.Id;

    public string DisplayName => Definition.Theme.DisplayName;

    public string Description => Definition.Theme.Description;

    public string Metadata => $"{Definition.CategoryDisplayName} · {(Definition.IsStatic ? "静态" : "动态")}";

    public ImageSource? Preview { get; } = preview;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private double cardWidth = double.NaN;
}

public sealed partial class ThemeGroupViewModel(
    string id,
    string displayName,
    string description) : ObservableObject
{
    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;

    public string Description { get; } = description;

    public ObservableCollection<ThemeCardViewModel> Themes { get; } = [];

    [ObservableProperty]
    private bool isSelected;

    public string CountText => $"{Themes.Count} 个方案";

    internal void SetThemes(IEnumerable<ThemeCardViewModel> themes)
    {
        Themes.Clear();
        foreach (ThemeCardViewModel theme in themes)
        {
            Themes.Add(theme);
        }

        OnPropertyChanged(nameof(CountText));
    }
}

public sealed partial class ScreenViewModel : ObservableObject
{
    private readonly List<ThemeCardViewModel> _allThemes = [];
    private bool _synchronizingSelection;

    public ScreenViewModel()
    {
        ThemeGroups =
        [
            new ThemeGroupViewModel("monitor", "电脑监控", "电脑状态、性能与网络信息"),
            new ThemeGroupViewModel("time", "时间与天气", "时间、日期、天气与图片时间"),
            new ThemeGroupViewModel("info", "资讯与数据", "AI 用量与市场行情"),
            new ThemeGroupViewModel("music", "音乐播放", "封面、歌词与播放进度"),
            new ThemeGroupViewModel("matrix", "点阵风格", "适合点阵文字显示的时钟方案")
        ];
    }

    public ObservableCollection<ThemeGroupViewModel> ThemeGroups { get; }

    public ObservableCollection<ThemeCardViewModel> VisibleThemes { get; } = [];

    public int AllThemeCount => _allThemes.Count;

    public string VisibleThemeCountText => $"{(IsAllCategorySelected ? "全部方案" : ThemeGroups.First(group => group.IsSelected).DisplayName)} · {VisibleThemes.Count} 个";

    [ObservableProperty]
    private bool isAllCategorySelected = true;

    [ObservableProperty]
    private ThemeCardViewModel? selectedTheme;

    public event Action<ThemeDefinition>? ThemeSelected;

    public void SetThemes(IEnumerable<ThemeCardViewModel> themes, string? selectedThemeId)
    {
        foreach (ThemeCardViewModel theme in _allThemes)
        {
            theme.PropertyChanged -= Theme_OnPropertyChanged;
        }

        _allThemes.Clear();
        _allThemes.AddRange(themes);
        foreach (ThemeCardViewModel theme in _allThemes)
        {
            theme.PropertyChanged += Theme_OnPropertyChanged;
        }

        RefreshThemeGroups();
        SelectTheme(selectedThemeId, notify: false);
        SelectCategory(SelectedTheme?.Definition.CategoryId ?? "all");
    }

    public void SelectTheme(string? themeId, bool notify)
    {
        ThemeCardViewModel? selected = _allThemes.FirstOrDefault(theme =>
            string.Equals(theme.Id, themeId, StringComparison.OrdinalIgnoreCase))
            ?? _allThemes.FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        _synchronizingSelection = true;
        try
        {
            foreach (ThemeCardViewModel theme in _allThemes)
            {
                theme.IsSelected = ReferenceEquals(theme, selected);
            }

            SelectedTheme = selected;
        }
        finally
        {
            _synchronizingSelection = false;
        }

        if (notify)
        {
            ThemeSelected?.Invoke(selected.Definition);
        }
    }

    public void UpdateCardWidth(double availableWidth)
    {
        if (availableWidth <= 0)
        {
            return;
        }

        int columnCount = availableWidth >= 960 ? 3 : availableWidth >= 320 ? 2 : 1;
        double cardWidth = Math.Max(148, (availableWidth - 12 * columnCount) / columnCount);
        foreach (ThemeCardViewModel theme in _allThemes)
        {
            theme.CardWidth = cardWidth;
        }
    }

	[RelayCommand]
	public void SelectCategory(string categoryId)
    {
        IsAllCategorySelected = string.Equals(categoryId, "all", StringComparison.OrdinalIgnoreCase);
        foreach (ThemeGroupViewModel group in ThemeGroups)
        {
            group.IsSelected = string.Equals(group.Id, categoryId, StringComparison.OrdinalIgnoreCase);
        }

        RefreshVisibleThemes();
    }

    private void RefreshThemeGroups()
    {
        foreach (ThemeGroupViewModel group in ThemeGroups)
        {
            group.SetThemes(_allThemes.Where(theme =>
                string.Equals(theme.Definition.CategoryId, group.Id, StringComparison.OrdinalIgnoreCase)));
        }

        OnPropertyChanged(nameof(VisibleThemeCountText));
        OnPropertyChanged(nameof(AllThemeCount));
    }

    private void RefreshVisibleThemes()
    {
        IEnumerable<ThemeCardViewModel> themes = IsAllCategorySelected
            ? _allThemes
            : ThemeGroups.FirstOrDefault(group => group.IsSelected)?.Themes ?? [];

        VisibleThemes.Clear();
        foreach (ThemeCardViewModel theme in themes)
        {
            VisibleThemes.Add(theme);
        }

        OnPropertyChanged(nameof(VisibleThemeCountText));
    }

    private void Theme_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_synchronizingSelection
            && e.PropertyName == nameof(ThemeCardViewModel.IsSelected)
            && sender is ThemeCardViewModel { IsSelected: true } theme)
        {
            SelectTheme(theme.Id, notify: true);
        }
    }
}
