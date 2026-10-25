using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniClaw.Host.Runtime;

/// <summary>Canonical reversible JSON structural delta. Its serialized shape is part of the v4 resource wire format.</summary>
internal sealed record ReversibleJsonDelta(ReversibleJsonDelta.NodeDelta Root)
{
    internal sealed record NodeDelta(string Kind, string BeforeHash, string AfterHash,
        string? BeforeJson = null, string? AfterJson = null,
        string[]? BeforeKeys = null, string[]? AfterKeys = null,
        PropertyDelta[]? Properties = null, int BeforeLength = 0, int AfterLength = 0,
        int Prefix = 0, int Suffix = 0, JsonElement[]? Removed = null, JsonElement[]? Added = null);
    internal sealed record PropertyDelta(string Name, bool BeforePresent, bool AfterPresent, NodeDelta? Change,
        string? ValueJson = null);

    public static ReversibleJsonDelta Between(JsonElement before, JsonElement after) => new(Build(before, after));
    public JsonElement Apply(JsonElement value, bool reverse = false)
    {
        try
        {
            var result = ApplyNode(Root, JsonNode.Parse(value.GetRawText()), reverse);
            using var document = JsonDocument.Parse(result?.ToJsonString() ?? "null");
            return document.RootElement.Clone();
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("reversible-json-delta-invalid", ex);
        }
    }

    private static NodeDelta Build(JsonElement before, JsonElement after)
    {
        var bh = Hash(before);
        var ah = Hash(after);
        if (before.ValueKind == JsonValueKind.Object && after.ValueKind == JsonValueKind.Object)
        {
            var old = before.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            var next = after.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            var changes = old.Keys.Concat(next.Keys).Distinct(StringComparer.Ordinal)
                .Where(k => !old.ContainsKey(k) || !next.ContainsKey(k) || Hash(old[k]) != Hash(next[k]))
                .Select(k => new PropertyDelta(k, old.ContainsKey(k), next.ContainsKey(k),
                    old.ContainsKey(k) && next.ContainsKey(k) ? Build(old[k], next[k]) : null,
                    old.ContainsKey(k) == next.ContainsKey(k) ? null : (old.ContainsKey(k) ? old[k] : next[k]).GetRawText())).ToArray();
            return new("object", bh, ah, BeforeKeys: old.Keys.ToArray(), AfterKeys: next.Keys.ToArray(), Properties: changes);
        }
        if (before.ValueKind == JsonValueKind.Array && after.ValueKind == JsonValueKind.Array)
        {
            var a = before.EnumerateArray().ToArray();
            var b = after.EnumerateArray().ToArray();
            var prefix = 0;
            while (prefix < a.Length && prefix < b.Length && Hash(a[prefix]) == Hash(b[prefix]))
                prefix++;
            var suffix = 0;
            while (suffix < a.Length - prefix && suffix < b.Length - prefix && Hash(a[^(suffix + 1)]) == Hash(b[^(suffix + 1)]))
                suffix++;
            return new("array", bh, ah, BeforeLength: a.Length, AfterLength: b.Length, Prefix: prefix, Suffix: suffix,
                Removed: a.Skip(prefix).Take(a.Length - prefix - suffix).Select(v => v.Clone()).ToArray(),
                Added: b.Skip(prefix).Take(b.Length - prefix - suffix).Select(v => v.Clone()).ToArray());
        }
        return new("scalar", bh, ah, BeforeJson: before.GetRawText(), AfterJson: after.GetRawText());
    }

    private static JsonNode? ApplyNode(NodeDelta delta, JsonNode? current, bool reverse)
    {
        if (Hash(current) != (reverse ? delta.AfterHash : delta.BeforeHash))
            throw new InvalidDataException("reversible-json-delta-wrong-base");
        if (delta.Kind == "scalar")
        {
            var raw = reverse ? delta.BeforeJson : delta.AfterJson;
            if (raw is null)
                throw new InvalidDataException("reversible-json-delta-scalar-missing");
            var result = JsonNode.Parse(raw);
            if (Hash(result) != (reverse ? delta.BeforeHash : delta.AfterHash))
                throw new InvalidDataException("reversible-json-delta-scalar-result");
            return result;
        }
        if (delta.Kind == "array")
        {
            if (current is not JsonArray array || array.Count != (reverse ? delta.AfterLength : delta.BeforeLength))
                throw new InvalidDataException("reversible-json-delta-array-base");
            var remove = reverse ? delta.Added! : delta.Removed!;
            var insert = reverse ? delta.Removed! : delta.Added!;
            if (delta.Prefix < 0 || delta.Suffix < 0 || delta.Prefix + delta.Suffix + remove.Length != array.Count)
                throw new InvalidDataException("reversible-json-delta-array-range");
            for (var i = 0; i < remove.Length; i++)
            if (Hash(array[delta.Prefix + i]) != Hash(remove[i]))
                throw new InvalidDataException("reversible-json-delta-array-removed-value");
            for (var i = 0; i < remove.Length; i++)
                array.RemoveAt(delta.Prefix);
            for (var i = 0; i < insert.Length; i++)
                array.Insert(delta.Prefix + i, JsonNode.Parse(insert[i].GetRawText()));
            if (array.Count != (reverse ? delta.BeforeLength : delta.AfterLength) || Hash(array) != (reverse ? delta.BeforeHash : delta.AfterHash))
                throw new InvalidDataException("reversible-json-delta-array-result");
            return array;
        }
        if (delta.Kind != "object" || current is not JsonObject obj)
            throw new InvalidDataException("reversible-json-delta-kind");
        var expectedKeys = reverse ? delta.AfterKeys! : delta.BeforeKeys!;
        if (!obj.Select(p => p.Key).SequenceEqual(expectedKeys, StringComparer.Ordinal))
            throw new InvalidDataException("reversible-json-delta-object-order");
        foreach (var property in delta.Properties!)
        {
            var had = reverse ? property.AfterPresent : property.BeforePresent;
            var has = reverse ? property.BeforePresent : property.AfterPresent;
            if (obj.ContainsKey(property.Name) != had)
                throw new InvalidDataException("reversible-json-delta-property-presence");
            if (had && has)
                obj[property.Name] = ApplyNode(property.Change!, obj[property.Name], reverse);
            else if (has)
                obj[property.Name] = JsonNode.Parse(property.ValueJson ?? throw new InvalidDataException("reversible-json-delta-property-value-missing"));
            else
                obj.Remove(property.Name);
        }
        var keys = reverse ? delta.BeforeKeys! : delta.AfterKeys!;
        if (obj.Count != keys.Length || keys.Any(k => !obj.ContainsKey(k)))
            throw new InvalidDataException("reversible-json-delta-object-keys");
        var ordered = new JsonObject();
        foreach (var key in keys)
            ordered.Add(key, obj[key]?.DeepClone());
        if (Hash(ordered) != (reverse ? delta.BeforeHash : delta.AfterHash))
            throw new InvalidDataException("reversible-json-delta-object-result");
        return ordered;
    }
    private static string Hash(JsonElement value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.GetRawText())));
    private static string Hash(JsonNode? value) => value is null ? Hash(JsonSerializer.SerializeToElement<object?>(null)) : Hash(JsonSerializer.SerializeToElement(value));
}
