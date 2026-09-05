using System.Globalization;
using System.Text.Json;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Validator;

/// <summary>Deterministic validator for the JSON Schema keywords used by the V4 repository contracts.</summary>
public static class JsonSchemaSubsetValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(string dataPath, string schemaPath, string relativeFile)
    {
        var issues = new List<ValidationIssue>();
        try
        {
            using var data = JsonDocument.Parse(File.ReadAllText(dataPath));
            using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
            ValidateNode(data.RootElement, schema.RootElement, "$", relativeFile, issues);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            issues.Add(new("ERROR", relativeFile, "$", ex.Message));
        }
        return issues;
    }

    private static void ValidateNode(JsonElement value, JsonElement schema, string path, string file, List<ValidationIssue> issues)
    {
        if (schema.TryGetProperty("type", out var type) && !MatchesType(value, type.GetString()))
        {
            issues.Add(new("ERROR", file, path, $"Expected {type.GetString()}, got {value.ValueKind}."));
            return;
        }

        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(x => x.GetRawText() == value.GetRawText()))
            issues.Add(new("ERROR", file, path, "Value is not in the allowed enum."));

        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray().Select(x => x.GetString()!))
                    if (!value.TryGetProperty(name, out _)) issues.Add(new("ERROR", file, path + "." + name, "Required property is missing."));

            if (schema.TryGetProperty("properties", out var properties))
                foreach (var property in value.EnumerateObject())
                    if (properties.TryGetProperty(property.Name, out var propertySchema))
                        ValidateNode(property.Value, propertySchema, path + "." + property.Name, file, issues);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("minItems", out var minItems) && value.GetArrayLength() < minItems.GetInt32())
                issues.Add(new("ERROR", file, path, $"Array must contain at least {minItems.GetInt32()} item(s)."));
            if (schema.TryGetProperty("items", out var itemSchema))
            {
                var index = 0;
                foreach (var item in value.EnumerateArray()) ValidateNode(item, itemSchema, $"{path}[{index++}]", file, issues);
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (schema.TryGetProperty("minLength", out var minLength) && text.Length < minLength.GetInt32())
                issues.Add(new("ERROR", file, path, "String is shorter than minLength."));
            if (schema.TryGetProperty("format", out var format) && !MatchesFormat(text, format.GetString()))
                issues.Add(new("ERROR", file, path, $"Value is not a valid {format.GetString()}."));
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            var number = value.GetDecimal();
            if (schema.TryGetProperty("minimum", out var minimum) && number < minimum.GetDecimal()) issues.Add(new("ERROR", file, path, "Value is below minimum."));
            if (schema.TryGetProperty("maximum", out var maximum) && number > maximum.GetDecimal()) issues.Add(new("ERROR", file, path, "Value is above maximum."));
        }
    }

    private static bool MatchesType(JsonElement value, string? type) => type switch
    {
        "array" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => true
    };

    private static bool MatchesFormat(string value, string? format) => format switch
    {
        "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "date-time" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
        "time" => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
        "uri" => Uri.TryCreate(value, UriKind.Absolute, out _),
        _ => true
    };
}
