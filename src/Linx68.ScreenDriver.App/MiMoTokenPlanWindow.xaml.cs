using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;
using Microsoft.Web.WebView2.Core;

namespace Linx68.ScreenDriver.App;

public partial class MiMoTokenPlanWindow : Window, IAiQuotaSnapshotSource, IDisposable
{
    private static readonly Uri PlanUri = new("https://platform.xiaomimimo.com/console/plan-manage");
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private Task? _initialization;
    private AiQuotaSnapshot? _cachedSnapshot;
    private DateTimeOffset _cachedAt;
    private bool _allowClose;
    private bool _hiding;

    public MiMoTokenPlanWindow()
    {
        InitializeComponent();
        Browser.NavigationCompleted += Browser_OnNavigationCompleted;
        SourceInitialized += (_, _) => WindowBackdrop.TryApplyRoundedCorners(this);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                InteractionMotion.Reveal(DialogRoot, 8, 0.994);
            }
        };
    }

    public event EventHandler<AiQuotaSnapshot>? SnapshotUpdated;

    public async Task InitializeAsync()
    {
        _initialization ??= InitializeCoreAsync();
        await _initialization;
    }

    public async Task<AiQuotaSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        await ReadAsync(false, cancellationToken);

    public async Task<AiQuotaSnapshot> ReadAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!force && _cachedSnapshot is not null && DateTimeOffset.UtcNow - _cachedAt < CacheLifetime)
        {
            return _cachedSnapshot;
        }

        if (Browser.CoreWebView2 is null)
        {
            throw new InvalidOperationException("请先点击“登录小米控制台”完成登录。");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var current = Browser.Source;
        if (current is null || !string.Equals(current.Host, PlanUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("请先在登录窗口进入小米 Token Plan 页面。");
        }

        var script = """
            (async () => {
              const read = async (url) => {
                const response = await fetch(url, { credentials: 'include', cache: 'no-store' });
                const text = await response.text();
                let body;
                try { body = JSON.parse(text); } catch { throw new Error('小米控制台返回了无法识别的数据'); }
                if (!response.ok) throw new Error(body.message || body.msg || `HTTP ${response.status}`);
                return body;
              };
              const [detail, usage] = await Promise.all([
                read('/api/v1/tokenPlan/detail'),
                read('/api/v1/tokenPlan/usage')
              ]);
              return JSON.stringify({ detail, usage });
            })()
            """;

        var parameters = JsonSerializer.Serialize(new
        {
            expression = script,
            awaitPromise = true,
            returnByValue = true
        });
        var encodedResult = await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Runtime.evaluate",
            parameters);
        using var evaluation = JsonDocument.Parse(encodedResult);
        if (evaluation.RootElement.TryGetProperty("exceptionDetails", out var exception))
        {
            var description = exception.TryGetProperty("exception", out var exceptionValue) &&
                              exceptionValue.TryGetProperty("description", out var descriptionValue)
                ? descriptionValue.GetString()
                : null;
            throw new InvalidDataException(description ?? "小米控制台数据请求失败。");
        }

        var result = evaluation.RootElement
            .GetProperty("result")
            .GetProperty("value")
            .GetString();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidDataException("没有读到小米 Token Plan 数据，请确认已经登录。");
        }

        using var container = JsonDocument.Parse(result);
        var snapshot = XiaomiMiMoTokenPlanParser.Parse(
            container.RootElement.GetProperty("detail").GetRawText(),
            container.RootElement.GetProperty("usage").GetRawText());
        _cachedSnapshot = snapshot;
        _cachedAt = DateTimeOffset.UtcNow;
        var usedPercent = 100d - snapshot.ClampedRemainingPercent;
        StatusText.Text = $"已读取 · 剩余 {snapshot.ClampedRemainingPercent:0}% · 已使用 {usedPercent:0}%";
        SnapshotUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    private async Task InitializeCoreAsync()
    {
        var profilePath = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"))
            ? Path.Combine(AppContext.BaseDirectory, "Data", "MiMoWebView2")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KeyboardScreenStudio",
                "MiMoWebView2");
        Directory.CreateDirectory(profilePath);
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profilePath);
        await Browser.EnsureCoreWebView2Async(environment);
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.Source = PlanUri;
    }

    private async void Browser_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        try
        {
            await ReadAsync(true);
        }
        catch
        {
            StatusText.Text = "请完成登录，然后点击“读取当前用量”。";
        }
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "正在读取用量…";
        try
        {
            await ReadAsync(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private async void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_hiding)
        {
            return;
        }

        _hiding = true;
        try
        {
            await InteractionMotion.HideAsync(DialogRoot, 4);
            Hide();
        }
        finally
        {
            InteractionMotion.Reset(DialogRoot);
            _hiding = false;
        }
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }

    public void Dispose()
    {
        _allowClose = true;
        Browser.Dispose();
        Close();
    }
}
