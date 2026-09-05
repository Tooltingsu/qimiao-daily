using System.IO;

namespace QimiaoDaily.Desktop.ViewModels;

public sealed record ArtworkCard(Guid Id,string Title,string Author,string Platform,string SourceUrl,string ThumbnailUrl,string PublishedText,bool SelectedForReport,string CharacterName,string FranchiseName,string Category,string Tags,int? Width,int? Height,string? PerceptualHash, bool IsMarked = false)
{
 public bool IsCachedLocally=>File.Exists(ThumbnailUrl);
 public string CacheStatusText=>IsCachedLocally?"预览图已下载到本地，可复制或单击预览。":"预览图暂未缓存；请打开原站查看。";
 public string ReportSelectionText=>SelectedForReport?"\u53d6\u6d88\u9009\u62e9":"\u9009\u5165\u4eca\u65e5\u65e5\u62a5";
 public string MetadataText=>string.Join(" · ",new[]{CharacterName,FranchiseName,Category}.Where(x=>!string.IsNullOrWhiteSpace(x)));
 public string SizeText=>Width is > 0&&Height is > 0?$"{Width}×{Height}":"尺寸未知";
 public string FingerprintText=>string.IsNullOrWhiteSpace(PerceptualHash)?"感知哈希未生成":"感知哈希已生成";
 public string MarkText=>IsMarked?"取消标记":"标记";
 public string MetadataDisplay=>string.Join(" \u00b7 ",new[]{CharacterName,FranchiseName,Category}.Where(x=>!string.IsNullOrWhiteSpace(x)));
 public string TagsDisplay=>string.IsNullOrWhiteSpace(Tags)?"":"\u6807\u7b7e\uff1a"+Tags;
}
