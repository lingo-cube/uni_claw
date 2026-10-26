// WORLD-VISUALIZATION-001 切片 1 原型导出器（change-scoped，非产品代码）。
// 经正式产品链（UniKernel + WorldModel）构造一个演示 session，用切片 0 的两个
// 只读缝（ProjectWorldTimeline / ProjectWorldGraph）导出可视化数据。
// 夹具模式沿 WorldKnowledgeGapTests / WorldSemanticReuseTests /
// WorldPageCorrectionTests（受控证据），输出 prototype/web/data.js。
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

var T0 = new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
const string Device = "demo-device";
const string Package = "com.android.settings";
const string HomeSig = Package + ":id/home_root";
const string WifiSig = Package + ":id/wifi_entry";
const string DisplaySig = Package + ":id/display_entry";
const string DetailSig = Package + ":id/display_detail";
const string Scope = "run:viz-demo";

var profile = new NativeContextSignatureProfile("demo-page-context:v1", Device, Package,
    UiHierarchyAcquirerKind.AndroidXUiAutomator, "androidx-uiautomator/1",
    [HomeSig, WifiSig, DisplaySig, DetailSig], new Dictionary<string, SemanticPageCandidate>
    { [HomeSig] = new("settings-home", "设置", "系统设置主页"),
      [WifiSig] = new("wifi-page", "Wi‑Fi", "无线网络设置") });

IReadOnlySet<string> worldScope = new HashSet<string>(StringComparer.Ordinal)
{
    "ui.screen", PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
    "ui.semantic.page", SemanticVerificationCodec.Subject, PageInterpretationCodec.Subject,
    "ui.node.*", ContainerContextCodec.Subject,
};
var world = new WorldModel(worldScope, new ProductAssociationStrategy(), nativeContextProfiles: [profile]);
var ledger = new EvidenceLedger();
var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance);

string Process(ObservationProposal proposal)
{
    var result = kernel.Process(proposal);
    if (result.Admission.Decision != AdmissionDecision.Accepted)
        throw new InvalidOperationException("demo evidence rejected: " + result.Admission.Decision);
    return result.Admission.EvidenceId!;
}

PageCaptureObservation Capture(string id, int minute, string rowText) =>
    new(id, "cycle:" + id, "demo-session", T0.AddMinutes(minute),
        CoordinateSpace.DeviceViewport(1080, 2160, captureId: id),
        [new("title", "text", "设置", new UiBounds(0, 0, 1000, 180)),
         new("row", "ui.element", rowText, new UiBounds(0, 300, 1000, 420))])
    { DeviceId = Device, Coverage = new(CoverageCompleteness.Partial, "viewport-export;offscreen-unproven") };

ObservationProposal CaptureProposal(PageCaptureObservation capture) =>
    new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
        ObservationContext.External, new("demo-recorded-layout", capture.CaptureTime,
            "capture:" + capture.CaptureId, ["demo-fixture:" + capture.CaptureId]));

