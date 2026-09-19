# WRC-001 — World Reconstruction ContentOnly Semantic Oracle Tracer

lifecycle_state: closed · disposition: none  # Acceptance 1–6 verified; no residual Human Gate · depth: decision-heavy · base: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 · pin: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 (2026-09-13)

> This Change is test/simulation-only. It does not amend Product Runtime authority,
> create a public Interface, or promote the Oracle into a production World Model.

## Intent（WHAT/WHY）

在单一 WorldBelief revision 内，对一个 `ContentOnly` scoped input 做可重复的
semantic reconstruction tracer，验证兼容输入可生成 non-authoritative
WorldPresentation，并能稳定定位 coverage 与语义差异。目标是建立 World
Reconstruction 的 deterministic evidence，不改变 Product Runtime 的 owner、
authority 或 lifecycle。

## Fixed decisions

- realization 仅限 test/simulation；不进入 Product Runtime 或 Product Host。
- 每次 reconstruction 只绑定单一 WorldBelief revision；不做 temporal fusion，
  不把不同 revision 拼接成 current truth。
- coverage 固定为 `ContentOnly`；固定区域被排除时不判定为 missing。
- Human-reviewed Ground Truth 是非权威测试参照，不写回 Evidence、WorldBelief、
  Control、Assurance、Run State 或 Outcome。
- 输出仅为 non-authoritative `WorldPresentation`、coverage manifest、semantic
  digest、typed diff 与 lineage；不新增公共 Interface。
- 正向路径验证 compatible reconstruction；负向路径覆盖 missing、duplicate、
  wrong-parent、frame-mismatch，并报告稳定 first divergence。
- Oracle 零产品引用；不改 Product Runtime authority，不创建第二份 WorldBelief。

## Scope

- 输入：单 revision 的 ContentOnly scoped projections、SpatialFrame、资产引用与
  Human-reviewed GT。
- 输出：确定性的 presentation、coverage、digest、typed divergence 与 lineage
  报告。
- 负向：每类结构错误都必须 fail closed 或产生稳定、可分类的 first divergence，
  不得静默融合。

## Out of scope

- Product Runtime、Product Host、生产 World Model 或任何运行时 authority 修改。
- temporal fusion、跨 revision 拼接、页面全局 truth、自动 baseline promotion。
- 新公共 `Ixxx` Interface、迁移、资产治理平台、模型质量评测与 live device 测试。

## Acceptance

1. compatible ContentOnly input 在单一 revision 内生成确定性的
   non-authoritative WorldPresentation、coverage manifest、semantic digest 与
   lineage。
2. ContentOnly 排除的 fixed regions 不被误报为 missing；scope 与 SpatialFrame
   明确记录。
3. missing、duplicate、wrong-parent、frame-mismatch 各自产生稳定 typed diff
   与 first divergence；不得静默通过或跨 revision 补齐。
4. Human-reviewed GT 只作为 test oracle 输入；生产程序集无 Oracle 引用，且不
   写入 Product Runtime authority。
5. 同一输入重复运行 semantic digest 与 divergence 输出一致；不新增公共
   Interface，不产生 temporal fusion。
6. 并行 dirty 文件保持不变；本 Change 只允许修改本 state 与其授权的
   test/simulation tracer 文件。

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: >
    运行 WRC-001 专项 deterministic tests；检查正向 ContentOnly reconstruction、
    四类负向 first divergence、重复 digest、lineage/scope/frame、Oracle 零产品
    引用；执行 git diff --check 与精确路径 status。
  expected: >
    Acceptance 1–6 全部满足；所有输出可重复；不存在跨 revision temporal fusion、
    产品引用或公共 Interface。
  actual: >
    PASS。WRC-001 专项 13/13；Simulation.Tests 63/63；全解决方案
    Kernel.Tests 382/382、Agent.Tests 17/17、Simulation.Tests 63/63（合计
    462/462）。同帧 Settings fixture 生成 7 个 ContentOnly presentation
    elements，排除 2 个 fixed-region GT elements；canonical semantic digest =
    7083130907b024d75b8d7d204dc3255f230c6ba41420e641e00dfc83b34c81d5。
    四个要求内差异与额外 OutOfCoverage 均稳定；GT comparison 不进入 actual
    digest；asset/GT capture-revision-frame correlation、asset SHA256、asset 与
    occurrence 顺序 canonicalization、Product assembly 反向依赖闭包均通过。
  evidence: >
    tests/UniClaw.Simulation.Tests/WorldReconstruction.cs；
    tests/UniClaw.Simulation.Tests/WorldReconstructionFixture.cs；
    tests/UniClaw.Simulation.Tests/WorldReconstructionTests.cs；命令：
    dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj
    --filter FullyQualifiedName~WorldReconstructionTests --no-restore；
    dotnet test tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj
    --no-restore；dotnet test UniClaw.Kernel.slnx --no-restore；git diff --check。
    NuGet vulnerability-cache access warning 为环境告警，不影响 build/test 结果。
```

## Ownership / constraints

- Owner：test/simulation reconstruction tracer。
- Authority：Human-reviewed GT 仅测试参照；Product Runtime authority 不变。
- 不触碰并行 dirty：UAR-002、FSV-001、感知资产、show-me 文件及其他未授权路径。
- 不提交 commit；使用精确路径与 `apply_patch`；不得以测试绿灯宣称 Product
  readiness。

## Status log

- 2026-09-13 · implementing·human-selected · Human 授权建立 WRC-001：固定
  test/simulation-only、单一 WorldBelief revision、ContentOnly、非权威 GT、无
  temporal fusion、non-authoritative presentation/coverage/digest/typed diff/
  lineage，以及四类负向 first divergence。base/pin 固定为
  `40f190c6af6a3143b48e576a8bf4051e1edc2e26`；并行 dirty 不可触碰。
- 2026-09-13 · closed·verified · 以 TDD 完成单 revision ContentOnly tracer：
  同帧 screenshot/XML/response/GT 资产经 correlation 与 SHA256 门进入；actual
  presentation 与 Human Oracle comparison 分离；missing、duplicate、
  wrong-parent、frame-mismatch、out-of-coverage 均有 deterministic first
  divergence。Luna 三轮只读复审最终无阻断；专项 13/13、Simulation 63/63、
  全解决方案 462/462，`git diff --check` clean。未新增公共 Interface、未修改
  Product Runtime authority、未 temporal-fuse、未提交 commit。
