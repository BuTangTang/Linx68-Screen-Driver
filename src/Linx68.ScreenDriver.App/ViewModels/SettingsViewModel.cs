using CommunityToolkit.Mvvm.ComponentModel;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string endpointPart1 = string.Empty;

    [ObservableProperty]
    private string endpointPart2 = string.Empty;

    [ObservableProperty]
    private string endpointPart3 = string.Empty;

    [ObservableProperty]
    private string endpointPart4 = string.Empty;

    [ObservableProperty]
    private string safeLeft = "10";

    [ObservableProperty]
    private string safeTop = "52";

    [ObservableProperty]
    private string safeRight = "10";

    [ObservableProperty]
    private string safeBottom = "12";

    [ObservableProperty]
    private bool minimizeToTray = true;

    [ObservableProperty]
    private bool closeToTray = true;

    [ObservableProperty]
    private bool startMinimized;

    [ObservableProperty]
    private bool launchAtStartup;

    public string EndpointIp => string.Join('.',
        EndpointPart1.Trim(),
        EndpointPart2.Trim(),
        EndpointPart3.Trim(),
        EndpointPart4.Trim());

    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SetEndpoint(DeviceEndpoint.ExtractIp(settings.DeviceEndpoint));
        SafeLeft = settings.SafeArea.Left.ToString();
        SafeTop = settings.SafeArea.Top.ToString();
        SafeRight = settings.SafeArea.Right.ToString();
        SafeBottom = settings.SafeArea.Bottom.ToString();
        MinimizeToTray = settings.MinimizeToTray;
        CloseToTray = settings.CloseToTray;
        StartMinimized = settings.StartMinimized;
        LaunchAtStartup = settings.LaunchAtStartup;
    }

    public bool SetEndpoint(string? value)
    {
        string candidate = DeviceEndpoint.ExtractIp(value);
        if (!System.Net.IPAddress.TryParse(candidate, out System.Net.IPAddress? address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            EndpointPart1 = string.Empty;
            EndpointPart2 = string.Empty;
            EndpointPart3 = string.Empty;
            EndpointPart4 = string.Empty;
            return false;
        }

        string[] segments = address.ToString().Split('.');
        EndpointPart1 = segments[0];
        EndpointPart2 = segments[1];
        EndpointPart3 = segments[2];
        EndpointPart4 = segments[3];
        return true;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.DeviceEndpoint = DeviceEndpoint.TryCreate(EndpointIp, out Uri endpoint)
            ? endpoint.AbsoluteUri
            : EndpointIp;
        settings.SafeArea = ReadSafeArea();
        settings.MinimizeToTray = MinimizeToTray;
        settings.CloseToTray = CloseToTray;
        settings.StartMinimized = StartMinimized;
        settings.LaunchAtStartup = LaunchAtStartup;
    }

    partial void OnEndpointPart1Changed(string value) => OnPropertyChanged(nameof(EndpointIp));

    partial void OnEndpointPart2Changed(string value) => OnPropertyChanged(nameof(EndpointIp));

    partial void OnEndpointPart3Changed(string value) => OnPropertyChanged(nameof(EndpointIp));

    partial void OnEndpointPart4Changed(string value) => OnPropertyChanged(nameof(EndpointIp));

    private ScreenInsets ReadSafeArea()
    {
        int left = ReadClamped(SafeLeft, 10, 0, 60);
        int top = ReadClamped(SafeTop, 52, 0, 160);
        int right = ReadClamped(SafeRight, 10, 0, 60);
        int bottom = ReadClamped(SafeBottom, 12, 0, 100);
        return left + right >= 132 || top + bottom >= 418
            ? new ScreenInsets(10, 52, 10, 12)
            : new ScreenInsets(left, top, right, bottom);
    }

    private static int ReadClamped(string value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, out int result)
            ? Math.Clamp(result, minimum, maximum)
            : fallback;
}
