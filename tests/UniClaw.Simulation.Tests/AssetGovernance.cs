using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HumanPromotionAuthority = UniClaw.Simulation.Tests.FileSystemGovernanceStore.HumanPromotionAuthority;

namespace UniClaw.Simulation.Tests;

// ============================================================================
// ABG-001 — Minimal Bundle v0 Asset Governance & Baseline Promotion（Phase 3
// 治理半边，test-side）。全部类型 internal，永不进入产品程序集（D1）。
//
// 语义来源（不重锁，只执行）：
//  - roadmap §7.2.1：Content Identity 只由 bytes 计算；关键字段缺失 → 显式
//    Unknown（sentinel "not-recorded" → null），不得推断。
//  - roadmap §7.2.1 责任四分：Capture Producer / Asset Governance Authority /
//    Scenario Assertion Owner / Human Promotion Authority——authority records
//    不合并，能力面互斥（可反射断言）。
//  - roadmap §7.3：晋升证据（两遍 digest 一致、非逐 cycle、Trace 三臂一致、
//    Human-reviewed）；Baseline 更新不覆盖：v2 新版本，v1 保留。
//  - 协议 P26 / D8：sealed Trace 只能经 ScenarioImporter 派生 stimulus；
//    治理 store 拒绝把 Trace Event 当资产语义注册。
// ============================================================================

/// <summary>治理层 fail-closed 载体（D7：typed reason，只携带首因，不降级）。</summary>
internal sealed class AssetGovernanceException(string message) : Exception(message);

/// <summary>
/// Content Identity = bytes SHA-256（D2）。path 只是当前物理位置 hint——
/// 改名/移动不改 identity（store 按 identity 寻址）。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed record GovernanceCaptureRecord(
    string ContentSha256,
    string Kind,
    string? CurrentPathHint,
    DateTimeOffset RegisteredAtVirtualTime,
    IReadOnlyList<string> LineageParents,
    UiSystemIndexFields Index);

/// <summary>
/// D3 可检索维度（目标 UI system 版本 / app build / Runtime artifact）。
/// null = 显式 Unknown（关键字段缺失，沿用 RFS-001 sentinel "not-recorded"
/// 语义）——永不推断具体值。test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed record UiSystemIndexFields(
    string? TargetOsVersion,
    string? AppBuild,
    string? RuntimeArtifactSha256);

/// <summary>
/// Capture Producer 的提交物：bytes + 原始 provenance（lineage hints +
/// path hint）。不铸造 identity、不写 registry——注册权在 Authority。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed record GovernanceCaptureSubmit
{
    public required string Kind { get; init; }
    public required byte[] Bytes { get; init; }
    public required IReadOnlyList<string> LineageParentHints { get; init; }
    public string? CurrentPathHint { get; init; }
    internal CaptureProducer? Producer { get; init; }
}

/// <summary>
/// 四责任之一（D4）：只拥有 bytes 与原始 provenance。能力面：Submit。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed class CaptureProducer
{
    /// <summary>提交 capture（bytes + lineage/path hints）。不产生 registry 记录。</summary>
    internal GovernanceCaptureSubmit Submit(
        string kind, byte[] bytes, IReadOnlyList<string> lineageParentHints, string? currentPathHint = null) =>
        new()
        {
            Kind = kind,
            Bytes = bytes,
            LineageParentHints = lineageParentHints,
            CurrentPathHint = currentPathHint,
            Producer = this,
        };
}

/// <summary>
/// 四责任之一（D4）：identity / manifest / lineage / lifecycle records 的
/// 唯一拥有者。不拥有 GT/expected claims，不做 baseline 晋升。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed class AssetGovernanceAuthority(FileSystemGovernanceStore store)
{
    /// <summary>
    /// 注册 capture：bytes → 内容寻址 identity；同内容/语境幂等，跨环境
    /// 或 lineage 保留新 occurrence。跨 producer 提交 / 断 lineage /
    /// stimulus 语义 kind → fail-closed 首因。
    /// </summary>
    internal GovernanceCaptureRecord Register(
        CaptureProducer producer, GovernanceCaptureSubmit submit, UiSystemIndexFields index)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentNullException.ThrowIfNull(submit);
        if (!ReferenceEquals(submit.Producer, producer))
            throw new AssetGovernanceException("cross-producer-submit: 提交物不属于该 producer");
        return store.Register(submit.Kind, submit.Bytes, submit.CurrentPathHint, submit.LineageParentHints, index);
    }

    /// <summary>
    /// 注册 bundle 全资产 + bundle 自身（完整序列化 JSON bytes；先核对
    /// bundle digest 与所有资产 hash，单次 registry 发布）。bundle lineage
    /// parents = 各资产 content hash。
    /// </summary>
    internal GovernanceCaptureRecord RegisterBundle(MinimalScenarioBundle bundle, UiSystemIndexFields index) =>
        store.RegisterBundle(bundle, index);

    /// <summary>按 identity 取记录；未注册 → unknown-identity（fail closed）。</summary>
    internal GovernanceCaptureRecord Get(string contentSha256) => store.Get(contentSha256);

    internal IReadOnlyList<GovernanceCaptureRecord> FindByOsVersion(string osVersion) =>
        store.FindByOsVersion(osVersion);

    /// <summary>
    /// 按 app build 检索（D3 语义）：查询 sentinel "not-recorded" = 显式
    /// Unknown 查询——只命中 Index.AppBuild 为 null 的记录；查询具体版本
    /// 只精确命中显式记录该值的记录（无 → 空，绝不把 null 推断成具体值）。
    /// </summary>
    internal IReadOnlyList<GovernanceCaptureRecord> FindByAppBuild(string appBuild) =>
        store.FindByAppBuild(appBuild);

    internal IReadOnlyList<GovernanceCaptureRecord> FindByRuntimeArtifact(string kernelSha256) =>
        store.FindByRuntimeArtifact(kernelSha256);

    internal IReadOnlyList<GovernanceCaptureRecord> FindByLineageParent(string parentSha256) =>
        store.FindByLineageParent(parentSha256);

    /// <summary>完整性门：从当前 bytes 重算 hash 比对（bytes-missing / hash-mismatch 首因）。</summary>
    internal void VerifyIntegrity(string contentSha256) => store.VerifyIntegrity(contentSha256);

    /// <summary>读取已核验的内容 bytes；bundle 返回完整可检视输入，而非 digest 文本。</summary>
    internal byte[] ReadContentBytes(string contentSha256) => store.ReadContentBytes(contentSha256);

    /// <summary>从内容地址重载完整 Minimal Bundle；未知 stimulus/schema 故障关闭。</summary>
    internal MinimalScenarioBundle LoadBundle(string contentSha256) => store.LoadBundle(contentSha256);

    /// <summary>registry 记录数（fail-closed 零部分注册的断言面）。</summary>
    internal int RegisteredCount => store.RegisteredCount;
}

