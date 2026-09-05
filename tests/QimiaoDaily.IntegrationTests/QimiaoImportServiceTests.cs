using QimiaoDaily.Services;

namespace QimiaoDaily.IntegrationTests;

public sealed class QimiaoImportServiceTests
{
    [Fact]
    public async Task PreviewDoesNotWriteUntilSelectedChangesAreConfirmed()
    {
        var store = new InMemoryImportStore();
        var service = new QimiaoImportService(store);
        const string json = """
            { "schemaVersion": 1, "events": [{ "id": "e-1", "game": "GENSHIN", "name": "Test", "startAt": "2026-08-21T04:00:00+08:00" }], "banners": [], "versions": [], "birthdays": [], "anniversaries": [] }
            """;

        var preview = await service.PreviewAsync(json);

        Assert.Single(preview.Entries);
        Assert.Equal(QimiaoImportChangeKind.New, preview.Entries[0].ChangeKind);
        Assert.Empty(store.Records);

        await service.ConfirmAsync(preview, [preview.Entries[0].SelectionKey]);

        Assert.Single(store.Records);
        var secondPreview = await service.PreviewAsync(json);
        Assert.Equal(QimiaoImportChangeKind.Duplicate, Assert.Single(secondPreview.Entries).ChangeKind);
    }

    [Fact]
    public async Task PreviewClassifiesModifiedDuplicateAndNaturalKeyConflict()
    {
        var store = new InMemoryImportStore([
            new QimiaoImportStoredRecord("event", "e-1", "GENSHIN|Test|2026-08-21", "{\"name\":\"Old\"}"),
            new QimiaoImportStoredRecord("birthday", "b-old", "GENSHIN|Amber", "{\"day\":10}")
        ]);
        var service = new QimiaoImportService(store);
        const string json = """
            { "schemaVersion": 1,
              "events": [
                { "id": "e-1", "game": "GENSHIN", "name": "Test", "startAt": "2026-08-21" },
                { "id": "e-2", "game": "GENSHIN", "name": "Same", "startAt": "2026-08-22" }
              ],
              "banners": [], "versions": [],
              "birthdays": [ { "id": "b-new", "game": "GENSHIN", "character": "Amber", "month": 8, "day": 10 } ],
              "anniversaries": [] }
            """;

        var preview = await service.PreviewAsync(json);

        Assert.Contains(preview.Entries, x => x.ChangeKind == QimiaoImportChangeKind.Modified && x.RecordId == "e-1");
        Assert.Contains(preview.Entries, x => x.ChangeKind == QimiaoImportChangeKind.Conflict && x.RecordId == "b-new");
    }

    private sealed class InMemoryImportStore(IEnumerable<QimiaoImportStoredRecord>? records = null) : IQimiaoImportStore
    {
        public List<QimiaoImportStoredRecord> Records { get; } = records?.ToList() ?? [];
        public Task<IReadOnlyList<QimiaoImportStoredRecord>> ReadAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QimiaoImportStoredRecord>>(Records);
        public Task UpsertAsync(QimiaoImportStoredRecord record, CancellationToken cancellationToken = default)
        {
            Records.RemoveAll(x => x.RecordType == record.RecordType && x.RecordId == record.RecordId);
            Records.Add(record);
            return Task.CompletedTask;
        }
    }
}
