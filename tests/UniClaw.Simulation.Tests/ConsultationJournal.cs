using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-004（ABS-001/002）：Product Consult seam 消费侧的 sim-only 观测装饰
/// （probe object / decorator）。记录每次 consultation 的 Product
/// <see cref="AgentDecisionContext"/>（append-only transcript）与 seam 级纪律
/// （late-call / missing-run-correlation / consult-count-mismatch）。
/// 与 realization 无关：scripted double、注入的测试 realization、未来 DSH
/// conformance 同律——测试观测不再迫使 host / runner / digest 依赖任何
/// concrete realization，AgentConsultations 计数对注入 realization 同样成立
/// （消灭 agent 版「注入即 -1」洞）。
/// </summary>
internal sealed class ConsultationJournal
{
    private readonly Func<AgentDecisionContext, AgentDecision?> _realization;
    private readonly List<AgentDecisionContext> _calls = new();
    private readonly List<string> _violations = new();
    private bool _terminal;

    public ConsultationJournal(Func<AgentDecisionContext, AgentDecision?> realization)
    {
        _realization = realization ?? throw new ArgumentNullException(nameof(realization));
    }

    /// <summary>consultation transcript（append-only；Product context 原样记录）。</summary>
    public IReadOnlyList<AgentDecisionContext> Calls => _calls;

    /// <summary>seam 级纪律违规（late-call / missing-run-correlation）。</summary>
    public IReadOnlyList<string> Violations => _violations;

    /// <summary>seam 上的 consultation（realization 无关——任意决策面经此计数）。</summary>
    public AgentDecision? Consult(AgentDecisionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _calls.Add(ctx);

        if (_terminal)
        {
            // late-call 建模（原 ScriptedUniAgent.MarkTerminal 语义上移至 seam
            // 消费侧——run terminal 后任何 realization 的 consultation 都是违规）
            _violations.Add("late-call");
            return null;
        }
        if (ctx.RunId.Length == 0)
            _violations.Add("missing-run-correlation");

        return _realization(ctx);
    }

    /// <summary>run terminal 标记（Host.DriveOnce 在 terminal 后调用）。</summary>
    public void MarkTerminal() => _terminal = true;

    /// <summary>场景收尾核对：实际咨询数 vs 期望（realization 无关）。</summary>
    public IReadOnlyList<string> CheckExpectedConsultations(int expected) =>
        _calls.Count == expected
            ? Array.Empty<string>()
            : new[] { $"consult-count-mismatch:expected-{expected}:got-{_calls.Count}" };
}