ObservationProposal IdentityProposal(PageCaptureObservation capture, string source, string signature) =>
    new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
            capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
            signature, capture.Items[0].Text))), IngressKind.Observation, ObservationContext.External,
        new("demo-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["capture:" + capture.CaptureId]))
    { DerivedFromEvidenceIds = [source], TransitiveEvidenceBasis = new HashSet<string> { source },
      FusionRule = "controlled-page-identity", FusionVersion = "1" };

string Identify(PageCaptureObservation capture, string signature)
{
    var captureId = Process(CaptureProposal(capture));
    return Process(IdentityProposal(capture, captureId, signature)); // identity evidence id
}

// ---- Session 剧情 ----
var home1 = Capture("home-1", 1, "Wi‑Fi");
var home1Identity = Identify(home1, HomeSig);
var home1Revision = world.Current!.RevisionId;
var homeContainer = world.Current.CurrentPageObservation!.ContainerId!;

var wifi1 = Capture("wifi-1", 2, "Wi‑Fi");
var wifi1Identity = Identify(wifi1, WifiSig);
var wifi1Revision = world.Current!.RevisionId;
var wifiContainer = world.Current.CurrentPageObservation!.ContainerId!;

void Navigate(string from, string fromRevision, string fromIdentity, string to, string toRevision, string toIdentity)
{
    // 原型说明：RecordObservedNavigation 是 internal（产品路径经 Kernel post-action
    // 验证链进入）；原型经反射直调同一 World 登记口，数据仍走真实 World 路径。
    var navType = typeof(WorldModel).Assembly.GetType("UniClaw.Kernel.World.UiRealization.ObservedNavigation")!;
    var nav = Activator.CreateInstance(navType, fromRevision, from, fromIdentity, toRevision, to, toIdentity,
        Scope, "verification:post-action:verified");
    var method = typeof(WorldModel).GetMethod("RecordObservedNavigation",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    if (method.Invoke(world, [nav]) is not true)
    {
        string Anchor(string revisionId) => world.RevisionHistory.First(r => r.RevisionId == revisionId)
            .WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var claim) ? claim.Value : "none";
        throw new InvalidOperationException(
            $"navigation rejected: from={from}@{fromRevision}(anchor={Anchor(fromRevision)}) " +
            $"to={to}@{toRevision}(anchor={Anchor(toRevision)}) currentAnchor={Anchor(world.Current!.RevisionId)}");
    }
}
Navigate(homeContainer, home1Revision, home1Identity, wifiContainer, wifi1Revision, wifi1Identity);

var home2 = Capture("home-2", 3, "蓝牙");
var home2Identity = Identify(home2, HomeSig);
var home2Revision = world.Current!.RevisionId;
Navigate(wifiContainer, wifi1Revision, wifi1Identity, homeContainer, home2Revision, home2Identity); // 返回 → 导航环

UiNodeObservation Node(string captureId, int index, string resourceId, int? parent) => new(
    new OccurrenceRef(captureId, index), parent is { } p ? new OccurrenceRef(captureId, p) : null,
    null, index, null, ObservedValue<string>.Observed("android.view.View"),
    ObservedValue<string>.Observed(resourceId), ObservedValue<string>.Observed(Package),
    ObservedValue<string>.Observed("设置"), ObservedValue<string>.Unknown("missing"),
    ObservedValue<string>.Unknown("missing"), ObservedValue<bool>.Unknown("missing"),
    ObservedValue<CheckedState>.Unknown("missing"), ObservedValue<bool>.Observed(true),
    ObservedValue<bool>.Unknown("missing"), ObservedValue<bool>.Unknown("missing"),
    ObservedValue<bool>.Observed(false), ObservedValue<bool>.Observed(false),
    ObservedValue<bool>.Observed(false), ObservedValue<bool>.Observed(true),
    ObservedValue<bool>.Unknown("missing"), ObservedValue<UiBounds>.Observed(new(0, index * 300, 900, index * 300 + 250)));

// Contains 结构关系：wifi capture 的原生父子上下文锚。
var wifiHierarchy = new UiHierarchyObservation(
    new CaptureMetadata(wifi1.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
        "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, wifi1.CaptureTime,
        TimeSpan.FromMilliseconds(12), Device, wifi1.SessionCorrelation, wifi1.ObservationCycleId,
        new HierarchyCapabilities(HierarchyCapability.None),
        new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export"), wifi1.Space),
    [], [Node(wifi1.CaptureId, 0, HomeSig, null), Node(wifi1.CaptureId, 1, WifiSig, 0)]);
var anchors = NativeContextObservationProjector.Project(wifiHierarchy, profile, ObservationContext.External)
    .Select(p => ledger.CanonicalRecords[Process(p)]).ToArray();
var parentAnchor = anchors.Single(r => r.Claim.Subject.EndsWith("#0.resource_id", StringComparison.Ordinal));
var childAnchor = anchors.Single(r => r.Claim.Subject.EndsWith("#1.resource_id", StringComparison.Ordinal));
Process(ContainerContextCodec.CreateProposal(parentAnchor, childAnchor));

// 语义：home 页建立稳定语义 → 换采集清空 → 原生锚再对应 → 沿用（Reused）。
var home3 = Capture("home-3", 5, "Wi‑Fi");
var home3Evidence = Process(CaptureProposal(home3));
var home3Identity = Process(IdentityProposal(home3, home3Evidence, HomeSig));
var semanticRequest = new SemanticPerceptionRequest("meaning:" + home3.CaptureId,
    new CapabilityCorrelation("demo-session", captureId: home3.CaptureId, observationCycleId: home3.ObservationCycleId),
    new(home3.CaptureId, home3.ObservationCycleId, home3.SessionCorrelation, home3.CaptureTime,
        home3.Space, home3.Items, [home3Evidence]),
    [new SemanticPageCandidate("settings-home", "设置", "系统设置主页")]);
var semanticProposal = SemanticObservationProjector.CreateProposals(semanticRequest,
    SemanticPerceptionResult.Observed(semanticRequest, "settings-home", home3.Items[0].Text)).Single();
var semanticEvidence = Process(semanticProposal);
UiNodeObservation SemanticNode(string captureId, int index, string resourceId, string? text, int? parent) => new(
    new OccurrenceRef(captureId, index), parent is { } p ? new OccurrenceRef(captureId, p) : null,
    null, index, null, ObservedValue<string>.Observed("android.view.View"),
    ObservedValue<string>.Observed(resourceId), ObservedValue<string>.Observed(Package),
    ObservedValue<string>.Observed(text ?? ""), ObservedValue<string>.Unknown("missing"),
    ObservedValue<string>.Unknown("missing"), ObservedValue<bool>.Unknown("missing"),
    ObservedValue<CheckedState>.Unknown("missing"), ObservedValue<bool>.Observed(true),
    ObservedValue<bool>.Unknown("missing"), ObservedValue<bool>.Unknown("missing"),
    ObservedValue<bool>.Observed(false), ObservedValue<bool>.Observed(true),
    ObservedValue<bool>.Observed(true), ObservedValue<bool>.Observed(true),
    ObservedValue<bool>.Unknown("missing"), ObservedValue<UiBounds>.Observed(new(0, index * 300, 900, index * 300 + 250)));

// 沿 ReuseTests.AddTypedSources：锚/包来源一律经官方 projector 构造。
string TypedAnchor(PageCaptureObservation capture, string signature)
{
    var metadata = new CaptureMetadata(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
        "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, capture.CaptureTime,
        TimeSpan.FromMilliseconds(10), Device, capture.SessionCorrelation, capture.ObservationCycleId,
        new HierarchyCapabilities(HierarchyCapability.SemanticText),
        new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export-only"), capture.Space);
    var observation = new UiHierarchyObservation(metadata, [],
        [SemanticNode(capture.CaptureId, 0, signature, capture.Items[0].Text, null),
         SemanticNode(capture.CaptureId, 1, Package + ":id:row", capture.Items[1].Text, 0)]);
    var proposals = NativeContextObservationProjector.Project(observation, profile, ObservationContext.External);
    // 锚与包提案都要入账：World 的 NativeAnchorMatches 会核对同节点 package 记录。
    string anchorId = "";
    foreach (var proposal in proposals)
    {
        var id = Process(proposal);
        if (proposal.Claim.Subject == "ui.node." + capture.CaptureId + "#0.resource_id") anchorId = id;
    }
    return anchorId;
}
var anchor3 = TypedAnchor(home3, HomeSig);
var verificationProposal = NativeContextObservationProjector.VerifySemantic(
    ledger.CanonicalRecords[semanticEvidence], ledger.CanonicalRecords[home3Evidence],
    ledger.CanonicalRecords[home3Identity], ledger.CanonicalRecords[anchor3], profile);
if (verificationProposal is null) throw new InvalidOperationException("semantic verification projector refused demo sources");
var verificationEvidence = Process(verificationProposal);
foreach (var decision in world.SemanticVerificationLog)
    Console.WriteLine($"semantic-verification {decision.EvidenceId[..12]} applied={decision.Applied} reason={decision.Reason}");
_ = verificationEvidence;

var home4 = Capture("home-4", 7, "显示"); Identify(home4, HomeSig); // 语义知识清空（当前锚未对应）
_ = TypedAnchor(home4, HomeSig); // 原生锚再对应 → Reused=true 沿用

// 纠错：一个误归属 capture（identity 错指 home）被原生上下文纠正到 wifi。
var mystery = Capture("mystery-1", 9, "Wi‑Fi");
var mysteryEvidence = Process(CaptureProposal(mystery));
var wrongIdentity = Process(IdentityProposal(mystery, mysteryEvidence, HomeSig));
var mysteryAnchor = TypedAnchor(mystery, WifiSig); // 原生锚证明它其实是 Wi‑Fi 页
var correction = new PageInterpretationCorrection(mysteryEvidence, wrongIdentity,
    new(mystery.CaptureId, mystery.ObservationCycleId, mystery.SessionCorrelation,
        mystery.CaptureTime, WifiSig, "Wi‑Fi"), [mysteryAnchor], "demo-page-context:v1");
Process(new ObservationProposal(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)),
    IngressKind.Observation, ObservationContext.External,
    new Provenance("history-interpreter", mystery.CaptureTime, "reinterpret:" + mysteryEvidence, ["demo-fixture"]))
    { DerivedFromEvidenceIds = [mysteryEvidence, mysteryAnchor],
      TransitiveEvidenceBasis = new HashSet<string> { mysteryEvidence, mysteryAnchor },
      FusionRule = "page.interpretation", FusionVersion = "1" });
Console.WriteLine($"correction applied: {world.PageCorrectionLog[^1].Applied} ({world.PageCorrectionLog[^1].Reason})");

// ---- 复杂多轮扩展（复杂视图验证：仓库无含纠错的真实录制，按用户指示构造）----
// 覆盖：三层导航、导航环、同页第三轮重访、双稳定语义、Contains×2、
// 纠错②（接受：误归 display-detail → wifi）、纠错③（拒绝：旧解释不匹配）。

// display 家族（第三层）。
var display1 = Capture("display-1", 11, "显示");
var display1Identity = Identify(display1, DisplaySig);
var display1Revision = world.Current!.RevisionId;
var displayContainer = world.Current.CurrentPageObservation!.ContainerId!;
Navigate(homeContainer, home2Revision, home2Identity, displayContainer, display1Revision, display1Identity);

var detail1 = Capture("display-detail-1", 12, "亮度");
var detail1Identity = Identify(detail1, DetailSig);
var detail1Revision = world.Current!.RevisionId;
var detailContainer = world.Current.CurrentPageObservation!.ContainerId!;
Navigate(displayContainer, display1Revision, display1Identity, detailContainer, detail1Revision, detail1Identity);

// 同容器第二轮重访（返回上一层 → 导航环）。注意：不能用 home 做重访——
// 纠错①撤回了仅由误解释支撑的 home signature，此后 HomeSig 不再匹配旧容器
// （产品语义：被撤销解释支撑的签名不参与未来匹配），home5 会被判成新容器。
var display2 = Capture("display-2", 13, "显示");
var display2Identity = Identify(display2, DisplaySig);
var display2Revision = world.Current!.RevisionId;
Navigate(detailContainer, detail1Revision, detail1Identity, displayContainer, display2Revision, display2Identity);

// 第二条 Contains：detail capture 的原生父上下文锚指向 display 页。不能用
// home 作父端——纠错①已撤回仅由误解释支撑的 home signature，World 对 Contains
// 要求两端 signature 唯一对应既有 Container（纠错涟漪的真实语义）。
var detailHierarchy = new UiHierarchyObservation(
    new CaptureMetadata(detail1.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
        "androidx-uiautomator/1", UiHierarchyFormat.AccessibilityTree, detail1.CaptureTime,
        TimeSpan.FromMilliseconds(12), Device, detail1.SessionCorrelation, detail1.ObservationCycleId,
        new HierarchyCapabilities(HierarchyCapability.None),
        new HierarchyCoverage(CoverageCompleteness.Partial, "viewport-export"), detail1.Space),
    [], [Node(detail1.CaptureId, 0, DisplaySig, null), Node(detail1.CaptureId, 1, DetailSig, 0)]);
var detailAnchors = NativeContextObservationProjector.Project(detailHierarchy, profile, ObservationContext.External)
    .Select(p => ledger.CanonicalRecords[Process(p)]).ToArray();
var displayAnchor2 = detailAnchors.Single(r => r.Claim.Subject.EndsWith("#0.resource_id", StringComparison.Ordinal));
var detailAnchor2 = detailAnchors.Single(r => r.Claim.Subject.EndsWith("#1.resource_id", StringComparison.Ordinal));
Process(ContainerContextCodec.CreateProposal(displayAnchor2, detailAnchor2));

// wifi 页建立自己的稳定语义（第二个 profile meaning）。
var wifi2 = Capture("wifi-2", 15, "Wi‑Fi");
var wifi2Evidence = Process(CaptureProposal(wifi2));
var wifi2Identity = Process(IdentityProposal(wifi2, wifi2Evidence, WifiSig));
var wifiSemanticRequest = new SemanticPerceptionRequest("meaning:" + wifi2.CaptureId,
    new CapabilityCorrelation("demo-session", captureId: wifi2.CaptureId, observationCycleId: wifi2.ObservationCycleId),
    new(wifi2.CaptureId, wifi2.ObservationCycleId, wifi2.SessionCorrelation, wifi2.CaptureTime,
        wifi2.Space, wifi2.Items, [wifi2Evidence]),
    [new SemanticPageCandidate("wifi-page", "Wi‑Fi", "无线网络设置")]);
var wifiSemanticEvidence = Process(SemanticObservationProjector.CreateProposals(wifiSemanticRequest,
    SemanticPerceptionResult.Observed(wifiSemanticRequest, "wifi-page", wifi2.Items[0].Text)).Single());
var wifi2Anchor = TypedAnchor(wifi2, WifiSig);
var wifiVerification = NativeContextObservationProjector.VerifySemantic(
    ledger.CanonicalRecords[wifiSemanticEvidence], ledger.CanonicalRecords[wifi2Evidence],
    ledger.CanonicalRecords[wifi2Identity], ledger.CanonicalRecords[wifi2Anchor], profile);
if (wifiVerification is null) throw new InvalidOperationException("wifi semantic verification projector refused demo sources");
_ = Process(wifiVerification);
var lastDecision = world.SemanticVerificationLog[^1];
Console.WriteLine($"semantic-verification {lastDecision.EvidenceId[..12]} applied={lastDecision.Applied} reason={lastDecision.Reason}");

// 纠错②（接受）：误归 display-detail 的 capture 被原生锚纠正到 wifi。
var mystery2 = Capture("mystery-2", 17, "Wi‑Fi");
var mystery2Evidence = Process(CaptureProposal(mystery2));
var wrong2 = Process(IdentityProposal(mystery2, mystery2Evidence, DetailSig));
var mystery2Anchor = TypedAnchor(mystery2, WifiSig);
var correction2 = new PageInterpretationCorrection(mystery2Evidence, wrong2,
    new(mystery2.CaptureId, mystery2.ObservationCycleId, mystery2.SessionCorrelation,
        mystery2.CaptureTime, WifiSig, "Wi‑Fi"), [mystery2Anchor], "demo-page-context:v1");
Process(new ObservationProposal(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction2)),
    IngressKind.Observation, ObservationContext.External,
    new Provenance("history-interpreter", mystery2.CaptureTime, "reinterpret:" + mystery2Evidence, ["demo-fixture"]))
    { DerivedFromEvidenceIds = [mystery2Evidence, mystery2Anchor],
      TransitiveEvidenceBasis = new HashSet<string> { mystery2Evidence, mystery2Anchor },
      FusionRule = "page.interpretation", FusionVersion = "1" });
