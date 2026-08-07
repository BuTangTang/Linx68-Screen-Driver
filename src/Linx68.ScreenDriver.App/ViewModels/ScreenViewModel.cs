using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Linx68.ScreenDriver.Core;
using System.Windows.Media;

namespace Linx68.ScreenDriver.App.ViewModels;

public sealed partial class ThemeCardViewModel(
    ThemeDefinition definition,
    ImageSource? preview) : ObservableObject
{
    public ThemeDefinition Definition { get; } = definition;

    public string Id => Definition.Id;

    public string DisplayName => Definition.Theme.DisplayName;

    public string Description => Definition.Theme.Description;

    public string Metadata => $"{Definition.CategoryDisplayName}  ·  {(Definition.IsStatic ? "静态" : "动态")}";

    public ImageSource? Preview { get; } = preview;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private double cardWidth = double.NaN;
}

public sealed partial class ThemeCategoryOptionViewModel(
    string id,
    string displayName) : ObservableObject
{
    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;

    [ObservableProperty]
    private bool isSelected;
}

public sealed partial class ScreenViewModel : ObservableObject
{
    private readonly List<ThemeCardViewModel> _allThemes = [];
    private bool _synchronizingSelection;
    private bool _synchronizingCategory;

    public ScreenViewModel()
    {
        Categories =
        [
            new ThemeCategoryOptionViewModel("all", "全部"),
            new ThemeCategoryOptionViewModel("monitor", "监控"),
            new ThemeCategoryOptionViewModel("time", "时间"),
            new ThemeCategoryOptionViewModel("info", "资讯"),
            new ThemeCategoryOptionViewModel("music", "音乐"),
            new ThemeCategoryOptionViewModel("matrix", "点阵")
        ];

        foreach (ThemeCategoryOptionViewModel category in Categories)
        {
            category.PropertyChanged += Category_OnPropertyChanged;
        }

        SelectCategory("all");
    }

    public ObservableCollection<ThemeCategoryOptionViewModel> Categories { get; }

    public ObservableCollection<ThemeCardViewModel> VisibleThemes { get; } = [];

    [ObservableProperty]
    private string selectedCategory = "all";

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

        RefreshVisibleThemes();
        SelectTheme(selectedThemeId, notify: false);
    }

    public void SelectCategory(string? categoryId)
    {
        SelectedCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Id, categoryId, StringComparison.OrdinalIgnoreCase))?.Id ?? "all";
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

        double cardWidth = availableWidth >= 400
            ? Math.Max(196, (availableWidth - 44) / 2)
            : Math.Max(196, availableWidth - 12);
        foreach (ThemeCardViewModel theme in VisibleThemes)
        {
            theme.CardWidth = cardWidth;
        }
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        _synchronizingCategory = true;
        try
        {
            foreach (ThemeCategoryOptionViewModel category in Categories)
            {
                category.IsSelected = string.Equals(category.Id, value, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            _synchronizingCategory = false;
        }

        RefreshVisibleThemes();
    }

    private void RefreshVisibleThemes()
    {
        VisibleThemes.Clear();
        foreach (ThemeCardViewModel theme in _allThemes.Where(theme =>
                     SelectedCategory == "all" || theme.Definition.CategoryId == SelectedCategory))
        {
            VisibleThemes.Add(theme);
        }
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

    private void Category_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_synchronizingCategory
            && e.PropertyName == nameof(ThemeCategoryOptionViewModel.IsSelected)
            && sender is ThemeCategoryOptionViewModel { IsSelected: true } category)
        {
            SelectCategory(category.Id);
        }
    }
}
