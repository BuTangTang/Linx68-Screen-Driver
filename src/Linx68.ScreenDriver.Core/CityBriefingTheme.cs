using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class CityBriefingTheme : IScreenTheme
{
    public string Id => "city-briefing";

    public string DisplayName => "城市晨报";

    public string Description => "时间、天气与今日出行提示";

    public string Details => "把当前城市、天气、体感、湿度和一句出行提示组织成一张紧凑晨报。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        WeatherSnapshot? weather = snapshot.Weather;
        Color primary = Colors.White;
        Color secondary = Color.FromRgb(156, 174, 190);
        Color surface = Color.FromRgb(15, 29, 39);
        Color stroke = Color.FromRgb(39, 64, 76);

        canvas.Gradient(Color.FromRgb(5, 18, 28), Color.FromRgb(8, 37, 43), new Point(0, 0), new Point(1, 1));
        canvas.Ellipse(new Rect(safe.Left, safe.Top + 8, 6, 6), canvas.AccentColor);
        canvas.Text("城市晨报", 10.5, secondary, new Point(safe.Left + 11, safe.Top + 4), FontWeights.SemiBold);
        canvas.Text(snapshot.Timestamp.ToString("HH:mm"), 11, primary, new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

        canvas.AlignedText(snapshot.Timestamp.ToString("HH:mm"), 38, primary,
            new Rect(safe.Left, safe.Top + 38, safe.Width, 53), FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText(snapshot.Timestamp.ToString("M月d日  dddd"), 12, secondary,
            new Rect(safe.Left, safe.Top + 91, safe.Width, 20), FontWeights.Medium, TextAlignment.Left);

        Rect weatherCard = new(safe.Left, safe.Top + 126, safe.Width, 104);
        canvas.RoundedGradientRect(weatherCard, 13, Color.FromRgb(18, 43, 54), Color.FromRgb(13, 34, 45), stroke);
        if (weather is { Available: true })
        {
            canvas.FittedText(weather.LocationName, 14, 10, primary, new Point(weatherCard.Left + 11, weatherCard.Top + 9), FontWeights.SemiBold, TextAlignment.Left, weatherCard.Width - 58, 21);
            canvas.Text(weather.ConditionText, 10, secondary, new Point(weatherCard.Left + 11, weatherCard.Top + 31), FontWeights.Medium);
            canvas.AlignedText($"{weather.TemperatureC:0}°", 34, canvas.AccentColor,
                new Rect(weatherCard.Left + 9, weatherCard.Top + 48, weatherCard.Width - 58, 43), FontWeights.SemiBold, TextAlignment.Left);
            DotMatrixWeatherClockTheme.DrawWeatherIcon(
                canvas,
                WeatherCondition.IconFromCode(weather.WeatherCode),
                new Rect(weatherCard.Right - 50, weatherCard.Top + 31, 42, 42),
                primary,
                canvas.AccentColor);
        }
        else
        {
            canvas.AlignedText("等待天气数据", 16, primary,
                new Rect(weatherCard.Left + 10, weatherCard.Top + 25, weatherCard.Width - 20, 28), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText("可先使用手动城市", 10, secondary,
                new Rect(weatherCard.Left + 10, weatherCard.Top + 57, weatherCard.Width - 20, 18), FontWeights.Normal, TextAlignment.Center);
        }

        Rect briefing = new(safe.Left, safe.Top + 238, safe.Width, 64);
        canvas.RoundedRect(briefing, 12, surface, stroke);
        canvas.Text(GetGreeting(snapshot.Timestamp.Hour), 10, canvas.AccentColor, new Point(briefing.Left + 11, briefing.Top + 9), FontWeights.Bold);
        canvas.WrappedText(
            GetAdvice(weather),
            12.5,
            primary,
            new Point(briefing.Left + 11, briefing.Top + 27),
            FontWeights.SemiBold,
            TextAlignment.Left,
            briefing.Width - 22,
            2,
            15);

        Rect apparent = new(safe.Left, safe.Bottom - 58, (safe.Width - 8) / 2, 48);
        Rect humidity = new(apparent.Right + 8, apparent.Top, apparent.Width, apparent.Height);
        DrawMetric(canvas, apparent, "体感", weather is { Available: true } ? $"{weather.ApparentTemperatureC:0}°" : "—", primary, secondary, surface, stroke);
        DrawMetric(canvas, humidity, "湿度", weather is { Available: true } ? $"{weather.RelativeHumidityPercent}%" : "—", primary, secondary, surface, stroke);
    }

    private static void DrawMetric(ScreenCanvas canvas, Rect rect, string label, string value, Color primary, Color secondary, Color surface, Color stroke)
    {
        canvas.RoundedRect(rect, 10, surface, stroke);
        canvas.CenteredText(value, 14, primary, new Rect(rect.Left, rect.Top + 6, rect.Width, 19), FontWeights.SemiBold);
        canvas.CenteredText(label, 9, secondary, new Rect(rect.Left, rect.Top + 27, rect.Width, 13), FontWeights.Medium);
    }

    private static string GetGreeting(int hour) => hour switch
    {
        < 5 => "夜深了",
        < 11 => "早上好",
        < 14 => "中午好",
        < 18 => "下午好",
        _ => "晚上好"
    };

    private static string GetAdvice(WeatherSnapshot? weather)
    {
        if (weather is not { Available: true }) return "先看看时间，也记得照顾好自己。";
        if (WeatherCondition.IconFromCode(weather.WeatherCode) is WeatherIconKind.Rain or WeatherIconKind.Thunderstorm) return "记得带伞，路上多留一点时间。";
        if (weather.TemperatureC >= 31) return "天气偏热，带水并注意防晒。";
        if (weather.TemperatureC <= 8) return "气温偏低，添件外套会更舒服。";
        if (weather.RelativeHumidityPercent >= 85) return "空气湿润，通勤注意地面湿滑。";
        return "天气不错，按自己的节奏出发。";
    }
}
