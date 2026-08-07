using System.Windows;
using Linx68.ScreenDriver.Core;
using Microsoft.Win32;

namespace Linx68.ScreenDriver.App;

internal static class AppearanceManager
{
    public static bool IsDark { get; private set; }

    public static bool Apply(AppearanceMode mode)
    {
        IsDark = mode == AppearanceMode.Dark || mode == AppearanceMode.System && IsSystemDarkMode();
        var source = new ResourceDictionary
        {
            Source = new Uri(
                IsDark
                    ? "pack://application:,,,/Linx68.ScreenDriver.App;component/Themes/Palette.Dark.xaml"
                    : "pack://application:,,,/Linx68.ScreenDriver.App;component/Themes/Palette.Light.xaml",
                UriKind.Absolute)
        };

        (ResourceDictionary Host, int Index)? paletteLocation = FindPaletteLocation(System.Windows.Application.Current.Resources);
        if (paletteLocation is not null)
        {
            paletteLocation.Value.Host.MergedDictionaries[paletteLocation.Value.Index] = source;
        }

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            window.InvalidateVisual();
        }

        return IsDark;
    }

    private static (ResourceDictionary Host, int Index)? FindPaletteLocation(ResourceDictionary dictionary)
    {
        for (int index = 0; index < dictionary.MergedDictionaries.Count; index++)
        {
            ResourceDictionary merged = dictionary.MergedDictionaries[index];
            if (merged.Source?.OriginalString.Contains("Palette.", StringComparison.OrdinalIgnoreCase) == true)
            {
                return (dictionary, index);
            }
        }

        foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
        {
            (ResourceDictionary Host, int Index)? match = FindPaletteLocation(merged);
            if (match is not null)
            {
                return match;
            }
        }
        return null;
    }

    public static bool IsSystemDarkMode()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            object? value = key?.GetValue("AppsUseLightTheme")
                ?? key?.GetValue("SystemUsesLightTheme");
            return value is int number && number == 0;
        }
        catch
        {
            return false;
        }
    }
}
