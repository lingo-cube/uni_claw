using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// HOST-001 v0 确定性外部缝（spec v0.3 §2，显式命名的 dev-profile 件；
/// 2026-09-20 仿真方针：外部组件先仿真，跑通核心模型+能力接口）：
/// 帧源（屏幕身份 + 内容双 claim）、occurrence 派生、agent 咨询、
/// 确定性投递驱动、虚拟时钟。换入路径：live 感知 / ADB / 智能 agent
/// 各自是已登记的后续 change。
/// </summary>
public static class V0Runtime
{
    public const string ScreenId = "screen-1";
    public static readonly DateTimeOffset T0 = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>虚拟时钟：每帧推进一分钟——组合根持有时间权威（freshness 注入先例）。</summary>
    public sealed class VirtualClock
    {
        private long _ticks;
        public DateTimeOffset Now => T0.AddMinutes(_ticks);
        public void Tick() => _ticks++;
    }

    /// <summary>v0 帧契约：{"role":"switch","state":"off","b":[x1,y1,x2,y2]}（归一化坐标）。</summary>
    public sealed class FrameFeed
    {
        private readonly VirtualClock _clock;
        private int _phase;

        public FrameFeed(VirtualClock clock) => _clock = clock;

        public RunDriverInput? Next(ObservationContext expected)
        {
            switch (_phase)
            {
                case 0 when expected == ObservationContext.External:
                    _phase = 1;
                    return Frame("off", expected);
                case 1 when expected == ObservationContext.PostActionEffectFlow:
                    _phase = 2;
                    return Frame("on", expected, includeStateClaim: true);
                default:
                    return null; // 合法等待
            }
        }

        private RunDriverInput Frame(string state, ObservationContext context, bool includeStateClaim = false)
        {
            _clock.Tick();
            var frame = $"{{\"role\":\"switch\",\"state\":\"{state}\",\"b\":[0.40,0.50,0.60,0.70]}}";
            var proposals = new List<ObservationProposal>();
            // 屏幕身份 claim（stable value → ProductAssociationStrategy Matched，不铸新容器）
            proposals.Add(new ObservationProposal(
                new ObservationClaim(ProductAssociationStrategy.ScreenIdentitySubject, ScreenId),
                IngressKind.Observation, context,
                new Provenance("host.v0", _clock.Now, "scope:ui.screen", new[] { $"v0:screen:{ScreenId}" })));
            if (includeStateClaim)
                proposals.Add(new ObservationProposal(
                    new ObservationClaim("switch.state", state),
                    IngressKind.Observation, context,
                    new Provenance("host.v0", _clock.Now, "scope:switch.state", new[] { $"v0:state:{state}" })));
            // 内容 claim 放批尾：occurrence 是 revision-local（逐条证据重派生），
            // 最终 revision 必须承载 occurrence——post-action 唯一目标验证才有对象
            proposals.Add(new ObservationProposal(
                new ObservationClaim("screen.frame", frame),
                IngressKind.Observation, context,
                new Provenance("host.v0", _clock.Now, "scope:screen.frame", new[] { $"v0:frame:{state}" })));
            return new RunDriverInput.Observation(proposals);
        }
    }

    /// <summary>内容帧 → occurrence（owner = previous revision 的唯一根容器；非帧 claim 不派生）。</summary>
    public sealed class FrameOccurrenceStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (record.Claim.Subject != "screen.frame")
                return Array.Empty<ProposedOccurrence>();

            using var document = JsonDocument.Parse(record.Claim.Value);
            var entry = document.RootElement;
            var bounds = entry.GetProperty("b").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var owner = previous?.Containers.Count == 1
                ? previous.Containers[0].Identity.ContainerId
                : null;
            return new[]
            {
                new ProposedOccurrence(
                    OwningContainerId: owner,
                    Role: entry.GetProperty("role").GetString()!,
                    SemanticDescriptor: null,
                    State: entry.GetProperty("state").GetString(),
                    Locator: new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3], "v0.frame")),
            };
        }
    }

    /// <summary>v0 咨询：单步「tap switch 至 on」（Golden-Path 形状；智能升级=后续 change）。</summary>
    public static AgentDecision Consult(AgentDecisionContext context) => new AgentDecision.Act(
        new AgentActionProposal(
            context.DecisionId,
            new[] { new AgentActionStep("switch", TargetDescriptor: null, EffectClass: "tap", DesiredState: "on") },
            Justification: "v0-single-step-goal"));
}
