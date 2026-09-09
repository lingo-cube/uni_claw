# PER-003 — 真实感知资产驱动 Entity Model（real-asset strategies + P22 producer 导出缝）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: a43b0cf4

## Intent（WHAT/WHY）
UIW-003/004 落地的 entity model 目前只被测试 doubles 驱动；PER-002 的
target-neutral corpus（真机帧：SCROLL-01 v1→v2、POPUP 系列、NAV-03、golden-run
真实 YOLO+OCR）已 committed 但未触达新 seams。本 change：① 产品侧补 P22
producer 导出缝（UWM-009 S2 Scroll Continuity 具名 buyer 的最后一块）；
② 用真实 corpus 资产驱动 observation / association / continuity 三 seams 的
deterministic 真实策略（test 侧，PER-002 "corpus 约定留 corpus 层" 先例），
端到端再现冻结语义。

## Scope
- 产品（最小面）：`EffectBoundary.ExportTransitionContext(effectClass, receipt)`
  → TransitionContext（transitionKind=effectClass、correlation=ReceiptId、
  Strength=Attempt）；`ActResult` 增 `TransitionContext?`（receipt null → null；
  AttemptReport reflux 不携带 TC——irrelevant 路径无作用）；协议基线 P22
  Status 一行同步（producer seam landed）。
- 测试（真实资产）：corpus 驱动三 seams 真实策略（frame 按 Provenance.Scope
  = artifact id 分组；occurrence 提案按 corpus 约定：class→Role、text→
  SemanticDescriptor、keyed nodes；association = 真实 signature Matched/New +
  Overlays；continuity = row-anchor SemanticDescriptor 匹配）+ 端到端场景
  E1–E6（见 Acceptance）。

## Out of Scope（禁止）
- 产品侧策略 realization（seam 家族保持 realization-agnostic；corpus 约定
  不得进产品代码）。Control/Traversal；EntityScopedObligation 物理入口；
  perception-side projection；corpus/评估文档改动；UIW-003/004 语义与既有
  测试断言改动。

## Decisions（Leader 预固定）
- D1 P22 producer：导出方法在 EB（runtime effect flow owner 侧），Act 组合缝
  填充 ActResult.TransitionContext；Kernel 零新状态；TC 语义严格遵守
  ADR-0012（non-evidentiary prior，不 establish Matched/New）。
- D2 真实策略全部 test 侧、确定性（同调用序列同输出，replay 稳定）、零
  wall-clock/random。
- D3 frame 分组 = record.Provenance.Scope（FastPerception 产物 = "artifact:<id>"）；
  新 scope 到达 = 新 frame（occurrence 集合随帧替换——revision-local 语义再现）。
- D4 association：signature 相等 → Matched（supporting = 当前帧 signature
  evidence）；无候选 → New；POPUP 序列 → 第二 container + Overlays（双方
  evidence 齐备才过 gate）。P22 prior 只影响 ranking，反证压过 prior。
- D5 continuity：候选 SemanticDescriptor == item anchor → SameReferent；不等
  → Contradicted；缺失 → Insufficient；多候选同描述 → Ambiguous。
- D6 诚实覆盖：语料缺失的场景如实标注 REAL_ASSET_COVERAGE_PARTIAL 族标记，
  不伪造（PER-002 先例）。

## Acceptance
E1 golden-run DIRECT 帧 → container New + occurrence 景观（真实 YOLO/OCR 值）
E2 SCROLL-01 v1→v2 真机帧 + ActResult.TransitionContext（P22 producer）→
   Matched、ContainerIdentity 稳定、行文本位移入 Conflict（PER-002 S3 语义
   真资产再现）
E3 真实 continuity：demand → ReferenceEstablished → 滚动帧 → SameReferent
   （row-anchor）→ ResolveCurrent → 重绑
E4 POPUP-01 真实 dialog → 第二 container New + Overlays relation
E5 NAV-03 parent/childA 同文本真帧 → twins/recycled 语义（Ambiguous 或
   Contradicted，按实际判别证据，不强制）
E6 P22 prior 不压反证：signature 变化时 prior 在场也不 Matched
E7 replay 确定性（同帧序列同 ids/决策）+ 既有 152 零回归

## Constraints
corpus 只读；真实资产不可伪造；产品 diff 面 = EffectBoundary.cs + UniKernel.cs
（ActResult 一字段 + 组合一行）+ TargetBinding 无；不改 view allowlist。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ 真实资产使用清单（场景×帧×真值）
  expected: E1–E7 GREEN；全解决方案全绿；产品 diff 最小面
  actual: >
    159/159 GREEN（Kernel 142 + Agent 17；新增 E1–E7，既有 152 零回归——Leader
    独立复跑确认）。产品面 = EffectBoundary.ExportTransitionContext + ActResult.
    Transition 可选字段 + Act 组合一行 + 协议基线 P22 Status 单 bullet；
    World/ 零改动。真实资产 7 帧全经 RawArtifact.Capture(真实 PNG) →
    FastPerception → Process。语料真值如实落断言：E3 = Contradicted
    （"Item 01" 滚出视口，item 保持 Established）、E5 = Matched+SameReferent
    （parent/childA signature 真实相等）；E6 prior 不压签名反证；E7 双实例
    replay ids/log 逐项相等。诚实标注 2 条：golden 帧无 page signature /
    ui.node.* class（detect 家族扩展 + 帧首条建 root 策略）。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；git diff 抽审记录
```

## Status log
2026-09-08 · understanding→resolved · corpus/Process/Act 实况确认（P22 consumer
  面在、producer 缺；Provenance.Scope 可作 frame 分组键）
2026-09-08 · resolved→persisted→planned · state.md 建立；切片 = P22 producer →
  真实策略 → E1–E7 → 全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent：RED（ActResult.Transition
  编译失败）→ ExportTransitionContext + Act 填充 + corpus 三真实策略 + E1–E7 → GREEN
2026-09-08 · implemented→reviewed · REVIEW：产品面最小（EB/UniKernel/协议一行）；
  P22 红线合规（零副作用、reflux 不携带 TC、短路路径 null、prior 不压反证 E6）；
  E3/E5 按语料真值断言未强制；诚实标注 2 条（golden detect 家族 / 帧型区分）
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 159/159 GREEN + diff
  抽审合规 → REAL_ASSET_ENTITY_MODEL_ESTABLISHED（UWM-009 S2 具名 buyer 全链点亮）
