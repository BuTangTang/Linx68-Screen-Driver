using System.Windows;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows.Media.Animation;

namespace Linx68.ScreenDriver.App;

public partial class FeatureNoticeWindow : Window
{
    public FeatureNoticeWindow(
        string title,
        string subtitle,
        string body,
        IReadOnlyList<string> details)
    {
        InitializeComponent();
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        BodyText.Text = body;
        DetailsList.ItemsSource = details;
    }

    public static FeatureNoticeWindow CreateStockNotice() => new(
        "股票数据说明",
        "使用前，请了解实验性数据来源",
        "股票主题使用 Yahoo Finance 的公开网页数据接口，不属于官方稳定 API。",
        [
            "行情可能延迟、不准确或随时不可用",
            "数据仅用于桌面信息展示，不构成投资建议",
            "请勿依赖该功能进行交易或其他重要决策"
        ]);

    public static FeatureNoticeWindow CreateMiMoNotice() => new(
        "MiMo 用量说明",
        "使用前，请了解非官方集成方式",
        "AI 用量主题读取你在 Xiaomi MiMo 控制台中的登录会话与套餐数据，不属于小米官方集成。",
        [
            "登录状态仅保存在本机 WebView2 数据目录",
            "平台页面或接口变化可能导致功能失效",
            "请勿在公共电脑登录，并自行留意账号与平台规则"
        ]);

    public static FeatureNoticeWindow CreateCodexNotice() => new(
        "Codex 接入说明",
        "在每台电脑上独立安装并授权",
        "本应用只协助安装、登录与迁移 Codex 偏好配置，不会读取、复制或导出你的登录令牌。",
        [
            "可导出和导入的是 config.toml，不包含账号、API Key 或 auth.json",
            "新电脑导入配置后，仍需使用自己的浏览器完成 Codex 登录",
            "只从受信任的电脑导入配置；导入前会自动备份本机配置"
        ]);

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            NoticeCard.Opacity = 1;
            NoticeTranslate.Y = 0;
            AcknowledgeButton.Focus();
            return;
        }

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        NoticeCard.Opacity = 1;
        NoticeCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);

        NoticeTranslate.Y = 0;
        NoticeTranslate.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            },
            HandoffBehavior.SnapshotAndReplace);
        AcknowledgeButton.Focus();
    }

    private void AcknowledgeButton_OnClick(object sender, RoutedEventArgs e) =>
        DialogResult = true;

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = true;
        }
    }
}
