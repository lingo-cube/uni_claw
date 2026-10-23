using System.Text.Json;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class WorldKnowledgeConsultationTests
{
    [Fact]
    public void EachConsultationReadsCurrentOwnerGaps_AndEarlierContextRemainsFixed()
    {
        var time = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        var capture = new PageCaptureObservation("consult-c1", "cycle-c1", "session", time,
            CoordinateSpace.DeviceViewport(100, 100, captureId: "consult-c1"),
            [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
             new("button", "button", "Wi-Fi", new UiBounds(0, 20, 90, 50))]);
        var captureProposal = new ObservationProposal(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External, new("controlled-layout", time, "viewport", ["fixture:c1"]));
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new ProductAssociationStrategy());
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new ForbiddenDriver());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new Fresh()), effects);
        kernel.Process(captureProposal);
        var contexts = new List<JsonElement>();
        var reads = 0;
        var inputs = new RunDriverInputs
        {
            NextInput = _ =>
            {
                if (++reads == 1) return new RunDriverInput.Interruption(new("gap-review", "world-review", 1));
                if (reads > 2) return null;
                var source = world.Current!.CurrentPageObservation!.EvidenceId;
                var identity = new ObservationProposal(new(PageObservationCodec.IdentitySubject,
                    PageObservationCodec.Encode(new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
                        capture.SessionCorrelation, capture.CaptureTime, "settings", "Settings"))),
                    IngressKind.Observation, ObservationContext.External, new("controlled-identity", time, "viewport", ["fixture:identity"]))
                { DerivedFromEvidenceIds = [source], TransitiveEvidenceBasis = new HashSet<string> { source },
                  FusionRule = "controlled-page-identity", FusionVersion = "1" };
                return new RunDriverInput.Observation([identity]);
            },
            ConsultAgent = context =>
            {
                contexts.Add(JsonSerializer.SerializeToElement(context, new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
                return contexts.Count == 1 ? new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, 1)) : null;
            }
        };
        Assert.True(kernel.AdmitContract(new("v1", "inspect settings",
            new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new("pending", RunObligationKind.MaterialEffect, "test.pending", "done", true)],
            TaskInstanceId: "task-gaps", ProductSessionId: "session-gaps", ContractRef: "contract-gaps")).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);
        var result = driver.Drive();

        Assert.True(contexts.Count == 2, $"consultations={contexts.Count}; reads={reads}; result={result.Status}/{result.Reason}");
        Assert.True(contexts[0].TryGetProperty("worldKnowledge", out var first), "formal context must contain World-derived gaps");
        Assert.Equal(3, first.GetProperty("gaps").GetArrayLength());
        var second = contexts[1].GetProperty("worldKnowledge");
        Assert.Single(second.GetProperty("gaps").EnumerateArray());
        Assert.NotEqual(first.GetProperty("sourceRevisionId").GetString(), second.GetProperty("sourceRevisionId").GetString());
        Assert.Empty(effects.ReceiptLog);
        Assert.Equal(3, first.GetProperty("gaps").GetArrayLength());
    }

    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "fixture");
    }
    private sealed class ForbiddenDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("zero Effect expected");
    }
}
