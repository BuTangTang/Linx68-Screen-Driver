using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class CodexTasksTheme : IScreenTheme
{
    public string Id => "codex-tasks";
    public string DisplayName => "Codex 任务";
    public string Description => "四条任务进度一屏查看";
    public string Details => "仅展示运行中、需要确认或已经完成的 Codex 任务。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        CodexTaskSnapshot tasks = snapshot.CodexTasks ?? CodexTaskSnapshot.Loading(snapshot.Timestamp);
        IReadOnlyList<CodexTaskItem> displayTasks = tasks.GetDisplayTasks(4);

        canvas.Gradient(Color.FromRgb(12, 18, 31), Color.FromRgb(8, 26, 35), new Point(0, 0), new Point(1, 1));
        canvas.Ellipse(
            new Rect(safe.Left, safe.Top + 8, 6, 6),
            tasks.Available ? canvas.AccentColor : Color.FromRgb(108, 121, 136));
        canvas.Text("Codex 任务", 12.5, Colors.White,
            new Point(safe.Left + 11, safe.Top + 4), FontWeights.SemiBold);
        string headerDetail = tasks.Available
            ? displayTasks.Count > 0 ? $"{displayTasks.Count} 条" : "暂无任务"
            : "暂不可用";
        canvas.Text(headerDetail, 10.5, Color.FromRgb(166, 183, 201),
            new Point(safe.Left, safe.Top + 5), FontWeights.Medium, TextAlignment.Right, safe.Width);

        const double cardHeight = 75;
        const double cardGap = 5;
        double firstCardTop = safe.Bottom - (cardHeight * 4 + cardGap * 3);
        for (int index = 0; index < 4; index++)
        {
            var card = new Rect(safe.Left, firstCardTop + index * (cardHeight + cardGap), safe.Width, cardHeight);
            CodexTaskItem? task = CodexTaskCardRenderer.GetTask(displayTasks, index);
            if (!tasks.Available && tasks.ErrorMessage is not null)
            {
                task = new CodexTaskItem("任务状态暂不可用", CodexTaskStatus.Unavailable, DateTimeOffset.MinValue);
            }
            else if (!tasks.Available)
            {
                task = new CodexTaskItem("正在获取 Codex 任务", CodexTaskStatus.Loading, DateTimeOffset.MinValue);
            }

            CodexTaskCardRenderer.Draw(canvas, card, task);
        }
    }
}
