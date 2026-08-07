using System.IO;
using System.Globalization;
using System.Text.Json;

namespace Linx68.ScreenDriver.Core;

public static class XiaomiMiMoTokenPlanParser
{
    public static AiQuotaSnapshot Parse(string detailJson, string usageJson)
    {
        using var detailDocument = JsonDocument.Parse(detailJson);
        using var usageDocument = JsonDocument.Parse(usageJson);
        var detail = UnwrapData(detailDocument.RootElement, "套餐详情");
        var usage = UnwrapData(usageDocument.RootElement, "用量");

        if (!usage.TryGetProperty("usage", out var usageContainer) ||
            !usageContainer.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("小米控制台没有返回 Credits 用量。");
        }

        JsonElement? selected = null;
        decimal selectedLimit = 0;
        foreach (var item in items.EnumerateArray())
        {
            var name = ReadString(item, "name");
            var limit = ReadDecimal(item, "limit");
            if (limit <= 0 || name.Contains("compensation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (selected is null || limit > selectedLimit)
            {
                selected = item;
                selectedLimit = limit;
            }
        }

        if (selected is null)
        {
            throw new InvalidDataException("当前套餐没有可读取的 Credits 配额。");
        }

        var used = ReadDecimal(selected.Value, "used");
        var remaining = Math.Clamp(selectedLimit - used, 0, selectedLimit);
        var planName = ReadString(detail, "planName");
        var platformName = string.IsNullOrWhiteSpace(planName)
            ? "Xiaomi MiMo"
            : $"Xiaomi MiMo · {planName}";

        return AiQuotaSnapshot.ForApiKey(
            platformName,
            new AiQuotaBalance(AiQuotaMetric.Credit, used, selectedLimit, remaining, "Credits"),
            AiResetPeriod.Monthly,
            ReadDate(detail, "currentPeriodEnd"));
    }

    private static JsonElement UnwrapData(JsonElement root, string label)
    {
        if (root.TryGetProperty("code", out var codeElement) &&
            codeElement.TryGetInt32(out var code) &&
            code is not (0 or 200))
        {
            var message = ReadString(root, "message");
            if (string.IsNullOrWhiteSpace(message))
            {
                message = ReadString(root, "msg");
            }

            throw new InvalidDataException(
                string.IsNullOrWhiteSpace(message) ? $"{label}读取失败（{code}）。" : message);
        }

        return root.TryGetProperty("data", out var data) ? data : root;
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number)
            ? number
            : 0;
    }

    private static DateTimeOffset? ReadDate(JsonElement element, string name)
    {
        var text = ReadString(element, name);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }
}