using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;
using Xunit;

namespace UniClaw.Core.Tests.Domain.CrossCutting;

public class ImmutableObjectDictionaryConverterTests
{
    private static readonly JsonSerializerOptions Options = DomainJsonOptions.Default;

    [Fact]
    public void String_Value_RoundTrips()
    {
        var dict = ImmutableDictionary<string, object>.Empty.Add("key", "value");
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<ImmutableDictionary<string, object>>(json, Options)!;

        Assert.Equal("value", result["key"]);
        Assert.IsType<string>(result["key"]);
    }

    [Fact]
    public void Empty_Dictionary_Deserializes_To_Empty()
    {
        var json = "{}";
        var result = JsonSerializer.Deserialize<ImmutableDictionary<string, object>>(json, Options)!;

        Assert.Equal(ImmutableDictionary<string, object>.Empty, result);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Mixed_Types_RoundTrips()
    {
        var dict = ImmutableDictionary<string, object>.Empty
            .Add("name", "test")
            .Add("count", 5L)
            .Add("flag", true);
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<ImmutableDictionary<string, object>>(json, Options)!;

        Assert.Equal("test", result["name"]);
        Assert.Equal(5L, result["count"]);
        Assert.True((bool)result["flag"]);
    }

    [Fact]
    public void Typed_ImmutableDictionary_Not_Intercepted()
    {
        // ImmutableDictionary<string, string> should NOT be handled by ImmutableObjectDictionaryConverter
        var dict = ImmutableDictionary<string, string>.Empty.Add("key", "value");
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<ImmutableDictionary<string, string>>(json, Options)!;

        Assert.Equal("value", result["key"]);
    }

    [Fact]
    public void Null_Deserializes_To_Null()
    {
        var json = "null";
        var result = JsonSerializer.Deserialize<ImmutableDictionary<string, object>>(json, Options);

        Assert.Null(result);
    }
}
