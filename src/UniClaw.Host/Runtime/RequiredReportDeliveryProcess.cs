using System.Diagnostics;
using System.Text;

namespace UniClaw.Host.Runtime;

/// <summary>
/// Bounded execution for a required-report generator. It keeps the child-owned
/// OS lease alive until the child itself exits, including during Host shutdown.
/// </summary>
public sealed class RequiredReportDeliveryProcess : IAsyncDisposable
{
    private const int RetainedPipeCharacters = 64 * 1024;
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(120);

    private readonly object _gate = new();
    private readonly Dictionary<int, (TaskCompletionSource Completion, string Label)> _processes = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly string? _pythonExecutable;
    private readonly int _maxConcurrency;
    private readonly TimeSpan _operationTimeout;
    private bool _closing;
    private bool _stopDisposed;

    public RequiredReportDeliveryProcess(string? pythonExecutable = null, int maxConcurrency = 4,
        TimeSpan? operationTimeout = null)
    {
        if (maxConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        var timeout = operationTimeout ?? DefaultOperationTimeout;
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        _pythonExecutable = pythonExecutable;
        _maxConcurrency = maxConcurrency;
        _operationTimeout = timeout;
    }

    public record RequiredReportProcessDrain(bool Settled, IReadOnlyList<string> UnconfirmedOperations);

    public async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string entry, string[] arguments,
        CancellationToken token = default, string? leasePath = null)
    {
        var info = new ProcessStartInfo(_pythonExecutable ?? Environment.GetEnvironmentVariable("UNICLAW_TOOL_PYTHON") ?? "python3")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (leasePath is not null)
        {
            info.ArgumentList.Add("-c"); info.ArgumentList.Add(ReportProcessWrapper);
            info.ArgumentList.Add(entry); info.ArgumentList.Add(leasePath);
        }
        else info.ArgumentList.Add(entry);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        Process process;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_closing) throw new InvalidOperationException("report-host-closing");
            if (_processes.Count >= _maxConcurrency) throw new InvalidOperationException("report-process-backpressure");
            process = Process.Start(info) ?? throw new InvalidOperationException("report-process-not-started");
            var label = Path.GetFileName(entry) == "generator.py"
                ? Path.GetFileName(Path.GetDirectoryName(entry))! : "input-description";
            _processes.Add(process.Id, (completion, label));
        }

        using var ownedProcess = process;
        try
        {
            async Task<string> ReadAsync(StreamReader reader)
            {
                var result = new StringBuilder(); var buffer = new char[2048]; int count;
                while ((count = await reader.ReadAsync(buffer)) > 0)
                    if (result.Length < RetainedPipeCharacters)
                        result.Append(buffer, 0, Math.Min(count, RetainedPipeCharacters - result.Length));
                return result.ToString();
            }

            var stdout = ReadAsync(process.StandardOutput); var stderr = ReadAsync(process.StandardError);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
            budget.CancelAfter(_operationTimeout);
            try { await process.WaitForExitAsync(budget.Token); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                // The owning delivery retains its lease until this task settles.
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(stdout, stderr);
                throw;
            }
            return (process.ExitCode, await stdout, await stderr);
        }
        finally
        {
            // Failed termination is reported by Close; do not pretend an active child has settled.
            if (process.HasExited)
            {
                lock (_gate) _processes.Remove(process.Id);
                completion.TrySetResult();
            }
        }
    }

    public async Task<RequiredReportProcessDrain> CloseAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        Task[] completions;
        lock (_gate)
        {
            _closing = true;
            completions = _processes.Values.Select(x => x.Completion.Task).ToArray();
        }
        await _stop.CancelAsync().ConfigureAwait(false);
        try { await Task.WhenAll(completions).WaitAsync(timeout).ConfigureAwait(false); }
        catch (TimeoutException) { }
        lock (_gate)
        {
            var pending = _processes.Values.Select(x => x.Label).Distinct(StringComparer.Ordinal).ToArray();
            return new(pending.Length == 0, pending);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_stopDisposed) return;
        }
        var drain = await CloseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!drain.Settled) return;
        lock (_gate)
        {
            if (_stopDisposed) return;
            _stop.Dispose();
            _stopDisposed = true;
        }
    }

    // Bounded diagnostic capture drains both pipes even after the retained text budget is reached.
    // The child holds its own OS lease: killing only the Host cannot release an active generator.
    private const string ReportProcessWrapper = """
        import os, sys, runpy
        entry, lease_path = sys.argv[1:3]
        lease = open(lease_path, 'a+b')
        try:
            if os.name == 'nt':
                import msvcrt
                if os.fstat(lease.fileno()).st_size == 0:
                    lease.write(b'0'); lease.flush()
                lease.seek(0)
                msvcrt.locking(lease.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(lease.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            print('report-generator-busy', file=sys.stderr)
            sys.exit(75)
        sys.argv = [entry] + sys.argv[3:]
        sys.path.insert(0, os.path.dirname(entry))
        try:
            runpy.run_path(entry, run_name='__main__')
        finally:
            lease.close()
        """;
}
