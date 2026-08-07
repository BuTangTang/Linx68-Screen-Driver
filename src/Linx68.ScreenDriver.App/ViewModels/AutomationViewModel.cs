using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.App.ViewModels;

public sealed record ThemeOptionViewModel(string Id, string DisplayName);

public partial class AutomationViewModel : ObservableObject
{
    public ObservableCollection<ThemeOptionViewModel> IdleThemes { get; } = [];

    public ObservableCollection<ThemeOptionViewModel> PlayingThemes { get; } = [];

    [ObservableProperty]
    private bool autoPush = true;

    [ObservableProperty]
    private int refreshSeconds = 1;

    [ObservableProperty]
    private bool autoSwitchToMusic;

    [ObservableProperty]
    private bool autoMediaThemeSwitch;

    [ObservableProperty]
    private ThemeOptionViewModel? selectedIdleTheme;

    [ObservableProperty]
    private ThemeOptionViewModel? selectedPlayingTheme;

    public void Load(AppSettings settings, IReadOnlyList<ThemeDefinition> themes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(themes);

        IdleThemes.Clear();
        PlayingThemes.Clear();
        foreach (ThemeDefinition definition in themes)
        {
            var option = new ThemeOptionViewModel(definition.Id, definition.Theme.DisplayName);
            if (definition.Category == ThemeCategory.Music)
            {
                PlayingThemes.Add(option);
            }
            else
            {
                IdleThemes.Add(option);
            }
        }

        AutoPush = settings.AutoPush;
        RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 1, 30);
        AutoSwitchToMusic = settings.AutoSwitchToMusic;
        AutoMediaThemeSwitch = settings.AutoMediaThemeSwitch;
        SelectedIdleTheme = IdleThemes.FirstOrDefault(theme =>
            string.Equals(theme.Id, settings.MediaIdleThemeId, StringComparison.OrdinalIgnoreCase))
            ?? IdleThemes.FirstOrDefault(theme => theme.Id == "system")
            ?? IdleThemes.FirstOrDefault();
        SelectedPlayingTheme = PlayingThemes.FirstOrDefault(theme =>
            string.Equals(theme.Id, settings.MediaPlayingThemeId, StringComparison.OrdinalIgnoreCase))
            ?? PlayingThemes.FirstOrDefault(theme => theme.Id == "music")
            ?? PlayingThemes.FirstOrDefault();
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.AutoPush = AutoPush;
        settings.RefreshSeconds = RefreshSeconds;
        settings.AutoSwitchToMusic = AutoSwitchToMusic;
        settings.AutoMediaThemeSwitch = AutoMediaThemeSwitch;
        settings.MediaIdleThemeId = SelectedIdleTheme?.Id ?? "system";
        settings.MediaPlayingThemeId = SelectedPlayingTheme?.Id ?? "music";
    }

    partial void OnRefreshSecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 1, 30);
        if (clamped != value)
        {
            RefreshSeconds = clamped;
        }
    }
}
