namespace QimiaoDaily.Collectors;

public enum VideoPreviewKind { Video, PreviewNotice, PreviewLive, Ignore }
public sealed record VideoPreviewClassification(VideoPreviewKind Kind, string VideoType, string Reason);

public static class VideoPreviewClassifier
{
    public static VideoPreviewClassification Classify(string title, string? body = null)
    {
        var text = $"{title}\n{body}";
        if (string.IsNullOrWhiteSpace(title)) return new(VideoPreviewKind.Ignore, "", "missing title");
        if (text.Contains("\u56de\u987e")) return new(VideoPreviewKind.Video, "RECAP", "recap is never a preview live");
        if (text.Contains("\u524d\u77bb"))
        {
            if (text.Contains("\u9884\u544a")) return new(VideoPreviewKind.PreviewNotice, "PREVIEW_NOTICE", "explicit preview notice");
            if (text.Contains("\u4eca\u65e5") && text.Contains("\u76f4\u64ad") || text.Contains("\u5f00\u542f\u76f4\u64ad")) return new(VideoPreviewKind.PreviewLive, "PREVIEW_LIVE", "explicit live-state evidence");
            return new(VideoPreviewKind.Ignore, "", "preview has no notice or live-state evidence");
        }
        if (text.Contains("\u89d2\u8272PV")) return new(VideoPreviewKind.Video, "CHARACTER_PV", "character PV");
        if (text.Contains("\u89d2\u8272\u9884\u544a")) return new(VideoPreviewKind.Video, "CHARACTER_TRAILER", "character trailer");
        if (text.Contains("\u89d2\u8272\u6f14\u793a")) return new(VideoPreviewKind.Video, "CHARACTER_DEMO", "character demo");
        if (text.Contains("\u52a8\u753b\u77ed\u7247")) return new(VideoPreviewKind.Video, "ANIMATION", "animation");
        if (text.Contains("\u7248\u672cPV")) return new(VideoPreviewKind.Video, "VERSION_PV", "version PV");
        if (text.Contains("EP")) return new(VideoPreviewKind.Video, "EP", "official EP");
        return new(VideoPreviewKind.Video, "OFFICIAL_PROMO", "video came from a verified official channel");
    }
}
