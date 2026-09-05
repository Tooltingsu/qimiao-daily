using System.Text.Json;

namespace QimiaoDaily.Services;

public enum QimiaoImportChangeKind { New, Modified, Duplicate, Conflict }

public sealed record QimiaoImportStoredRecord(string RecordType, string RecordId, string NaturalKey, string PayloadJson);
public sealed record QimiaoImportPreviewEntry(string SelectionKey, string RecordType, string RecordId, string NaturalKey, QimiaoImportChangeKind ChangeKind, string PayloadJson);
public sealed record QimiaoImportPreview(IReadOnlyList<QimiaoImportPreviewEntry> Entries);

/// <summary>Application-layer persistence adapter. Preview never calls UpsertAsync.</summary>
public interface IQimiaoImportStore
{
    Task<IReadOnlyList<QimiaoImportStoredRecord>> ReadAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(QimiaoImportStoredRecord record, CancellationToken cancellationToken = default);
    async Task UpsertManyAsync(IReadOnlyList<QimiaoImportStoredRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records) await UpsertAsync(record, cancellationToken);
    }
}

public sealed class QimiaoImportService(IQimiaoImportStore store)
{
    private static readonly string[] Categories = ["events", "banners", "versions", "birthdays", "anniversaries", "calendarEvents"];

    public async Task<QimiaoImportPreview> PreviewAsync(string json, CancellationToken cancellationToken = default)
    {
        var incoming = Parse(json);
        var existing = await store.ReadAllAsync(cancellationToken);
        var entries = incoming.Select(record => ToPreview(record, existing)).ToList();
        return new QimiaoImportPreview(entries);
    }

    public async Task<int> ConfirmAsync(QimiaoImportPreview preview, IEnumerable<string> selectedKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(selectedKeys);
        var selected = selectedKeys.ToHashSet(StringComparer.Ordinal);
        var applicable = preview.Entries.Where(x => selected.Contains(x.SelectionKey) && x.ChangeKind != QimiaoImportChangeKind.Duplicate).ToList();
        await store.UpsertManyAsync(applicable.Select(entry => new QimiaoImportStoredRecord(entry.RecordType, entry.RecordId, entry.NaturalKey, entry.PayloadJson)).ToList(), cancellationToken);
        return applicable.Count;
    }

    private static QimiaoImportPreviewEntry ToPreview(QimiaoImportStoredRecord incoming, IReadOnlyList<QimiaoImportStoredRecord> existing)
    {
        var byId = existing.SingleOrDefault(x => x.RecordType == incoming.RecordType && x.RecordId == incoming.RecordId);
        var kind = byId is not null
            ? (string.Equals(byId.PayloadJson, incoming.PayloadJson, StringComparison.Ordinal) ? QimiaoImportChangeKind.Duplicate : QimiaoImportChangeKind.Modified)
            : existing.Any(x => x.RecordType == incoming.RecordType && x.NaturalKey == incoming.NaturalKey) ? QimiaoImportChangeKind.Conflict
            : QimiaoImportChangeKind.New;
        return new QimiaoImportPreviewEntry($"{incoming.RecordType}:{incoming.RecordId}", incoming.RecordType, incoming.RecordId, incoming.NaturalKey, kind, incoming.PayloadJson);
    }

    private static IReadOnlyList<QimiaoImportStoredRecord> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != 1)
            throw new ArgumentException("qimiao-import.json must declare schemaVersion 1.", nameof(json));
        var records = new List<QimiaoImportStoredRecord>();
        foreach (var category in Categories)
        {
            if (!root.TryGetProperty(category, out var values)) continue;
            if (values.ValueKind != JsonValueKind.Array) throw new ArgumentException($"'{category}' must be an array.", nameof(json));
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException($"Each '{category}' entry must be an object.", nameof(json));
                var type = category switch
                {
                    "events" => "event",
                    "banners" => "banner",
                    "versions" => "version",
                    "birthdays" => "birthday",
                    "anniversaries" => "anniversary",
                    "calendarEvents" => "calendarEvent",
                    _ => throw new ArgumentOutOfRangeException(nameof(category))
                };
                var naturalKey = NaturalKey(type, value);
                var id = GetString(value, "id") ?? $"natural:{type}:{naturalKey}";
                records.Add(new QimiaoImportStoredRecord(type, id, naturalKey, value.GetRawText()));
            }
        }
        return records;
    }

    private static string NaturalKey(string type, JsonElement item) => type switch
    {
        "event" or "banner" => Join(GetRequired(item, "game"), GetRequired(item, "name"), GetRequired(item, "startAt")),
        "version" => Join(GetRequired(item, "game"), GetRequired(item, "versionNumber")),
        "birthday" => Join(GetRequired(item, "game"), GetRequired(item, "character")),
        "anniversary" => Join(GetRequired(item, "title"), GetRequired(item, "startedOn")),
        "calendarEvent" => Join(GetRequired(item, "date"), GetRequired(item, "title")),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string Join(params string[] values) => string.Join("|", values.Select(x => x.Trim()));
    private static string GetRequired(JsonElement item, string name) => GetString(item, name) ?? throw new ArgumentException($"Import entry requires '{name}'.");
    private static string? GetString(JsonElement item, string name)
    {
        foreach (var property in item.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind switch { JsonValueKind.String => property.Value.GetString(), JsonValueKind.Number => property.Value.GetRawText(), _ => null };
        return null;
    }
}
