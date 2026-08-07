namespace Linx68.ScreenDriver.Core;

public enum AiQuotaSourceKind
{
    XiaomiMiMoTokenPlanChina,
    OpenAICodex
}

public sealed class AiQuotaSettings
{
    public AiQuotaSourceKind SourceKind { get; set; } = AiQuotaSourceKind.OpenAICodex;

    public string DisplayName { get; set; } = "Codex";
}
