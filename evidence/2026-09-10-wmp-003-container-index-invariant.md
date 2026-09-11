# WMP-003 — Container Index Count-Stable Fast-Path Invariant Evidence

## 1. Base / motivation

- Base：`391fd1a8`（含另一会话 PER-004，零文件重叠）；主树 dirty 仅两份既有
  无关 untracked 文档。
- 动机：WMP-002 dogfood 的 fresh agent 识别出 `WorldRevisionIndex.Create`
  的容器索引快路径（count 相等 ⇒ 整表复用 parent `ContainerPositions`，
  `WorldRevisionIndex.cs:63-69`）依赖「容器集合 append-only / Matched 只改
  basis」这一外部不变式，而非结构自证；v0.1 无公开路径可达置换，但未来
  container 终止操作会触碰该分支。

## 2. 固化内容（test-only）

- **公开不变式扫描**（`PublicFlows_CountStableTransitions_NeverPermuteContainerIdentities`）：
  30 步交错 New/Matched reconcile + 1 次 continuity-only revision（容器集合
  引用复用、LogicalItem mint）；逐对断言「容器数不变 ⇒ identity 序列逐项
  相同（含顺序）」，非稳定迁移必为纯 append；结尾跑 WMP-002 canonical
  oracle battery。实测：29 transitions / 27 count-stable / 1 continuity /
  3 containers，全绿。
- **continuity 引用复用**（`ContinuityRevisions_ReuseContainerCollection_PositionsUnchanged`）：
  `Assert.Same(seeded.Containers, continuity.Containers)` 实证引用共享 +
  battery。
- **内部 seam canary**
  （`InternalSeamCanary_CountStablePermutation_ServesStalePositions`）：
  构造 count-stable 置换（ctr-a/ctr-b 互换），经真实 parent index 命中快
  路径，**如实断言**今日行为 = serve stale positions（a→0、b→1 为 parent
  槽位）。未来任何使该分支正确化或引入替换流的修改都会令 canary RED，
  迫使有意识更新契约。注释明示「非公开接口证据」。
- 既有测试文件唯一改动：`WorldModelCanonicalOracleTests.Battery` private→
  internal（复用而非复制 battery；state 已按 A7 修订留痕）。

## 3. REVIEW / VERIFY

- Standards 轴：1 个硬违规（state Constraints 与实现漂移——已按 A7 修订
  state 并留痕）；judgement calls 已处置（continuity 测试补 Assert.Same 使
  名实相符、canary revision id 区分、容器 id 投影去重）。保留 non-blocking：
  跨文件调用 battery（有记录的测试缝复用）。
- Spec 轴：canary 与 fast path 对齐性经独立核实（断言映射 = 代码实际行为）；
  指出 A1 的 continuity 腿未入扫描——已补齐（交错 1 次 continuity 迁移，
  逐对断言覆盖全部可达 count-stable 种类）。
- 全量：主树 Kernel 279/279 + Agent 17/17。
- clean isolated checkout（精确提交树）：见 state.md Verification。
