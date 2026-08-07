# ARCHITECTURE.md - 灵犀68屏幕驱动架构设计

> 本文档描述项目架构，让新 AI 阅读后可理解整个系统设计。

---

## 1. 整体架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                         用户界面层                               │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ MainWindow   │  │ MiMoToken    │  │ AccentColorDialog      │ │
│  │ (主窗口)     │  │ PlanWindow   │  │ (颜色选择器)            │ │
│  │              │  │ (登录窗口)    │  │                        │ │
│  └──────┬───────┘  └──────┬───────┘  └────────────────────────┘ │
│         │                 │                                      │
│  ┌──────▼─────────────────▼──────────────────────────────────┐ │
│  │              DevicePreviewControl                          │ │
│  │              (设备预览 154×440)                             │ │
│  └───────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                         业务逻辑层                               │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │                    MainWindow.xaml.cs                      │ │
│  │  - 定时刷新 (DispatcherTimer)                              │ │
│  │  - 设置管理 (ApplySettingsToControls / ApplyControlsTo)    │ │
│  │  - 主题切换                                                │ │
│  │  - 托盘管理                                                │ │
│  └───────────────────────────┬───────────────────────────────┘ │
├───────────────────────────────┼─────────────────────────────────┤
│                         核心渲染层                               │
│  ┌─────────────────────────────▼─────────────────────────────┐ │
│  │                    ScreenRenderer                          │ │
│  │  Render(theme, snapshot, options...) → RenderedFrame       │ │
│  └─────────────────────────────┬─────────────────────────────┘ │
│                                │                                │
│  ┌─────────────────────────────▼─────────────────────────────┐ │
│  │                    ScreenCanvas                            │ │
│  │  Fill / RoundedRect / Text / ProgressBar / Image           │ │
│  └───────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                         数据采集层                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐ │
│  │ WindowsSystem│  │ WindowsMusic │  │ XiaomiMiMoToken      │ │
│  │ SnapshotSource│  │ SnapshotSource│  │ PlanParser           │ │
│  │ (CPU/Mem/Net)│  │ (媒体会话)    │  │ (AI 用量解析)        │ │
│  └──────────────┘  └──────────────┘  └──────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                         传输层                                   │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │              HttpImageDeviceTransport                      │ │
│  │  PushAsync(endpoint, frame) → DevicePushResult            │ │
│  └───────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                         持久化层                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │                    JsonSettingsStore                        │ │
│  │  LoadAsync() / SaveAsync(AppSettings)                      │ │
│  └───────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. 模块之间关系

### 2.1 依赖关系

```
Linx68.ScreenDriver.App
    ├── 依赖 → Linx68.ScreenDriver.Core
    └── 依赖 → Microsoft.Web.WebView2

Linx68.ScreenDriver.Core
    └── 无外部依赖（仅 .NET 8 + WPF）
```

### 2.2 核心接口

| 接口 | 实现 | 职责 |
|------|------|------|
| `IScreenTheme` | 18 个内置主题 + ImageTheme | 主题绘制 |
| `ISystemSnapshotSource` | WindowsSystemSnapshotSource | 系统数据采集 |
| `IMusicSnapshotSource` | WindowsMusicSnapshotSource | 音乐数据采集 |
| `ILyricsSnapshotSource` | LrcLibLyricsSnapshotSource | 可选同步歌词采集与缓存 |
| `IAiQuotaSnapshotSource` | MiMoTokenPlanWindow | AI 用量采集 |
| `IDeviceTransport` | HttpImageDeviceTransport | 设备通信 |

### 2.3 为什么用接口

- **可测试性**：可 mock 数据源进行测试
- **可扩展性**：新增平台只需实现接口
- **可替换性**：传输层可从 HTTP 换成其他协议

---

## 3. 数据流

### 3.1 主渲染流程

