using UniClaw.Kernel.World;

namespace UniClaw.Host.Runtime;

public sealed partial class FileWorldJournal
{
    internal static (string RecordingId, string Schema, IReadOnlyList<WorldHistoryRecord> Rows, long SourceBytes) ReadEncoded(string path)
    {
        // Reuse the original complete frame/hash/head verifier. Only the separate
        // data query skips full revision decoding; the original reader never does.
        using var reader = new FileWorldJournal(path, true, 16 * 1024 * 1024, false, new(), encodedOnly: true);
        return (reader.RecordingId, reader._sourceSchema, reader._records.ToArray(),
            reader._stream.Length + new FileInfo(reader._headPath).Length);
    }
}
