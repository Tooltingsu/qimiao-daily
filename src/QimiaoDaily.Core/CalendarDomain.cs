using System.Globalization;

namespace QimiaoDaily.Core;

public sealed record BirthdayRecord(string Character,string Franchise,int Month,int Day,string Source,string SourceUrl,string Evidence,VerificationStatus VerificationStatus,DateTimeOffset VerifiedAt,bool Enabled);
public sealed record AnniversaryRecord(string Title,DateOnly StartedOn,bool Enabled);
public sealed record CalendarOccurrence(string Kind,string Title,DateOnly Date,string? Detail=null);

public static class ChineseCalendarEngine
{
 private static readonly ChineseLunisolarCalendar Lunar=new();
 private static readonly int[] SolarTermMinutes=[0,21208,42467,63836,85337,107014,128867,150921,173149,195551,218072,240693,263343,285989,308563,331033,353350,375494,397447,419210,440795,462224,483532,504758];
 private static readonly string[] SolarTerms=["\u5c0f\u5bd2","\u5927\u5bd2","\u7acb\u6625","\u96e8\u6c34","\u60ca\u86f0","\u6625\u5206","\u6e05\u660e","\u8c37\u96e8","\u7acb\u590f","\u5c0f\u6ee1","\u8292\u79cd","\u590f\u81f3","\u5c0f\u6691","\u5927\u6691","\u7acb\u79cb","\u5904\u6691","\u767d\u9732","\u79cb\u5206","\u5bd2\u9732","\u971c\u964d","\u7acb\u51ac","\u5c0f\u96ea","\u5927\u96ea","\u51ac\u81f3"];
 public static IReadOnlyList<CalendarOccurrence> Occurrences(DateOnly date,IEnumerable<BirthdayRecord> birthdays,IEnumerable<AnniversaryRecord> anniversaries){var list=new List<CalendarOccurrence>();foreach(var b in birthdays.Where(x=>x.Enabled&&x.Month==date.Month&&x.Day==date.Day))list.Add(new("BIRTHDAY",b.Character,date,b.Franchise));foreach(var a in anniversaries.Where(x=>x.Enabled&&x.StartedOn.Month==date.Month&&x.StartedOn.Day==date.Day))list.Add(new("ANNIVERSARY",a.Title,date,$"{date.Year-a.StartedOn.Year}\u5468\u5e74"));if(date.Month==1&&date.Day==1)list.Add(new("FESTIVAL","\u5143\u65e6",date));var lunar=LunarDate(date);foreach(var f in Festivals(lunar.Month,lunar.Day,Lunar.GetDaysInMonth(lunar.Year,lunar.Month)))list.Add(new("FESTIVAL",f,date));var term=SolarTerm(date);if(term is "\u6e05\u660e")list.Add(new("FESTIVAL","\u6e05\u660e",date));if(term is not null)list.Add(new("SOLAR_TERM",term,date));return list;}
 private static (int Year,int Month,int Day)LunarDate(DateOnly date){var d=date.ToDateTime(TimeOnly.MinValue);return(Lunar.GetYear(d),Lunar.GetMonth(d),Lunar.GetDayOfMonth(d));}
 private static IEnumerable<string> Festivals(int m,int d,int days){if(m==1&&d==1)yield return "\u6625\u8282";if(m==1&&d==15)yield return "\u5143\u5bb5";if(m==5&&d==5)yield return "\u7aef\u5348";if(m==7&&d==7)yield return "\u4e03\u5915";if(m==8&&d==15)yield return "\u4e2d\u79cb";if(m==9&&d==9)yield return "\u91cd\u9633";if(m==12&&d==days)yield return "\u9664\u5915";}
 public static DateTime SolarTermInstantUtc(int year,int index)=>new DateTime(1900,1,6,2,5,0,DateTimeKind.Utc).AddMilliseconds(31556925974.7*(year-1900)+SolarTermMinutes[index]*60000d);
 public static string? SolarTerm(DateOnly date){for(var i=0;i<24;i++){var t=TimeZoneInfo.ConvertTimeFromUtc(SolarTermInstantUtc(date.Year,i),TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));if(t.Month==date.Month&&t.Day==date.Day)return SolarTerms[i];}return null;}
}
