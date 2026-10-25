using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniClaw.Host.Runtime;

public enum HistoryVersionDirection
{
    Forward, Reverse
}
public sealed record HistoryVersionReadCounts(int FramesRead, int SnapshotReads, long BytesRead);
public sealed record HistorySnapshotWriteCounts(long BaselineWrites, long BaselineFlushes, long BaselineBytes);
public sealed record HistoryVersionOptions
{
    public int MaxTailRecords { get; init; } = 64;
    public long MinDeltaBytesForSnapshot { get; init; } = 65536;
    public int SnapshotByteRatio { get; init; } = 4;
}

internal interface IHistoryVersionCodec<T> where T : class
{
    string VersionSchema
    {
        get;
    }
    string HeadSchema
    {
        get;
    }
    int Sequence(T row);
    JsonElement Mutable(T row);
    T WithoutMutable(T row);
    T WithMutable(T row, JsonElement mutable);
}