/// <summary>
/// 断言声明（GT/expected claim；附着在已注册 identity 上）。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed record AssertionClaim(
    string ClaimId,
    string BundleContentSha256,
    ScenarioExpectation Expectation,
    string ReviewedBy,
    string? SupersedesClaimId,
    DateTimeOffset CreatedAtVirtualTime);

/// <summary>
/// 四责任之一（D4）：GT/expected/compatibility claims 的唯一拥有者。
/// 修改断言 = 新 claim（新 claimId，引用旧 claim）；原 claim 不可变。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed class ScenarioAssertionOwner(FileSystemGovernanceStore store)
{
    /// <summary>对已注册 bundle 附着 expected 断言（Human-reviewed 标记必填）。</summary>
    internal AssertionClaim AttachExpected(string bundleContentSha256, ScenarioExpectation expectation, string reviewedBy) =>
        store.AttachClaim(bundleContentSha256, expectation, null, reviewedBy);

    /// <summary>
    /// 断言修订（D6）：产生新 claim 并引用旧 claim；不修改旧 claim 内容
    /// （旧 claim 文件保持原字节，仍可解析）。
    /// </summary>
    internal AssertionClaim ReviseExpected(string bundleContentSha256, ScenarioExpectation newExpectation, string reviewedBy)
    {
        var latest = store.LatestClaimForScenario(bundleContentSha256)
            ?? throw new AssetGovernanceException($"claim-unknown: 该 scenario 尚无已晋升前序断言 {bundleContentSha256}");
        return store.AttachClaim(bundleContentSha256, newExpectation, latest.ClaimId, reviewedBy);
    }

    /// <summary>读取自己的 claim（只读；claims 属 AssertionOwner 能力面）。</summary>
    internal AssertionClaim GetClaim(string claimId) => store.GetClaim(claimId);
}

/// <summary>
/// Baseline 晋升记录（D5/D6）。supersededBy 是 registry 维护的链指针：v2 晋升
/// 后 v1 的晋升字段（baselineId/bundle/claim/version/promotedBy/promotedAt）
/// 不可变，且内容 seal（AssertNoOverlay）不改。recordSeal 不入记录面。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed record BaselineRecord(
    string BaselineId,
    string ScenarioId,
    string BundleSha256,
    string ClaimId,
    string VersionTag,
    string PromotedBy,
    DateTimeOffset PromotedAtVirtualTime,
    string? SupersededBy);

/// <summary>
/// 晋升证据（§7.3 最小子集；全部显式输入，缺任一 → 首因拒绝）：
/// 两遍 digest 由晋升方对同一 SourceBundle 重新运行 ScenarioRunner 核对；
/// PerCycleZero / TraceArmsEquivalent 承接既有测试，仍是声明式证据位。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed record PromotionEvidence
{
    public required string ScenarioId { get; init; }
    public MinimalScenarioBundle? SourceBundle { get; init; }
    public string? BundleContentSha256 { get; init; }
    public string? ClaimId { get; init; }
    public string? DigestRun1 { get; init; }
    public string? DigestRun2 { get; init; }
    public bool? PerCycleZero { get; init; }
    public bool? TraceArmsEquivalent { get; init; }
    public string? HumanReviewedBy { get; init; }
}

/// <summary>
/// Baseline 版本链 registry（D5/D6）：记录只经 HumanPromotionAuthority 铸造
/// （本类 Promote 纯委托）；ResolveLatest 沿 supersede 链；ResolveVersion
/// 保证 v1 在 v2 之后仍可解析且内容不变；AssertNoOverlay 复核每条记录的
/// 内容 seal。test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed class BaselineRegistry(FileSystemGovernanceStore store)
{
    /// <summary>唯一记录创建路径（委托 HumanPromotionAuthority.Promote）。</summary>
    internal BaselineRecord Promote(
        HumanPromotionAuthority promoter, AssetGovernanceAuthority authority,
        ScenarioAssertionOwner assertions, PromotionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(promoter);
        return promoter.Promote(authority, assertions, evidence);
    }

    /// <summary>最新版本（沿 supersede 链末位）；无晋升记录 → baseline-unknown。</summary>
    internal BaselineRecord ResolveLatest(string scenarioId)
    {
        var chain = store.LoadBaselineChain(scenarioId);
        return chain.Count == 0
            ? throw new AssetGovernanceException($"baseline-unknown:{scenarioId}")
            : chain[^1];
    }

    /// <summary>按版本标签解析（v1 在 v2 之后仍可解析）；未注册版本 → fail closed。</summary>
    internal BaselineRecord ResolveVersion(string scenarioId, string versionTag)
    {
        var chain = store.LoadBaselineChain(scenarioId);
        return chain.FirstOrDefault(r => r.VersionTag == versionTag)
            ?? throw new AssetGovernanceException($"baseline-version-unknown:{scenarioId}:{versionTag}");
    }

    /// <summary>内容 seal 复核：任何记录被就地改写（overlay）→ fail closed。</summary>
    internal void AssertNoOverlay(string scenarioId) => store.AssertNoOverlay(scenarioId);
}

