using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Runtime;

/// <summary>Typed resource snapshot encoding; the serialized root retains the original v4 shape.</summary>
internal sealed record ResourceSnapshotDelta(ReversibleJsonDelta.NodeDelta Root)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static ResourceSnapshotDelta Between(RunResourceSnapshot before, RunResourceSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return new(ReversibleJsonDelta.Between(JsonSerializer.SerializeToElement(before, Json),
            JsonSerializer.SerializeToElement(after, Json)).Root);
    }

    public RunResourceSnapshot Apply(RunResourceSnapshot value, bool reverse = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        try
        {
            return new ReversibleJsonDelta(Root).Apply(JsonSerializer.SerializeToElement(value, Json), reverse)
                .Deserialize<RunResourceSnapshot>(Json) ?? throw new InvalidDataException("resource-delta-null-result");
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("resource-delta-invalid", ex);
        }
    }
}
