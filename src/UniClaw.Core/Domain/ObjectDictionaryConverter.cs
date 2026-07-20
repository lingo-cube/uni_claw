using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain;

/// <summary>
/// Custom STJ converter for <c>Dictionary&lt;string, object&gt;</c>.
/// Deserializes JSON objects by inferring CLR types from JsonElement ValueKind:
/// String→string, Number→long/double, True/False→bool, Null→null,
/// Array/Object→preserve as JsonElement (no data loss).
/// Only intercepts <c>Dictionary&lt;string, object&gt;</c>;
/// typed dictionaries (<c>Dictionary&lt;string, T&gt;</c>) are handled by STJ natively.
/// </summary>
public sealed class ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            throw new JsonException($"Expected StartObject or Null, got {reader.TokenType}");
        }

        var dict = new Dictionary<string, object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dict;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");

            string key = reader.GetString()!;
            reader.Read();

            object? value = InferClrValue(ref reader);
            dict[key] = value!;
        }

        // Should not reach here — EndObject returns above
        throw new JsonException("Unexpected end of JSON object");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, object> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            WriteClrValue(writer, kvp.Value);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Infers a CLR value from the current reader position based on JsonElement ValueKind.
    /// </summary>
    internal static object? InferClrValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out long longVal))
                    return longVal;
                return reader.GetDouble();

            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.Null:
                return null;

            // Array and Object — preserve as JsonElement to avoid data loss
            case JsonTokenType.StartArray:
            case JsonTokenType.StartObject:
                var element = JsonElement.ParseValue(ref reader);
                return element;

            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    /// <summary>
    /// Writes a CLR value to the JSON writer based on its runtime type.
    /// </summary>
    internal static void WriteClrValue(Utf8JsonWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else if (value is string s)
        {
            writer.WriteStringValue(s);
        }
        else if (value is long l)
        {
            writer.WriteNumberValue(l);
        }
        else if (value is double d)
        {
            writer.WriteNumberValue(d);
        }
        else if (value is int i)
        {
            writer.WriteNumberValue(i);
        }
        else if (value is bool b)
        {
            writer.WriteBooleanValue(b);
        }
        else if (value is JsonElement je)
        {
            je.WriteTo(writer);
        }
        else
        {
            // Fallback: serialize via STJ for unknown CLR types
            JsonSerializer.Serialize(writer, value, value.GetType(), DomainJsonOptions.Default);
        }
    }
}
