<p align="center">
  <img src="docs/images/keyboard-screen-studio-hero.png" alt="灵犀68屏幕驱动" width="100%" />
</p>

# 灵犀68屏幕驱动

灵犀68屏幕驱动是一款由 butang 维护的 Windows 键盘竖屏控制工具。GitHub 仓库与工程文件使用英文名称 Linx68 Screen Driver。它不是 Windows 内核设备驱动，而是用于设计、预览和推送键盘屏幕画面的桌面应用。程序会将系统状态、时间、天气、媒体信息和资讯数据渲染为适配 142×428 屏幕的 JPEG 画面，再通过键盘图像 API 在局域网内推送到设备。

项目由个人维护、免费开源，不包含广告、第一方遥测或项目作者运营的云服务。

## 功能概览

| 分类 | 当前功能 |
| --- | --- |
| 监控 | CPU、内存、下载与上传速度，多种系统状态布局 |
| 时间 | 极简时钟、霓虹时钟、翻页时钟、图片时间 |
| 资讯 | 五日天气、股票行情（Beta）、AI 用量（Beta） |
| 音乐 | Windows 媒体会话、动态黑胶、动态磁带、同步歌词、封面与播放进度 |
| 点阵 | 点阵时钟、点阵时钟天气 |

除此之外，应用还支持：

- 19 个适配竖屏小尺寸显示的内置主题
- 自定义界面字体、主题强调色、内容安全区与图片位置
- 根据媒体播放状态自动切换音乐主题和待机主题
- 可选通过 LRCLIB 获取逐行同步歌词，并支持 ±3 秒时间偏移
- 定时刷新、自动推送、系统托盘、后台驻留与开机自启动
- Windows 深浅色模式自适应托盘图标
- 142×428 离屏渲染、最高质量基线 JPEG 与 512KB 大小限制
- 首次启动设备连接引导，以及实验功能的一次性风险提示

## 运行要求

- Windows 10 19041 或更高版本
- 支持图像 API 的 Linx68 键盘
- 键盘与电脑处于同一局域网
- 从源码构建时需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 快速开始

1. 在键盘菜单中开启“图像 API”。
2. 扫码取得键盘的 IPv4 地址。
3. 启动灵犀68屏幕驱动，按照首次启动引导填写四段 IP。
4. 选择显示主题，后续设置会自动保存并推送。

应用只需要填写设备 IP，并会自动组成上传地址：

```text
http://设备IP/image/upload
```

设备通信使用未加密 HTTP，仅应在可信的本地网络中使用。

## 从源码运行

```powershell
git clone https://github.com/BuTangTang/Linx68-Screen-Driver.git
cd Linx68-Screen-Driver
dotnet restore Linx68.ScreenDriver.sln
dotnet run --project src/Linx68.ScreenDriver.App/Linx68.ScreenDriver.App.csproj
```

## 构建与验证

```powershell
dotnet build Linx68.ScreenDriver.sln -c Release
dotnet run --project tests/Linx68.ScreenDriver.SmokeTests/Linx68.ScreenDriver.SmokeTests.csproj -c Release
dotnet run --project tests/Linx68.ScreenDriver.UiSmokeTests/Linx68.ScreenDriver.UiSmokeTests.csproj -c Release
dotnet list Linx68.ScreenDriver.sln package --vulnerable --include-transitive
```

生成干净的 Windows x64 自包含绿色版：

```powershell
powershell -ExecutionPolicy Bypass -File tools/Publish-Portable.ps1
```

打包脚本只从源码构建，并排除设置、WebView2 登录资料、用户字体、PDB 和 XML 调试文件。输出位于被 Git 忽略的 `artifacts` 目录，脚本不会自动上传或发布任何内容。

## 隐私与实验功能

应用不包含第一方分析或遥测。只有启用对应功能时，才会直接联系相关服务：

- 天气查询发送至 Open-Meteo；自动定位时，坐标还会发送至 BigDataCloud 以解析城市名。
- 股票代码发送至 Yahoo Finance。该功能使用非官方实验性数据接口，可能延迟、不准确或失效，不构成投资建议。
- MiMo 用量通过本地 WebView2 读取用户已登录的小米控制台会话。它不是小米官方集成，平台调整可能导致功能失效。
- 在线歌词默认关闭；启用后会将当前曲名、歌手和时长发送至 LRCLIB，并缓存匹配结果以减少请求。
- JPEG 画面只从电脑发送到用户配置的局域网设备地址。

本项目目前为个人非商业项目，使用 Open-Meteo 免费非商业接口。MIT 许可证允许他人修改和商业使用源码，但商业再使用者必须自行确认并取得第三方数据服务所要求的许可。

不要提交或分享本机使用中的 `Data`、`settings.json`、`MiMoWebView2`、`dist` 或 `artifacts` 目录。详细说明见 [PRIVACY.md](PRIVACY.md) 和 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 字体与视觉资产

项目只捆绑采用 SIL Open Font License 1.1 的 Doto 字体。用户放入 `Fonts` 文件夹的字体仅供本机渲染使用，请勿在没有再分发许可的情况下加入仓库或发布包。

应用图标、托盘图标与项目宣传视觉采用独立资产许可，不包含在源码 MIT 许可中，详见 [ASSET_LICENSE.md](ASSET_LICENSE.md)。

## 项目结构

```text
src/Linx68.ScreenDriver.App             WPF 宿主、页面 ViewModel 与交互界面
src/Linx68.ScreenDriver.Application     刷新、推送、设置和数据源端口
src/Linx68.ScreenDriver.Core            主题渲染、领域模型与纯解析逻辑
src/Linx68.ScreenDriver.Infrastructure  Windows、HTTP、JSON 等端口实现
tests/Linx68.ScreenDriver.SmokeTests    核心与集成烟雾测试
tests/Linx68.ScreenDriver.UiSmokeTests  WPF 页面状态与几何烟雾测试
tools                                  主题预览和绿色版构建工具
docs/images                            README 与项目宣传图片
showcase                               主题展示图片
```

分层边界、刷新流程与扩展方式见 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 许可证与归属

灵犀68屏幕驱动基于 ZCat95 的 Keyboard Screen Studio 修改，源代码采用 [MIT License](LICENSE)。原始作品版权归 ZCat95，修改部分版权归 butang。第三方组件、字体和数据服务继续遵循各自条款。

灵犀68屏幕驱动是独立的非官方项目，与 Linx68 品牌、设备制造商、Microsoft、BigDataCloud、Open-Meteo、Yahoo、Xiaomi、Nothing 或 Apple 不存在隶属、授权、认可或赞助关系。所有产品名称和商标归其各自权利人所有。

欢迎通过 Issue 反馈问题或提出主题建议。提交代码前，请先运行项目中的两组自动化测试。
