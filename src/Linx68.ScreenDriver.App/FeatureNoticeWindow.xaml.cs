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

    public static FeatureNoticeWindow CreateCodexNotice() => new(
        "Codex 额度说明",
        "显示当前 ChatGPT Codex 额度窗口",
        "Codex 额度主题通过本机 Codex App Server 读取当前剩余比例、额度窗口和下次重置时间。",
        [
            "需要在本机完成 Codex 的 ChatGPT 登录，API Key 登录不提供订阅额度窗口",
            "本应用只读取额度结果，不会读取、复制或导出 auth.json、账号令牌或系统凭据",
            "额度窗口和重置时间由 Codex 返回；未登录或数据不可用时会明确显示未获取"
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
