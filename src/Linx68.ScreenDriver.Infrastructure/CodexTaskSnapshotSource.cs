using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

/// <summary>
/// Reads a minimal, display-safe list of local Codex tasks through the local
/// App Server. The reader is strictly read-only and never opens, resumes, or
/// modifies a Codex thread.
/// </summary>
public sealed class CodexTaskSnapshotSource : ICodexTaskSnapshotSource
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);

    private static readonly TimeSpan LocalActivityWindow = TimeSpan.FromMinutes(2);

    private const int MaximumRolloutTailBytes = 512 * 1024;

    private const int TaskStatusProbeLimit = 6;

    private const int PlanProgressProbeLimit = 4;

    internal static IReadOnlyList<string> ReadOnlyRequestMethods { get; } =
        ["initialize", "initialized", "thread/list", "thread/turns/list"];

    public async Task<CodexTaskSnapshot> ReadAsync(CancellationToken cancellationToken = default)
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
                    },
                    capabilities = new { experimentalApi = true }
                }
            }, timeout.Token).ConfigureAwait(false);
            JsonElement initializeResponse = await ReadResponseAsync(process, 1, timeout.Token).ConfigureAwait(false);
            if (initializeResponse.TryGetProperty("error", out JsonElement initializeError))
            {
                throw new InvalidOperationException(GetErrorMessage(initializeError));
            }

            await SendAsync(process, new { method = "initialized", @params = new { } }, timeout.Token).ConfigureAwait(false);
            await SendAsync(process, new
            {
                method = "thread/list",
                id = 2,
                @params = new
                {
                    cursor = (string?)null,
                    limit = 20,
                    sortKey = "updated_at",
                    sortDirection = "desc",
                    archived = false,
                    useStateDbOnly = true,
                    sourceKinds = new[] { "cli", "vscode", "appServer" }
                }
            }, timeout.Token).ConfigureAwait(false);

            while (true)
            {
                string? line = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (!TryParseProtocolResponse(line, 2, out JsonElement message))
                {
                    continue;
                }
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
                    throw new InvalidOperationException("Codex 没有返回任务列表。");
                }

                DateTimeOffset observedAt = DateTimeOffset.UtcNow;
                List<ThreadCandidate> candidates = ParseThreadCandidates(result, observedAt);
                if (candidates.Count == 0)
                {
                    return ToSnapshot(candidates, observedAt);
                }

                return await ReadTaskStatusesAsync(process, candidates, observedAt, timeout.Token).ConfigureAwait(false);
            }

            string errorOutput = await standardError.ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput)
                ? "Codex 未返回任务列表，请确认本机已登录 ChatGPT。"
                : "Codex 未返回任务列表：" + errorOutput.Trim());
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("读取 Codex 任务超时，请稍后重试。", ex);
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

    internal static CodexTaskSnapshot ParseThreadList(JsonElement result, DateTimeOffset observedAt)
    {
        return ToSnapshot(ParseThreadCandidates(result, observedAt), observedAt);
    }

    internal static bool IsInProgressTurn(JsonElement result)
    {
        return GetLatestTurnStatus(result) == CodexTaskStatus.Active;
    }

    internal static CodexTaskStatus? GetLatestTurnStatus(JsonElement result)
    {
        if (!result.TryGetProperty("data", out JsonElement data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement turn in data.EnumerateArray())
        {
            if (!turn.TryGetProperty("status", out JsonElement status)
                || status.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return status.GetString() switch
            {
                "inProgress" => CodexTaskStatus.Active,
                "completed" => CodexTaskStatus.Completed,
                _ => CodexTaskStatus.Recent
            };
        }

        return null;
    }

    internal static DateTimeOffset? GetLatestTurnCompletedAt(JsonElement result)
    {
        if (!result.TryGetProperty("data", out JsonElement data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement turn in data.EnumerateArray())
        {
            if (!turn.TryGetProperty("completedAt", out JsonElement completedAt)
                || completedAt.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(completedAt.GetInt64());
        }

        return null;
    }

    internal static bool TryParseProtocolMessage(string line, out JsonElement message)
    {
        return TryParseNextProtocolMessage(line, 0, out message, out _);
    }

    internal static bool TryParseProtocolResponse(string line, int expectedId, out JsonElement message)
    {
        message = default;
        int searchStart = 0;
        while (TryParseNextProtocolMessage(line, searchStart, out JsonElement candidate, out int nextSearchStart))
        {
            if (candidate.TryGetProperty("id", out JsonElement id)
                && id.ValueKind == JsonValueKind.Number
                && id.GetInt32() == expectedId)
            {
                message = candidate;
                return true;
            }

            searchStart = nextSearchStart;
        }

        return false;
    }

    private static bool TryParseNextProtocolMessage(
        string line,
        int searchStart,
        out JsonElement message,
        out int nextSearchStart)
    {
        message = default;
        nextSearchStart = line.Length;
        int objectStart = line.IndexOf('{', searchStart);
        if (objectStart < 0)
        {
            return false;
        }

        int depth = 0;
        bool insideString = false;
        bool escaped = false;
        for (int index = objectStart; index < line.Length; index++)
        {
            char current = line[index];
            if (insideString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    insideString = false;
                }

                continue;
            }

            if (current == '"')
            {
                insideString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}' && --depth == 0)
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(
                        line.AsMemory(objectStart, index - objectStart + 1));
                    message = document.RootElement.Clone();
                    nextSearchStart = index + 1;
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static async Task<CodexTaskSnapshot> ReadTaskStatusesAsync(
        Process process,
        List<ThreadCandidate> candidates,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        int requestCount = Math.Min(candidates.Count, TaskStatusProbeLimit);
        for (int index = 0; index < requestCount; index++)
        {
            int requestId = index + 3;
            await SendAsync(process, new
            {
                method = "thread/turns/list",
                id = requestId,
                @params = new
                {
                    threadId = candidates[index].ThreadId,
                    limit = 1,
                    sortDirection = "desc",
                    itemsView = "notLoaded"
                }
            }, cancellationToken).ConfigureAwait(false);
            JsonElement message = await ReadResponseAsync(process, requestId, cancellationToken).ConfigureAwait(false);
            if (!message.TryGetProperty("result", out JsonElement result)
                || candidates[index].Task.Status == CodexTaskStatus.WaitingForApproval
                || GetLatestTurnStatus(result) is not { } turnStatus)
            {
                continue;
            }

            ThreadCandidate candidate = candidates[index];
            candidates[index] = candidate with
            {
                Task = candidate.Task with
                {
                    Status = turnStatus,
                    CompletedAt = GetLatestTurnCompletedAt(result)
                }
            };
        }

        ApplyLocalActivity(candidates, observedAt);
        ApplyLocalPlanProgress(candidates);
        return ToSnapshot(candidates, observedAt);
    }

    private static List<ThreadCandidate> ParseThreadCandidates(JsonElement result, DateTimeOffset observedAt)
    {
        if (!result.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Codex 没有返回任务数组。");
        }

        var candidates = new List<ThreadCandidate>();
        foreach (JsonElement thread in data.EnumerateArray())
        {
            if (!thread.TryGetProperty("id", out JsonElement threadId) || threadId.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string title = GetDisplayTitle(thread);
            DateTimeOffset createdAt = GetCreatedAt(thread, observedAt);
            DateTimeOffset updatedAt = GetUpdatedAt(thread, observedAt);
            CodexTaskStatus status = GetStatus(thread);
            candidates.Add(new ThreadCandidate(
                threadId.GetString()!,
                createdAt,
                new CodexTaskItem(title, status, updatedAt)));
        }

        return candidates.OrderByDescending(candidate => candidate.Task.UpdatedAt).ToList();
    }

    private static CodexTaskSnapshot ToSnapshot(
        IEnumerable<ThreadCandidate> candidates,
        DateTimeOffset observedAt)
    {
        return new CodexTaskSnapshot(
            true,
            candidates.Select(candidate => candidate.Task).OrderByDescending(task => task.UpdatedAt).ToArray(),
            observedAt);
    }

    internal static ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "codex.cmd" : "codex",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
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

    private static async Task<JsonElement> ReadResponseAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                throw new InvalidOperationException($"Codex App Server 在响应请求 {expectedId} 前已关闭。");
            }

            if (!TryParseProtocolResponse(line, expectedId, out JsonElement message))
            {
                continue;
            }

            return message;
        }
    }

    private static string GetDisplayTitle(JsonElement thread)
    {
        if (!thread.TryGetProperty("name", out JsonElement name) || name.ValueKind != JsonValueKind.String)
        {
            return "未命名 Codex 任务";
        }

        string title = name.GetString()?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(title) ? "未命名 Codex 任务" : title;
    }

    private static DateTimeOffset GetUpdatedAt(JsonElement thread, DateTimeOffset fallback) =>
        thread.TryGetProperty("updatedAt", out JsonElement updatedAt) && updatedAt.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(updatedAt.GetInt64())
            : fallback;

    private static DateTimeOffset GetCreatedAt(JsonElement thread, DateTimeOffset fallback) =>
        thread.TryGetProperty("createdAt", out JsonElement createdAt) && createdAt.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(createdAt.GetInt64())
            : fallback;

    private static void ApplyLocalActivity(List<ThreadCandidate> candidates, DateTimeOffset observedAt)
    {
        for (int index = 0; index < candidates.Count; index++)
        {
            ThreadCandidate candidate = candidates[index];
            if (candidate.Task.Status == CodexTaskStatus.WaitingForApproval
                || !TryFindRolloutPath(candidate, out string? rolloutPath)
                || rolloutPath is null
                || GetLocalRolloutDetails(rolloutPath, observedAt) is not { } localDetails)
            {
                continue;
            }

            string title = localDetails.Status == CodexTaskStatus.Active
                && string.Equals(candidate.Task.Title, "未命名 Codex 任务", StringComparison.Ordinal)
                ? "当前 Codex 任务"
                : candidate.Task.Title;
            candidates[index] = candidate with
            {
                Task = candidate.Task with
                {
                    Title = title,
                    Status = localDetails.Status,
                    CompletedAt = localDetails.CompletedAt ?? candidate.Task.CompletedAt
                }
            };
        }
    }

    private static void ApplyLocalPlanProgress(List<ThreadCandidate> candidates)
    {
        int[] candidateIndexes = candidates
            .Select((candidate, index) => new { candidate, index })
            .Where(entry => entry.candidate.Task.Status is CodexTaskStatus.WaitingForApproval
                or CodexTaskStatus.Active
                or CodexTaskStatus.Completed)
            .OrderBy(entry => entry.candidate.Task.Status switch
            {
                CodexTaskStatus.WaitingForApproval => 0,
                CodexTaskStatus.Active => 1,
                _ => 2
            })
            .ThenByDescending(entry => entry.candidate.Task.UpdatedAt)
            .Take(PlanProgressProbeLimit)
            .Select(entry => entry.index)
            .ToArray();
        foreach (int index in candidateIndexes)
        {
            ThreadCandidate candidate = candidates[index];
            if (!TryFindRolloutPath(candidate, out string? rolloutPath)
                || rolloutPath is null)
            {
                continue;
            }

            if (!TryReadLatestPlanProgress(rolloutPath, out int completedSteps, out int totalSteps))
            {
                continue;
            }

            candidates[index] = candidate with
            {
                Task = candidate.Task with
                {
                    CompletedPlanSteps = completedSteps,
                    TotalPlanSteps = totalSteps
                }
            };
        }
    }

    internal static bool TryReadLatestPlanProgress(
        string rolloutPath,
        out int completedSteps,
        out int totalSteps)
    {
        completedSteps = 0;
        totalSteps = 0;
        bool found = false;
        try
        {
            using var stream = new FileStream(
                rolloutPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (!line.Contains("update_plan", StringComparison.Ordinal)
                    || !TryParsePlanProgressFromRolloutLine(line, out int parsedCompleted, out int parsedTotal))
                {
                    continue;
                }

                completedSteps = parsedCompleted;
                totalSteps = parsedTotal;
                found = true;
            }

            return found;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    internal static bool TryParsePlanProgressFromRolloutLine(
        string line,
        out int completedSteps,
        out int totalSteps)
    {
        completedSteps = 0;
        totalSteps = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("type", out JsonElement recordType)
                || recordType.ValueKind != JsonValueKind.String
                || !string.Equals(recordType.GetString(), "response_item", StringComparison.Ordinal)
                || !root.TryGetProperty("payload", out JsonElement payload)
                || !payload.TryGetProperty("type", out JsonElement payloadType)
                || payloadType.ValueKind != JsonValueKind.String
                || !string.Equals(payloadType.GetString(), "custom_tool_call", StringComparison.Ordinal)
                || !payload.TryGetProperty("name", out JsonElement name)
                || name.ValueKind != JsonValueKind.String
                || !payload.TryGetProperty("input", out JsonElement input))
            {
                return false;
            }

            string? planJson = null;
            if (string.Equals(name.GetString(), "update_plan", StringComparison.Ordinal))
            {
                planJson = input.ValueKind == JsonValueKind.String
                    ? input.GetString()
                    : input.ValueKind == JsonValueKind.Object ? input.GetRawText() : null;
            }
            else if (string.Equals(name.GetString(), "exec", StringComparison.Ordinal)
                     && input.ValueKind == JsonValueKind.String)
            {
                planJson = TryExtractJsonArgument(input.GetString() ?? string.Empty, "tools.update_plan(");
            }

            return planJson is not null
                && TryCountPlanSteps(planJson, out completedSteps, out totalSteps);
        }
        catch (JsonException)
        {
            completedSteps = 0;
            totalSteps = 0;
            return false;
        }
    }

    private static string? TryExtractJsonArgument(string source, string marker)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        int objectStart = source.IndexOf('{', markerIndex + marker.Length);
        if (objectStart < 0)
        {
            return null;
        }

        int depth = 0;
        bool insideString = false;
        bool escaped = false;
        for (int index = objectStart; index < source.Length; index++)
        {
            char current = source[index];
            if (insideString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    insideString = false;
                }

                continue;
            }

            if (current == '"')
            {
                insideString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}' && --depth == 0)
            {
                return source.Substring(objectStart, index - objectStart + 1);
            }
        }

        return null;
    }

    private static bool TryCountPlanSteps(
        string planJson,
        out int completedSteps,
        out int totalSteps)
    {
        completedSteps = 0;
        totalSteps = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(planJson);
            if (!document.RootElement.TryGetProperty("plan", out JsonElement plan)
                || plan.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement step in plan.EnumerateArray())
            {
                totalSteps++;
                if (step.TryGetProperty("status", out JsonElement status)
                    && status.ValueKind == JsonValueKind.String
                    && string.Equals(status.GetString(), "completed", StringComparison.Ordinal))
                {
                    completedSteps++;
                }
            }

            return totalSteps > 0;
        }
        catch (JsonException)
        {
            return TryCountJavascriptPlanStatuses(planJson, out completedSteps, out totalSteps);
        }
    }

    private static bool TryCountJavascriptPlanStatuses(
        string source,
        out int completedSteps,
        out int totalSteps)
    {
        completedSteps = 0;
        totalSteps = 0;
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            int cursor;
            if (current is '"' or '\'')
            {
                char keyQuote = current;
                int keyStart = index + 1;
                int keyEnd = keyStart;
                bool escaped = false;
                for (; keyEnd < source.Length; keyEnd++)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (source[keyEnd] == '\\')
                    {
                        escaped = true;
                    }
                    else if (source[keyEnd] == keyQuote)
                    {
                        break;
                    }
                }
                if (keyEnd >= source.Length)
                {
                    return false;
                }
                if (!source.AsSpan(keyStart, keyEnd - keyStart).SequenceEqual("status"))
                {
                    index = keyEnd;
                    continue;
                }

                cursor = keyEnd + 1;
            }
            else
            {
                const string statusKey = "status";
                if (!source.AsSpan(index).StartsWith(statusKey, StringComparison.Ordinal)
                    || index > 0 && (char.IsLetterOrDigit(source[index - 1]) || source[index - 1] == '_'))
                {
                    continue;
                }

                cursor = index + statusKey.Length;
                if (cursor < source.Length && (char.IsLetterOrDigit(source[cursor]) || source[cursor] == '_'))
                {
                    continue;
                }
            }

            while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
            {
                cursor++;
            }
            if (cursor >= source.Length || source[cursor++] != ':')
            {
                continue;
            }
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
            {
                cursor++;
            }
            if (cursor >= source.Length || source[cursor] is not ('"' or '\''))
            {
                continue;
            }

            char valueQuote = source[cursor++];
            int valueStart = cursor;
            while (cursor < source.Length && source[cursor] != valueQuote)
            {
                cursor++;
            }
            if (cursor >= source.Length)
            {
                return false;
            }

            string status = source.Substring(valueStart, cursor - valueStart);
            if (status is not ("pending" or "in_progress" or "completed"))
            {
                continue;
            }

            totalSteps++;
            if (status == "completed")
            {
                completedSteps++;
            }
            index = cursor;
        }

        return totalSteps > 0;
    }

    private static bool TryFindRolloutPath(ThreadCandidate candidate, out string? rolloutPath)
    {
        rolloutPath = null;
        if (!Guid.TryParse(candidate.ThreadId, out _))
        {
            return false;
        }

        string codexHome = Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        string sessionsRoot = Path.Combine(codexHome, "sessions");
        if (!Directory.Exists(sessionsRoot))
        {
            return false;
        }

        DateTime[] dates =
        [
            candidate.CreatedAt.UtcDateTime.Date,
            candidate.CreatedAt.LocalDateTime.Date,
            candidate.Task.UpdatedAt.UtcDateTime.Date,
            candidate.Task.UpdatedAt.LocalDateTime.Date
        ];
        foreach (DateTime date in dates.Distinct())
        {
            string dayDirectory = Path.Combine(
                sessionsRoot,
                date.ToString("yyyy"),
                date.ToString("MM"),
                date.ToString("dd"));
            if (!Directory.Exists(dayDirectory))
            {
                continue;
            }

            rolloutPath = Directory
                .EnumerateFiles(dayDirectory, $"*{candidate.ThreadId}.jsonl", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (rolloutPath is not null)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsLocallyActiveRollout(string rolloutPath, DateTimeOffset observedAt)
    {
        return GetLocalRolloutStatus(rolloutPath, observedAt) == CodexTaskStatus.Active;
    }

    internal static CodexTaskStatus? GetLocalRolloutStatus(string rolloutPath, DateTimeOffset observedAt)
    {
        return GetLocalRolloutDetails(rolloutPath, observedAt)?.Status;
    }

    internal static DateTimeOffset? GetLocalRolloutCompletedAt(string rolloutPath, DateTimeOffset observedAt)
    {
        return GetLocalRolloutDetails(rolloutPath, observedAt)?.CompletedAt;
    }

    private static LocalRolloutDetails? GetLocalRolloutDetails(string rolloutPath, DateTimeOffset observedAt)
    {
        try
        {
            DateTimeOffset lastWriteAt = File.GetLastWriteTimeUtc(rolloutPath);
            TimeSpan age = observedAt.ToUniversalTime() - lastWriteAt;
            if (age < TimeSpan.Zero)
            {
                return null;
            }

            using var stream = new FileStream(
                rolloutPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumRolloutTailBytes)
            {
                stream.Seek(-MaximumRolloutTailBytes, SeekOrigin.End);
            }

            using var reader = new StreamReader(stream);
            if (stream.Position > 0)
            {
                reader.ReadLine();
            }

            CodexTaskStatus? latestStatus = null;
            DateTimeOffset? completedAt = null;
            while (reader.ReadLine() is { } line)
            {
                if (!line.Contains("task_started", StringComparison.Ordinal)
                    && !line.Contains("task_complete", StringComparison.Ordinal))
                {
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("type", out JsonElement recordType)
                    || recordType.ValueKind != JsonValueKind.String
                    || !string.Equals(recordType.GetString(), "event_msg", StringComparison.Ordinal)
                    || !root.TryGetProperty("payload", out JsonElement payload)
                    || !payload.TryGetProperty("type", out JsonElement payloadType)
                    || payloadType.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (string.Equals(payloadType.GetString(), "task_started", StringComparison.Ordinal))
                {
                    latestStatus = CodexTaskStatus.Active;
                }
                else if (string.Equals(payloadType.GetString(), "task_complete", StringComparison.Ordinal))
                {
                    latestStatus = CodexTaskStatus.Completed;
                    completedAt = root.TryGetProperty("timestamp", out JsonElement timestamp)
                        && timestamp.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(timestamp.GetString(), out DateTimeOffset parsedTimestamp)
                            ? parsedTimestamp
                            : lastWriteAt;
                }
            }

            if (latestStatus == CodexTaskStatus.Completed)
            {
                return new LocalRolloutDetails(CodexTaskStatus.Completed, completedAt);
            }

            // A long-running turn can write more than the bounded tail after its
            // task_started envelope. Continued recent writes still prove that the
            // rollout is active; task_complete, when present, always wins above.
            return age <= LocalActivityWindow
                ? new LocalRolloutDetails(CodexTaskStatus.Active, null)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static CodexTaskStatus GetStatus(JsonElement thread)
    {
        if (!thread.TryGetProperty("status", out JsonElement status) || status.ValueKind != JsonValueKind.Object)
        {
            return CodexTaskStatus.Recent;
        }

        bool waitingForApproval = status.TryGetProperty("activeFlags", out JsonElement activeFlags)
            && activeFlags.ValueKind == JsonValueKind.Array
            && activeFlags.EnumerateArray().Any(flag =>
                flag.ValueKind == JsonValueKind.String
                && string.Equals(flag.GetString(), "waitingOnApproval", StringComparison.Ordinal));
        if (waitingForApproval)
        {
            return CodexTaskStatus.WaitingForApproval;
        }

        if (!status.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.String)
        {
            return CodexTaskStatus.Recent;
        }

        return type.GetString() switch
        {
            "active" => CodexTaskStatus.Active,
            "completed" => CodexTaskStatus.Completed,
            _ => CodexTaskStatus.Recent
        };
    }

    private static string GetErrorMessage(JsonElement error) =>
        error.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String
            ? "Codex 任务读取失败：" + message.GetString()
            : "Codex 任务读取失败。";

    private sealed record ThreadCandidate(string ThreadId, DateTimeOffset CreatedAt, CodexTaskItem Task);

    private sealed record LocalRolloutDetails(CodexTaskStatus Status, DateTimeOffset? CompletedAt);
}
