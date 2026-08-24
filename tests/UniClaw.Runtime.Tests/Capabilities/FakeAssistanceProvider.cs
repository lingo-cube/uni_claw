using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Capabilities;

/// <summary>
/// Test-side fake for the L1 CONSULT seam (External Contract Plane 3 — Assistance).
/// Deterministic responder; records every consult for assertions. Never a real
/// model (project convention: "fake 在测试侧").
/// </summary>
public sealed class FakeAssistanceProvider : IAssistanceProvider
{
    private readonly Func<AssistanceContext, AssistanceAdvice?> _responder;
    private readonly Func<AssistanceContext, Exception?>? _throwing = null;

    public FakeAssistanceProvider(Func<AssistanceContext, AssistanceAdvice?> responder)
        => _responder = responder;

    /// <summary>Responder that throws for every consult (consult-failure path).</summary>
    public static FakeAssistanceProvider Throwing()
        => new(_ => throw new InvalidOperationException("simulated consult failure"));

    /// <summary>Number of ConsultAsync invocations.</summary>
    public int Consults { get; private set; }

    /// <summary>Every received context (correlation / world-version assertions).</summary>
    public List<AssistanceContext> Received { get; } = [];

    public Task<AssistanceAdvice?> ConsultAsync(AssistanceContext context, CancellationToken cancellationToken)
    {
        Consults++;
        Received.Add(context);
        if (_throwing is not null)
        {
            var ex = _throwing(context);
            if (ex is not null)
            {
                throw ex;
            }
        }

        return Task.FromResult(_responder(context));
    }
}
