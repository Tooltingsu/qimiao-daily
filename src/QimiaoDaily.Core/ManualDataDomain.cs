namespace QimiaoDaily.Core;

/// <summary>Describes how a V3 record entered the formal data set.</summary>
public enum DataOrigin
{
    Manual,
    Imported,
    Calculated,
    AutoCollected,
    LegacyAuto
}

public sealed class Banner
{
    private readonly List<BannerCharacter> _characters = [];

    public Banner(string game, string name, string type, DateTimeOffset startAt, DateTimeOffset endAt, DataOrigin origin)
    {
        Game = game;
        Name = name;
        Type = type;
        StartAt = startAt;
        EndAt = endAt;
        Origin = origin;
    }

    public string Game { get; }
    public string Name { get; }
    public string Type { get; }
    public DateTimeOffset StartAt { get; }
    public DateTimeOffset EndAt { get; }
    public DataOrigin Origin { get; }
    public IReadOnlyList<BannerCharacter> Characters => _characters;

    public void AddCharacter(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Character name is required.", nameof(name));
        _characters.Add(new BannerCharacter(name.Trim(), _characters.Count));
    }
}

public sealed record BannerCharacter(string Name, int SortOrder);

public sealed record ManualEventInput(string Game, string Name, DateTimeOffset StartAt, DateTimeOffset EndAt, string Notes);
public sealed record BannerInput(string Game, string Name, string Type, string? CustomType, DateTimeOffset StartAt, DateTimeOffset EndAt, string Notes, IReadOnlyList<string> Characters);
public sealed record GameVersionInput(string Game, string VersionNumber, string VersionName, DateTimeOffset StartAt, DateTimeOffset EndAt, string Notes);
public sealed record AnniversaryInput(string Title, DateOnly StartedOn, string Notes);
