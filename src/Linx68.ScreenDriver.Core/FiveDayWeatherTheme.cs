using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class FiveDayWeatherTheme : IScreenTheme
{
    public string Id => "weather-five-day";
    public string DisplayName => "五日天气";
    public string Description => "未来五日天气与高低温";
    public string Details => "显示未来五天的日期、天气状态和最高最低温度。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var safe = canvas.SafeBounds;
        var weather = snapshot.Weather;
        var secondary = Color.FromRgb(150, 160, 172);
        var card = Color.FromRgb(15, 20, 27);
        var stroke = Color.FromRgb(29, 36, 45);

        canvas.Fill(Color.FromRgb(5, 9, 14));
        canvas.AlignedText("五日天气", 11, secondary,
            new Rect(safe.Left, safe.Top + 7, safe.Width, 18), FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText(snapshot.Timestamp.ToString("HH:mm"), 13, Colors.White,
            new Rect(safe.Left, safe.Top + 5, safe.Width, 20), FontWeights.SemiBold, TextAlignment.Right);

        var days = weather?.DailyForecast?.Take(5).ToArray() ?? [];
        if (weather is not { Available: true } || days.Length == 0)
        {
            canvas.AlignedText("等待天气数据", 15, Colors.White,
                new Rect(safe.Left, safe.Top + 145, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText("请在屏幕配置中填写城市", 10, secondary,
                new Rect(safe.Left, safe.Top + 178, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        canvas.AlignedText(weather.LocationName, 16, Colors.White,
            new Rect(safe.Left, safe.Top + 35, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Left);

        const double rowHeight = 48;
        const double gap = 6;
        var top = safe.Top + 68;
        for (var index = 0; index < days.Length; index++)
        {
            var day = days[index];
            var row = new Rect(safe.Left, top + index * (rowHeight + gap), safe.Width, rowHeight);
            canvas.RoundedRect(row, 10, card, stroke);
            var dayLabel = index == 0
                ? "今天"
                : day.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd", CultureInfo.GetCultureInfo("zh-CN"));
            var content = new Rect(row.Left + 11, row.Top + 6, row.Width - 22, row.Height - 12);
            var textWidth = content.Width - 42;
            canvas.AlignedText(dayLabel, 10.5,
                index == 0 ? canvas.AccentColor : secondary,
                new Rect(content.Left, content.Top, textWidth, 16),
                FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText($"{day.MaximumTemperatureC:0}°", 15,
                Colors.White,
                new Rect(content.Left, content.Top + 17, textWidth, content.Height - 17),
                FontWeights.SemiBold, TextAlignment.Left);
            DotMatrixWeatherClockTheme.DrawWeatherIcon(
                canvas,
                WeatherCondition.IconFromCode(day.WeatherCode),
                new Rect(content.Right - 34, content.Top, 34, content.Height),
                Colors.White,
                canvas.AccentColor);
        }

        var updateText = weather.IsStale ? "上次天气数据" : $"更新 {weather.UpdatedAt:HH:mm}";
        canvas.AlignedText(updateText, 9, secondary,
            new Rect(safe.Left, safe.Bottom - 18, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
    }
}