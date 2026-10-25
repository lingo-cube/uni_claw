using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using UniClaw.Kernel.World;

namespace UniClaw.Host.Runtime;

/// <summary>File representation only. Typed writes serialize changed values, not full revisions.</summary>
internal sealed class WorldRevisionDeltaWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    private static readonly JsonPropertyInfo[] Properties = Json.GetTypeInfo(typeof(WorldBeliefRevision)).Properties
        .Where(p => p.Get is not null).ToArray();
    private Dictionary<string, object?> _previous = new(StringComparer.Ordinal);
    private int _count;

    public JsonElement Encode(WorldBeliefRevision revision) => EncodeFields(Properties.ToDictionary(
        p => p.Name, p => p.Get!(revision), StringComparer.Ordinal));

    public JsonElement Encode(JsonElement revision) => EncodeFields(revision.EnumerateObject()
        .ToDictionary(p => p.Name, p => (object?)p.Value.Clone(), StringComparer.Ordinal));

    private JsonElement EncodeFields(Dictionary<string, object?> fields)
    {
        var values = new Dictionary<string, object?>();
        var maps = new Dictionary<string, object>();
        var arrays = new Dictionary<string, object>();
        foreach (var (name, current) in fields)
        {
            var hadPrevious = _previous.TryGetValue(name, out var previous);
            if (hadPrevious && Same(current, previous)) continue;
            if (name == "worldState" && IsMap(current))
            {
                var before = IsMap(previous) ? Map(previous!) : new Dictionary<string, object?>();
                var after = Map(current!);
                maps.Add(name, new
                {
                    set = after.Where(p => !before.TryGetValue(p.Key, out var old) || !Same(p.Value, old))
                        .ToDictionary(p => p.Key, p => p.Value),
                    remove = before.Keys.Where(k => !after.ContainsKey(k)).ToArray(),
                    order = Edit(before.Keys.Cast<object?>().ToArray(), after.Keys.Cast<object?>().ToArray()),
                });
            }
            else if (IsArray(current))
                arrays.Add(name, Edit(IsArray(previous) ? Items(previous!) : [], Items(current!)));
            else values.Add(name, current);
        }
        var encoded = JsonSerializer.SerializeToElement(new { @base = _count, order = fields.Keys.ToArray(), values, maps, arrays }, Json);
        _previous = fields;
        _count++;
        return encoded;
    }

    private static bool Same(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is JsonElement || b is JsonElement)
            return Element(a).GetRawText() == Element(b).GetRawText();
        return Equals(a, b);
    }
    private static JsonElement Element(object? value) => value is JsonElement json ? json : JsonSerializer.SerializeToElement(value, Json);
    private static bool IsMap(object? value) => value is IReadOnlyDictionary<string, WorldClaim>
        || value is JsonElement { ValueKind: JsonValueKind.Object };
    private static Dictionary<string, object?> Map(object value) => value is JsonElement json
        ? json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value, StringComparer.Ordinal)
        : ((IReadOnlyDictionary<string, WorldClaim>)value).ToDictionary(p => p.Key, p => (object?)p.Value, StringComparer.Ordinal);
    private static bool IsArray(object? value) => value is JsonElement { ValueKind: JsonValueKind.Array }
        || value is IEnumerable and not string;
    private static object?[] Items(object value) => value is JsonElement json
        ? json.EnumerateArray().Select(v => (object?)v).ToArray() : ((IEnumerable)value).Cast<object?>().ToArray();
    private static object Edit(object?[] before, object?[] after)
    {
        var prefix = 0;
        while (prefix < before.Length && prefix < after.Length && Same(before[prefix], after[prefix])) prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix
            && Same(before[^(suffix + 1)], after[^(suffix + 1)])) suffix++;
        return new { prefix, suffix, items = after.Skip(prefix).Take(after.Length - prefix - suffix).ToArray() };
    }
}

/// <summary>Reconstructs frozen historical JSON; never creates a live WorldModel or reruns strategies.</summary>
internal sealed class WorldRevisionDeltaReader
{
    private Dictionary<string, JsonElement> _previous = new(StringComparer.Ordinal);
    private int _count;

