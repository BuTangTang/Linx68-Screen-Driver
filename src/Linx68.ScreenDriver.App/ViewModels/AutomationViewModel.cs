using CommunityToolkit.Mvvm.ComponentModel;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.App.ViewModels;

public partial class AutomationViewModel : ObservableObject
{
    [ObservableProperty]
    private bool autoPush = true;

    [ObservableProperty]
    private int refreshSeconds = 1;

    [ObservableProperty]
    private bool autoSwitchToMusic;

    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AutoPush = settings.AutoPush;
        RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 1, 30);
        AutoSwitchToMusic = settings.AutoSwitchToMusic;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.AutoPush = AutoPush;
        settings.RefreshSeconds = RefreshSeconds;
        settings.AutoSwitchToMusic = AutoSwitchToMusic;
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