Console.WriteLine($"correction2 applied: {world.PageCorrectionLog[^1].Applied} ({world.PageCorrectionLog[^1].Reason})");

// 纠错③（拒绝）：目标真实但旧解释不匹配（wrong-previous-identity 模式）。
var mystery3 = Capture("mystery-3", 19, "显示");
var mystery3Evidence = Process(CaptureProposal(mystery3));
_ = Process(IdentityProposal(mystery3, mystery3Evidence, DisplaySig));
var mystery3Anchor = TypedAnchor(mystery3, DisplaySig);
var correction3 = new PageInterpretationCorrection(mystery3Evidence, "missing-identity-evidence",
    new(mystery3.CaptureId, mystery3.ObservationCycleId, mystery3.SessionCorrelation,
        mystery3.CaptureTime, DisplaySig, "显示"), [mystery3Anchor], "demo-page-context:v1");
Process(new ObservationProposal(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction3)),
    IngressKind.Observation, ObservationContext.External,
    new Provenance("history-interpreter", mystery3.CaptureTime, "reinterpret:" + mystery3Evidence, ["demo-fixture"]))
    { DerivedFromEvidenceIds = [mystery3Evidence, mystery3Anchor],
      TransitiveEvidenceBasis = new HashSet<string> { mystery3Evidence, mystery3Anchor },
      FusionRule = "page.interpretation", FusionVersion = "1" });