/// <summary>
/// 四责任 + registry 的组合根：concrete filesystem store（root 注入），
/// 所有持久化立即落盘（temp + 原子 move；TRW-001 风格），读侧对每个文件
/// 校验 schemaVersion（未知 → schema-unknown）。虚拟时钟固定基准
/// 2026-09-13T12:00:00Z + 递增序号（无 wall-clock）。test-side 治理层，
/// 非产品契约（ABG-001 D1）。
/// </summary>
internal sealed class GovernanceStack : IDisposable
{
    private readonly FileSystemGovernanceStore _store;

    private GovernanceStack(FileSystemGovernanceStore store)
    {
        _store = store;
        Producer = new CaptureProducer();
        Authority = new AssetGovernanceAuthority(store);
        Assertions = new ScenarioAssertionOwner(store);
        Promoter = new HumanPromotionAuthority(store);
        Baselines = new BaselineRegistry(store);
    }

    internal CaptureProducer Producer { get; }
    internal AssetGovernanceAuthority Authority { get; }
    internal ScenarioAssertionOwner Assertions { get; }
    internal HumanPromotionAuthority Promoter { get; }
    internal BaselineRegistry Baselines { get; }

    public static GovernanceStack Create(string root)
    {
        return new GovernanceStack(new FileSystemGovernanceStore(root));
    }

    public void Dispose() => _store.Dispose();
}

/// <summary>
/// Concrete filesystem 治理 store（D1/D2）：
///  - captures/{sha[..2]}/{sha}.bytes——内容寻址 blob（identity 即路径键，
///    改名不变性是结构性的）；
///  - registry/index.json——content identity 对应的 occurrence records；
///  - claims/{claimId}.json + claims/index.json——断言 claims；
///  - baselines/{scenarioId}.json——晋升版本链。
/// 每个文件带 schemaVersion（未知 → AssetGovernanceException("schema-unknown:
/// &lt;v&gt;")）。Bundle 先完成输入校验，再单次发布 registry；发布失败
/// 回滚内存索引，但先写 blob 可能留下未注册的孤儿文件。单文件发布用 temp +
/// File.Move，不声明跨文件事务或 fsync crash durability。
/// test-side 治理层，非产品契约（ABG-001 D1）。
/// </summary>
internal sealed class FileSystemGovernanceStore : IDisposable
{
    private const string SchemaVersion = "1";
    private static readonly DateTimeOffset VirtualBase = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly Dictionary<string, GovernanceCaptureRecord> _records = new(StringComparer.Ordinal);
    private readonly List<GovernanceCaptureRecord> _occurrences = new();
    private readonly List<string> _claimOrder = new();
    private readonly Dictionary<string, List<BaselineRecord>> _baselineChains = new(StringComparer.Ordinal);
    private long _clockTicks;

