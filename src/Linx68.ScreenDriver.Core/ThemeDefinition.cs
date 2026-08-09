namespace Linx68.ScreenDriver.Core;

public enum ThemeCategory
{
    Monitor,
    Time,
    Information,
    Music,
    Matrix,
    Other
}

[Flags]
public enum ThemeDataRequirements
{
    None = 0,
    System = 1 << 0,
    Music = 1 << 1,
    Lyrics = 1 << 2,
    AiQuota = 1 << 3,
    Weather = 1 << 4,
    CodexTasks = 1 << 5
}

[Flags]
public enum ThemeSettingsSections
{
    None = 0,
    System = 1 << 0,
    Music = 1 << 1,
    Image = 1 << 2,
    AiQuota = 1 << 3,
    Weather = 1 << 4
}

public sealed record ThemeDefinition(
    IScreenTheme Theme,
    ThemeCategory Category,
    ThemeDataRequirements DataRequirements,
    ThemeSettingsSections SettingsSections = ThemeSettingsSections.None,
    bool IsStatic = false)
{
    public string Id => Theme.Id;

    public string CategoryId => Category switch
    {
        ThemeCategory.Monitor => "monitor",
        ThemeCategory.Time => "time",
        ThemeCategory.Information => "info",
        ThemeCategory.Music => "music",
        ThemeCategory.Matrix => "matrix",
        _ => "all"
    };

    public string CategoryDisplayName => Category switch
    {
        ThemeCategory.Monitor => "监控",
        ThemeCategory.Time => "时间与天气",
        ThemeCategory.Information => "Codex 信息",
        ThemeCategory.Music => "音乐",
        ThemeCategory.Matrix => "点阵",
        _ => "其他"
    };

    public bool Requires(ThemeDataRequirements requirement) =>
        (DataRequirements & requirement) == requirement;

    public bool Shows(ThemeSettingsSections section) =>
        (SettingsSections & section) == section;
}
