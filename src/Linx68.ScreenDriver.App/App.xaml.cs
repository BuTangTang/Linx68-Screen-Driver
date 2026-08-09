using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Threading;
using Linx68.ScreenDriver.App.ViewModels;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Linx68.ScreenDriver.App;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = "Local\\Linx68ScreenDriver.Instance.v2";
    private const string ActivationEventName = "Local\\Linx68ScreenDriver.Activate.v2";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        if (!isFirstInstance)
        {
            _activationEvent.Set();
            TryRestoreExistingInstanceWindow();
            _activationEvent.Dispose();
            _activationEvent = null;
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        var settingsStore = new JsonSettingsStore();
        var initialSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        AppearanceManager.Apply(initialSettings.AppearanceMode);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(initialSettings);
        builder.Services.AddSingleton<ISettingsStore>(settingsStore);
        builder.Services.AddSingleton<ISystemSnapshotSource, WindowsSystemSnapshotSource>();
        builder.Services.AddSingleton<IMusicSnapshotSource, WindowsMusicSnapshotSource>();
        builder.Services.AddSingleton<LrcLibLyricsSnapshotSource>();
        builder.Services.AddSingleton<ILyricsSnapshotSource, NetEaseLyricsSnapshotSource>();
        builder.Services.AddSingleton<IMusicSnapshotEnricher, NetEaseMusicSnapshotEnricher>();
        builder.Services.AddSingleton<IWeatherSnapshotSource, OpenMeteoWeatherSnapshotSource>();
        builder.Services.AddSingleton<IDeviceTransport, HttpImageDeviceTransport>();
        builder.Services.AddSingleton<IDashboardSnapshotBuilder, DashboardSnapshotBuilder>();
        builder.Services.AddSingleton<IAutomaticWeatherLocationProvider, WindowsWeatherLocationProvider>();
        builder.Services.AddSingleton<IWeatherSettingsResolver, WeatherSettingsResolver>();
        builder.Services.AddSingleton<IDashboardRefreshService, DashboardRefreshService>();
        builder.Services.AddSingleton<IDisplayPushService, DisplayPushService>();
        builder.Services.AddSingleton<ImageTheme>();
        builder.Services.AddSingleton<ScreenViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton(_ => new FontFolderCatalog(
            Path.Combine(AppContext.BaseDirectory, "Fonts")));
        builder.Services.AddSingleton<MainWindow>();
        _host = builder.Build();
        _host.Start();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Title = "灵犀68屏幕驱动";
        window.ShowInTaskbar = true;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.WindowState = WindowState.Normal;
        window.Topmost = true;
        window.ContentRendered += (_, _) =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
            window.Topmost = false;
        };
        window.Show();
        window.Activate();

        _activationCancellation = new CancellationTokenSource();
        _ = Task.Run(() => WaitForActivation(_activationCancellation.Token));
    }

    private static void TryRestoreExistingInstanceWindow()
    {
        int currentProcessId = Environment.ProcessId;
        string[] compatibleProcessNames =
        [
            Process.GetCurrentProcess().ProcessName,
            "KeyboardScreenStudio",
            "Linx68ScreenManager",
            "Linx68ScreenDriver"
        ];

        foreach (string processName in compatibleProcessNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    if (process.Id == currentProcessId)
                    {
                        continue;
                    }

                    EnumWindows((windowHandle, parameter) =>
                    {
                        GetWindowThreadProcessId(windowHandle, out uint windowProcessId);
                        if (windowProcessId != process.Id)
                        {
                            return true;
                        }

                        var className = new StringBuilder(256);
                        _ = GetClassName(windowHandle, className, className.Capacity);
                        if (!className.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal))
                        {
                            return true;
                        }

                        ShowWindowAsync(windowHandle, ShowWindowCommand.Restore);
                        SetForegroundWindow(windowHandle);
                        return false;
                    }, IntPtr.Zero);
                }
            }
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    private enum ShowWindowCommand
    {
        Restore = 9
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr windowHandle, ShowWindowCommand command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    private void WaitForActivation(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _activationEvent is not null)
            {
                _activationEvent.WaitOne();
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Dispatcher.BeginInvoke(() => (MainWindow as Linx68.ScreenDriver.App.MainWindow)?.RestoreFromExternalActivation());
            }
        }
        catch (ObjectDisposedException)
        {
            // The activation event is disposed during shutdown; the listener exits quietly.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        try
        {
            _activationEvent?.Set();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by the non-first-instance path.
        }
        _activationEvent?.Dispose();
        _activationCancellation?.Dispose();
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host.Dispose();
            _host = null;
        }
        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex is not owned by this thread (e.g. abandoned); nothing to release.
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