    /// <summary>
    /// 唯一可调用私有 baseline 写入路径的角色。测试中的显式调用代表 Human
    /// decision；同程序集代码仍非身份认证边界，不能据此声称产品级授权。
    /// </summary>
    internal sealed class HumanPromotionAuthority(FileSystemGovernanceStore store)
    {
        internal BaselineRecord Promote(
            AssetGovernanceAuthority authority, ScenarioAssertionOwner assertions, PromotionEvidence evidence)
        {
            ArgumentNullException.ThrowIfNull(authority);
            ArgumentNullException.ThrowIfNull(assertions);
            ArgumentNullException.ThrowIfNull(evidence);

            string? Missing() => evidence switch
            {
                { BundleContentSha256: null or "" } => "bundle-content-sha256",
                { ClaimId: null or "" } => "claim-id",
                { DigestRun1: null or "" } => "digest-run1",
                { DigestRun2: null or "" } => "digest-run2",
                { PerCycleZero: null or false } => "per-cycle-zero",
                { TraceArmsEquivalent: null or false } => "trace-arms-equivalent",
                { HumanReviewedBy: null or "" } => "human-reviewed-by",
                { SourceBundle: null } => "source-bundle",
                _ => null,
            };
            var missing = Missing();
            if (missing is not null)
                throw new AssetGovernanceException($"promotion-evidence-incomplete:{missing}");

            var bundle = evidence.SourceBundle!;
            if (ScenarioBundleDigest.Compute(bundle with { BundleDigest = "" }) != bundle.BundleDigest)
                throw new AssetGovernanceException("bundle-digest-mismatch");
            if (bundle.ScenarioId != evidence.ScenarioId)
                throw new AssetGovernanceException($"scenario-mismatch:{evidence.ScenarioId}:{bundle.ScenarioId}");
            var expectedBundleBytes = BundleBytes(bundle);
            var recordedBytes = authority.ReadContentBytes(evidence.BundleContentSha256!);
            if (!recordedBytes.AsSpan().SequenceEqual(expectedBundleBytes))
                throw new AssetGovernanceException("bundle-content-mismatch");

            var bundleRecord = authority.Get(evidence.BundleContentSha256!);
            if (bundleRecord.Kind != "bundle")
                throw new AssetGovernanceException($"bundle-kind-mismatch:{bundleRecord.Kind}");
            if (!bundleRecord.LineageParents.SequenceEqual(bundle.Assets.Select(a => a.Sha256), StringComparer.Ordinal))
                throw new AssetGovernanceException("bundle-lineage-mismatch");

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var active = new HashSet<string>(StringComparer.Ordinal);
            void VerifyLineage(string sha)
            {
                if (visited.Contains(sha)) return;
                if (!active.Add(sha))
                    throw new AssetGovernanceException($"lineage-cycle:{sha}");
                var record = authority.Get(sha);
                authority.VerifyIntegrity(sha);
                foreach (var parent in record.LineageParents)
                    VerifyLineage(parent);
                active.Remove(sha);
                visited.Add(sha);
            }
            VerifyLineage(evidence.BundleContentSha256!);

            var claim = assertions.GetClaim(evidence.ClaimId!);
            if (claim.BundleContentSha256 != evidence.BundleContentSha256)
                throw new AssetGovernanceException(
                    $"claim-bundle-mismatch: claim={claim.ClaimId} bundle={claim.BundleContentSha256} evidence={evidence.BundleContentSha256}");
            if (claim.Expectation != bundle.Expected)
                throw new AssetGovernanceException("claim-expectation-mismatch");
            if (claim.ReviewedBy != evidence.HumanReviewedBy)
                throw new AssetGovernanceException("claim-reviewer-mismatch");

            if (evidence.DigestRun1 != evidence.DigestRun2)
                throw new AssetGovernanceException(
                    $"digest-inequality:{evidence.DigestRun1}:{evidence.DigestRun2}");

            var run1 = ScenarioRunner.Run(bundle).Report;
            var run2 = ScenarioRunner.Run(bundle).Report;
            if (!run1.AcceptancePassed || !run2.AcceptancePassed)
                throw new AssetGovernanceException("replay-acceptance-failed");
            if (run1.ScenarioId != evidence.ScenarioId || run2.ScenarioId != evidence.ScenarioId
                || run1.SemanticDigest != evidence.DigestRun1 || run2.SemanticDigest != evidence.DigestRun2)
                throw new AssetGovernanceException("digest-source-mismatch");

            return store.CreateBaseline(
                evidence.ScenarioId, evidence.BundleContentSha256!, claim.ClaimId, evidence.HumanReviewedBy!);
        }
    }

    internal FileSystemGovernanceStore(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        Directory.CreateDirectory(Path.Combine(root, "captures"));
        Directory.CreateDirectory(Path.Combine(root, "registry"));
        Directory.CreateDirectory(Path.Combine(root, "baselines"));
        Directory.CreateDirectory(Path.Combine(root, "claims"));

        var indexPath = Path.Combine(root, "registry", "index.json");
        if (File.Exists(indexPath))
            foreach (var dto in ReadFile<IndexFileDto>(indexPath).Records)
            {
                var occurrence = FromDto(dto);
                _occurrences.Add(occurrence);
                _records.TryAdd(dto.ContentSha256, occurrence);
            }

        var claimsIndexPath = Path.Combine(root, "claims", "index.json");
        if (File.Exists(claimsIndexPath))
            _claimOrder.AddRange(ReadFile<ClaimsIndexDto>(claimsIndexPath).OrderedClaimIds);
    }

    internal int RegisteredCount => _records.Count;

    // ---- 注册（identity / lineage）----

    internal GovernanceCaptureRecord Register(
        string kind, byte[] bytes, string? currentPathHint, IReadOnlyList<string> lineageParentHints,
        UiSystemIndexFields index) =>
        RegisterCore(kind, bytes, currentPathHint, lineageParentHints, index, persist: true);

    private GovernanceCaptureRecord RegisterCore(
        string kind, byte[] bytes, string? currentPathHint, IReadOnlyList<string> lineageParentHints,
        UiSystemIndexFields index, bool persist)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(lineageParentHints);
        if (string.IsNullOrWhiteSpace(kind))
            throw new AssetGovernanceException("kind-empty");
        // D8/P26：stimulus 语义只能经 ScenarioImporter 派生，store 拒绝把
        // Trace Event 当资产语义注册（bytes 级 opaque capture 不受限）。
        if (kind.StartsWith("scenario-stimulus", StringComparison.Ordinal))
            throw new AssetGovernanceException($"stimulus-not-registerable:{kind}");

        var sha = Sha256(bytes);

        // lineage 门（任何写入之前——失败零部分注册）
        foreach (var hint in lineageParentHints)
            if (!_records.ContainsKey(hint))
                throw new AssetGovernanceException($"lineage-parent-unknown:{hint}");

        // 内容身份与发生语境分离：同 bytes + 同语境幂等；新的环境或 lineage
        // 产生另一条 occurrence，但不产生第二份内容身份或 blob。
        var existing = _occurrences.FirstOrDefault(r =>
            r.ContentSha256 == sha && r.Kind == kind && r.Index == index
            && r.LineageParents.SequenceEqual(lineageParentHints, StringComparer.Ordinal));
        if (existing is not null)
        {
            EnsureBlob(sha, bytes);
            return existing;
        }

