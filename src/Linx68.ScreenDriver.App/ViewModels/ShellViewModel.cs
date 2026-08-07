using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Linx68.ScreenDriver.App.ViewModels;

public enum ShellPage
{
    Screen,
    Appearance,
    Automation,
    Settings,
    About
}

public partial class ShellViewModel : ObservableObject
{
    public ShellViewModel() : this(new ScreenViewModel())
    {
    }

    public ShellViewModel(ScreenViewModel screen)
    {
        Screen = screen;
    }

    public ScreenViewModel Screen { get; }

    public AppearanceViewModel Appearance { get; } = new();

    public AutomationViewModel Automation { get; } = new();

    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty]
    private ShellPage currentPage = ShellPage.Screen;

    public bool IsScreenPage => CurrentPage == ShellPage.Screen;

    public bool IsAppearancePage => CurrentPage == ShellPage.Appearance;

    public bool IsAutomationPage => CurrentPage == ShellPage.Automation;

    public bool IsSettingsPage => CurrentPage == ShellPage.Settings;

    public bool IsAboutPage => CurrentPage == ShellPage.About;

    public string PageTitle => CurrentPage switch
    {
        ShellPage.Screen => "显示方案",
        ShellPage.Appearance => "外观",
        ShellPage.Automation => "自动化",
        ShellPage.Settings => "其他设置",
        ShellPage.About => "关于",
        _ => "灵犀68屏幕驱动"
    };

    public string PageSubtitle => CurrentPage switch
    {
        ShellPage.Screen => "选择要推送到 Linx68 屏幕的画面",
        ShellPage.Appearance => "调整应用外观与键盘屏幕的字体和强调色",
        ShellPage.Automation => "设置推送频率和播放时自动切换",
        ShellPage.Settings => "设备地址、安全区和启动行为",
        ShellPage.About => "版本、许可与数据来源",
        _ => string.Empty
    };

    [RelayCommand]
    private void Navigate(string? page)
    {
        if (Enum.TryParse(page, ignoreCase: true, out ShellPage target))
        {
            CurrentPage = target;
        }
    }

    partial void OnCurrentPageChanged(ShellPage value)
    {
        OnPropertyChanged(nameof(IsScreenPage));
        OnPropertyChanged(nameof(IsAppearancePage));
        OnPropertyChanged(nameof(IsAutomationPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsAboutPage));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
    }
}
