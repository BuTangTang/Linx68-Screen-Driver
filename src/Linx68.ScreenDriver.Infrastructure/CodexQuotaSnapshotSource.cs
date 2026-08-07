using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

/// <summary>
/// Reads the signed-in ChatGPT Codex rate-limit window through the local Codex
/// App Server. Authentication remains entirely owned by the Codex CLI.
/// </summary>
public sealed class CodexQuotaSnapshotSource : IAiQuotaSnapshotSource
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);

    public async Task<AiQuotaSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo()
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("未检测到 Codex CLI，请先安装 Codex。", ex);
        }

        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            await SendAsync(process, new
            {
                method = "initialize",
                id = 1,
                @params = new
                {
                    clientInfo = new
                    {
                        name = "linx68_screen_driver",
                        title = "Linx68 Screen Driver",
                        version = "1.0.0"
                    }
                }
            }, timeout.Token).ConfigureAwait(false);
            await SendAsync(process, new { method = "initialized", @params = new { } }, timeout.Token).ConfigureAwait(false);
            await SendAsync(process, new { method = "account/rateLimits/read", id = 2, @params = new { } }, timeout.Token).ConfigureAwait(false);

            while (true)
            {
                string? line = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement message = document.RootElement;
                if (!message.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.Number || id.GetInt32() != 2)
                {
                    continue;
                }

                if (message.TryGetProperty("error", out JsonElement error))
                {
                    throw new InvalidOperationException(GetErrorMessage(error));
                }

                if (!message.TryGetProperty("result", out JsonElement result))
                {
                    throw new InvalidOperationException("Codex 没有返回额度数据。");
                }

                return ParseRateLimits(result);
            }

            string errorOutput = await standardError.ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput)
                ? "Codex 未返回额度数据，请确认本机已登录 ChatGPT。"
                : "Codex 未返回额度数据：" + errorOutput.Trim());
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("读取 Codex 额度超时，请稍后重试。", ex);
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static AiQuotaSnapshot ParseRateLimits(JsonElement result)
    {
        JsonElement rateLimit = FindRateLimit(result);
        JsonElement primary = GetRequiredObject(rateLimit, "primary", "Codex 没有返回主要额度窗口。");
        double usedPercent = GetRequiredDouble(primary, "usedPercent", "Codex 没有返回已用额度比例。");
        int windowMinutes = GetRequiredInt(primary, "windowDurationMins", "Codex 没有返回额度窗口时长。");
        DateTimeOffset? resetsAt = primary.TryGetProperty("resetsAt", out JsonElement reset) && reset.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(reset.GetInt64())
            : null;

        return AiQuotaSnapshot.ForSubscription(
            "Codex",
            remainingPercent: Math.Clamp(100d - usedPercent, 0d, 100d),
            resetPeriod: GetResetPeriod(windowMinutes),
            resetsAt: resetsAt);
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "codex.cmd" : "codex",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--stdio");
        return startInfo;
    }

    private static async Task SendAsync(Process process, object message, CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message)).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static JsonElement FindRateLimit(JsonElement result)
    {
        if (result.TryGetProperty("rateLimits", out JsonElement direct) && direct.ValueKind == JsonValueKind.Object)
        {
            return direct;
        }

        if (result.TryGetProperty("rateLimitsByLimitId", out JsonElement buckets) && buckets.ValueKind == JsonValueKind.Object)
        {
            if (buckets.TryGetProperty("codex", out JsonElement codex))
            {
                return codex;
            }

            foreach (JsonProperty bucket in buckets.EnumerateObject())
            {
                if (bucket.Value.TryGetProperty("primary", out _))
                {
                    return bucket.Value;
                }
            }
        }

        throw new InvalidOperationException("Codex 没有返回可用额度窗口。");
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName, string errorMessage) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.Object
            ? property
            : throw new InvalidOperationException(errorMessage);

    private static double GetRequiredDouble(JsonElement element, string propertyName, string errorMessage) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble()
            : throw new InvalidOperationException(errorMessage);

    private static int GetRequiredInt(JsonElement element, string propertyName, string errorMessage) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : throw new InvalidOperationException(errorMessage);

    private static string GetErrorMessage(JsonElement error) =>
        error.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String
            ? "Codex 额度读取失败：" + message.GetString()
            : "Codex 额度读取失败。";

    private static AiResetPeriod GetResetPeriod(int windowMinutes) => windowMinutes switch
    {
        <= 60 => AiResetPeriod.Hourly,
        <= 24 * 60 => AiResetPeriod.Daily,
        <= 7 * 24 * 60 => AiResetPeriod.Weekly,
        _ => AiResetPeriod.Custom
    };
}
