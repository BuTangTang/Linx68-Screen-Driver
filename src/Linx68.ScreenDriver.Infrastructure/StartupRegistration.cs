using Microsoft.Win32;

namespace Linx68.ScreenDriver.Infrastructure;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Linx68ScreenDriver";
    private static readonly string[] LegacyValueNames = ["Linx68ScreenManager", "KeyboardScreenStudio"];

    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return false;
            }

            if (!enabled)
            {
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
                foreach (string legacyValueName in LegacyValueNames)
                {
                    runKey.DeleteValue(legacyValueName, throwOnMissingValue: false);
                }
                return true;
            }

            string executablePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            runKey.SetValue(ValueName, $"\"{executablePath}\" --startup", RegistryValueKind.String);
            foreach (string legacyValueName in LegacyValueNames)
            {
                runKey.DeleteValue(legacyValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
