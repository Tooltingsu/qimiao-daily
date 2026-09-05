namespace QimiaoDaily.Desktop.ViewModels;
public sealed record CalendarDayCard(DateTime Date,string DayNumber,string Details,string KindText="",string FranchiseText="");
public sealed class CalendarEventCard{public Guid Id{get;init;}public DateTime Date{get;set;}public string Kind{get;set;}=string.Empty;public string Title{get;set;}=string.Empty;public string Detail{get;set;}=string.Empty;}
