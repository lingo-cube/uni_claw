using System.Reflection;
using UniClaw.Core;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Core;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Tests.Perception;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// PROTOCOL-FIX-004 S4 验收锚定 —— CanonicalBinding 去执行材料（P10
/// 收件人是 Assurance，不需要执行坐标；locator 唯一载体归位
/// DeliveryTarget）。三个锚：A1 形状快照（三字段零残留）；A2 Core 投影
/// 改源（DeliveryTarget）后输出字节等价（同输入对照，D2）；A3/A4
/// lowering 从 BindingView 取 locator（行为零变化 + DeliveryTarget 唯一
/// 来源）。
/// </summary>
public sealed class CanonicalBindingLocatorNarrowingTests
{
    // ---- A1：形状快照锚定（三字段零残留） --------------------------------

    [Fact]
    public void CanonicalBinding_ShapeSnapshot_HasNoLocatorFields()
    {
        var names = typeof(CanonicalBinding).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToArray();

        // grep 探针（反射级）：三字段零残留
        Assert.DoesNotContain("TargetLocator", names);
        Assert.DoesNotContain("TargetNative", names);
        Assert.DoesNotContain("TargetSpace", names);

        // 形状快照：全量公开属性集（有意的收窄——新增/删除都必须显式改本快照）
        Assert.Equal(
            new[]
            {
                "BindingId", "IntentId", "EffectClass", "TargetSubject",
                "TargetValue", "RevisionId", "RevisionNumber",
                "TargetOccurrenceId", "OwningContainerId", "LogicalItemId",
            },
            names);
    }

    // ---- A2：Core 投影改源（DeliveryTarget）输出字节等价（D2） -----------

    [Fact]
    public void CoreProjection_FromDeliveryTarget_ByteEquivalentToLegacyBindingSource()
    {
        // 同输入对照锚：与迁移前 binding.TargetNative / binding.TargetLocator
        // 路径产出的字面量逐一相等（CoreProjectionSeamTests.LocatorMaterial
        // 同源数字，D2 字节等价）。
        var native = CoreSemanticProjection.ProjectTargetBinding(
            Bind("bind-n"), new CoreId("slice:x"), new CoreId("seg:x"), "rev-1",
            target: new DeliveryTarget(
                "occ-n",
                Native: NativeLocator.BrowserNode("node-42")));
        var spatial = CoreSemanticProjection.ProjectTargetBinding(
            Bind("bind-s"), new CoreId("slice:x"), new CoreId("seg:x"), "rev-1",
            target: new DeliveryTarget(
                "occ-s",
                Spatial: new SpatialLocator(0.1, 0.2, 0.3, 0.4, "device-viewport")));
        var bare = CoreSemanticProjection.ProjectTargetBinding(
            Bind("bind-b"), new CoreId("slice:x"), new CoreId("seg:x"), "rev-1",
            target: new DeliveryTarget("occ-b"));

        Assert.Equal("browser.backend-node-id", native.LocatorKind);
        Assert.Equal("node-42", native.LocatorKey);
        Assert.Equal("spatial:device-viewport", spatial.LocatorKind);
        Assert.Equal("0.1000,0.2000,0.3000,0.4000", spatial.LocatorKey);
        Assert.Equal("none", bare.LocatorKind);
        Assert.Equal("", bare.LocatorKey);
    }

    // ---- A3/A4：lowering 从 view 取 locator；DeliveryTarget 唯一来源 ------

    [Fact]
    public void Lowering_TakesLocatorFromBindingView_DeliveryTargetIsOnlyCarrier()
    {
        var locator = new SpatialLocator(0.044, 0.072, 0.955, 0.097, "device-viewport");
        var space = new CoordinateSpace("device-viewport:1080x1920@0", 1080, 1920,
            ScreenRotation.None, captureId: "cap-1");
        var view = new BindingView(
            "rev-1", 1, HasTargetSubjectClaim: false, HasTargetOccurrence: true,
            TargetOccurrenceLocator: locator,
            TargetOccurrenceNative: NativeLocator.BrowserNode("node-9"),
            TargetOccurrenceSpace: space);
        var driver = new CapturingDriver();
        var boundary = new EffectBoundary(driver);
        var intent = new ControlIntent("intent-1", ControlIntentKind.Act, "tap",
            "menu.row", "rev-1");
        var decision = boundary.Bind(intent,
            CandidateBinding.ForUiTarget(new UiTargetReference("occ-1", "rev-1")), view);

        var canonical = decision.Canonical!;
        var (gate, receipt) = boundary.Dispatch(
            canonical,
            new AssuranceJudgment("intent-1", canonical.BindingId, "rev-1",
                IsAdmissible: true, Checks: Array.Empty<AssuranceCheck>(),
                RejectionReason: null,
                Freshness: new FreshnessJudgment(FreshnessSufficiency.Sufficient, "test:sufficient")),
            view);

        Assert.Null(decision.RejectionReason);
        Assert.True(gate.Allowed);
        Assert.NotNull(receipt);

        // DeliveryTarget 是 locator 唯一载体：driver 收到的地址与 view 逐项相等
        var request = Assert.Single(driver.Requests);
        Assert.Equal("occ-1", request.Target.OccurrenceReference);
        Assert.Same(locator, request.Target.Spatial);
        Assert.Same(view.TargetOccurrenceNative, request.Target.Native);
        Assert.Same(space, request.Target.Space);
    }

    private static CanonicalBinding Bind(string bindingId) =>
        new(bindingId, "intent-x", "tap", "occ-x", "v", "rev-1", 1, TargetOccurrenceId: "occ-x");

    private sealed class CapturingDriver : IEffectDriver
    {
        public List<DispatchRequest> Requests { get; } = new();

        public DispatchResult Deliver(DispatchRequest request)
        {
            Requests.Add(request);
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "test:delivered",
                new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        }
    }
}
