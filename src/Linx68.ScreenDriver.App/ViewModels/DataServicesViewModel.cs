using CommunityToolkit.Mvvm.ComponentModel;
using Linx68.ScreenDriver.Application;

namespace Linx68.ScreenDriver.App.ViewModels;

public sealed partial class DataSourceStatusViewModel(
    string title,
	string initialSource,
    string initialSummary) : ObservableObject
{
    public string Title { get; } = title;

	private string source = initialSource;

	public string Source
	{
		get => source;
		set => SetProperty(ref source, value);
	}

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsProblem))]
    private DataLoadState state = DataLoadState.Empty;

    [ObservableProperty]
    private string summary = initialSummary;

    [ObservableProperty]
    private string detail = "尚未刷新";

    [ObservableProperty]
    private string timing = "—";

    public string StateLabel => State switch
    {
        DataLoadState.Loading => "更新中",
        DataLoadState.Ready => "正常",
        DataLoadState.Empty => "暂无数据",
        DataLoadState.Stale => "上次数据",
        DataLoadState.Error => "需要处理",
        _ => "未知"
    };

    public bool IsReady => State == DataLoadState.Ready;

    public bool IsProblem => State is DataLoadState.Stale or DataLoadState.Error;

    public void Set(
        DataLoadState newState,
        string newSummary,
        string newDetail,
		TimeSpan? duration = null,
		string? newSource = null)
    {
        State = newState;
        Summary = newSummary;
        Detail = newDetail;
        Timing = duration is null ? "—" : $"{Math.Max(0, duration.Value.TotalMilliseconds):0} ms";
		if (!string.IsNullOrWhiteSpace(newSource)) Source = newSource;
    }

    public void BeginRefresh(string? loadingDetail = null)
    {
		bool hasPreviousResult = State != DataLoadState.Empty
			|| !string.Equals(Detail, "尚未刷新", StringComparison.Ordinal);
        State = DataLoadState.Loading;
		Detail = loadingDetail ?? (hasPreviousResult ? $"保留上次内容 · {Summary}" : "正在读取");
        Timing = "计时中";
    }
}
public sealed partial class DataServicesViewModel : ObservableObject
{
    public DataSourceStatusViewModel Codex { get; } = new("Codex", "本机 Codex App Server", "等待读取额度与任务");

    public DataSourceStatusViewModel Music { get; } = new("网易云音乐", "Windows 媒体会话", "等待检测播放器");

    public DataSourceStatusViewModel Weather { get; } = new("天气定位", "Windows 位置服务", "等待定位");

    public DataSourceStatusViewModel Device { get; } = new("设备连接", "Linx68 HTTP 图像接口", "尚未连接设备");

    [ObservableProperty]
    private string overallStatus = "等待首次刷新";

    [ObservableProperty]
    private string lastRefreshText = "尚未刷新全部";

    public void BeginRefresh()
    {
        Codex.BeginRefresh();
        Music.BeginRefresh();
        Weather.BeginRefresh();
        OverallStatus = "正在并行刷新";
    }

    public void CompleteRefresh(TimeSpan duration, DateTimeOffset completedAt)
    {
		UpdateOverallStatus();
		LastRefreshText = $"{completedAt:HH:mm:ss} · 总耗时 {duration.TotalMilliseconds:0} ms";
	}

	public void UpdateOverallStatus()
	{
        DataSourceStatusViewModel[] sources = [Codex, Music, Weather, Device];
        int readyCount = sources.Count(status => status.State == DataLoadState.Ready);
        int loadingCount = sources.Count(status => status.State == DataLoadState.Loading);
        int emptyCount = sources.Count(status => status.State == DataLoadState.Empty);
        int problemCount = sources
            .Count(status => status.State is DataLoadState.Stale or DataLoadState.Error);
        var notes = new List<string>(3);
        if (problemCount > 0) notes.Add($"{problemCount} 项需要留意");
        if (emptyCount > 0) notes.Add($"{emptyCount} 项暂无数据");
        if (loadingCount > 0) notes.Add($"{loadingCount} 项更新中");
        OverallStatus = notes.Count == 0
            ? "4/4 已就绪 · 数据状态正常"
            : $"{readyCount}/4 已就绪 · {string.Join(" · ", notes)}";
    }
}
