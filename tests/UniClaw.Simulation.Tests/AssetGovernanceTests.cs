using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HumanPromotionAuthority = UniClaw.Simulation.Tests.FileSystemGovernanceStore.HumanPromotionAuthority;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// ABG-001 验收 1–9：改名不变性（Acceptance 1）、关键 metadata 检索 +
/// 显式 Unknown（2）、fail-closed 四臂零部分注册（3）、显式 Human 晋升 +
/// 证据门（4）、断言修订 v2 不覆盖 v1（5）、authority 能力分离（6）、
/// sealed Trace 只经 Importer（7）。Acceptance 8/9（产品闭包零接触 / 全量
/// 回归）由套件结果承载：本文件只落在 tests/UniClaw.Simulation.Tests，
/// ProductHostClosureTests 与其余套件必须继续全绿。
/// </summary>
public sealed class AssetGovernanceTests
{
    private sealed record StackHandle(GovernanceStack Stack, string Root) : IDisposable
    {
        public void Dispose() => Stack.Dispose();
    }

    private static StackHandle CreateStack()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-abg001-" + Guid.NewGuid().ToString("N"));
        return new StackHandle(GovernanceStack.Create(root), root);
    }

    /// <summary>bundle 语境 index：OsVersion 如实、AppBuild sentinel → null（显式 Unknown）、kernel hash。</summary>
    private static UiSystemIndexFields IndexOf(MinimalScenarioBundle bundle) => new(
        bundle.TargetUiSystem.OsVersion,
        bundle.TargetUiSystem.AppBuild == "not-recorded" ? null : bundle.TargetUiSystem.AppBuild,
        bundle.RuntimeArtifact.KernelAssemblySha256);

    private static byte[] GoldenAssetBytes(BundleAssetEntry entry) =>
        File.ReadAllBytes(Path.Combine(GoldenPaths.RepoRoot(), GoldenPaths.BundleRoot, entry.RelativePath));

    private static string BlobPath(string root, string sha) =>
        Path.Combine(root, "captures", sha[..2], sha + ".bytes");

    /// <summary>
    /// 真实两遍 digest 证据（honesty 约束）：对 bundle 实际跑两次
    /// ScenarioRunner，取两次 SemanticDigest——相等本身就是证据，不伪造。
    /// </summary>
    private static PromotionEvidence RealEvidence(
        MinimalScenarioBundle bundle, string bundleContentSha256, string claimId, string reviewedBy = "human-reviewer")
    {
        var digest1 = ScenarioRunner.Run(bundle).Report.SemanticDigest;
        var digest2 = ScenarioRunner.Run(bundle).Report.SemanticDigest;
        Assert.NotEmpty(digest1);
        Assert.Equal(digest1, digest2);
        return new PromotionEvidence
        {
            ScenarioId = bundle.ScenarioId,
            SourceBundle = bundle,
            BundleContentSha256 = bundleContentSha256,
            ClaimId = claimId,
            DigestRun1 = digest1,
            DigestRun2 = digest2,
            PerCycleZero = true,
            TraceArmsEquivalent = true,
            HumanReviewedBy = reviewedBy,
        };
    }

    // ---- Acceptance 1：改名不变性 + 幂等注册 ----

    [Fact]
    public void RenameInvariance_SameBytes_SameIdentity_IdempotentRegistration()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var asset = bundle.Assets.Single(a => a.AssetId == "golden-v1-case-b-off-perception");
        var bytes = GoldenAssetBytes(asset);
        using var handle = CreateStack();
        var stack = handle.Stack;
        var index = IndexOf(bundle);

        // 同 bytes 两个物理位置（不同文件名）
        var dir = Path.Combine(Path.GetTempPath(), "uniclaw-abg001-rename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var pathA = Path.Combine(dir, "case-b-off-copy.json");
        var pathB = Path.Combine(dir, "renamed-elsewhere.bin");
        File.WriteAllBytes(pathA, bytes);
        File.WriteAllBytes(pathB, bytes);

        var first = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("perception-response", bytes, Array.Empty<string>(), pathA), index);
        var second = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("perception-response", bytes, Array.Empty<string>(), pathB), index);

        // identity 只由 bytes 决定（D2）：与 golden manifest 声明 hash 同源
        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(BundleAssetFiles.HashOf(asset.RelativePath), first.ContentSha256);

        // 幂等：registry 恰一条记录，既有记录不被改写（identity 断言；path hint 非身份）
        Assert.Equal(1, stack.Authority.RegisteredCount);
        Assert.Equal(first, stack.Authority.Get(first.ContentSha256));
    }

    // ---- Acceptance 2：关键 metadata 检索 + 显式 Unknown ----

    [Fact]
    public void KeyMetadata_Retrievable_UnknownExplicit()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var index = IndexOf(bundle);

        var bundleRecord = stack.Authority.RegisterBundle(bundle, index);
        var perception = bundle.Assets.Single(a => a.AssetId == "golden-v1-case-b-off-perception");
        var perceptionSha = BundleAssetFiles.HashOf(perception.RelativePath);

        // UI system 版本检索：命中 perception 资产记录与 bundle 记录
        var byOs = stack.Authority.FindByOsVersion(bundle.TargetUiSystem.OsVersion);
        Assert.Contains(byOs, r => r.ContentSha256 == perceptionSha);
        Assert.Contains(byOs, r => r.ContentSha256 == bundleRecord.ContentSha256);

        // Runtime artifact hash 检索：命中 bundle 记录
        var byArtifact = stack.Authority.FindByRuntimeArtifact(bundle.RuntimeArtifact.KernelAssemblySha256);
        Assert.Contains(byArtifact, r => r.ContentSha256 == bundleRecord.ContentSha256);

        // lineage parent 检索：perception 资产是 bundle 记录的直系 parent
        var byLineage = stack.Authority.FindByLineageParent(perceptionSha);
        Assert.Contains(byLineage, r => r.ContentSha256 == bundleRecord.ContentSha256);

        // app build：sentinel "not-recorded" = 显式 Unknown 查询——只命中
        // Index.AppBuild 为 null 的记录（D3）；具体版本查询返回空而非推断。
        Assert.Null(bundleRecord.Index.AppBuild);
        var unknownQuery = stack.Authority.FindByAppBuild("not-recorded");
        Assert.NotEmpty(unknownQuery);
        Assert.All(unknownQuery, r => Assert.Null(r.Index.AppBuild));
        Assert.Empty(stack.Authority.FindByAppBuild("33.0.1"));
    }

    [Fact]
    public void SameContent_FromTwoEnvironments_HasOneIdentityAndBothOccurrences()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var asset = bundle.Assets[0];
        var bytes = GoldenAssetBytes(asset);
        using var handle = CreateStack();
        var stack = handle.Stack;
        var firstIndex = new UiSystemIndexFields("android-14", "build-a", "runtime-a");
        var secondIndex = new UiSystemIndexFields("android-15", "build-b", "runtime-b");

        var first = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit(asset.Kind, bytes, Array.Empty<string>(), "first-name"), firstIndex);
        var second = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit(asset.Kind, bytes, Array.Empty<string>(), "renamed"), secondIndex);

        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(1, stack.Authority.RegisteredCount);
        Assert.Contains(stack.Authority.FindByOsVersion("android-14"), r => r.ContentSha256 == first.ContentSha256);
        Assert.Contains(stack.Authority.FindByOsVersion("android-15"), r => r.ContentSha256 == first.ContentSha256);
        Assert.Contains(stack.Authority.FindByAppBuild("build-b"), r => r.ContentSha256 == first.ContentSha256);
        Assert.Contains(stack.Authority.FindByRuntimeArtifact("runtime-b"), r => r.ContentSha256 == first.ContentSha256);

        using var reopened = new StackHandle(GovernanceStack.Create(handle.Root), handle.Root);
        Assert.Contains(reopened.Stack.Authority.FindByOsVersion("android-15"), r => r.ContentSha256 == first.ContentSha256);
    }

    [Fact]
    public void MissingIndexedVersions_AreExplicitUnknown_NotConcreteMatches()
    {
        using var handle = CreateStack();
        var stack = handle.Stack;
        var record = stack.Authority.Register(stack.Producer,
            stack.Producer.Submit("raw-capture", new byte[] { 9, 8, 7 }, Array.Empty<string>()),
            new UiSystemIndexFields(null, null, null));

        Assert.Contains(stack.Authority.FindByOsVersion("not-recorded"), r => r.ContentSha256 == record.ContentSha256);
        Assert.Contains(stack.Authority.FindByAppBuild("not-recorded"), r => r.ContentSha256 == record.ContentSha256);
        Assert.Contains(stack.Authority.FindByRuntimeArtifact("not-recorded"), r => r.ContentSha256 == record.ContentSha256);
        Assert.Empty(stack.Authority.FindByOsVersion("android-15"));
        Assert.Empty(stack.Authority.FindByRuntimeArtifact("runtime-b"));
    }

    // ---- Acceptance 3：fail-closed 四臂（首因 + 零部分注册）----

    [Fact]
    public void FailClosed_MissingBytes_WrongHash_BrokenLineage_WrongSchema()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var index = IndexOf(bundle);
        var perception = bundle.Assets.Single(a => a.AssetId == "golden-v1-case-b-off-perception");
        var screenshot = bundle.Assets.Single(a => a.AssetId == "golden-v1-case-b-off-screenshot");
        using var handle = CreateStack();
        var stack = handle.Stack;

        var good = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("detectorResult", GoldenAssetBytes(perception), Array.Empty<string>()), index);
        var good2 = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("rawScreenshot", GoldenAssetBytes(screenshot), Array.Empty<string>()), index);
        var baseline = stack.Authority.RegisteredCount;

        // 臂 1：bytes-missing（blob 被移走后完整性门）
        File.Delete(BlobPath(handle.Root, good.ContentSha256));
        var missing = Assert.Throws<AssetGovernanceException>(
            () => stack.Authority.VerifyIntegrity(good.ContentSha256));
        Assert.StartsWith("bytes-missing", missing.Message);

        // 臂 2：hash-mismatch（blob 被篡改后完整性门；首因含 expected:actual）
        File.WriteAllBytes(BlobPath(handle.Root, good2.ContentSha256), new byte[] { 1, 2, 3 });
        var tampered = Assert.Throws<AssetGovernanceException>(
            () => stack.Authority.VerifyIntegrity(good2.ContentSha256));
        Assert.StartsWith("hash-mismatch", tampered.Message);
        Assert.Contains(good2.ContentSha256, tampered.Message);

        // 臂 3：lineage-parent-unknown（parent 未注册；任何写入之前拒绝）
        var brokenLineage = Assert.Throws<AssetGovernanceException>(() => stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("rawScreenshot", GoldenAssetBytes(screenshot), new[] { "deadbeefdeadbeef" }), index));
        Assert.StartsWith("lineage-parent-unknown", brokenLineage.Message);

        // 臂 4：schema-unknown（持久化文件 schemaVersion 未知 → 读侧 fail closed）
        string schemaRoot;
        using (var fresh = CreateStack())
        {
            fresh.Stack.Authority.Register(
                fresh.Stack.Producer, fresh.Stack.Producer.Submit("detectorResult", GoldenAssetBytes(perception), Array.Empty<string>()), index);
            schemaRoot = fresh.Root;
        }
        var indexPath = Path.Combine(schemaRoot, "registry", "index.json");
        File.WriteAllText(indexPath, File.ReadAllText(indexPath).Replace(
            "\"schemaVersion\":\"1\"", "\"schemaVersion\":\"99\""));
        var wrongSchema = Assert.Throws<AssetGovernanceException>(() => GovernanceStack.Create(schemaRoot));
        Assert.Equal("schema-unknown:99", wrongSchema.Message);

        // 零部分注册：四臂全部失败后 registry 计数不变
        Assert.Equal(baseline, stack.Authority.RegisteredCount);
    }

    [Fact]
    public void IdempotentReregistration_RejectsCorruptedStoredBytes()
    {
        using var handle = CreateStack();
        var stack = handle.Stack;
        var bytes = new byte[] { 4, 5, 6 };
        var index = new UiSystemIndexFields("android-14", null, "runtime-a");
        var first = stack.Authority.Register(stack.Producer,
            stack.Producer.Submit("raw-capture", bytes, Array.Empty<string>()), index);
        File.WriteAllBytes(BlobPath(handle.Root, first.ContentSha256), new byte[] { 0 });

        var failure = Assert.Throws<AssetGovernanceException>(() => stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("raw-capture", bytes, Array.Empty<string>()), index));
        Assert.StartsWith("hash-mismatch:", failure.Message);
        Assert.Equal(1, stack.Authority.RegisteredCount);
    }

    [Fact]
    public void RegisterBundle_LateMissingAsset_DoesNotPartiallyRegisterEarlierAssets()
    {
        var original = GoldenScenarioBundles.WifiToggleOffToOn();
        var assets = original.Assets.ToArray();
        Assert.True(assets.Length > 1);
        assets[1] = assets[1] with { RelativePath = "missing-abg001-late-asset.bin" };
        var invalid = ScenarioBundleDigest.Sealed(original with { Assets = assets });
        using var handle = CreateStack();

        var failure = Assert.Throws<AssetGovernanceException>(() =>
            handle.Stack.Authority.RegisterBundle(invalid, IndexOf(invalid)));

        Assert.StartsWith("bytes-missing:", failure.Message);
        Assert.Equal(0, handle.Stack.Authority.RegisteredCount);
    }

    [Fact]
    public void RegisterBundle_RegistryPublishFailure_LeavesNoRegisteredIdentity()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        Directory.CreateDirectory(Path.Combine(handle.Root, "registry", "index.json"));

        var failure = Record.Exception(() =>
            handle.Stack.Authority.RegisterBundle(bundle, IndexOf(bundle)));

        Assert.NotNull(failure);
        Assert.Equal(0, handle.Stack.Authority.RegisteredCount);
    }

    [Fact]
    public void RegisteredBundle_IdentityAddressesFullReplayInput_NotDigestText()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var record = handle.Stack.Authority.RegisterBundle(bundle, IndexOf(bundle));

        var bytes = handle.Stack.Authority.ReadContentBytes(record.ContentSha256);
        var actualSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(actualSha, record.ContentSha256);
        using var json = JsonDocument.Parse(bytes);
        Assert.Equal(bundle.BundleId, json.RootElement.GetProperty("bundleId").GetString());
        Assert.Equal(bundle.BundleDigest, json.RootElement.GetProperty("bundleDigest").GetString());
        var firstStimulus = json.RootElement.GetProperty("stimuli")[0].GetProperty("payload");
        Assert.Equal(
            ((ScenarioStimulus.ObservationFrame)bundle.Stimuli[0]).PerceptionArtifactId,
            firstStimulus.GetProperty("perceptionArtifactId").GetString());
        Assert.Equal("switch.wifi",
            firstStimulus.GetProperty("reviewedStateClaims")[0].GetProperty("item1").GetString());
    }

    [Fact]
    public void PersistedBundle_ReloadsAndRedrivesTheSameSemanticDigest()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var registered = handle.Stack.Authority.RegisterBundle(bundle, IndexOf(bundle));
        using var reopened = new StackHandle(GovernanceStack.Create(handle.Root), handle.Root);

        var restored = reopened.Stack.Authority.LoadBundle(registered.ContentSha256);
        Assert.Equal(bundle.BundleDigest, restored.BundleDigest);
        Assert.Equal(bundle.Stimuli.Count, restored.Stimuli.Count);
        Assert.Equal(ScenarioRunner.Run(bundle).Report.SemanticDigest,
            ScenarioRunner.Run(restored).Report.SemanticDigest);
    }

    [Fact]
    public void RegisterBundle_ConflictingIndexedVersion_IsRejectedBeforeWrites()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var falseIndex = IndexOf(bundle) with { TargetOsVersion = "unrelated-os" };

        var failure = Assert.Throws<AssetGovernanceException>(() =>
            handle.Stack.Authority.RegisterBundle(bundle, falseIndex));

        Assert.StartsWith("metadata-mismatch:target-os-version", failure.Message);
        Assert.Equal(0, handle.Stack.Authority.RegisteredCount);
    }

    // ---- Acceptance 4：Baseline 只能显式晋升（证据门）----

    [Fact]
    public void Baseline_RequiresExplicitHumanPromotion_AndEvidence()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var bundleRecord = stack.Authority.RegisterBundle(bundle, IndexOf(bundle));
        var claim = stack.Assertions.AttachExpected(bundleRecord.ContentSha256, bundle.Expected, "human-reviewer");

        // 无晋升调用 → 无 baseline 记录
        var noBaseline = Assert.Throws<AssetGovernanceException>(
            () => stack.Baselines.ResolveLatest(bundle.ScenarioId));
        Assert.StartsWith("baseline-unknown", noBaseline.Message);

        // 证据缺失（两遍 digest 未提供）→ 首因拒绝
        var incomplete = new PromotionEvidence
        {
            ScenarioId = bundle.ScenarioId,
            BundleContentSha256 = bundleRecord.ContentSha256,
            ClaimId = claim.ClaimId,
            PerCycleZero = true,
            TraceArmsEquivalent = true,
            HumanReviewedBy = "human-reviewer",
        };
        var rejected = Assert.Throws<AssetGovernanceException>(
            () => stack.Baselines.Promote(stack.Promoter, stack.Authority, stack.Assertions, incomplete));
        Assert.StartsWith("promotion-evidence-incomplete:digest-run1", rejected.Message);
        Assert.StartsWith("baseline-unknown",
            Assert.Throws<AssetGovernanceException>(() => stack.Baselines.ResolveLatest(bundle.ScenarioId)).Message);

        // 完整证据（真实两遍运行 digest）→ v1 记录存在
        var v1 = stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(bundle, bundleRecord.ContentSha256, claim.ClaimId));
        Assert.Equal("v1", v1.VersionTag);
        Assert.Equal(bundleRecord.ContentSha256, v1.BundleSha256);
        Assert.Equal(claim.ClaimId, v1.ClaimId);
        Assert.Equal(v1, stack.Baselines.ResolveLatest(bundle.ScenarioId));
    }

    [Fact]
    public void Promotion_MismatchedScenarioOrReplayDigest_IsRejected()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var bundleRecord = stack.Authority.RegisterBundle(bundle, IndexOf(bundle));
        var claim = stack.Assertions.AttachExpected(bundleRecord.ContentSha256, bundle.Expected, "human-reviewer");
        var real = RealEvidence(bundle, bundleRecord.ContentSha256, claim.ClaimId);

        var wrongScenario = Assert.Throws<AssetGovernanceException>(() => stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions, real with { ScenarioId = "unrelated-scenario" }));
        Assert.StartsWith("scenario-mismatch:", wrongScenario.Message);

        var wrongReplay = Assert.Throws<AssetGovernanceException>(() => stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            real with { DigestRun1 = "fabricated", DigestRun2 = "fabricated" }));
        Assert.StartsWith("digest-source-mismatch", wrongReplay.Message);

        Assert.StartsWith("baseline-unknown:",
            Assert.Throws<AssetGovernanceException>(() => stack.Baselines.ResolveLatest(bundle.ScenarioId)).Message);
    }

    [Fact]
    public void Promotion_RejectsMissingTransitiveLineageAncestor()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var index = IndexOf(bundle);
        var ancestor = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("raw-capture", new byte[] { 71, 72, 73 }, Array.Empty<string>()), index);
        var firstAsset = bundle.Assets[0];
        stack.Authority.Register(stack.Producer,
            stack.Producer.Submit(firstAsset.Kind, GoldenAssetBytes(firstAsset), new[] { ancestor.ContentSha256 }), index);
        var bundleRecord = stack.Authority.RegisterBundle(bundle, index);
        var claim = stack.Assertions.AttachExpected(bundleRecord.ContentSha256, bundle.Expected, "human-reviewer");
        File.Delete(BlobPath(handle.Root, ancestor.ContentSha256));

        var failure = Assert.Throws<AssetGovernanceException>(() => stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(bundle, bundleRecord.ContentSha256, claim.ClaimId)));

        Assert.StartsWith("bytes-missing:" + ancestor.ContentSha256, failure.Message);
    }

    // ---- Acceptance 5：断言修订 → v2，永不覆盖 v1 ----

    [Fact]
    public void AssertionRevision_ProducesV2_NeverOverlaysV1()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var bundleRecord = stack.Authority.RegisterBundle(bundle, IndexOf(bundle));
        var claimA = stack.Assertions.AttachExpected(bundleRecord.ContentSha256, bundle.Expected, "human-reviewer-1");
        var v1 = stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(bundle, bundleRecord.ContentSha256, claimA.ClaimId, "human-reviewer-1"));
        var v1Snapshot = stack.Baselines.ResolveVersion(bundle.ScenarioId, "v1");

        // 真正改变断言：已满足场景为 0 effect；新 bundle/version 承载新输入与期望。
        var alreadyOn = GoldenScenarioBundles.AlreadyOnZeroEffect();
        var revisedBundle = ScenarioBundleDigest.Sealed(alreadyOn with
        {
            BundleId = bundle.BundleId,
            BundleVersion = "v2",
            ScenarioId = bundle.ScenarioId,
            ScenarioVersion = "v2",
        });
        var revisedRecord = stack.Authority.RegisterBundle(revisedBundle, IndexOf(revisedBundle));
        Assert.NotEqual(bundle.Expected, revisedBundle.Expected);

        // 断言修订：新 claim（新 claimId，引用旧 claim）；旧 claim 原样可解析。
        var claimB = stack.Assertions.ReviseExpected(
            revisedRecord.ContentSha256,
            revisedBundle.Expected,
            "human-reviewer-2");
        Assert.NotEqual(claimA.ClaimId, claimB.ClaimId);
        Assert.Equal(claimA.ClaimId, claimB.SupersedesClaimId);
        Assert.Equal(claimA, stack.Assertions.GetClaim(claimA.ClaimId));

        // v2 晋升（新证据：再次真实两遍运行）
        var v2 = stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(revisedBundle, revisedRecord.ContentSha256, claimB.ClaimId, "human-reviewer-2"));
        Assert.Equal("v2", v2.VersionTag);
        Assert.Equal(v2, stack.Baselines.ResolveLatest(bundle.ScenarioId));

        // v1 仍可解析且晋升字段内容不变（supersededBy 是 registry 链指针）
        var v1After = stack.Baselines.ResolveVersion(bundle.ScenarioId, "v1");
        Assert.Equal(v1Snapshot.BaselineId, v1After.BaselineId);
        Assert.Equal(v1Snapshot.BundleSha256, v1After.BundleSha256);
        Assert.Equal(v1Snapshot.ClaimId, v1After.ClaimId);
        Assert.Equal(v1Snapshot.VersionTag, v1After.VersionTag);
        Assert.Equal(v1Snapshot.PromotedBy, v1After.PromotedBy);
        Assert.Equal(v1Snapshot.PromotedAtVirtualTime, v1After.PromotedAtVirtualTime);
        Assert.Equal(v2.BaselineId, v1After.SupersededBy);
        stack.Baselines.AssertNoOverlay(bundle.ScenarioId);

        // 覆盖尝试：同 claim + 同 bundle 再次晋升 → baseline-version-exists:v1
        var overlay = Assert.Throws<AssetGovernanceException>(() => stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(bundle, bundleRecord.ContentSha256, claimA.ClaimId, "human-reviewer-1")));
        Assert.StartsWith("baseline-version-exists:v1", overlay.Message);
        stack.Baselines.AssertNoOverlay(bundle.ScenarioId);
    }

    [Fact]
    public void FailedV2Publish_DoesNotExposeAnUnpersistedBaseline()
    {
        var v1Bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var v1Record = stack.Authority.RegisterBundle(v1Bundle, IndexOf(v1Bundle));
        var firstClaim = stack.Assertions.AttachExpected(v1Record.ContentSha256, v1Bundle.Expected, "reviewer-1");
        var v1 = stack.Baselines.Promote(stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(v1Bundle, v1Record.ContentSha256, firstClaim.ClaimId, "reviewer-1"));

        var alreadyOn = GoldenScenarioBundles.AlreadyOnZeroEffect();
        var v2Bundle = ScenarioBundleDigest.Sealed(alreadyOn with
        {
            BundleId = v1Bundle.BundleId,
            BundleVersion = "v2",
            ScenarioId = v1Bundle.ScenarioId,
            ScenarioVersion = "v2",
        });
        var v2Record = stack.Authority.RegisterBundle(v2Bundle, IndexOf(v2Bundle));
        var secondClaim = stack.Assertions.ReviseExpected(v2Record.ContentSha256, v2Bundle.Expected, "reviewer-2");
        Directory.CreateDirectory(Path.Combine(handle.Root, "baselines", v1Bundle.ScenarioId + ".json.tmp"));

        Assert.NotNull(Record.Exception(() => stack.Baselines.Promote(
            stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(v2Bundle, v2Record.ContentSha256, secondClaim.ClaimId, "reviewer-2"))));
        Assert.Equal(v1, stack.Baselines.ResolveLatest(v1Bundle.ScenarioId));
        using var reopened = new StackHandle(GovernanceStack.Create(handle.Root), handle.Root);
        Assert.Equal(v1, reopened.Stack.Baselines.ResolveLatest(v1Bundle.ScenarioId));
    }

    [Fact]
    public void ResolveVersion_RejectsTamperedSealedBaselineWithoutExplicitAuditCall()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var registered = stack.Authority.RegisterBundle(bundle, IndexOf(bundle));
        var claim = stack.Assertions.AttachExpected(registered.ContentSha256, bundle.Expected, "reviewer-original");
        stack.Baselines.Promote(stack.Promoter, stack.Authority, stack.Assertions,
            RealEvidence(bundle, registered.ContentSha256, claim.ClaimId, "reviewer-original"));

        var path = Path.Combine(handle.Root, "baselines", bundle.ScenarioId + ".json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("reviewer-original", "reviewer-mutated"));
        using var reopened = new StackHandle(GovernanceStack.Create(handle.Root), handle.Root);

        var failure = Assert.Throws<AssetGovernanceException>(() =>
            reopened.Stack.Baselines.ResolveVersion(bundle.ScenarioId, "v1"));
        Assert.StartsWith("baseline-overlay-detected:", failure.Message);
    }

    [Fact]
    public void FailedClaimIndexPublish_DoesNotExposeAnOrphanRevision()
    {
        var firstBundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var firstRecord = stack.Authority.RegisterBundle(firstBundle, IndexOf(firstBundle));
        var original = stack.Assertions.AttachExpected(firstRecord.ContentSha256, firstBundle.Expected, "reviewer-1");
        var alreadyOn = GoldenScenarioBundles.AlreadyOnZeroEffect();
        var revisedBundle = ScenarioBundleDigest.Sealed(alreadyOn with
        {
            BundleId = firstBundle.BundleId,
            BundleVersion = "v2",
            ScenarioId = firstBundle.ScenarioId,
            ScenarioVersion = "v2",
        });
        var revisedRecord = stack.Authority.RegisterBundle(revisedBundle, IndexOf(revisedBundle));
        Directory.CreateDirectory(Path.Combine(handle.Root, "claims", "index.json.tmp"));

        Assert.NotNull(Record.Exception(() => stack.Assertions.ReviseExpected(
            revisedRecord.ContentSha256, revisedBundle.Expected, "reviewer-2")));
        Assert.StartsWith("claim-unknown:", Assert.Throws<AssetGovernanceException>(() =>
            stack.Assertions.GetClaim("claim-002")).Message);
        Assert.Equal(original, stack.Assertions.GetClaim(original.ClaimId));
    }

    // ---- Acceptance 6：authority 能力分离（能力面互斥可断言）----

    [Fact]
    public void AuthoritySeparation_NoCrossCapabilityPath()
    {
        static IReadOnlyList<string> Verbs(Type type) =>
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => m.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        // 每个角色的能力动词恰为自身职责（多余动词 = 越权面）
        Assert.Equal(new[] { "Submit" }, Verbs(typeof(CaptureProducer)));
        Assert.Equal(new[] { "AttachExpected", "GetClaim", "ReviseExpected" }, Verbs(typeof(ScenarioAssertionOwner)));
        Assert.Equal(new[] { "Promote" }, Verbs(typeof(HumanPromotionAuthority)));
        Assert.Equal(
            new[] { "FindByAppBuild", "FindByLineageParent", "FindByOsVersion", "FindByRuntimeArtifact", "Get", "LoadBundle", "ReadContentBytes", "Register", "RegisterBundle", "VerifyIntegrity" },
            Verbs(typeof(AssetGovernanceAuthority)));
        Assert.Equal(
            new[] { "AssertNoOverlay", "Promote", "ResolveLatest", "ResolveVersion" },
            Verbs(typeof(BaselineRegistry)));

        // BaselineRecord 的唯一铸造者 = HumanPromotionAuthority（BaselineRegistry
        // 纯委托）；其余三方不存在任何返回 BaselineRecord 的方法（无晋升路径）
        var roles = new[] { typeof(CaptureProducer), typeof(AssetGovernanceAuthority), typeof(ScenarioAssertionOwner) };
        foreach (var role in roles)
            Assert.False(role.GetMethods().Any(m => m.ReturnType == typeof(BaselineRecord)),
                role.Name + " 不得存在 baseline 铸造路径");
        Assert.Equal(typeof(BaselineRecord),
            typeof(HumanPromotionAuthority).GetMethod("Promote", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.ReturnType);
        Assert.Null(typeof(GovernanceStack).GetProperty("Store", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.True(typeof(FileSystemGovernanceStore).GetMethod("CreateBaseline", BindingFlags.Instance | BindingFlags.NonPublic)!.IsPrivate);
    }

    // ---- Acceptance 7：sealed Trace 只经 ScenarioImporter（D8/P26）----

    [Fact]
    public void SealedTrace_EntersGovernance_OnlyViaImporter()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        using var handle = CreateStack();
        var stack = handle.Stack;
        var index = IndexOf(bundle);

        // 未封存 trace（Quarantined）作为 opaque bytes 可入内容寻址存储（存储
        // 通用性），但这不赋予其任何 stimulus 资产语义
        var waiting = ScenarioRunner.Run(
            GoldenScenarioBundles.MissingPostActionStimulus(), new RunOptions { TraceArm = TraceArm.Enabled });
        Assert.NotNull(waiting.Host.TraceScope);
        var quarantined = waiting.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(UniClaw.Kernel.Trace.RecorderTerminal.Quarantined, quarantined.RecorderTerminal);
        var opaqueTraceBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            quarantined.RunId,
            Terminal = quarantined.RecorderTerminal.ToString(),
        });
        var traceRecord = stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("trace-events", opaqueTraceBytes, Array.Empty<string>()), index);
        Assert.Equal("trace-events", traceRecord.Kind);
        Assert.StartsWith("bundle-kind-mismatch:", Assert.Throws<AssetGovernanceException>(() =>
            stack.Assertions.AttachExpected(traceRecord.ContentSha256, bundle.Expected, "human-reviewer")).Message);

        // Quarantined artifact 不能派生 stimulus（P26 门在治理语境中重申）
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(GoldenScenarioBundles.MissingPostActionStimulus(), quarantined));

        // 把 Trace Event 当资产语义注册 → 拒绝（D8：stimulus 只经 Importer）
        var asAssetSemantics = Assert.Throws<AssetGovernanceException>(() => stack.Authority.Register(
            stack.Producer, stack.Producer.Submit("scenario-stimulus-direct-from-trace-events", opaqueTraceBytes, Array.Empty<string>()),
            index));
        Assert.StartsWith("stimulus-not-registerable", asAssetSemantics.Message);

        // 直连路径不存在：治理面任何类型都没有 stimulus 注册动词
        var governanceTypes = new[]
        {
            typeof(CaptureProducer), typeof(AssetGovernanceAuthority), typeof(ScenarioAssertionOwner),
            typeof(HumanPromotionAuthority), typeof(BaselineRegistry), typeof(FileSystemGovernanceStore),
            typeof(GovernanceStack),
        };
        foreach (var type in governanceTypes)
            Assert.False(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(m => m.Name.Contains("stimulus", StringComparison.OrdinalIgnoreCase)),
                type.Name + " 不得存在 stimulus 直连注册路径");

        // 合法入口仍通：Finalized artifact 经 Importer 派生 stimuli 入治理语境
        var completed = ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Enabled });
        var sealedArtifact = completed.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(UniClaw.Kernel.Trace.RecorderTerminal.Finalized, sealedArtifact.RecorderTerminal);
        var imported = ScenarioImporter.Derive(bundle, sealedArtifact);
        Assert.NotEmpty(imported.Bundle.Stimuli);
    }
}
