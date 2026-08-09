灵犀68屏幕驱动绿色版

1. 双击 Linx68ScreenDriver.exe 启动，无需安装 .NET。
2. 首次启动时程序会自动创建 Data 文件夹并保存本机配置。
3. 将获得合法使用权的 TTF 或 OTF 字体放入 Fonts 文件夹后，程序会自动识别。
4. 程序通过 HTTP POST 将 JPEG 直接推送到键盘设备。
5. 在“AI 用量”主题中选择 OpenAI Codex 后，可在程序内安装/更新 Codex、登录并检查状态。
6. 换电脑时仅导出和导入 Codex 的 config.toml；新电脑仍需要单独登录 Codex。

隐私提醒

- Data\settings.json 包含设备地址、城市和本地图片路径。
- Data\MiMoWebView2 包含小米登录浏览器资料，不要分享或上传 Data 文件夹。
- Codex 的 auth.json 和系统凭据属于登录令牌，程序不会导出、导入或读取它们；不要手动分享。
- 关闭程序后删除 Data\MiMoWebView2 可清除本地保存的小米登录状态。
- 设备传输为未加密 HTTP，只应在可信本地网络中使用。

第三方服务

- 天气数据由 Open-Meteo 提供，采用 CC BY 4.0：
  https://open-meteo.com/en/license
- Xiaomi MiMo 用量功能为非官方实验性集成，可能延迟、不准确或失效。
- 本项目与 Linx68 品牌、设备制造商及上述平台不存在隶属、授权、认可或赞助关系。

许可证

项目源码采用 MIT License。应用图标与托盘图标采用单独的视觉资产许可。完整项目许可、隐私说明及第三方许可文件随程序提供。
