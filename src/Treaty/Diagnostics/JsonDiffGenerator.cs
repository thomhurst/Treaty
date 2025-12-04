using System.Text;
using System.Text.Json;

namespace Treaty.Diagnostics;

/// <summary>
/// Generates visual diffs between JSON documents.
/// </summary>
public static class JsonDiffGenerator
{
    /// <summary>
    /// Compares two JSON strings and returns the differences.
    /// </summary>
    /// <param name="expected">The expected JSON (from schema/sample).</param>
    /// <param name="actual">The actual JSON (from response).</param>
    /// <returns>A list of differences found.</returns>
    public static IReadOnlyList<JsonDiff> Compare(string? expected, string? actual)
    {
        var diffs = new List<JsonDiff>();

        if (string.IsNullOrWhiteSpace(expected) && string.IsNullOrWhiteSpace(actual))
            return diffs;

        if (string.IsNullOrWhiteSpace(expected))
        {
            diffs.Add(JsonDiff.Added("$", actual ?? "null"));
            return diffs;
        }

        if (string.IsNullOrWhiteSpace(actual))
        {
            diffs.Add(JsonDiff.Removed("$", expected));
            return diffs;
        }

        try
        {
            using var expectedDoc = JsonDocument.Parse(expected);
            using var actualDoc = JsonDocument.Parse(actual);

            CompareElements(expectedDoc.RootElement, actualDoc.RootElement, "$", diffs);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as simple string comparison
            if (expected != actual)
            {
                diffs.Add(JsonDiff.Changed("$", expected, actual));
            }
        }

        return diffs;
    }

    private static void CompareElements(JsonElement expected, JsonElement actual, string path, List<JsonDiff> diffs)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            diffs.Add(JsonDiff.TypeMismatch(path, GetTypeName(expected.ValueKind), GetTypeName(actual.ValueKind)));
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(expected, actual, path, diffs);
                break;

            case JsonValueKind.Array:
                CompareArrays(expected, actual, path, diffs);
                break;

            case JsonValueKind.String:
                var expectedStr = expected.GetString();
                var actualStr = actual.GetString();
                if (expectedStr != actualStr)
                {
                    diffs.Add(JsonDiff.Changed(path, $"\"{expectedStr}\"", $"\"{actualStr}\""));
                }
                break;

            case JsonValueKind.Number:
                if (expected.GetRawText() != actual.GetRawText())
                {
                    diffs.Add(JsonDiff.Changed(path, expected.GetRawText(), actual.GetRawText()));
                }
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                if (expected.GetBoolean() != actual.GetBoolean())
                {
                    diffs.Add(JsonDiff.Changed(path, expected.GetBoolean().ToString().ToLower(), actual.GetBoolean().ToString().ToLower()));
                }
                break;

            case JsonValueKind.Null:
                // Both null, no diff
                break;
        }
    }

    private static void CompareObjects(JsonElement expected, JsonElement actual, string path, List<JsonDiff> diffs)
    {
        var expectedProps = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        // Check for missing properties
        foreach (var prop in expectedProps)
        {
            var propPath = $"{path}.{prop.Key}";
            if (!actualProps.TryGetValue(prop.Key, out var actualValue))
            {
                diffs.Add(JsonDiff.Removed(propPath, FormatValue(prop.Value)));
            }
            else
            {
                CompareElements(prop.Value, actualValue, propPath, diffs);
            }
        }

        // Check for extra properties
        foreach (var prop in actualProps)
        {
            if (!expectedProps.ContainsKey(prop.Key))
            {
                var propPath = $"{path}.{prop.Key}";
                diffs.Add(JsonDiff.Added(propPath, FormatValue(prop.Value)));
            }
        }
    }

    private static void CompareArrays(JsonElement expected, JsonElement actual, string path, List<JsonDiff> diffs)
    {
        var expectedItems = expected.EnumerateArray().ToList();
        var actualItems = actual.EnumerateArray().ToList();

        var maxLength = Math.Max(expectedItems.Count, actualItems.Count);

        for (int i = 0; i < maxLength; i++)
        {
            var itemPath = $"{path}[{i}]";

            if (i >= expectedItems.Count)
            {
                diffs.Add(JsonDiff.Added(itemPath, FormatValue(actualItems[i])));
            }
            else if (i >= actualItems.Count)
            {
                diffs.Add(JsonDiff.Removed(itemPath, FormatValue(expectedItems[i])));
            }
            else
            {
                CompareElements(expectedItems[i], actualItems[i], itemPath, diffs);
            }
        }
    }

    private static string FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.Null => "null",
            JsonValueKind.Object => element.GetRawText(),
            JsonValueKind.Array => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    private static string GetTypeName(JsonValueKind kind)
    {
        return kind switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null => "null",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Formats a list of diffs as a unified diff string.
    /// </summary>
    /// <param name="diffs">The diffs to format.</param>
    /// <returns>A formatted diff string.</returns>
    public static string FormatDiffs(IReadOnlyList<JsonDiff> diffs)
    {
        if (diffs.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("--- Expected");
        sb.AppendLine("+++ Actual");
        sb.AppendLine();

        foreach (var diff in diffs)
        {
            var prefix = diff.Type switch
            {
                DiffType.Added => "+",
                DiffType.Removed => "-",
                DiffType.Changed => "~",
                DiffType.TypeMismatch => "!",
                _ => " "
            };

            sb.AppendLine($"{prefix} {diff.Path}:");
            if (diff.Expected != null)
                sb.AppendLine($"    Expected: {diff.Expected}");
            if (diff.Actual != null)
                sb.AppendLine($"    Actual:   {diff.Actual}");
            if (diff.Description != null)
                sb.AppendLine($"    Note: {diff.Description}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats two JSON strings side by side for comparison.
    /// </summary>
    /// <param name="expected">The expected JSON.</param>
    /// <param name="actual">The actual JSON.</param>
    /// <param name="indent">Indentation for nested objects.</param>
    /// <returns>A side-by-side comparison string.</returns>
    public static string FormatSideBySide(string? expected, string? actual, int indent = 2)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Expected:                          Actual:");
        sb.AppendLine(new string('-', 70));

        var expectedLines = FormatJsonPretty(expected).Split('\n');
        var actualLines = FormatJsonPretty(actual).Split('\n');

        var maxLines = Math.Max(expectedLines.Length, actualLines.Length);
        var colWidth = 35;

        for (int i = 0; i < maxLines; i++)
        {
            var left = i < expectedLines.Length ? expectedLines[i].TrimEnd() : "";
            var right = i < actualLines.Length ? actualLines[i].TrimEnd() : "";

            if (left.Length > colWidth - 3)
                left = left[..(colWidth - 3)] + "...";

            sb.AppendLine($"{left.PadRight(colWidth)} | {right}");
        }

        return sb.ToString();
    }

    private static string FormatJsonPretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "(empty)";

        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}
