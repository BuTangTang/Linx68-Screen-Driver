using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

internal static class CodexTaskCardRenderer
{
    private static readonly Color CardFill = Color.FromRgb(19, 28, 45);
    private static readonly Color CardStroke = Color.FromRgb(49, 67, 92);
    private static readonly Color SecondaryText = Color.FromRgb(165, 180, 201);
    private static readonly Color CompletedColor = Color.FromRgb(96, 171, 245);
    private static readonly Color ActiveColor = Color.FromRgb(71, 213, 190);
    private static readonly Color ApprovalColor = Color.FromRgb(248, 182, 81);
    private static readonly Color MutedColor = Color.FromRgb(124, 141, 163);

    public static void Draw(
        ScreenCanvas canvas,
        Rect card,
        CodexTaskItem? task)
    {
        CodexTaskItem displayTask = task ?? new CodexTaskItem(
            "暂无 Codex 任务",
            CodexTaskStatus.Empty,
            DateTimeOffset.MinValue);
        CodexTaskStatus status = displayTask.Status;
        Color color = GetColor(status);
        Color cardStroke = status is CodexTaskStatus.Active or CodexTaskStatus.WaitingForApproval or CodexTaskStatus.Completed
            ? color
            : CardStroke;
        double strokeWidth = status is CodexTaskStatus.Active or CodexTaskStatus.Completed ? 1.7 : 1.2;
        canvas.RoundedRect(card, 11, CardFill, cardStroke, strokeWidth);
        if (status is CodexTaskStatus.Active or CodexTaskStatus.WaitingForApproval or CodexTaskStatus.Completed)
        {
            canvas.RoundedRect(new Rect(card.Left + 3, card.Top + 6, 5, card.Height - 12), 2.5, color);
        }

        string label = GetLabel(status);
        string detail = GetDetail(displayTask, status);
        bool useTwoLineTitle = card.Height >= 60;
        double statusSize = useTwoLineTitle ? 11.2 : 10.5;
        double detailSize = useTwoLineTitle ? 10 : 9.5;
        double titleSize = useTwoLineTitle ? 13.2 : 12.5;

        canvas.Ellipse(new Rect(card.Left + 13, card.Top + 12, 6, 6), color);
        canvas.Text(label, statusSize, color, new Point(card.Left + 24, card.Top + 6), FontWeights.Bold);
        canvas.FittedText(
            detail,
            detailSize,
            8.5,
            SecondaryText,
            new Point(card.Right - 50, card.Top + 7),
            FontWeights.Medium,
            TextAlignment.Right,
            40,
            15);

        Color titleColor = status is CodexTaskStatus.Loading or CodexTaskStatus.Unavailable or CodexTaskStatus.Empty
            ? SecondaryText
            : Colors.White;
        if (useTwoLineTitle)
        {
            canvas.WrappedText(
                displayTask.Title,
                titleSize,
                titleColor,
                new Point(card.Left + 13, card.Top + 22),
                FontWeights.SemiBold,
                TextAlignment.Left,
                card.Width - 26,
                2,
                14);
        }
        else
        {
            canvas.Text(
                displayTask.Title,
                titleSize,
                titleColor,
                new Point(card.Left + 13, card.Top + 30),
                FontWeights.SemiBold,
                TextAlignment.Left,
                card.Width - 26,
                18);
        }
    }

    public static CodexTaskItem? GetTask(IReadOnlyList<CodexTaskItem> tasks, int index) =>
        index >= 0 && index < tasks.Count ? tasks[index] : null;

    private static Color GetColor(CodexTaskStatus status) => status switch
    {
        CodexTaskStatus.WaitingForApproval => ApprovalColor,
        CodexTaskStatus.Active => ActiveColor,
        CodexTaskStatus.Completed => CompletedColor,
        _ => MutedColor
    };

    private static string GetLabel(CodexTaskStatus status) => status switch
    {
        CodexTaskStatus.WaitingForApproval => "需要确认",
        CodexTaskStatus.Active => "运行中",
        CodexTaskStatus.Completed => "完成了",
        CodexTaskStatus.Loading => "正在读取",
        CodexTaskStatus.Unavailable => "任务状态",
        _ => "暂无任务"
    };

    internal static string GetDetail(CodexTaskItem task, CodexTaskStatus status)
    {
        if (status is CodexTaskStatus.Loading or CodexTaskStatus.Unavailable or CodexTaskStatus.Empty || task.UpdatedAt == DateTimeOffset.MinValue)
        {
            return string.Empty;
        }

        if (task.HasPlanProgress)
        {
            return $"{task.CompletedPlanSteps}/{task.TotalPlanSteps}";
        }

        DateTimeOffset detailAt = status == CodexTaskStatus.Completed
            ? task.CompletedAt ?? task.UpdatedAt
            : task.UpdatedAt;
        return detailAt.ToLocalTime().ToString("HH:mm");
    }
}