        EnsureBlob(sha, bytes);
        var record = new GovernanceCaptureRecord(
            sha, kind, currentPathHint, VirtualNow(),
            lineageParentHints.ToArray(), index);
        _records.TryAdd(sha, record);
        _occurrences.Add(record);
        if (persist)
        {
            try { PersistRegistry(); }
            catch
            {
                _occurrences.Remove(record);
                if (_records.TryGetValue(sha, out var first) && ReferenceEquals(first, record))
                    _records.Remove(sha);
                throw;
            }
        }
        return record;
    }

    internal GovernanceCaptureRecord RegisterBundle(MinimalScenarioBundle bundle, UiSystemIndexFields index)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(index);

        var expectedAppBuild = bundle.TargetUiSystem.AppBuild == "not-recorded"
            ? null : bundle.TargetUiSystem.AppBuild;
        if (index.TargetOsVersion != bundle.TargetUiSystem.OsVersion)
            throw new AssetGovernanceException("metadata-mismatch:target-os-version");
        if (index.AppBuild != expectedAppBuild)
            throw new AssetGovernanceException("metadata-mismatch:app-build");
        if (index.RuntimeArtifactSha256 != bundle.RuntimeArtifact.KernelAssemblySha256)
            throw new AssetGovernanceException("metadata-mismatch:runtime-artifact");

        // bundle canonical digest 先验（失败 → 任何资产都不注册）
        var canonical = ScenarioBundleDigest.Compute(bundle with { BundleDigest = ""});
        if (canonical != bundle.BundleDigest)
            throw new AssetGovernanceException($"hash-mismatch:{bundle.BundleDigest}:{canonical}");
        var bundleBytes = BundleBytes(bundle);

        var validated = new List<(BundleAssetEntry Entry, byte[] Bytes)>();
        foreach (var entry in bundle.Assets)
        {
            var path = Path.Combine(GoldenPaths.RepoRoot(), GoldenPaths.BundleRoot, entry.RelativePath);
            if (!File.Exists(path))
                throw new AssetGovernanceException($"bytes-missing:{entry.RelativePath}");
            var bytes = File.ReadAllBytes(path);
            var actual = Sha256(bytes);
            if (actual != entry.Sha256)
                throw new AssetGovernanceException($"hash-mismatch:{entry.Sha256}:{actual}");
            validated.Add((entry, bytes));
        }
        var countBefore = _occurrences.Count;
        var identitiesBefore = _records.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        try
        {
            var parents = new List<string>(validated.Count);
            foreach (var (entry, bytes) in validated)
                parents.Add(RegisterCore(entry.Kind, bytes, entry.RelativePath, Array.Empty<string>(), index, persist: false).ContentSha256);
            var record = RegisterCore("bundle", bundleBytes, null, parents, index, persist: false);
            PersistRegistry();
            return record;
        }
        catch
        {
            _occurrences.RemoveRange(countBefore, _occurrences.Count - countBefore);
            _records.Clear();
            foreach (var (sha, record) in identitiesBefore)
                _records.Add(sha, record);
            throw;
        }
    }

    internal GovernanceCaptureRecord Get(string contentSha256) =>
        _records.TryGetValue(contentSha256, out var record)
            ? record
            : throw new AssetGovernanceException($"unknown-identity:{contentSha256}");

    internal IReadOnlyList<GovernanceCaptureRecord> FindByOsVersion(string osVersion) =>
        _occurrences.Where(r => osVersion == "not-recorded"
            ? r.Index.TargetOsVersion is null
            : r.Index.TargetOsVersion == osVersion).ToList();

    /// <summary>D3：sentinel "not-recorded" = 显式 Unknown 查询（只命中 null）。</summary>
    internal IReadOnlyList<GovernanceCaptureRecord> FindByAppBuild(string appBuild) =>
        appBuild == "not-recorded"
            ? _occurrences.Where(r => r.Index.AppBuild is null).ToList()
            : _occurrences.Where(r => r.Index.AppBuild == appBuild).ToList();

    internal IReadOnlyList<GovernanceCaptureRecord> FindByRuntimeArtifact(string kernelSha256) =>
        _occurrences.Where(r => kernelSha256 == "not-recorded"
            ? r.Index.RuntimeArtifactSha256 is null
            : r.Index.RuntimeArtifactSha256 == kernelSha256).ToList();

    internal IReadOnlyList<GovernanceCaptureRecord> FindByLineageParent(string parentSha256) =>
        _occurrences.Where(r => r.LineageParents.Contains(parentSha256, StringComparer.Ordinal)).ToList();

    internal void VerifyIntegrity(string contentSha256) => _ = ReadContentBytes(contentSha256);

    internal byte[] ReadContentBytes(string contentSha256)
    {
        Get(contentSha256);
        var blob = BlobPath(contentSha256);
        if (!File.Exists(blob))
            throw new AssetGovernanceException($"bytes-missing:{contentSha256}");
        var bytes = File.ReadAllBytes(blob);
        var actual = Sha256(bytes);
        if (actual != contentSha256)
            throw new AssetGovernanceException($"hash-mismatch:{contentSha256}:{actual}");
        return bytes;
    }

    internal MinimalScenarioBundle LoadBundle(string contentSha256)
    {
        var record = Get(contentSha256);
        if (record.Kind != "bundle")
            throw new AssetGovernanceException($"bundle-kind-mismatch:{record.Kind}");
        try
        {
            using var document = JsonDocument.Parse(ReadContentBytes(contentSha256));
            var root = document.RootElement;
            T Required<T>(string name) where T : class =>
                JsonSerializer.Deserialize<T>(root.GetProperty(name).GetRawText(), BundleJsonOptions)
                ?? throw new AssetGovernanceException($"bundle-field-missing:{name}");
            var stimuli = root.GetProperty("stimuli").EnumerateArray().Select(item =>
            {
                var payload = item.GetProperty("payload").GetRawText();
                return item.GetProperty("kind").GetString() switch
                {
                    nameof(ScenarioStimulus.ObservationFrame) =>
                        (ScenarioStimulus?)JsonSerializer.Deserialize<ScenarioStimulus.ObservationFrame>(payload, BundleJsonOptions),
                    nameof(ScenarioStimulus.CancelRequest) =>
                        JsonSerializer.Deserialize<ScenarioStimulus.CancelRequest>(payload, BundleJsonOptions),
                    var kind => throw new AssetGovernanceException($"stimulus-kind-unknown:{kind}"),
                } ?? throw new AssetGovernanceException("stimulus-payload-missing");
            }).ToArray();
            var contractJson = root.GetProperty("contract");
            IReadOnlySet<string>? ContractSet(string name) =>
                contractJson.GetProperty(name).ValueKind == JsonValueKind.Null
                    ? null
                    : JsonSerializer.Deserialize<string[]>(contractJson.GetProperty(name).GetRawText(), BundleJsonOptions)!
                        .ToHashSet(StringComparer.Ordinal);
            T? ContractPart<T>(string name) where T : class =>
                contractJson.GetProperty(name).ValueKind == JsonValueKind.Null
                    ? null
                    : JsonSerializer.Deserialize<T>(contractJson.GetProperty(name).GetRawText(), BundleJsonOptions);
            var contract = new UniClaw.Kernel.Run.ExecutionContract(
                contractJson.GetProperty("version").GetString()!,
                contractJson.GetProperty("objective").GetString()!,
                ContractSet("scope"), ContractSet("allowedEffects"), ContractSet("forbiddenEffects"),
                ContractPart<List<string>>("proofCriteria"),
                ContractPart<List<UniClaw.Kernel.Run.RunObligation>>("obligations"));
            var bundle = new MinimalScenarioBundle
            {
                BundleId = Required<string>("bundleId"),
                BundleVersion = Required<string>("bundleVersion"),
                ScenarioId = Required<string>("scenarioId"),
                ScenarioVersion = Required<string>("scenarioVersion"),
                RuntimeArtifact = Required<RuntimeArtifactIdentity>("runtimeArtifact"),
                TargetUiSystem = Required<TargetUiSystemIdentity>("targetUiSystem"),
                Assets = Required<List<BundleAssetEntry>>("assets"),
                Stimuli = stimuli,
                ProducerIdentities = Required<ProducerSchemaConfigIdentity>("producerIdentities"),
                AgentScript = Required<AgentScriptStep>("agentScript"),
                Expected = Required<ScenarioExpectation>("expected"),
                Contract = contract,
                Goal = Required<GoalSpec>("goal"),
                BundleDigest = Required<string>("bundleDigest"),
            };
            if (ScenarioBundleDigest.Compute(bundle with { BundleDigest = "" }) != bundle.BundleDigest)
                throw new AssetGovernanceException("bundle-digest-mismatch");
            return bundle;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or KeyNotFoundException)
        {
            throw new AssetGovernanceException($"bundle-decode-failed:{ex.GetType().Name}:{ex.Message}");
        }
    }

    // ---- claims（ScenarioAssertionOwner 存储）----

    internal AssertionClaim AttachClaim(
        string bundleContentSha256, ScenarioExpectation expectation, string? supersedesClaimId, string reviewedBy)
    {
        var current = ReadBundleMetadata(bundleContentSha256);
        if (expectation != current.Expected)
            throw new AssetGovernanceException("claim-expectation-mismatch");
        if (string.IsNullOrWhiteSpace(reviewedBy))
            throw new AssetGovernanceException("promotion-evidence-incomplete:human-reviewed-by");
        var latest = LatestClaimForScenario(bundleContentSha256);
        if (supersedesClaimId is null && latest is not null)
            throw new AssetGovernanceException("claim-revision-required");
        if (supersedesClaimId is not null)
        {
            if (latest is null || latest.ClaimId != supersedesClaimId)
                throw new AssetGovernanceException("claim-supersession-mismatch");
            var previous = ReadBundleMetadata(latest.BundleContentSha256);
            if (bundleContentSha256 == latest.BundleContentSha256)
                throw new AssetGovernanceException("bundle-version-required");
            if (VersionNumber(current.BundleVersion) <= VersionNumber(previous.BundleVersion))
                throw new AssetGovernanceException("bundle-version-not-increasing");
            if (expectation == latest.Expectation)
                throw new AssetGovernanceException("assertion-unchanged");
        }
        var claimId = "claim-" + (_claimOrder.Count + 1).ToString("000", System.Globalization.CultureInfo.InvariantCulture);
        var claim = new AssertionClaim(
            claimId, bundleContentSha256, expectation, reviewedBy, supersedesClaimId, VirtualNow());
        var claimPath = Path.Combine(_root, "claims", claimId + ".json");
        WriteFile(claimPath, new ClaimFileDto(
            SchemaVersion, claimId, bundleContentSha256, expectation, reviewedBy, supersedesClaimId, claim.CreatedAtVirtualTime));
        _claimOrder.Add(claimId);
        try
        {
            WriteFile(Path.Combine(_root, "claims", "index.json"), new ClaimsIndexDto(SchemaVersion, _claimOrder.ToList()));
        }
        catch
        {
            _claimOrder.RemoveAt(_claimOrder.Count - 1);
            // 本次 claim 未成为 authority index 成员。清理失败时仍保留原始发布
            // 异常；遗留文件在 GetClaim 的 index 门下不可见。
            try { if (File.Exists(claimPath)) File.Delete(claimPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
        return claim;
    }

    internal AssertionClaim? LatestClaimForScenario(string bundleContentSha256)
    {
        var scenarioId = ReadBundleMetadata(bundleContentSha256).ScenarioId;
        AssertionClaim? latest = null;
        foreach (var claimId in _claimOrder)
        {
            var claim = GetClaim(claimId);
            if (ReadBundleMetadata(claim.BundleContentSha256).ScenarioId == scenarioId)
                latest = claim;
        }
        return latest;
    }

    private (string ScenarioId, string BundleVersion, ScenarioExpectation Expected) ReadBundleMetadata(string sha)
    {
        var record = Get(sha);
        if (record.Kind != "bundle")
            throw new AssetGovernanceException($"bundle-kind-mismatch:{record.Kind}");
        using var document = JsonDocument.Parse(ReadContentBytes(sha));
        var root = document.RootElement;
        var expected = JsonSerializer.Deserialize<ScenarioExpectation>(
            root.GetProperty("expected").GetRawText(), BundleJsonOptions)
            ?? throw new AssetGovernanceException("bundle-expected-missing");
        return (
            root.GetProperty("scenarioId").GetString() ?? throw new AssetGovernanceException("bundle-scenario-missing"),
            root.GetProperty("bundleVersion").GetString() ?? throw new AssetGovernanceException("bundle-version-missing"),
            expected);
    }

    private static int VersionNumber(string version) =>
        version.StartsWith('v') && int.TryParse(version.AsSpan(1), out var number) && number > 0
            ? number
            : throw new AssetGovernanceException($"bundle-version-invalid:{version}");

    internal AssertionClaim GetClaim(string claimId)
    {
        if (!_claimOrder.Contains(claimId, StringComparer.Ordinal))
            throw new AssetGovernanceException($"claim-unknown:{claimId}");
        var path = Path.Combine(_root, "claims", claimId + ".json");
        if (!File.Exists(path))
            throw new AssetGovernanceException($"claim-unknown:{claimId}");
        var dto = ReadFile<ClaimFileDto>(path);
        return new AssertionClaim(
            dto.ClaimId, dto.BundleContentSha256, dto.Expectation, dto.ReviewedBy,
            dto.SupersedesClaimId, dto.CreatedAtVirtualTime);
    }

    // ---- baselines（版本链）----

    private BaselineRecord CreateBaseline(
        string scenarioId, string bundleSha256, string claimId, string promotedBy)
    {
        var chain = LoadBaselineChain(scenarioId).ToList();

        // 覆盖门：同 bundle + 同 claim 重晋 = 对既有版本的 overlay 尝试
        var overlay = chain.FirstOrDefault(r => r.BundleSha256 == bundleSha256 && r.ClaimId == claimId);
        if (overlay is not null)
            throw new AssetGovernanceException($"baseline-version-exists:{overlay.VersionTag}");

        var versionTag = "v" + (chain.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var record = new BaselineRecord(
            $"baseline-{scenarioId}-{versionTag}", scenarioId, bundleSha256, claimId,
            versionTag, promotedBy, VirtualNow(), SupersededBy: null);
        chain.Add(record);
        if (chain.Count > 1)
        {
            // 链指针：v(n-1).SupersededBy → v(n)（晋升字段与内容 seal 不变）
            var previous = chain[^2];
            chain[^2] = previous with { SupersededBy = record.BaselineId };
        }
        PersistBaselineChain(scenarioId, chain);
        return record;
    }

    internal IReadOnlyList<BaselineRecord> LoadBaselineChain(string scenarioId)
    {
        if (_baselineChains.TryGetValue(scenarioId, out var cached))
            return cached.ToArray();
        var path = BaselinePath(scenarioId);
        if (!File.Exists(path))
        {
            var empty = new List<BaselineRecord>();
            _baselineChains[scenarioId] = empty;
            return empty.ToArray();
        }
        var loaded = new List<BaselineRecord>();
        foreach (var dto in ReadFile<BaselineFileDto>(path).Records)
        {
            if (RecordSeal(dto) != dto.RecordSeal)
                throw new AssetGovernanceException($"baseline-overlay-detected:{dto.BaselineId}");
            loaded.Add(new BaselineRecord(
                dto.BaselineId, dto.ScenarioId, dto.BundleSha256, dto.ClaimId, dto.VersionTag,
                dto.PromotedBy, dto.PromotedAtVirtualTime, dto.SupersededBy));
        }
        _baselineChains[scenarioId] = loaded;
        return loaded.ToArray();
    }

    internal void AssertNoOverlay(string scenarioId)
    {
        // 文件侧记录（非缓存）逐条复核 seal
        var path = BaselinePath(scenarioId);
        if (!File.Exists(path))
            throw new AssetGovernanceException($"baseline-unknown:{scenarioId}");
        foreach (var dto in ReadFile<BaselineFileDto>(path).Records)
        {
            if (RecordSeal(dto) != dto.RecordSeal)
                throw new AssetGovernanceException($"baseline-overlay-detected:{dto.BaselineId}");
            var reconstructed = new BaselineRecord(
                dto.BaselineId, dto.ScenarioId, dto.BundleSha256, dto.ClaimId, dto.VersionTag,
                dto.PromotedBy, dto.PromotedAtVirtualTime, dto.SupersededBy);
            var cached = LoadBaselineChain(scenarioId).Single(r => r.BaselineId == dto.BaselineId);
            if (cached != reconstructed)
                throw new AssetGovernanceException($"baseline-overlay-detected:{dto.BaselineId}");
        }
    }

    public void Dispose()
    {
    }

    // ---- 基础设施 ----

    private DateTimeOffset VirtualNow() => VirtualBase.AddTicks(System.Threading.Interlocked.Increment(ref _clockTicks));

    private string BlobPath(string sha) => Path.Combine(_root, "captures", sha[..2], sha + ".bytes");

    private string BaselinePath(string scenarioId)
    {
        var fileName = string.Join("_", scenarioId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(_root, "baselines", fileName + ".json");
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static byte[] BundleBytes(MinimalScenarioBundle bundle) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            bundle.BundleId,
            bundle.BundleVersion,
            bundle.ScenarioId,
            bundle.ScenarioVersion,
            bundle.RuntimeArtifact,
            bundle.TargetUiSystem,
            bundle.Assets,
            Stimuli = bundle.Stimuli.Select(stimulus => new
            {
                Kind = stimulus.GetType().Name,
                Payload = JsonSerializer.SerializeToElement(stimulus, stimulus.GetType(), BundleJsonOptions),
            }).ToArray(),
            bundle.ProducerIdentities,
            bundle.AgentScript,
            bundle.Expected,
            bundle.Contract,
            bundle.Goal,
            bundle.BundleDigest,
        }, BundleJsonOptions);

    private static readonly JsonSerializerOptions BundleJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
    };

    private void EnsureBlob(string sha, byte[] bytes)
    {
        var blob = BlobPath(sha);
        if (File.Exists(blob))
        {
            var actual = Sha256(File.ReadAllBytes(blob));
            if (actual != sha)
                throw new AssetGovernanceException($"hash-mismatch:{sha}:{actual}");
            return;
        }
        if (_records.ContainsKey(sha))
            throw new AssetGovernanceException($"bytes-missing:{sha}");
        Directory.CreateDirectory(Path.GetDirectoryName(blob)!);
        var tmp = blob + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, blob, overwrite: false);
    }

    private void PersistRegistry() => WriteFile(
        Path.Combine(_root, "registry", "index.json"),
        new IndexFileDto(SchemaVersion, _occurrences
            .Select(r => new RecordDto(
                r.ContentSha256, r.Kind, r.CurrentPathHint, r.RegisteredAtVirtualTime,
                r.LineageParents.ToList(),
                new IndexDto(r.Index.TargetOsVersion, r.Index.AppBuild, r.Index.RuntimeArtifactSha256)))
            .ToList()));

    private void PersistBaselineChain(string scenarioId, List<BaselineRecord> chain)
    {
        var dtos = chain.Select(r => new BaselineRecordDto(
            r.BaselineId, r.ScenarioId, r.BundleSha256, r.ClaimId, r.VersionTag,
            r.PromotedBy, r.PromotedAtVirtualTime, r.SupersededBy,
            RecordSeal(new BaselineRecordDto(
                r.BaselineId, r.ScenarioId, r.BundleSha256, r.ClaimId, r.VersionTag,
                r.PromotedBy, r.PromotedAtVirtualTime, r.SupersededBy, "")))).ToList();
        WriteFile(BaselinePath(scenarioId), new BaselineFileDto(SchemaVersion, scenarioId, dtos));
        _baselineChains[scenarioId] = chain;
    }

    private static string RecordSeal(BaselineRecordDto dto) => Sha256(Encoding.UTF8.GetBytes(string.Join("|",
        dto.BaselineId, dto.ScenarioId, dto.BundleSha256, dto.ClaimId, dto.VersionTag,
        dto.PromotedBy, dto.PromotedAtVirtualTime.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture))));

    private static GovernanceCaptureRecord FromDto(RecordDto dto) => new(
        dto.ContentSha256, dto.Kind, dto.CurrentPathHint, dto.RegisteredAtVirtualTime,
        dto.LineageParents, new UiSystemIndexFields(dto.Index.TargetOsVersion, dto.Index.AppBuild, dto.Index.RuntimeArtifactSha256));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static T ReadFile<T>(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var version = document.RootElement.TryGetProperty("schemaVersion", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : "(missing)";
        if (version != SchemaVersion)
            throw new AssetGovernanceException($"schema-unknown:{version}");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)!;
    }

    private static void WriteFile<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    // ---- 持久化 DTO（schemaVersion 首字段）----

    private sealed record IndexFileDto(string SchemaVersion, List<RecordDto> Records);
    private sealed record RecordDto(
        string ContentSha256, string Kind, string? CurrentPathHint,
        DateTimeOffset RegisteredAtVirtualTime, List<string> LineageParents, IndexDto Index);
    private sealed record IndexDto(string? TargetOsVersion, string? AppBuild, string? RuntimeArtifactSha256);

    private sealed record ClaimsIndexDto(string SchemaVersion, List<string> OrderedClaimIds);

    private sealed record ClaimFileDto(
        string SchemaVersion, string ClaimId, string BundleContentSha256,
        ScenarioExpectation Expectation, string ReviewedBy, string? SupersedesClaimId,
        DateTimeOffset CreatedAtVirtualTime);

    private sealed record BaselineFileDto(string SchemaVersion, string ScenarioId, List<BaselineRecordDto> Records);
    private sealed record BaselineRecordDto(
        string BaselineId, string ScenarioId, string BundleSha256, string ClaimId, string VersionTag,
        string PromotedBy, DateTimeOffset PromotedAtVirtualTime, string? SupersededBy, string RecordSeal);
}
