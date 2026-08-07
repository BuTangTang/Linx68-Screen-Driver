using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Threading;
using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Infrastructure;

namespace Linx68.ScreenDriver.App;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = "Local\\KeyboardScreenStudio.Instance";
    private const string ActivationEventName = "Local\\KeyboardScreenStudio.Activate";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;

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

        var initialSettings = new JsonSettingsStore().LoadAsync().GetAwaiter().GetResult();
        AppearanceManager.Apply(initialSettings.AppearanceMode);
        var window = new MainWindow(initialSettings);
        MainWindow = window;
        window.Title = "灵犀68屏幕驱动";
        window.ShowInTaskbar = true;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.WindowState = WindowState.Normal;
        window.Topmost = true;
        window.Show();
        window.Activate();
        window.ContentRendered += (_, _) =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
            window.Topmost = false;
        };

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