    public JsonElement Decode(JsonElement packet)
    {
        var envelope = Fields(packet);
        if (envelope.Count != 5 || packet.GetProperty("base").GetInt32() != _count)
            throw new InvalidDataException("world-delta-base");
        var order = packet.GetProperty("order").EnumerateArray().Select(v => v.GetString()!).ToArray();
        if (order.Any(v => v is null) || order.Distinct(StringComparer.Ordinal).Count() != order.Length)
            throw new InvalidDataException("world-delta-field-order");
        var next = _previous.Where(p => order.Contains(p.Key, StringComparer.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var changed = new HashSet<string>(StringComparer.Ordinal);
        void Set(string key, JsonElement value)
        {
            if (!order.Contains(key, StringComparer.Ordinal) || !changed.Add(key)) throw new InvalidDataException("world-delta-field");
            next[key] = value;
        }
        foreach (var (key, value) in Fields(packet.GetProperty("values"))) Set(key, value);
        foreach (var (key, edit) in Fields(packet.GetProperty("maps")))
        {
            if (key != "worldState" || Fields(edit).Count != 3) throw new InvalidDataException("world-delta-map");
            var before = _previous.TryGetValue(key, out var old) && old.ValueKind == JsonValueKind.Object
                ? Fields(old) : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var after = new Dictionary<string, JsonElement>(before, StringComparer.Ordinal);
            var removed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in edit.GetProperty("remove").EnumerateArray())
            {
                var name = item.GetString()!;
                if (!removed.Add(name) || !after.Remove(name)) throw new InvalidDataException("world-delta-map-remove");
            }
            foreach (var (name, value) in Fields(edit.GetProperty("set")))
            {
                if (removed.Contains(name)) throw new InvalidDataException("world-delta-map-overlap");
                after[name] = value;
            }
            var keys = Apply(before.Keys.Select(k => JsonSerializer.SerializeToElement(k)).ToArray(), edit.GetProperty("order"))
                .Select(k => k.GetString()!).ToArray();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length || keys.Length != after.Count || keys.Any(k => !after.ContainsKey(k)))
                throw new InvalidDataException("world-delta-map-order");
            Set(key, Render(writer =>
            {
                writer.WriteStartObject();
                foreach (var name in keys) { writer.WritePropertyName(name); after[name].WriteTo(writer); }
                writer.WriteEndObject();
            }));
        }
        foreach (var (key, edit) in Fields(packet.GetProperty("arrays")))
        {
            var before = _previous.TryGetValue(key, out var old) && old.ValueKind == JsonValueKind.Array ? old.EnumerateArray().ToArray() : [];
            Set(key, JsonSerializer.SerializeToElement(Apply(before, edit)));
        }
        if (next.Count != order.Length || order.Any(k => !next.ContainsKey(k))) throw new InvalidDataException("world-delta-missing-field");
        var revision = Render(writer =>
        {
            writer.WriteStartObject();
            foreach (var key in order) { writer.WritePropertyName(key); next[key].WriteTo(writer); }
            writer.WriteEndObject();
        });
        _previous = Fields(revision);
        _count++;
        return revision;
    }

    private static JsonElement[] Apply(JsonElement[] before, JsonElement edit)
    {
        if (Fields(edit).Count != 3) throw new InvalidDataException("world-delta-edit-shape");
        var prefix = edit.GetProperty("prefix").GetInt32();
        var suffix = edit.GetProperty("suffix").GetInt32();
        if (prefix < 0 || suffix < 0 || prefix > before.Length || suffix > before.Length - prefix)
            throw new InvalidDataException("world-delta-edit-range");
        return before.Take(prefix).Concat(edit.GetProperty("items").EnumerateArray()).Concat(before.Skip(before.Length - suffix)).ToArray();
    }
    private static Dictionary<string, JsonElement> Fields(JsonElement value)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) if (!result.TryAdd(p.Name, p.Value)) throw new InvalidDataException("world-delta-duplicate-property");
        return result;
    }
    internal static JsonElement Render(Action<Utf8JsonWriter> write)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory)) write(writer);
        using var document = JsonDocument.Parse(memory.ToArray());
        return document.RootElement.Clone();
    }
}
