using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain;

/// <summary>
/// Custom STJ converter for <c>ImmutableDictionary&lt;string, object&gt;</c>.
/// Deserializes by first reading into <c>Dictionary&lt;string, object&gt;</c>
/// (using the same CLR type inference as <see cref="ObjectDictionaryConverter"/>),
/// then converting via <c>.ToImmutableDictionary()</c>.
/// Serialization writes the ImmutableDictionary contents identically to ObjectDictionaryConverter.
/// Only intercepts <c>ImmutableDictionary&lt;string, object&gt;</c>;
/// typed ImmutableDictionaries are handled by STJ natively.
/// </summary>
public sealed class ImmutableObjectDictionaryConverter : JsonConverter<ImmutableDictionary<string, object>>
{
    private static readonly ObjectDictionaryConverter InnerConverter = new();

    public override ImmutableDictionary<string, object>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var dict = InnerConverter.Read(ref reader, typeof(Dictionary<string, object>), options);

        if (dict is null)
            return null;

        if (dict.Count == 0)
            return ImmutableDictionary<string, object>.Empty;

        return dict.ToImmutableDictionary();
    }

    public override void Write(
        Utf8JsonWriter writer,
        ImmutableDictionary<string, object> value,
        JsonSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            ObjectDictionaryConverter.WriteClrValue(writer, kvp.Value);
        }

        writer.WriteEndObject();
    }
}