Console.WriteLine($"correction3 applied: {world.PageCorrectionLog[^1].Applied} reason={world.PageCorrectionLog[^1].Reason}");

// ---- 真实录制段（Recent corpus：真实设备 XML 布局 + 受控身份替代；截图
// 按 captureId 精确映射 evidence PNG，无截图帧不伪造，回退页面表达重绘）。
// 教训（诚实记录）：录制段必须用独立 world——混合 world 下 corpus 的受控签名
// 会被 observed-title-layout 关联策略跨来源误匹配到合成容器。----
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));
IReadOnlySet<string> recordedScope = new HashSet<string>(StringComparer.Ordinal)
{ PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject };
var recordedWorld = new WorldModel(recordedScope, new ProductAssociationStrategy());
var recordedLedger = new EvidenceLedger();
var recordedKernel = new UniKernel(recordedLedger, recordedWorld, DisabledRunTrace.Instance);
string RecordedProcess(ObservationProposal proposal)
{
    var result = recordedKernel.Process(proposal);
    if (result.Admission.Decision != AdmissionDecision.Accepted)
        throw new InvalidOperationException("recorded evidence rejected: " + result.Admission.Decision);
    return result.Admission.EvidenceId!;
}
var corpus = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot,
    "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json"))).RootElement;