```
用户操作/定时触发
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│              MainWindow.RefreshPreviewAsync()             │
│  1. _systemSource.ReadAsync() → SystemSnapshot           │
│  2. _musicSource.ReadAsync() → MusicSnapshot             │
│  3. ReadAiQuotaAsync() → AiQuotaSnapshot (可选)          │
│  4. 合并到 SystemSnapshot { Music, AiQuota }             │
└──────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│              ScreenRenderer.Render()                      │
│  1. 创建 DrawingVisual                                   │
│  2. theme.Draw(new ScreenCanvas(...), snapshot)          │
│  3. RenderTargetBitmap → JPEG 编码                       │
│  4. 检查 ≤ 512KB                                         │
│  5. 返回 RenderedFrame                                   │
└──────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│              显示 + 推送                                  │
│  1. DevicePreviewControl.FrameSource = frame             │
│  2. _transport.PushAsync(endpoint, frame) (如果启用)     │
└──────────────────────────────────────────────────────────┘
```

### 3.2 设置变更流程

```
用户修改控件
       │
       ▼
ScheduleAutoCommit() → 450ms 防抖
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│              CommitAndPushAsync()                         │
│  1. ApplyControlsToSettings()                            │
│  2. _settingsStore.SaveAsync(_settings)                  │
│  3. RefreshPreviewAsync()                                │
│  4. PushLatestAsync()                                    │
└──────────────────────────────────────────────────────────┘
```

### 3.3 AI 用量流程

```
用户点击"登录小米控制台"
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│              MiMoTokenPlanWindow                          │
│  1. 打开 WebView2                                        │
│  2. 导航到 https://platform.xiaomimimo.com/console/plan  │
│  3. 用户手动登录                                          │
│  4. 调用 /api/v1/tokenPlan/detail + /usage               │
│  5. XiaomiMiMoTokenPlanParser.Parse() → AiQuotaSnapshot  │
└──────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────────────────────┐
│              AiQuotaTheme.Draw()                          │
│  1. 读取 snapshot.AiQuota                                │
│  2. 绘制能量条、平台名称、剩余额度                         │
└──────────────────────────────────────────────────────────┘
```

---

## 4. UI 流程

### 4.1 窗口生命周期

```
App.xaml
  │
  ▼
MainWindow 构造
  ├── InitializeComponent()
  ├── BuiltInThemes.Create() → 15 个主题
  ├── BuildThemeList() → 动态生成 RadioButton
  ├── CreateTrayIcon() → NotifyIcon
  └── 初始化 Timer、事件处理器
       │
       ▼
MainWindow_OnLoaded
  ├── _settingsStore.LoadAsync() → 加载设置
  ├── ApplySettingsToControls() → 同步到 UI
  ├── _timer.Start() → 启动定时刷新
  ├── RefreshPreviewAsync() → 首次渲染
  └── StartMinimized → HideToTray()
       │
       ▼
正常运行
  ├── Timer_OnTick → 定时刷新
  ├── 用户操作 → ScheduleAutoCommit()
  └── 托盘操作 → 显示/隐藏/退出
```

### 4.2 托盘行为

```
最小化到托盘
  ├── MinimizeToTrayCheckBox.IsChecked == true
  ├── HideToTray()
  └── _trayIcon.Visible = true

关闭到托盘
  ├── CloseToTrayCheckBox.IsChecked == true
  ├── MainWindow_OnClosing → e.Cancel = true
  └── HideToTray()

托盘双击
  └── RestoreFromTray()
      ├── ShowInTaskbar = true
      ├── Show()
      ├── WindowState = _restoreWindowState
      └── InteractionMotion.Reveal()
```

---

## 5. 状态管理方式

### 5.1 应用状态

- **AppSettings**：所有配置项，通过 JsonSettingsStore 持久化
- **SystemSnapshot**：实时系统数据，每次刷新重新采集
- **RenderedFrame**：最新渲染帧，用于预览和推送

### 5.2 UI 状态

- **_loaded**：窗口是否已加载完成
- **_busy**：是否正在执行异步操作（防重入）
- **_autoCommitRunning / _autoCommitPending**：防抖队列状态
- **_suppressThemeRefresh**：临时抑制主题刷新

