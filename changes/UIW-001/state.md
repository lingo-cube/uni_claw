# UIW-001 — UIWorld Executable Vertical Slice（UWM-009 → Coding）
lifecycle_state: closed · disposition: none · depth: standard · base: a2bf82e9

## Intent（WHAT/WHY）
按 `docs/architecture/uworld-protocol-baseline-l4.md`（FROZEN v0.2）实现第一条
真实可运行的 UIWorld 生命周期主干，并验证未来 Replay/Simulation 所需的
product-side 可引用性条件（R-UW-01..07）。

## Scope
- S1–S8 核心：Accepted Evidence (+ P22 TransitionContext?) → Container
  Association（Matched/New/Ambiguous/Insufficient，含 Authority gates）→
  WorldBeliefRevision（Containers/Relations 为 revision-bound belief）→ P4 Slice。
- S9–S12 replayability readiness：lifecycle referenceability（AssociationLog）、
  语义重执行（ReplayFixture = 测试工具）、counterfactual branch、hidden-state 审计。
- 确定性 strategy seam（IAssociationStrategy，UIWorld 内部缝，非跨组件）。
- uni-agent legacy Trace 分析（docs/analysis/，Authority: NONE）。

## Out of Scope（禁止）
- 迁移/复制 uni-agent Trace、RuntimeObservability、Span/Event 管线；创建
  UniClaw.Trace subsystem / replay / simulation 引擎。
- 新增任何 Pxx（P3/P4/P11/P22 之外）；Trace/replay 未来 buyer 不构成本轮协议理由。
- UWM-009 / 协议基线 / 产品基线文档语义修改；Second canonical truth。
- Claim evolution 的 Revise/Supersede/Withdraw realization（§18 语义已冻、
  realization deferred）；CurrentContainer A→B 合法迁移语义（本轮场景不触及）。

## Decisions
- 实现落点：`src/UniClaw.Kernel/World/`（WorldModel 即 ContainerGraphWorldModel
  realization；assembly/namespace 布局 = realization，UWM-009 §2/G7）。
- Association 为 WorldModel 内部过程：`IAssociationStrategy` 为内部 seam，
  输入可见 previous revision aggregate（owner 内部，非跨组件 view，无
  ADR-0011 冲突）；Authority gates（evidence-backing、contradiction-blocks-
  matched、prior-only-blocked、Ambiguous/Insufficient 不变异）在 WorldModel
  边界强制，不委托 strategy。
- ContainerIdentity 铸造：`ctr-` + EvidenceId 内容哈希前缀（确定性，replay
  稳定；算法 = realization）。
- AssociationDecision append-only log（同 RelevanceLog 先例）：R-UW-02/03 的
  causality/decision-context 载体，不进 revision aggregate。
- CurrentContainer / signature 以 owner-derived claims 表达（subject 约定
  ui.container.current / ui.container.signature.<id> = realization）。
- P22 consumer 侧落地：TransitionContext 为 Reconcile 可选输入；producer 导出
  缝（EB 侧 wiring）= realization，本轮仅测试构造。
- SpatialFrame 规则 owner 侧执法：subject `spatial.<frame>.*` 约定，裸
  spatial subject fail-closed（P-UW-16）。
- uni-agent Trace = Legacy Evidence / Architecture Reference only（不迁移）。

## Acceptance（= 用户 graduation conditions）
1. S1–S8 GREEN；2. S9 GREEN；3. S10 GREEN；4. S11 GREEN；
5. S12 无未解释 hidden canonical input（分类 NONE 或显式 REPLAYABILITY GAP）；
6. legacy Trace 分析完成（真实代码证据）；7. 未迁移 Trace subsystem；
8. 未引入第二 canonical truth。→ 宣布
UIWORLD_EXECUTABLE_REPLAYABILITY_READY_BASELINE_ESTABLISHED。

## Constraints
既有 77 测试零回归；不修改 view allowlist 锁定面（N1–N4/N7）、Accepted7
输入封闭约束、N5 freshness 命名约束；产品代码不进 harness 层。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >
    dotnet test（全解决方案）；
    测试文件：UIWorldAssociationTests（S1,S2,S2b,S3,S4,S5,S6,S6b,S7,S8,Relations=11）
    + UIWorldReplayabilityTests（S9,S10,S11,S12,S12b=5）；doubles：UIWorldDoubles
  expected: >
    S1–S8 全 GREEN；S9–S12 全 GREEN；既有 77 测试零回归；
    R-UW-01..07 逐条满足；hidden-state 分类 = NONE
  actual: >
    93/93 GREEN（Kernel 76 + Agent 17；新增 16 [Fact]，既有 77 零回归）。
    R-UW 审计：01（S9 引用性）02（S9 causality：ParentRevisionId+EvidenceBasis+
    AssociationDecision.TransitionCorrelation）03（S9 identity 变异 decision context
    + per-container basis）04（S8/S9 SourceRevisionId）05（S4 gate：supporting/
    contradicting 与 prior 分携不压平）06（S11 immutable history / branch 隔离）
    07（S10 公共边界重放等价；S12 反射审计无可变静态；S12b 三次独立运行
    canonical 结果一致）。hidden canonical input = NONE（FreshnessBasis 源于
    CaptureTime 输入；identity 铸造内容派生；revision id 序列派生；无
    wall-clock/random/ambient/global mutable）。
    §18 Revise 最小 realization：owner-derived claims（CurrentContainer/signature）
    用 revise、evidence claims 保持 conflict（S7 共存证明）。
  evidence: dotnet test 输出（2026-09-09，76+17 全绿）；测试文件与
    docs/analysis/uni-agent-trace-replayability-analysis.md（代码级证据表）
```

## Status log
2026-09-09 · understanding→resolved · 基线/约束/legacy 研究完毕（uni-agent Trace 代码级 inventory）
2026-09-09 · resolved→persisted→planned · state.md 建立；TDD RED 开始
2026-09-09 · planned→implemented · RED（缺类型编译失败）→ 产品代码（TransitionContext/ContainerAssociation/WorldModel/UniKernel）→ 首轮 3 失败（CurrentContainer 合法迁移被 conflict 机制卡死 ×2 + spatial guard 被 relevance 短路）→ §18 Revise 语义最小 realization + scope 修正 → GREEN
2026-09-09 · implemented→reviewed · REVIEW：无越界改动（无 Trace subsystem、无新 Pxx、view allowlist 面/Control 签名零触碰；WorldBeliefRevision 仅追加可选 Containers/Relations）；既有 77 零回归
2026-09-09 · reviewed→verified→closed · 93/93 GREEN；R-UW-01..07 逐条核验；legacy 分析完成（Authority: NONE 落 docs/analysis/）；graduation 8 条件全满足 → UIWORLD_EXECUTABLE_REPLAYABILITY_READY_BASELINE_ESTABLISHED
