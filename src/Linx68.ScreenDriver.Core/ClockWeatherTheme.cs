using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class ClockWeatherTheme : IScreenTheme
{
    public string Id => "clock-weather";
    public string DisplayName => "时钟天气";
    public string Description => "大号时间与当前天气摘要";
    public string Details => "以时间为主体，集中显示城市、温度、天气和当天高低温。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        WeatherSnapshot? weather = snapshot.Weather;
        Color primary = Colors.White;
        Color secondary = Color.FromRgb(154, 165, 177);
        Color separator = Color.FromRgb(35, 42, 51);
        Color surface = Color.FromRgb(15, 20, 26);

        canvas.Fill(Color.FromRgb(5, 7, 10));
        canvas.Ellipse(new Rect(safe.Left, safe.Top + 8, 6, 6), Color.FromRgb(108, 121, 136));
        canvas.Text("时钟天气", 10, secondary, new Point(safe.Left + 11, safe.Top + 4), FontWeights.SemiBold);
        canvas.Text(snapshot.Timestamp.ToString("HH:mm"), 11, primary, new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

        canvas.AlignedText(snapshot.Timestamp.ToString("HH:mm"), 43, primary,
            new Rect(safe.Left, safe.Top + 38, safe.Width, 61), FontWeights.SemiBold, TextAlignment.Center);
        canvas.AlignedText(snapshot.Timestamp.ToString("dddd  MM / dd"), 14, secondary,
            new Rect(safe.Left, safe.Top + 106, safe.Width, 23), FontWeights.Medium, TextAlignment.Center);
        canvas.Line(new Point(safe.Left, safe.Top + 143), new Point(safe.Right, safe.Top + 143), separator);

        if (weather is not { Available: true })
        {
            canvas.AlignedText("天气待更新", 22, primary,
                new Rect(safe.Left, safe.Top + 181, safe.Width, 34), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText("请在显示方案中完成天气设置", 10, secondary,
                new Rect(safe.Left, safe.Top + 221, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
        }
        else
        {
            canvas.AlignedText(weather.LocationName, 13, primary,
                new Rect(safe.Left, safe.Top + 164, safe.Width - 48, 22), FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText($"{weather.TemperatureC:0}°", 38, canvas.AccentColor,
                new Rect(safe.Left, safe.Top + 187, safe.Width - 54, 53), FontWeights.SemiBold, TextAlignment.Left);
            DotMatrixWeatherClockTheme.DrawWeatherIcon(canvas, WeatherCondition.IconFromCode(weather.WeatherCode),
                new Rect(safe.Right - 46, safe.Top + 178, 44, 44), Colors.White, canvas.AccentColor);
            canvas.AlignedText(weather.ConditionText, 12, secondary,
                new Rect(safe.Left, safe.Top + 245, safe.Width, 20), FontWeights.Medium, TextAlignment.Left);

            var metricCard = new Rect(safe.Left, safe.Top + 278, safe.Width, 48);
            canvas.RoundedRect(metricCard, 10, surface, separator);
            canvas.AlignedText($"{weather.ApparentTemperatureC:0}°", 13, primary,
                new Rect(metricCard.Left + 11, metricCard.Top + 6, 45, 17), FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText("体感", 9, secondary,
                new Rect(metricCard.Left + 11, metricCard.Top + 25, 45, 14), FontWeights.Medium, TextAlignment.Left);
            canvas.AlignedText($"{weather.RelativeHumidityPercent}%", 13, primary,
                new Rect(metricCard.Right - 56, metricCard.Top + 6, 45, 17), FontWeights.SemiBold, TextAlignment.Right);
            canvas.AlignedText("湿度", 9, secondary,
                new Rect(metricCard.Right - 56, metricCard.Top + 25, 45, 14), FontWeights.Medium, TextAlignment.Right);
        }

        canvas.Line(new Point(safe.Left, safe.Bottom - 38), new Point(safe.Right, safe.Bottom - 38), separator);
        canvas.Text("秒表", 9, secondary, new Point(safe.Left, safe.Bottom - 27), FontWeights.SemiBold);
        canvas.Text(snapshot.Timestamp.ToString("HH : mm : ss"), 12, canvas.AccentColor,
            new Point(safe.Left, safe.Bottom - 29), FontWeights.SemiBold, TextAlignment.Right, safe.Width);
    }
}