var corpusWidth = corpus.GetProperty("width").GetInt32();
var corpusHeight = corpus.GetProperty("height").GetInt32();
var webAssets = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web")), "assets");
Directory.CreateDirectory(webAssets);
var screenshotByCapture = new Dictionary<string, string>(StringComparer.Ordinal);

foreach (var caseElement in corpus.GetProperty("cases").EnumerateArray())
{
    var caseId = caseElement.GetProperty("id").GetString()!;
    foreach (var side in new[] { "before", "after" })
    {
        var source = caseElement.GetProperty(side);
        var importedPath = caseElement.GetProperty("imported").GetProperty(side).GetProperty("path").GetString()!;
        var signature = caseElement.GetProperty("identityInput").GetProperty(side).GetProperty("signature").GetString()!;
        var imported = JsonSerializer.Deserialize<ImportedShape>(File.ReadAllText(Path.Combine(repoRoot, importedPath)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var meta = imported.Observation.Metadata;
        var items = new List<SemanticLayoutItem>();
        foreach (var node in imported.Observation.Nodes)
        {
            if (!node.Bounds.TryGetObserved(out var bounds) || !bounds.IsValid
                || bounds.X1 < 0 || bounds.Y1 < 0 || bounds.X2 > corpusWidth || bounds.Y2 > corpusHeight) continue;
            var text = node.Text.TryGetObserved(out var t) && !string.IsNullOrWhiteSpace(t) ? t
                : node.ContentDescription.TryGetObserved(out var d) ? d : null;
            if (!string.IsNullOrWhiteSpace(text))
                items.Add(new("text:" + node.OccurrenceRef.LocalIndex, "text", text, bounds));
        }
        foreach (var (candidate, index) in imported.Candidates.Select((c, i) => (c, i)))
        {
            if (candidate.Locator is not { } locator || string.IsNullOrWhiteSpace(candidate.SemanticDescriptor)) continue;
            items.Add(new("candidate:" + index, candidate.Role, candidate.SemanticDescriptor, new(
                (int)Math.Round(locator.X1 * corpusWidth), (int)Math.Round(locator.Y1 * corpusHeight),
                (int)Math.Round(locator.X2 * corpusWidth), (int)Math.Round(locator.Y2 * corpusHeight))));
        }
        var recorded = new PageCaptureObservation(meta.CaptureId, meta.ObservationCycleId!, meta.SessionCorrelation,
            meta.CaptureTimestamp, meta.Space!, items) { DeviceId = meta.DeviceId };
        Console.WriteLine($"recorded case={caseId}/{side} capture={meta.CaptureId} items={items.Count}");
        var recordedEvidence = RecordedProcess(CaptureProposal(recorded));
        RecordedProcess(IdentityProposal(recorded, recordedEvidence, signature));
        var recordedPage = recordedWorld.Current!.CurrentPageObservation;
        Console.WriteLine($"recorded case={caseId}/{side} container={(recordedPage?.ContainerId ?? "UNATTRIBUTED")}");
        // 截图精确映射：evidence/**/capture-<id>.png 存在才挂；拷至 web/assets。
        var hit = Directory.EnumerateFiles(Path.Combine(repoRoot, "evidence"), meta.CaptureId + ".png", SearchOption.AllDirectories).FirstOrDefault();
        if (hit is not null)
        {
            File.Copy(hit, Path.Combine(webAssets, meta.CaptureId + ".png"), overwrite: true);
            screenshotByCapture[recordedEvidence] = "assets/" + meta.CaptureId + ".png";
        }
    }
}
Console.WriteLine($"real screenshots attached: {screenshotByCapture.Count}");
Console.WriteLine($"synthetic containers: {world.Current!.Containers!.Count}, recorded containers: {recordedWorld.Current!.Containers!.Count}");

// ---- 经切片 0 只读缝导出（两个独立数据集：合成复杂 session + 真实录制 session）----
var webDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "web"));
Directory.CreateDirectory(webDir);
async Task ExportSet(UniKernel k, string provenance, string fileName, Dictionary<string, string> shots)
{
    var exportTimeline = k.ProjectWorldTimeline();
    var exportGraphs = new Dictionary<string, object>();
    foreach (var revision in exportTimeline.Revisions)
    {
        var graph = k.ProjectWorldGraph(Scope, revision.RevisionId)!;
        var labels = graph.NodeLabels.ToDictionary(l => l.ContainerId, StringComparer.Ordinal);
        exportGraphs[revision.RevisionId] = new
        {
            revisionId = graph.SourceRevisionId,
            currentContainerId = graph.CurrentContainerId,
            unknown = graph.Unknown,
            nodes = graph.Nodes.Select(n =>
            {
                var view = k.ProjectWorldPage(n.Identity.ContainerId, revision.RevisionId);
                var observation = view.Observations.LastOrDefault(o => o.IsCurrentCapture)
                    ?? view.Observations.LastOrDefault();
                object? thumb = observation is null ? null : new
                {
                    width = observation.Capture.Space.PixelWidth,
                    height = observation.Capture.Space.PixelHeight,
                    captureId = observation.CaptureEvidenceId,
                    isCurrent = observation.IsCurrentCapture,
                    screenshot = shots.TryGetValue(observation.CaptureEvidenceId, out var shot) ? shot : null,
                    items = observation.Occurrences.Take(14).Select(o => new
                    {
                        role = o.Role,
                        text = (o.SemanticDescriptor ?? "").Length > 12
                            ? o.SemanticDescriptor![..12] : o.SemanticDescriptor,
                        state = o.State,
                        x1 = o.Locator?.X1, y1 = o.Locator?.Y1,
                        x2 = o.Locator?.X2, y2 = o.Locator?.Y2,
                    }),
                    viewUnknown = view.Unknown,
                };
                return new
                {
                    id = n.Identity.ContainerId,
                    label = labels.TryGetValue(n.Identity.ContainerId, out var l) ? l.PageName : null,
                    labelUnknown = !labels.ContainsKey(n.Identity.ContainerId),
                    conflicted = labels.TryGetValue(n.Identity.ContainerId, out var l2) && l2.Conflicted,
                    reused = labels.TryGetValue(n.Identity.ContainerId, out var l3) && l3.Reused,
                    signatureWithdrawn = graph.SignatureWithdrawnContainers.Contains(n.Identity.ContainerId),
                    knowledgeSource = labels.TryGetValue(n.Identity.ContainerId, out var l4) ? l4.KnowledgeSourceRevisionId : null,
                    evidenceRefs = labels.TryGetValue(n.Identity.ContainerId, out var l5) ? l5.EvidenceRefs : [],
                    pageThumb = thumb,
                };
            }),
            navigationEdges = graph.Edges.Select(e => new { kind = "navigatesTo", source = e.SourceContainerId, target = e.TargetContainerId, scope = e.Scope }),
            structuralRelations = graph.StructuralRelations.Select(e => new { kind = e.Kind.ToString().ToLowerInvariant(), source = e.SourceContainerId, target = e.TargetContainerId }),
        };
    }
    var payload = new { scope = Scope, provenance, timeline = exportTimeline, graphs = exportGraphs };
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(webDir, fileName),
        "window.__WORLD_VIZ_DATA__ = " + json + ";\n", new System.Text.UTF8Encoding(false));
    Console.WriteLine($"exported {exportTimeline.Revisions.Count} revisions → web/{fileName}");
}
await ExportSet(kernel, "controlled-complex-fixture（受控复杂演示录制，经正式 UniKernel/WorldModel 链导出）", "data.js", new Dictionary<string, string>());
await ExportSet(recordedKernel, "recorded-device-xml（Recent 真实设备 XML 录制 + 受控身份替代；实拍截图按 captureId 精确映射）", "data-recorded.js", screenshotByCapture);
foreach (var s in kernel.ProjectWorldTimeline().Revisions)
    Console.WriteLine($"  {s.RevisionId} #{s.RevisionNumber} capture={s.LatestCaptureTime:HH:mm} " +
        $"page={s.Changes.PageObservation} nav={s.Changes.NavigationVerified} corr={s.Changes.CorrectionApplied}/{s.Changes.CorrectionRejected} " +
        $"sem={s.Changes.SemanticKnowledgeUpdated}/reused={s.Changes.SemanticKnowledgeReused} struct=[{string.Join(",", s.Changes.StructuralRelationChanges)}]");

file record ImportedShape(UiHierarchyObservation Observation, ProposedOccurrence[] Candidates);
