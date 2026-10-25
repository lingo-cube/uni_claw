using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Control;

namespace UniClaw.Host.Runtime;

/// <summary>Single original-fact file, written before external operations.
/// Read recovery requires a quiescent writer and grants no execution permission.</summary>
public sealed class FileRunRecoveryArchive : IAsyncRunRecoveryArchive
{
    private readonly HistoryWriteMeter _writeMeter = new();
    public HistoryWriteCounts WriteStatistics => _writeMeter.Snapshot;

    private sealed record Frame(string Schema, string ConsultationFile, string Payload, string Hash);
    private const string Schema = "uniclaw.host.original-run-archive.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _faulted;
    private readonly int _maxArchiveBytes;
    private readonly string _directory;
    private readonly string _consultationFile;
    public FileRunRecoveryArchive(string runDirectory, string consultationFile = "consultations.journal.jsonl", int maxArchiveBytes = 16 * 1024 * 1024)
    {
        if (Path.GetFileName(consultationFile) != consultationFile || consultationFile is "" or "." or "..")
            throw new ArgumentException("Archive consultation file must be a leaf name", nameof(consultationFile));
        if (maxArchiveBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxArchiveBytes));
        _maxArchiveBytes = maxArchiveBytes;
        _directory = Path.GetFullPath(runDirectory);
        _consultationFile = consultationFile;
    }

    public void Initialize(RunRecoveryRecord record) => InitializeAsync(record).AsTask().GetAwaiter().GetResult();
    public async ValueTask InitializeAsync(RunRecoveryRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("run-archive-write-in-progress");
        try
        {
            if (_faulted) throw new IOException("run-archive-faulted");
            try
            {
                var payload = JsonSerializer.Serialize(record, Json);
                var frame = new Frame(Schema, _consultationFile, payload, Hash(_consultationFile + "\n" + payload));
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame, Json) + "\n");
                if (bytes.Length > _maxArchiveBytes) throw new InvalidDataException("run-archive-exceeds-byte-limit");
                await using var stream = new FileStream(Path.Combine(_directory, "run-identity.json"), FileMode.CreateNew,
                    FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
                _writeMeter.FrameWrite(1, bytes.Length);
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                _writeMeter.FrameFlush();
                await stream.FlushAsync().ConfigureAwait(false);
                _writeMeter.Committed();
            }
            catch { _faulted = true; throw; }
        }
        finally { _writeGate.Release(); }
    }

    public RunRecoveryRecord Read() => JsonSerializer.Deserialize<RunRecoveryRecord>(ReadFrame().Payload, Json)
        ?? throw new InvalidDataException("run-archive-payload");

    private Frame ReadFrame()
    {
        using var stream = new FileStream(Path.Combine(_directory, "run-identity.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        var text = reader.ReadToEnd();
        if (!text.EndsWith('\n') || text.Count(c => c == '\n') != 1) throw new InvalidDataException("run-archive-incomplete");
        var frame = JsonSerializer.Deserialize<Frame>(text, Json) ?? throw new InvalidDataException("run-archive-frame");
        if (frame.Schema != Schema || frame.ConsultationFile != _consultationFile
            || frame.Hash != Hash(frame.ConsultationFile + "\n" + frame.Payload))
            throw new InvalidDataException("run-archive-integrity");
        return frame;
    }

    // Compatibility entry points select file sources; all semantic reads use the same Kernel reader.
    /// <inheritdoc cref="RunRecoveryReader.Inspect"/>
    public RunResourceRead Inspect(string expectedRunId) => WithReader(reader => reader.Inspect(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.RestoreRunState"/>
    public RunModel RestoreRunState(string expectedRunId) => WithReader(reader => reader.RestoreRunState(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.ReadControlTraversalHistory"/>
    public ControlTraversalHistoryRead ReadControlTraversalHistory(string expectedRunId) =>
        WithReader(reader => reader.ReadControlTraversalHistory(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.ReadRuntimePlanHistory"/>
    public RuntimePlanHistoryRead ReadRuntimePlanHistory(string expectedRunId) => WithReader(reader => reader.ReadRuntimePlanHistory(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.InspectRecovery"/>
    public RunRecoveryRead InspectRecovery(string expectedRunId) => WithReader(reader => reader.InspectRecovery(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.RestoreEvidence"/>
    public EvidenceLedger RestoreEvidence(string expectedRunId) => WithReader(reader => reader.RestoreEvidence(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.InspectEvidenceRecovery"/>
    public RunEvidenceRecoveryRead InspectEvidenceRecovery(string expectedRunId) => WithReader(reader => reader.InspectEvidenceRecovery(expectedRunId));
    /// <inheritdoc cref="RunRecoveryReader.InspectWorldRecovery"/>
    public RunWorldRecoveryRead InspectWorldRecovery(string expectedRunId) => WithReader(reader => reader.InspectWorldRecovery(expectedRunId));

    private T WithReader<T>(Func<RunRecoveryReader, T> query)
    {
        using var sources = new FileSources(_directory, _consultationFile);
        return query(new RunRecoveryReader(this, sources));
    }

    private sealed class FileSources(string directory, string consultationFile) : IRunRecoverySources, IDisposable
    {
        private readonly List<IDisposable> _opened = [];
        private T Own<T>(T source) where T : IDisposable { _opened.Add(source); return source; }
        public IRunStateJournal RunStateHistory => Own(new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"), true));
        public IEvidenceJournal EvidenceHistory => Own(new FileEvidenceJournal(Path.Combine(directory, "evidence.journal.jsonl"), true));
        public IWorldJournal WorldHistory => Own(new FileWorldJournal(Path.Combine(directory, "world.journal.jsonl"), true));
        public IControlTraversalJournal ControlTraversalHistory => Own(new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"), true));
        public IRuntimePlanJournal RuntimePlanHistory => Own(new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"), true));
        public RunResourceRead ReadResources(RunResourceBinding? binding, bool requireCommittedEndpoint)
        {
            var resources = Path.Combine(directory, "resource.journal.jsonl");
            return requireCommittedEndpoint
                ? FileRunResourceJournal.InspectRecovery(resources, binding ?? throw new ArgumentException("original binding required"),
                    Path.Combine(directory, consultationFile), Path.Combine(directory, "exec.journal"))
                : FileRunResourceJournal.Inspect(resources, binding,
                    Path.Combine(directory, consultationFile), Path.Combine(directory, "exec.journal"));
        }
        public void Dispose()
        {
            for (var index = _opened.Count - 1; index >= 0; index--) _opened[index].Dispose();
        }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
