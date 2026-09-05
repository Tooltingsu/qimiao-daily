using QimiaoDaily.Core;
using QimiaoDaily.Services;
namespace QimiaoDaily.Core.Tests;
public sealed class ReminderEngineTests
{
 [Fact] public void Build_EmitsShanghaiTomorrowEnd_OnlyForConfirmedItems(){var now=new DateTimeOffset(2026,8,14,16,0,0,TimeSpan.Zero);var confirmed=Item(now.AddHours(-1),now.AddHours(32));confirmed.AddEvidence(new EvidenceRecord("official","notice","https://example.invalid","evidence","test",now));confirmed.Confirm("tester","verified",now);var pending=Item(now.AddHours(-1),now.AddHours(32));var reminders=new ReminderEngine().Build([confirmed,pending],now);Assert.Contains(reminders,x=>x.TimelineItemId==confirmed.Id&&x.Kind==ReminderKind.EndsTomorrow);Assert.DoesNotContain(reminders,x=>x.TimelineItemId==pending.Id);}
 [Fact] public void Build_UsesShanghaiDate_ForNewVideo(){var now=new DateTimeOffset(2026,8,14,16,30,0,TimeSpan.Zero);var video=Item(now, null,"VIDEO");video.AddEvidence(new EvidenceRecord("official","video","https://example.invalid/video","evidence","test",now));video.Confirm("tester","verified",now);Assert.Contains(new ReminderEngine().Build([video],now),x=>x.Kind==ReminderKind.NewVideo);}
 private static TimelineItem Item(DateTimeOffset? start,DateTimeOffset? end,string type="EVENT")=>new("GENSHIN",type,"test",VerificationStatus.VerifiedOfficial,start?.ToString("O"),"Asia/Shanghai",start,TimePrecision.Exact,DateTimeOffset.UtcNow,end);
}
