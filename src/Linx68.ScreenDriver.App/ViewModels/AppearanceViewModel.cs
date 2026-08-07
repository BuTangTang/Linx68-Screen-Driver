using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Linx68.ScreenDriver.Core;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Linx68.ScreenDriver.App.ViewModels;

public sealed record ImageTimePlacementOption(
    ImageTimePlacement Value,
    string DisplayName);

public partial class AppearanceViewModel : ObservableObject
{
    private static readonly MediaBrush InvalidAccentBrush = new MediaSolidColorBrush(MediaColor.FromRgb(220, 74, 84));
    private static readonly MediaBrush ValidAccentBorderBrush = new MediaSolidColorBrush(MediaColor.FromRgb(217, 221, 229));
    private MediaColor _lastValidAccentColor = MediaColor.FromRgb(228, 105, 76);

    public AppearanceViewModel()
    {
        ImageTimePlacements =
        [
            new ImageTimePlacementOption(ImageTimePlacement.Top, "显示在上方"),
            new ImageTimePlacementOption(ImageTimePlacement.Bottom, "显示在下方")
        ];
    }

    public ObservableCollection<ScreenFontOption> FontOptions { get; } = [];

    public IReadOnlyList<ImageTimePlacementOption> ImageTimePlacements { get; }

    [ObservableProperty]
    private AppearanceMode appearanceMode = AppearanceMode.System;

    [ObservableProperty]
    private string accentColor = "#E4694C";

    [ObservableProperty]
    private ScreenFontOption? selectedFontOption;

    [ObservableProperty]
    private ImageTimePlacementOption? selectedImageTimePlacement;

    public bool IsSystemAppearance
    {
        get => AppearanceMode == AppearanceMode.System;
        set
        {
            if (value)
            {
                AppearanceMode = AppearanceMode.System;
            }
        }
    }

    public bool IsLightAppearance
    {
        get => AppearanceMode == AppearanceMode.Light;
        set
        {
            if (value)
            {
                AppearanceMode = AppearanceMode.Light;
            }
        }
    }

    public bool IsDarkAppearance
    {
        get => AppearanceMode == AppearanceMode.Dark;
        set
        {
            if (value)
            {
                AppearanceMode = AppearanceMode.Dark;
            }
        }
    }

    public MediaBrush AccentPreviewBrush => new MediaSolidColorBrush(_lastValidAccentColor);

    public MediaBrush AccentBorderBrush => IsAccentColorValid ? ValidAccentBorderBrush : InvalidAccentBrush;

    public bool IsAccentColorValid => TryParseAccentColor(AccentColor, out _);

    public void Load(AppSettings settings, IReadOnlyList<ScreenFontOption> fontOptions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SetFontOptions(fontOptions, settings.SelectedFontId);
        AppearanceMode = settings.AppearanceMode;
        AccentColor = settings.AccentColor;
        SelectedImageTimePlacement = ImageTimePlacements.FirstOrDefault(option =>
            option.Value == settings.ImageTimePlacement) ?? ImageTimePlacements[^1];
    }

    public void SetFontOptions(IReadOnlyList<ScreenFontOption> fontOptions, string? preferredId)
    {
        ArgumentNullException.ThrowIfNull(fontOptions);
        FontOptions.Clear();
        foreach (ScreenFontOption option in fontOptions)
        {
            FontOptions.Add(option);
        }

        SelectedFontOption = FontOptions.FirstOrDefault(option =>
            string.Equals(option.Id, preferredId, StringComparison.OrdinalIgnoreCase))
            ?? ScreenFontOption.Default;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.AppearanceMode = AppearanceMode;
        settings.AccentColor = IsAccentColorValid
            ? AccentColor.Trim().ToUpperInvariant()
            : "#E4694C";
        settings.SelectedFontId = SelectedFontOption?.Id ?? ScreenFontOption.Default.Id;
        settings.ImageTimePlacement = SelectedImageTimePlacement?.Value ?? ImageTimePlacement.Bottom;
    }

    partial void OnAppearanceModeChanged(AppearanceMode value)
    {
        OnPropertyChanged(nameof(IsSystemAppearance));
        OnPropertyChanged(nameof(IsLightAppearance));
        OnPropertyChanged(nameof(IsDarkAppearance));
    }

    partial void OnAccentColorChanged(string value)
    {
        if (TryParseAccentColor(value, out MediaColor color))
        {
            _lastValidAccentColor = color;
        }

        OnPropertyChanged(nameof(IsAccentColorValid));
        OnPropertyChanged(nameof(AccentPreviewBrush));
        OnPropertyChanged(nameof(AccentBorderBrush));
    }

    public static bool TryParseAccentColor(string? value, out MediaColor color)
    {
        color = default;
        try
        {
            if (string.IsNullOrWhiteSpace(value)
                || MediaColorConverter.ConvertFromString(value.Trim()) is not MediaColor parsed)
            {
                return false;
            }

            color = MediaColor.FromRgb(parsed.R, parsed.G, parsed.B);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
