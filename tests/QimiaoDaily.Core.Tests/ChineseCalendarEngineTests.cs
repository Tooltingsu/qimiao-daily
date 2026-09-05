using QimiaoDaily.Core;
namespace QimiaoDaily.Core.Tests;
public sealed class ChineseCalendarEngineTests
{
 [Fact] public void Occurrences_IncludesAllEnabledBirthdaysAndAnniversary(){var date=new DateOnly(2026,8,14);var birthdays=new[]{new BirthdayRecord("A","Genshin",8,14,"official","https://example.invalid","e",VerificationStatus.VerifiedOfficial,DateTimeOffset.UtcNow,true),new BirthdayRecord("B","NTE",8,14,"source","https://example.invalid","e",VerificationStatus.Unverified,DateTimeOffset.UtcNow,true)};var entries=ChineseCalendarEngine.Occurrences(date,birthdays,[new AnniversaryRecord("Test",new DateOnly(2020,8,14),true)]);Assert.Contains(entries,x=>x.Kind=="BIRTHDAY"&&x.Title=="A");Assert.Contains(entries,x=>x.Kind=="BIRTHDAY"&&x.Title=="B");Assert.Contains(entries,x=>x.Kind=="ANNIVERSARY"&&x.Detail=="6\u5468\u5e74");}
 [Fact] public void SolarTerm_UsesCalculatedDates(){Assert.Equal("2026-08-08",ChineseCalendarEngine.SolarTermInstantUtc(2026,14).AddHours(8).ToString("yyyy-MM-dd"));Assert.Equal("\u7acb\u79cb",ChineseCalendarEngine.SolarTerm(new DateOnly(2026,8,8)));}
 [Fact] public void Occurrences_IncludesNewYearAndQingmingAsTraditionalFestivals(){var newYear=ChineseCalendarEngine.Occurrences(new DateOnly(2026,1,1),[],[]);Assert.Contains(newYear,x=>x.Kind=="FESTIVAL"&&x.Title=="\u5143\u65e6");var qingming=ChineseCalendarEngine.Occurrences(new DateOnly(2026,4,5),[],[]);Assert.Contains(qingming,x=>x.Kind=="FESTIVAL"&&x.Title=="\u6e05\u660e");}
}
