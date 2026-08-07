using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed record CodexCliStatus(bool IsInstalled, bool IsSignedIn, string Message);

/// <summary>
/// Keeps Codex setup separate from this application's settings. Only the public
/// <c>config.toml</c> is portable; Codex authentication remains on each device.
/// </summary>
public sealed class CodexSetupService
{
    private const string ConfigFileName = "config.toml";

    private readonly string? _codexHome;

    public CodexSetupService(string? codexHome = null)
    {
        _codexHome = codexHome;
    }

    public string CodexHome => ResolveCodexHome(codexHome: _codexHome);

    public string ConfigPath => Path.Combine(CodexHome, ConfigFileName);

    public static string ResolveCodexHome(string? userProfile = null, string? codexHome = null)
    {
        string configuredHome = codexHome ?? Environment.GetEnvironmentVariable("CODEX_HOME") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(configuredHome))
        {
            return Path.GetFullPath(configuredHome);
        }

        string home = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".codex");
    }

    public async Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "codex",
                    Arguments = "login status",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string output = (await standardOutput.ConfigureAwait(false)) + (await standardError.ConfigureAwait(false));
            bool signedIn = process.ExitCode == 0 && !output.Contains("not logged", StringComparison.OrdinalIgnoreCase);
            return signedIn
                ? new CodexCliStatus(true, true, "Codex 已登录")
                : new CodexCliStatus(true, false, "Codex 尚未登录，请点击“登录 Codex”完成授权");
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new CodexCliStatus(false, false, "未检测到 Codex，请先点击“安装/更新 Codex”");
        }
    }

    public void StartInstaller() => StartInteractiveCommand(
        "irm https://chatgpt.com/codex/install.ps1 | iex");

    public void StartLogin() => StartInteractiveCommand("codex login");

    public void OpenConfig()
    {
        Directory.CreateDirectory(CodexHome);
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(
                ConfigPath,
                "# Codex personal configuration. This file can be moved to another trusted computer.\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = $"\"{ConfigPath}\"",
            UseShellExecute = true
        });
    }

    public void ExportConfig(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(ConfigPath))
        {
            File.Copy(ConfigPath, destination, overwrite: true);
            return;
        }

        File.WriteAllText(destination, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public string ImportConfig(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("未找到要导入的 Codex 配置文件。", source);
        }

        if (string.Equals(source, ConfigPath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        Directory.CreateDirectory(CodexHome);
        string? backupPath = null;
        if (File.Exists(ConfigPath))
        {
            backupPath = $"{ConfigPath}.backup-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.toml";
            File.Copy(ConfigPath, backupPath, overwrite: false);
        }
        File.Copy(source, ConfigPath, overwrite: true);
        return backupPath ?? string.Empty;
    }

    private static void StartInteractiveCommand(string command)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -Command \"{command}\"",
            UseShellExecute = true
        });
    }
}
