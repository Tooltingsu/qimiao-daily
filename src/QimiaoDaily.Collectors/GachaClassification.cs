namespace QimiaoDaily.Collectors;

public sealed record GachaClassificationResult(string PoolKind, string PoolPhase, string? GroupKey, bool HasConflict);

public static class GachaClassification
{
    public static GachaClassificationResult Classify(string gameCode, string? title, string? body)
    {
        var text = string.Concat(title?.Trim() ?? string.Empty, "\n", body?.Trim() ?? string.Empty);
        var kindSignals = new List<string>();
        if (text.Contains("\u96c6\u5f55\u7948\u613f", StringComparison.Ordinal)) kindSignals.Add("CHRONICLED");
        if (text.Contains("\u5149\u9525\u6d3b\u52a8\u8dc3\u8fc1", StringComparison.Ordinal)) kindSignals.Add("LIGHT_CONE");
        if (gameCode.Equals("STARRAIL", StringComparison.OrdinalIgnoreCase) && text.Contains("\u6d3b\u52a8\u8dc3\u8fc1", StringComparison.Ordinal) && !text.Contains("\u5149\u9525", StringComparison.Ordinal)) kindSignals.Add("CHARACTER");
        if (text.Contains("\u89d2\u8272\u6d3b\u52a8\u7948\u613f", StringComparison.Ordinal) || text.Contains("\u89d2\u8272\u7948\u613f", StringComparison.Ordinal)) kindSignals.Add("CHARACTER");
        if (text.Contains("\u6b66\u5668\u6d3b\u52a8\u7948\u613f", StringComparison.Ordinal) || text.Contains("\u6b66\u5668\u7948\u613f", StringComparison.Ordinal)) kindSignals.Add("SPECIAL");

        var kinds = kindSignals.Distinct(StringComparer.Ordinal).ToArray();
        var kindConflict = kinds.Length > 1;
        var poolKind = kindConflict ? "UNKNOWN" : kinds.FirstOrDefault() ?? "UNKNOWN";
        var hasFirst = text.Contains("\u4e0a\u534a", StringComparison.Ordinal);
        var hasSecond = text.Contains("\u4e0b\u534a", StringComparison.Ordinal);
        var phaseConflict = hasFirst && hasSecond;
        var poolPhase = phaseConflict ? "UNKNOWN" : hasFirst ? "FIRST_HALF" : hasSecond ? "SECOND_HALF" : "UNKNOWN";
        return new GachaClassificationResult(poolKind, poolPhase, null, kindConflict || phaseConflict);
    }
}
