using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 semantic digest：一次场景运行全部语义观察的 canonical rendering
/// 的 SHA-256（culture-invariant、ordinal 排序；绝不包含 wall-clock / latency /
/// 机器身份）。同 bundle 同 digest = 确定性 replay 的核心验收。
/// </summary>
internal static class SemanticDigest
{
    public static string Of(
        SimulationHost host, ScriptedUniAgent agent, ScenarioStimulusFeed feed,
        RunDriveResult result, GoalEvaluation? evaluation)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(result);

        var lines = new List<string>
        {
            "schema=sem-digest-v1",
            "stimuli.consumed=" + string.Join(",", feed.Consumed),
            "stimuli.unconsumed=" + string.Join(",", feed.Remaining),
            "stimuli.unexpected=" + string.Join(",", feed.Unexpected),
            "run=" + host.KernelCore.RunId
                + ":cycles=" + (host.KernelCore.RunState?.Progress.Cycles ?? 0).ToString(CultureInfo.InvariantCulture)
                + ":acts=" + (host.KernelCore.RunState?.Progress.Acts ?? 0).ToString(CultureInfo.InvariantCulture),
        };

        // revisions：逐 revision 的 WorldState 快照（subject=value@evidenceId，ordinal 排序）
        foreach (var revision in host.WorldCore.RevisionHistory)
        {
            var entries = revision.WorldState
                .Select(kv => kv.Key + "=" + kv.Value.Value + "@" + kv.Value.EvidenceId)
                .OrderBy(s => s, StringComparer.Ordinal);
            lines.Add("revisions=" + revision.RevisionId + "{" + string.Join(",", entries) + "}");
        }

        foreach (var receipt in host.KernelCore.EffectReceipts)
            lines.Add("receipts=" + receipt.ReceiptId + ":" + receipt.Outcome);

        foreach (var judgment in host.AssuranceCore.JudgmentLog)
            lines.Add("judgments=" + judgment.IntentId + ":" + judgment.BindingId + ":" + judgment.IsAdmissible);

        foreach (var proof in host.AssuranceCore.OutcomeProofLog)
            lines.Add("proofs=" + proof.ProofId + ":" + proof.Classification);

        foreach (var call in agent.Calls)
            lines.Add("agent=" + call.DecisionId + ":" + call.Phase);

        lines.Add(result.Outcome is null
            ? "outcome=none"
            : "outcome=" + result.Outcome.Classification + ":" + string.Join(",", result.Outcome.Obligations
                .Select(o => o.ObligationId + "=" + (o.Satisfied ? "satisfied" : "unsatisfied"))
                .OrderBy(s => s, StringComparer.Ordinal)));

        lines.Add(evaluation is null
            ? "goal=none"
            : "goal=" + evaluation.EvaluationId + ":" + evaluation.Satisfaction);

        var canonical = string.Join("\n", lines);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
