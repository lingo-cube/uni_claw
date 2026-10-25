using System.Buffers;
using System.Diagnostics;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>Per-run export limits. Required result/report writes do not use this lossy writer.</summary>
public sealed record AuxiliaryArtifactLimits
{
    public int MaxArtifactBytes { get; init; } = 16 * 1024 * 1024;
    public long MaxTotalBytes { get; init; } = 64 * 1024 * 1024;
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed record AuxiliaryArtifactStatus(int Written, int Failed, int Dropped, long AcceptedBytes,
    bool Unsettled, IReadOnlyList<string> Errors);

/// <summary>Shared by the two formal Hosts. Export failure never changes the recorded business result.</summary>
internal sealed class AuxiliaryArtifacts
{
    private readonly string _directory;
    private readonly AuxiliaryArtifactLimits _limits;
    private readonly List<string> _errors = [];
    private readonly HashSet<string> _faultedLogs = [];
    private readonly Dictionary<string, int> _logBytes = [];
    private readonly object _sync = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _written, _failed, _dropped;
    private long _acceptedBytes;
    private Task? _unsettled;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions LineJson = new(JsonSerializerDefaults.Web);

    internal AuxiliaryArtifacts(string directory, AuxiliaryArtifactLimits? limits = null)
    {
        _directory = directory;
        _limits = limits ?? new();
        if (_limits.MaxArtifactBytes <= 0 || _limits.MaxTotalBytes <= 0
            || _limits.WriteTimeout <= TimeSpan.Zero || _limits.WriteTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    internal AuxiliaryArtifactStatus Status
    {
        get { lock (_sync) return new(_written, _failed, _dropped, _acceptedBytes,
            _unsettled is { IsCompleted: false }, _errors.ToArray()); }
    }

    internal async ValueTask WriteAsync(string name, Func<object?> capture, CancellationToken cancellationToken = default)
        => _ = await WriteCoreAsync(name, capture, false, cancellationToken).ConfigureAwait(false);

    internal ValueTask<bool> AppendJsonLineAsync(string name, Func<object?> capture, CancellationToken cancellationToken = default)
        => WriteCoreAsync(name, capture, true, cancellationToken);

    private async ValueTask<bool> WriteCoreAsync(string name, Func<object?> capture, bool append, CancellationToken cancellationToken)
    {
        if (!_writeGate.Wait(0))
        { lock (_sync) { _dropped++; RecordError(name + ":write-in-progress"); } return false; }
        CancellationTokenSource? cancellation = null;
        Task? write = null;
        try
        {
            lock (_sync)
                if (_unsettled is { IsCompleted: false }) { _dropped++; return false; }
            if (append && _faultedLogs.Contains(name))
            { lock (_sync) { _dropped++; RecordError(name + ":append-stopped"); } return false; }
            if (Path.GetFileName(name) != name) throw new ArgumentException("Artifact must be a leaf name", nameof(name));
            var firstAppend = !_logBytes.TryGetValue(name, out var logBytes);
            int remaining;
            lock (_sync) remaining = (int)Math.Min(_limits.MaxArtifactBytes - (append ? logBytes : 0),
                _limits.MaxTotalBytes - _acceptedBytes);
            if (remaining == 0) { lock (_sync) { _dropped++; RecordError(name + ":byte-limit"); } return false; }
            // Bound UTF-8 writer allocation before encoding, not after creating an unbounded string.
            var buffer = new LimitedJsonBuffer(remaining, _limits.WriteTimeout, cancellationToken);
            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = !append }))
                JsonSerializer.Serialize(json, capture(), append ? LineJson : Json);
            if (append)
            {
                buffer.GetSpan(1)[0] = (byte)'\n';
                buffer.Advance(1);
            }
            var payload = buffer.Written;
            var bytes = payload.Length;
            lock (_sync)
            {
                if (bytes > _limits.MaxArtifactBytes || bytes > _limits.MaxTotalBytes - _acceptedBytes)
                { _dropped++; RecordError(name + ":byte-limit"); return false; }
                _acceptedBytes += bytes;
                if (append) _logBytes[name] = logBytes + bytes;
            }
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            write = append ? AppendFileAsync(Path.Combine(_directory, name), payload, firstAppend, cancellation.Token)
                : WriteFileAsync(Path.Combine(_directory, name), payload, cancellation.Token);
            await write.WaitAsync(_limits.WriteTimeout).ConfigureAwait(false);
            lock (_sync) _written++;
            return true;
        }
        catch (ByteLimitException) { lock (_sync) { _dropped++; RecordError(name + ":byte-limit"); } }
        catch (TimeoutException) when (write is not null && cancellation is not null)
        {
            if (append) _faultedLogs.Add(name);
            var settling = SettleAsync(write!, cancellation!);
            lock (_sync) { _failed++; RecordError(name + ":write-timeout"); _unsettled = settling; }
            cancellation = null; // The retained operation owns cancellation and eventual failure.
        }
        catch (Exception error)
        {
            // A failed append may have left a torn line. Stop before another
            // caller can append a later record to that uncertain suffix.
            if (append && write is not null) _faultedLogs.Add(name);
            lock (_sync) { _failed++; RecordError(name + ":" + error.GetType().Name); }
        }
        finally { cancellation?.Dispose(); _writeGate.Release(); }
        return false;
    }

