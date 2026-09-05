using System.Text.Json;
using System.Text.Json.Serialization;

namespace QimiaoDaily.V4.Core;

public sealed class V4Repository(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    public string PathFor(params string[] segments) => Path.Combine([Root, .. segments]);

    public T Read<T>(params string[] segments)
    {
        var path = PathFor(segments);
        if (!File.Exists(path)) throw new FileNotFoundException($"Required V4 data file is missing: {path}", path);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"V4 data file is empty: {path}");
    }

    public T ReadOr<T>(T fallback, params string[] segments)
    {
        var path = PathFor(segments);
        return File.Exists(path) ? Read<T>(segments) : fallback;
    }

    public void Write<T>(T value, params string[] segments)
    {
        var path = PathFor(segments);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
        File.Move(temp, path, true);
    }

    public void WriteText(string value, params string[] segments)
    {
        var path = PathFor(segments);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, value);
        File.Move(temp, path, true);
    }
}