### 5.3 状态同步

```
控件 → ApplyControlsToSettings() → _settings
                                       │
                                       ▼
_settingsStore.SaveAsync() → settings.json
                                       │
                                       ▼
RefreshPreviewAsync() → 最新帧 → DevicePreview + Push
```

---

## 6. 服务依赖关系

```
MainWindow
    ├── ISystemSnapshotSource (WindowsSystemSnapshotSource)
    ├── IMusicSnapshotSource (WindowsMusicSnapshotSource)
    ├── HttpImageDeviceTransport
    ├── JsonSettingsStore
    ├── ImageTheme
    ├── FontFolderCatalog
    ├── MiMoTokenPlanWindow (延迟创建)
    └── ScreenRenderer (可重建)
```

**为什么这样设计**：
- 所有依赖在构造函数中直接实例化（简单，个人项目够用）
- MiMoTokenPlanWindow 延迟创建（需要用户主动登录）
- ScreenRenderer 可重建（SafeArea 变化时需要更新 Profile）

---

## 7. 关键设计决策

### 7.1 为什么用 142×428 像素

- 物理设备屏幕分辨率
- 512KB JPEG 限制是设备内存约束

### 7.2 为什么用 JPEG 而非 PNG

- 设备只接受 JPEG
- JPEG 文件更小，适合网络传输

### 7.3 为什么 Core 层独立

- 理论上可在其他宿主（如控制台）复用
- 渲染逻辑与 UI 逻辑分离
- 但实际上只用于 WPF 应用

### 7.4 为什么用 WebView2 读取 AI 用量

- 小米控制台需要登录认证
- WebView2 可复用浏览器会话
- 通过 DevTools Protocol 读取数据

### 7.5 为什么 450ms 防抖

- 用户快速输入时避免频繁推送
- 保证最终值被提交
- 设置即生效的用户体验

---

## 8. 扩展方式

### 8.1 新增主题

```csharp
// 1. 实现 IScreenTheme 接口
public class MyTheme : IScreenTheme
{
    public string Id => "my-theme";
    public string DisplayName => "我的主题";
    public string Description => "...";
    public string Details => "...";
    
    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        // 使用 canvas 绘制
        canvas.Fill(Colors.Black);
        canvas.Text("Hello", 20, Colors.White, new Point(10, 10));
    }
}

// 2. 在 BuiltInThemes.Create() 中注册
new MyTheme()
```

### 8.2 新增数据源

```csharp
// 实现接口
public class MyDataSource : ISystemSnapshotSource
{
    public ValueTask<SystemSnapshot> ReadAsync(CancellationToken ct = default)
    {
        // 采集数据
        return ValueTask.FromResult(new SystemSnapshot(...));
    }
}
```

### 8.3 新增传输方式

```csharp
// 实现 IDeviceTransport
public class MyTransport : IDeviceTransport
{
    public Task<DevicePushResult> PushAsync(Uri endpoint, RenderedFrame frame, CancellationToken ct = default)
    {
        // 传输逻辑
    }
}
```

---

## 9. 性能相关设计

### 9.1 渲染性能

- 使用 WPF DrawingContext 直接绘制（高效）
- RenderTargetBitmap 渲染到内存（不涉及屏幕）
- JPEG 编码在后台线程（避免 UI 卡顿）

### 9.2 网络性能

- HttpClient 超时 8 秒
- 异步传输，不阻塞 UI
- 无重试机制（简单，够用）

### 9.3 内存管理

- BitmapImage 使用 CacheOption.OnLoad（加载后释放流）
- Freeze() 冻结位图（跨线程安全）
- MemoryStream 及时 Dispose

### 9.4 已知性能瓶颈

- **网络数据采集**：每次刷新都遍历所有 NetworkInterface
- **CPU 读取**：使用 Win32 API GetSystemTimes，需要两次采样计算差值
- **无增量更新**：每次刷新都重新渲染整个画面

---

*最后更新：2026-07-27*
