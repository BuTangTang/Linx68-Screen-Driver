# 灵犀68屏幕驱动架构

本文描述当前 `Linx68.ScreenDriver` 的运行时边界与扩展方式。目标是让主题渲染、数据采集、设置保存和 WPF 界面能分别演进，并保持应用在无网络、无媒体会话或外部服务失败时仍可使用。

## 分层与依赖方向

```text
Linx68.ScreenDriver.App             WPF 宿主、页面交互、预览、托盘和 WebView2 登录
        ↓
Linx68.ScreenDriver.Application     用例服务与端口：刷新、推送、设置、数据源接口
        ↓
Linx68.ScreenDriver.Core            领域模型、主题元数据、渲染器和纯解析逻辑
        ↑
Linx68.ScreenDriver.Infrastructure  Windows、HTTP、文件系统和 JSON 的端口实现
```

- `Core` 不引用其他项目；它定义 `ThemeDefinition`、`IScreenTheme`、`SystemSnapshot`、`AppSettings` 和 `ScreenRenderer`。
- `Application` 只引用 `Core`；它定义数据源、设置和传输端口，并编排 `DashboardRefreshService`、`DashboardSnapshotBuilder` 与 `DisplayPushService`。
- `Infrastructure` 实现 Application 端口，例如 Windows 媒体会话、Open-Meteo、Yahoo Finance、HTTP 图像上传和原子 JSON 设置保存。
- `App` 是唯一的 WPF 宿主。`App.xaml.cs` 使用 Generic Host 注册依赖；`MainWindow` 保留 WPF 专属行为，不直接决定数据采集和设备地址规则。

## 运行时组合

`App.xaml.cs` 在单实例检查完成后加载设置、应用初始外观，并注册以下主要服务：

| 端口/服务 | 默认实现 | 职责 |
| --- | --- | --- |
| `ISettingsStore` | `JsonSettingsStore` | 版本化加载、无效文件备份、原子保存 |
| `IMusicSnapshotSource` | `WindowsMusicSnapshotSource` | Windows 媒体会话 |
| `ISystemSnapshotSource` | `WindowsSystemSnapshotSource` | CPU、内存、网络 |
| `ILyricsSnapshotSource` | `LrcLibLyricsSnapshotSource` | 可选同步歌词和缓存 |
| `IWeatherSnapshotSource` | `OpenMeteoWeatherSnapshotSource` | 城市/坐标天气 |
| `IStockSnapshotSource` | `YahooStockSnapshotSource` | 可选行情和缓存 |
| `IAutomaticWeatherLocationProvider` | `WindowsWeatherLocationProvider` | Windows 定位与城市反查 |
| `IDashboardRefreshService` | `DashboardRefreshService` | 主题决策和按需快照刷新 |
| `IDisplayPushService` | `DisplayPushService` | IPv4 地址归一化和设备推送 |

MiMo 用量登录窗口仍属于 App：它需要用户交互和 WebView2 已登录会话。刷新服务只在 AI 主题需要数据时通过回调请求该窗口，避免让登录实现渗入 Core 或 Infrastructure。

## 刷新与推送流程

```text
定时器 / 保存设置 / 用户操作
             │
             ▼
MainWindow.RefreshPreviewAsync
             │
             ▼
DashboardRefreshService
  ├─ 读取 Windows 媒体会话
  ├─ 根据 ThemeDefinition 与媒体状态决定有效主题
  ├─ 仅在元数据声明需要时读取 AI、天气、歌词、股票
  └─ DashboardSnapshotBuilder 合成 SystemSnapshot
             │
             ▼
ScreenRenderer.Render → RenderedFrame (142 × 428 baseline JPEG)
             │
             ├─ DevicePreviewControl 显示预览
             └─ DisplayPushService → IDeviceTransport → /image/upload
```

`ThemeDefinition` 是按需读取的唯一依据：`ThemeDataRequirements` 描述数据依赖，`ThemeSettingsSections` 描述可显示的配置区，避免窗口再维护主题 ID 的硬编码分支。

## UI 状态

`ShellViewModel` 维护当前页和标题，并拥有各页 ViewModel：

- `ScreenViewModel`：主题卡、分类筛选、选中状态和响应式卡片宽度。
- `AppearanceViewModel`：应用外观、强调色校验、屏幕字体和图片时间位置。
- `AutomationViewModel`：自动推送、刷新间隔和媒体主题切换。
- `SettingsViewModel`：设备 IPv4 分段输入、内容安全区和托盘/启动行为。

`MainWindow` 目前仍是 WPF 组合控制器：它处理窗口生命周期、托盘、动画、文件/颜色选择器、首次引导、MiMo 登录和页面相关控件的可见性；这些行为不能由无 WPF 依赖的 Application 服务替代。主题的上下文数据卡仍会在后续切片继续从窗口中抽出。

## 设置与失败策略

- `AppSettings.SettingsVersion` 当前为 `2`。保存会先写同目录临时文件、刷新到磁盘，再原子替换目标文件。
- 无效 JSON 会保留为 `settings.json.invalid-<timestamp>.json`，应用回退到当前默认设置。
- 天气自动定位不可用时，`WeatherSettingsResolver` 回退到保存的城市并将回退状态交给 UI。
- 无效设备地址会由 `DisplayPushService` 拒绝，不会调用网络传输。
- 网络源各自负责超时与缓存；主题渲染使用可用的降级快照，而非阻塞整个应用。

## 验证与发布

```powershell
dotnet build Linx68.ScreenDriver.sln -c Release
dotnet run --project tests/Linx68.ScreenDriver.SmokeTests/Linx68.ScreenDriver.SmokeTests.csproj -c Release
dotnet run --project tests/Linx68.ScreenDriver.UiSmokeTests/Linx68.ScreenDriver.UiSmokeTests.csproj -c Release
```

- 核心烟雾测试覆盖主题 JPEG、解析器、缓存、设置恢复、刷新/推送应用服务和 Windows 数据源。
- UI 烟雾测试覆盖页面 ViewModel、关键几何、绑定式主题库、浅/深色切换与可选窗口截图。
- `tools/Publish-Portable.ps1` 只在 `artifacts` 下生成自包含绿色包；GitHub tag 工作流会先运行两组烟雾测试，再创建发布归档。

最后更新：2026-08-07
