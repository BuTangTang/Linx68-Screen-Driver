namespace Linx68.ScreenDriver.Core;

public enum CodexTaskStatus
{
    Active,
    WaitingForApproval,
    Completed,
    Recent,
    Loading,
    Unavailable,
    Empty
}

/// <summary>
/// A minimal, display-safe summary of a local Codex task. It deliberately
/// excludes thread IDs, working directories, previews, conversation items,
/// commands, and authentication information.
/// </summary>
public sealed record CodexTaskItem(
    string Title,
    CodexTaskStatus Status,
    DateTimeOffset UpdatedAt,
    int? CompletedPlanSteps = null,
    int? TotalPlanSteps = null,
    DateTimeOffset? CompletedAt = null)
{
    public bool HasPlanProgress =>
        CompletedPlanSteps is >= 0 && TotalPlanSteps is > 0 && CompletedPlanSteps <= TotalPlanSteps;
}

public sealed record CodexTaskSnapshot(
    bool Available,
    IReadOnlyList<CodexTaskItem> Tasks,
    DateTimeOffset UpdatedAt,
    string? ErrorMessage = null)
{
    public static CodexTaskSnapshot Empty(DateTimeOffset timestamp) =>
        new(true, Array.Empty<CodexTaskItem>(), timestamp);

    public static CodexTaskSnapshot Loading(DateTimeOffset timestamp) =>
        new(false, Array.Empty<CodexTaskItem>(), timestamp);

    public static CodexTaskSnapshot Unavailable(DateTimeOffset timestamp, string? errorMessage = null) =>
        new(false, Array.Empty<CodexTaskItem>(), timestamp, errorMessage);

    /// <summary>
    /// Orders task cards for a compact display. A confirmation request is more
    /// urgent than ordinary work, followed by a currently active task, then
    /// tasks whose latest turn is confirmed completed. Ambiguous, interrupted,
    /// or merely recent tasks are intentionally excluded from the display.
    /// </summary>
    public IReadOnlyList<CodexTaskItem> GetDisplayTasks(int maximumCount)
    {
        if (maximumCount <= 0 || Tasks.Count == 0)
        {
            return Array.Empty<CodexTaskItem>();
        }

        List<CodexTaskItem> ordered = Tasks
            .Where(task => task.Status is CodexTaskStatus.WaitingForApproval
                or CodexTaskStatus.Active
                or CodexTaskStatus.Completed)
            .OrderByDescending(task => task.UpdatedAt)
            .ToList();
        if (ordered.Count == 0)
        {
            return Array.Empty<CodexTaskItem>();
        }
        int primaryIndex = ordered.FindIndex(task => task.Status == CodexTaskStatus.WaitingForApproval);
        if (primaryIndex < 0)
        {
            primaryIndex = ordered.FindIndex(task => task.Status == CodexTaskStatus.Active);
        }
        if (primaryIndex < 0)
        {
            primaryIndex = 0;
        }

        var selected = new List<CodexTaskItem>(Math.Min(maximumCount, ordered.Count))
        {
            ordered[primaryIndex]
        };
        for (int index = 0; index < ordered.Count; index++)
        {
            if (selected.Count >= maximumCount)
            {
                break;
            }

            if (index != primaryIndex)
            {
                selected.Add(ordered[index]);
            }
        }

        return selected;
    }
}
