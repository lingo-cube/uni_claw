using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniClaw.Agent.Dsh;

public sealed record DshProcessOptions(
    string FileName,
    string Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>Minimal line-delimited JSON-RPC client for a local headless sidecar. It
/// deliberately knows only initialize/consult/abort_current_turn; Product policy and
/// effect authority stay in the Kernel.</summary>
public sealed class JsonRpcStdioTransport : IDshTransport
{
    private readonly DshProcessOptions _options;
    private readonly JsonSerializerOptions _json = ProductProtocolJson.CreateOptions();
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private Process? _process;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private int _rpcId;
    private bool _disposed;

    public JsonRpcStdioTransport(DshProcessOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.FileName))
            throw new ArgumentException("sidecar executable is required", nameof(options));
    }

    public async Task<HandshakeResponse> HandshakeAsync(HandshakeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<HandshakeResponse>("initialize", request, cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("sidecar returned null handshake");
    }

    public async Task<DshTransportResponse> ConsultAsync(ConsultationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync<DshTransportResponse>("consult", request, cancellationToken)
            .ConfigureAwait(false);
        return result ?? new DshTransportResponse(request.RequestId, request.Generation, null,
            "sidecar returned null consultation");
    }

    public async Task AbortCurrentTurnAsync(CancellationToken cancellationToken)
    {
        // The abort request is a realization-private mechanical interrupt. It is
        // best effort and has no Product state semantics.
        try
        {
            await SendAsync<JsonElement>("abort_current_turn", new { }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception) when (_process is null || _process.HasExited)
        {
            // A dead sidecar is already aborted.
        }
    }

    private async Task<T?> SendAsync<T>(string method, object parameters,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureStarted();
            var id = ++_rpcId;
            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(parameters, _json),
            };
            await _writer!.WriteLineAsync(request.ToJsonString(_json)).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            var line = await _reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                throw new IOException("DSH sidecar closed stdio before returning JSON-RPC response");
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id)
                throw new InvalidDataException("DSH JSON-RPC response id mismatch");
            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException($"DSH JSON-RPC error: {error}");
            if (!root.TryGetProperty("result", out var result))
                return default;
            return result.Deserialize<T>(_json);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private void EnsureStarted()
    {
        if (_process is not null) return;
        var start = new ProcessStartInfo
        {
            FileName = _options.FileName,
            Arguments = _options.Arguments,
            WorkingDirectory = _options.WorkingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (_options.Environment is not null)
            foreach (var pair in _options.Environment)
                start.Environment[pair.Key] = pair.Value;
        _process = Process.Start(start)
            ?? throw new InvalidOperationException("failed to start DSH sidecar");
        _reader = _process.StandardOutput;
        _writer = _process.StandardInput;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _writer?.Close();
        if (_process is { HasExited: false })
        {
            try
            {
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
            }
        }
        _reader?.Dispose();
        _writer?.Dispose();
        _process?.Dispose();
        _ioGate.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonRpcStdioTransport));
    }
}
