using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class AiQuotaTheme : IScreenTheme
{
    public string Id => "ai-quota";
    public string DisplayName => "AI 用量（测试版）";
    public string Description => "单平台剩余额度能量条";
    public string Details => "从下向上显示 AI 剩余额度，支持 API Key 与订阅制数据。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var quota = snapshot.AiQuota ?? AiQuotaSnapshot.Empty;
        var safe = canvas.SafeBounds;
        var percent = quota.Available ? quota.ClampedRemainingPercent : 0d;

        canvas.Fill(Color.FromRgb(6, 9, 13));

        canvas.Text(
            "AI 用量",
            8.5,
            Color.FromRgb(108, 121, 136),
            new Point(safe.Left, safe.Top + 7),
            FontWeights.SemiBold);

        var resetText = FormatReset(quota);
        if (!string.IsNullOrEmpty(resetText))
        {
            canvas.Text(
                resetText,
                7.5,
                Color.FromRgb(108, 121, 136),
                new Point(safe.Left, safe.Top + 8),
                FontWeights.Medium,
                TextAlignment.Right,
                safe.Width);
        }

        const double meterWidth = 48;
        const double meterHeight = 208;
        var meter = new Rect(
            safe.Left + (safe.Width - meterWidth) / 2,
            safe.Top + 38,
            meterWidth,
            meterHeight);

        canvas.RoundedRect(
            meter,
            meterWidth / 2,
            Color.FromRgb(18, 24, 31),
            Color.FromRgb(42, 51, 62),
            1);

        var inner = new Rect(meter.X + 6, meter.Y + 6, meter.Width - 12, meter.Height - 12);
        var fillHeight = inner.Height * percent / 100d;
        if (fillHeight > 0)
        {
            var fill = new Rect(
                inner.X,
                inner.Bottom - fillHeight,
                inner.Width,
                fillHeight);
            var fillRadius = Math.Min(inner.Width / 2, fill.Height / 2);
            canvas.RoundedRect(fill, fillRadius, canvas.AccentColor);

            if (fill.Height >= 10)
            {
                canvas.Ellipse(
                    new Rect(fill.X + 8, fill.Y + 4, fill.Width - 16, 3),
                    Mix(canvas.AccentColor, Colors.White, 0.48));
            }
        }

        var platformName = !string.IsNullOrWhiteSpace(quota.PlatformName)
            ? quota.PlatformName.Trim()
            : "AI";
        canvas.Text(
            platformName,
            15,
            Colors.White,
            new Point(safe.Left, meter.Bottom + 24),
            FontWeights.SemiBold,
            TextAlignment.Center,
            safe.Width,
            24);

        canvas.Text(
            quota.Available ? quota.RemainingDisplay : "--",
            quota.RemainingCount.HasValue ? 22 : 26,
            quota.Available ? canvas.AccentColor : Color.FromRgb(91, 103, 117),
            new Point(safe.Left, meter.Bottom + 51),
            FontWeights.Bold,
            TextAlignment.Center,
            safe.Width,
            38);

        canvas.Text(
            quota.Available ? FormatMetric(quota) : "等待额度数据",
            7.5,
            Color.FromRgb(91, 103, 117),
            new Point(safe.Left, safe.Bottom - 13),
            FontWeights.Medium,
            TextAlignment.Center,
            safe.Width,
            12);
    }

    private static string FormatMetric(AiQuotaSnapshot quota)
    {
        if (quota.AccessType == AiAccessType.Subscription)
        {
            return quota.ResetPeriod == AiResetPeriod.None
                ? "订阅额度"
                : $"订阅 · {ResetLabel(quota.ResetPeriod)}";
        }

        return quota.Balance?.Metric switch
        {
            AiQuotaMetric.Token => "密钥 · 令牌",
            AiQuotaMetric.Cost => "密钥 · 费用",
            AiQuotaMetric.Credit => "密钥 · 额度",
            AiQuotaMetric.Request => "密钥 · 请求",
            _ => "密钥 · 额度"
        };
    }

    private static string FormatReset(AiQuotaSnapshot quota)
    {
        if (!quota.Available || quota.ResetPeriod == AiResetPeriod.None)
        {
            return string.Empty;
        }

        return quota.ResetsAt is { } resetsAt
            ? resetsAt.ToLocalTime().ToString("MM月dd日 HH:mm")
            : ResetLabel(quota.ResetPeriod);
    }

    private static string ResetLabel(AiResetPeriod period) => period switch
    {
        AiResetPeriod.Hourly => "每小时",
        AiResetPeriod.Daily => "每日",
        AiResetPeriod.Weekly => "每周",
        AiResetPeriod.Monthly => "每月",
        AiResetPeriod.BillingCycle => "账期",
        AiResetPeriod.Custom => "自定义",
        _ => string.Empty
    };

    private static Color Mix(Color source, Color target, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromRgb(
            (byte)Math.Round(source.R + (target.R - source.R) * amount),
            (byte)Math.Round(source.G + (target.G - source.G) * amount),
            (byte)Math.Round(source.B + (target.B - source.B) * amount));
    }
}
