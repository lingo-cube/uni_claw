using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Core.Domain;
using Xunit;

namespace UniClaw.Core.Tests.Domain.CrossCutting;

public class ObjectDictionaryConverterTests
{
    private static readonly JsonSerializerOptions Options = DomainJsonOptions.Default;

    [Fact]
    public void String_Value_RoundTrips_As_String()
    {
        var dict = new Dictionary<string, object> { ["name"] = "Settings" };
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.Equal("Settings", result["name"]);
        Assert.IsType<string>(result["name"]);
    }

    [Fact]
    public void Number_Integer_RoundTrips_As_Long()
    {
        var dict = new Dictionary<string, object> { ["count"] = 42L };
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.Equal(42L, result["count"]);
        Assert.IsType<long>(result["count"]);
    }

    [Fact]
    public void Number_Double_RoundTrips_As_Double()
    {
        var dict = new Dictionary<string, object> { ["ratio"] = 3.14 };
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.Equal(3.14, (double)result["ratio"], precision: 2);
        Assert.IsType<double>(result["ratio"]);
    }

    [Fact]
    public void Number_LargeInteger_Exceeding_Long_Becomes_Double()
    {
        // Value exceeding long.MaxValue
        var json = "{\"big\":99999999999999999999}";
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.IsType<double>(result["big"]);
    }

    [Fact]
    public void Bool_Value_RoundTrips_As_Bool()
    {
        var dict = new Dictionary<string, object> { ["enabled"] = true, ["disabled"] = false };
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.True((bool)result["enabled"]);
        Assert.False((bool)result["disabled"]);
    }

    [Fact]
    public void Null_Value_Becomes_CSharp_Null()
    {
        var json = "{\"optional\":null}";
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.Null(result["optional"]);
    }

    [Fact]
    public void Nested_Object_Preserved_As_JsonElement()
    {
        var json = "{\"nested\":{\"a\":1,\"b\":\"x\"}}";
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.IsType<JsonElement>(result["nested"]);

        // Verify no data loss on round-trip
        var roundJson = JsonSerializer.Serialize(result, Options);
        var roundResult = JsonSerializer.Deserialize<Dictionary<string, object>>(roundJson, Options)!;
        Assert.IsType<JsonElement>(roundResult["nested"]);
    }

    [Fact]
    public void Array_Preserved_As_JsonElement()
    {
        var json = "{\"items\":[1,2,3]}";
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.IsType<JsonElement>(result["items"]);

        // Verify no data loss on round-trip
        var roundJson = JsonSerializer.Serialize(result, Options);
        Assert.Contains("[1,2,3]", roundJson);
    }

    [Fact]
    public void Empty_Dictionary_RoundTrips()
    {
        var dict = new Dictionary<string, object>();
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;

        Assert.Empty(result);
    }

    [Fact]
    public void Typed_Dictionary_Not_Intercepted()
    {
        // Dictionary<string, string> should NOT be handled by ObjectDictionaryConverter
        var dict = new Dictionary<string, string> { ["key"] = "value" };
        var json = JsonSerializer.Serialize(dict, Options);
        var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options)!;

        Assert.Equal("value", result["key"]);
    }
}
