using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain;

/// <summary>
/// Custom STJ converter for <c>object</c> typed properties.
/// Uses the same CLR type inference as <see cref="ObjectDictionaryConverter.InferClrValue"/>:
/// String → string, Number → long/double, True/False → bool, Null → null,
/// Array/Object → preserve as JsonElement (no data loss).
/// Required because STJ deserializes <c>object</c> as <c>JsonElement</c> by default,
/// breaking round-trip equality for types like <c>Target.Value</c>.
/// </summary>
public sealed class ObjectValueConverter : JsonConverter<object>
{
    public override object? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => ObjectDictionaryConverter.InferClrValue(ref reader);

    public override void Write(
        Utf8JsonWriter writer,
        object value,
        JsonSerializerOptions options)
        => ObjectDictionaryConverter.WriteClrValue(writer, value);
}
