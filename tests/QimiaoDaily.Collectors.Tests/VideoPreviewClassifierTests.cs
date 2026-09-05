using QimiaoDaily.Collectors;
namespace QimiaoDaily.Collectors.Tests;
public sealed class VideoPreviewClassifierTests
{
 [Theory]
 [InlineData("4.4版本前瞻特别节目预告",VideoPreviewKind.PreviewNotice)]
 [InlineData("4.4版本前瞻特别节目今日18:00开启直播",VideoPreviewKind.PreviewLive)]
 [InlineData("4.4版本前瞻特别节目回顾",VideoPreviewKind.Video)]
 [InlineData("角色PV丨惑心谲影",VideoPreviewKind.Video)]
 public void Classify_SeparatesPreviewStatesAndVideo(string title,VideoPreviewKind expected)=>Assert.Equal(expected,VideoPreviewClassifier.Classify(title).Kind);
}