    private static async Task AppendFileAsync(string path, ReadOnlyMemory<byte> payload, bool first, CancellationToken cancellation)
    {
        await using var file = new FileStream(path, first ? FileMode.CreateNew : FileMode.Append, FileAccess.Write, FileShare.Read,
            4096, FileOptions.Asynchronous);
        await file.WriteAsync(payload, cancellation).ConfigureAwait(false);
        await file.FlushAsync(cancellation).ConfigureAwait(false);
    }

    private void RecordError(string reason)
    {
        if (_errors.Count < 32) _errors.Add(reason);
    }

    private static async Task WriteFileAsync(string path, ReadOnlyMemory<byte> payload, CancellationToken cancellation)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".aux.tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                4096, FileOptions.Asynchronous))
            {
                await file.WriteAsync(payload, cancellation).ConfigureAwait(false);
                await file.FlushAsync(cancellation).ConfigureAwait(false);
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class ByteLimitException : IOException { }

    /// <summary>Reject writer size hints before allocating; never materialize the whole JSON string.</summary>
    private sealed class LimitedJsonBuffer(int limit, TimeSpan timeout, CancellationToken cancellation) : IBufferWriter<byte>
    {
        private byte[] _buffer = [];
        private int _used;
        private readonly long _started = Stopwatch.GetTimestamp();
        internal ReadOnlyMemory<byte> Written => _buffer.AsMemory(0, _used);
        public void Advance(int count)
        {
            Check();
            if (count < 0 || count > _buffer.Length - _used) throw new ByteLimitException();
            _used += count;
        }
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Check();
            sizeHint = Math.Max(1, sizeHint);
            if (sizeHint > limit - _used) throw new ByteLimitException();
            if (sizeHint > _buffer.Length - _used)
                Array.Resize(ref _buffer, (int)Math.Min(limit, Math.Max((long)_used + sizeHint, Math.Max(256L, (long)_buffer.Length * 2))));
            return _buffer.AsMemory(_used);
        }
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
        private void Check()
        {
            cancellation.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(_started) >= timeout) throw new TimeoutException("auxiliary-encoding-timeout");
        }
    }

    private static async Task SettleAsync(Task write, CancellationTokenSource cancellation)
    {
        try { await cancellation.CancelAsync().ConfigureAwait(false); await write.ConfigureAwait(false); }
        catch (Exception) { /* Failure already reported, keep observing completion. */ }
        finally { cancellation.Dispose(); }
    }
}
