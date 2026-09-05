using QimiaoDaily.Collectors;

using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
client.DefaultRequestHeaders.UserAgent.ParseAdd("QimiaoDaily-QA/1.0");
var names = args.Length == 0
    ? (await new Honkai3OfficialCharacterProvider(client).CollectAsync()).Select(x => x.Character).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
    : args;
var rows = await new MoegirlBirthdayProvider(client).CollectAsync(names);
Console.WriteLine($"Rows={rows.Count}; Known={rows.Count(x => x.Month is >= 1 and <= 12 && x.Day is >= 1 and <= 31)}; Unknown={rows.Count(x => x.Month is < 1 or > 12 || x.Day is < 1 or > 31)}");
foreach (var row in rows)
    Console.WriteLine($"{row.Character}\t{row.Month}/{row.Day}\t{row.EvidenceExcerpt}\t{row.SourceUrl}");
