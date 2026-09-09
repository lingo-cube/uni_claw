# WMP-001 — World Model Internal Performance（P2：revision-bound 索引与精确 COW）
lifecycle_state: closed · disposition: implemented · depth: decision-heavy · base: b7f430d1

## Intent（WHAT/WHY）
降低每个 `WorldBeliefRevision` 的 reconciliation、occurrence/Slice 派生、
`ResolveCurrent` / `ResolveContinuity` 与 demand 查询成本。LAT-001/FCR-001
已证明 corpus-double 的首要结构性热点仍是 `WorldModel.Reconcile`；本 change
以确定性的 scanned/copied entries 与 allocations 为主要证据，stage ticks
仅作辅助事实，不设未经稳定测量支持的绝对耗时门槛。

## Scope
- 在 World Model sole-owner 内建立与 revision 同生命周期的派生索引：仅覆盖
  当前调用路径实际消费的 container identity、occurrence id/role/container、
  LogicalItem id、conflict subject、container-scoped claim 查找。
- demand registry 保持非 revision、non-evidentiary 语义；增加 owner-internal
  DemandId 与 active LogicalItem demand 查找索引，登记/更新/撤销原子维护。
- 索引稳定并独立验证后，对 reconciliation 的 canonical immutable collections
  实施精确 copy-on-write / immutable structural sharing；未变化集合复用，变化
  集合只更新受影响部分。
- 复用 LAT-001 的 stage timing；增加程序集内部、非权威的 WMP 精确规模/分配
  计数，不改变既有 `RuntimeStage` 或 aggregate 字段定义，不扩大公开接口。
- 固定 real-asset cold/warm/partial/grounding 场景与小/中/大规模合成场景；记录
  优化开/关 canonical 深度等价、索引固定成本与规模拐点。

## Out of Scope（禁止）
- capture-epoch batch reconciliation、合并 Evidence/revision、改变 admission 或
  reconciliation 顺序；模型调用、post-action observation、warm provider、delta
  capture、verification orchestration。
- 把索引暴露给 Agent、Control、Assurance、Effect Boundary、Capability Plane；
  索引不得成为第二 truth，不得进入 canonical/replay 判定。
- 跨 revision 缓存或复用 Consumer Views、Slice、GroundingView、
  CanonicalBinding、ObservationOccurrence。
- 用 `ResolveContinuity` 绕过 ordinary revision advance 的 fresh
  `ResolveCurrent`；扩大既有 evidence-backed same-referent 协议。
- Development Harness / WorkItem / Worker / OpenSpec / legacy `uni-agent` 依赖。

## Decisions
- D1 索引是 `WorldModel` deep module 的私有 implementation；现有公开 interface
  与 owner/authority/lifecycle 不变。索引由 canonical revision 派生，永不反向
  驱动 belief；revision commit 前完整构造，失败不发布。
- D2 精确性能计数只在既有 `RuntimeStageMetrics` 被注入时启用，且仅通过 internal
  seam 连接 World Model；禁用时每条路径至多一次 null check。allocations 使用
  thread-local allocated-bytes 差分，只作为 benchmark 观察事实。
- D3 occurrence 索引按原始 occurrence 序位保存；任何筛选仍按 canonical 原序
  输出。`WorldState` / `EvidenceBasis` 通过基线同构的 lazy Frozen projection 保持
  实际枚举顺序；所有公开集合与有序 list 均须逐项相同。
- D4 reconciliation 先以 revision-bound lookup 索引消除查询扫描；随后使用
  persistent immutable state/graph/basis 与精确 COW 的 conflicts/containers/
  relations。若结构共享引入公开内容/顺序/相等性漂移则回退，不以性能换语义。
- D5 `Occurrences` 每个 evidence revision 仍由 strategy 新派生并新铸 id；索引
  只索引该 revision 的新集合。continuity-only revision 沿用 occurrences 及其
  对应索引，但不复用任何外部 view/binding。
- D6 优化关闭是 internal benchmark/reference mode，不是产品配置面；生产默认
  开启，公共构造与方法不新增参数。

## Assumptions
- Product Runtime 维持现有单线程 mutation 纪律；本 change 不声明并发写安全。
- 即便 `IReadOnlyDictionary` / `IReadOnlySet` 接口未声明排序，现有 concrete
  publication 的可观察枚举顺序仍按兼容面保留；全部公开集合与 Consumer View
  list 均不得漂移。

## Alternatives（含被拒）
- 全局/跨 revision view cache：拒绝，会泄漏 stale occurrence/binding。
- 每次 revision 从 canonical 集合重建所有索引：只保留 occurrence 替换所必需
  的固定成本；其余索引增量派生，避免把读扫描转嫁成同规模写扫描。
- capture epoch batch：拒绝，改变 revision causality 与 admission/reconcile 序。
- 为性能新增公共查询接口或 option：拒绝，扩大 interface 并泄漏 realization。

## Owner-Authority impact
零变更。World Model 仍是 Belief/Reconciliation sole authority；Evidence Ledger、
Assurance、Effect Boundary、Control、Capability Plane 的 authority 与消费接口均
不变。索引与计数均为非权威 owner-internal implementation。

## Acceptance
- A1 优化开/关下 WorldBelief/WorldState/WorldGraph/Conflicts/EvidenceBasis、
  containers/relations/occurrences/LogicalItems、Slice 与 Consumer Views 深度全等；
  replay、revision chain、集合顺序一致。
- A2 小/中/大场景覆盖多 container/occurrence/LogicalItem/demand/conflict；验证
  索引建立、增量更新、替换、删除、冲突、continuity、scope projection、
  ordinary revision advance。
- A3 stale anchor/occurrence/binding fail-closed；Slice/GroundingView/
  CanonicalBinding 不跨 revision 泄漏或复用。
- A4 分阶段记录 scanned/copied entries、allocations、ticks；主要验收为扫描与
  复制规模确定性下降，耗时只报告实测；如有固定索引成本，报告规模拐点。
- A5 先完成索引并独立验证，再实施结构共享；若索引已足够，结构共享可拆后续
  change，需在 evidence 说明裁决依据。
- A6 当前 HEAD 干净隔离检出全量测试继续通过；REVIEW 与 VERIFY 分离；只提交
  WMP-001 精确文件/hunk，保留并报告全部无关 dirty 文件归属。

## Constraints
- 产品实现面限 `src/UniClaw.Kernel/World/`，以及为 internal metrics 连接所需的
  `Diagnostics/RuntimeStageMetrics.cs` / `UniKernel.cs` 精确 hunk；测试限 Kernel
  tests；文档限本 state/evidence。
- 不改变已有 metrics 定义、trace 词表、canonical clock 或对外序列化形状。

## Verification
```yaml
verification:
  level: SCENARIO
  method: WMP focused tests + LAT/FCR real-asset scenarios + dotnet test UniClaw.Kernel.slnx + optimized-off/on benchmark
  expected: A1-A6 GREEN；确定性扫描/复制下降；耗时仅如实报告
  actual: WMP 13/13；benchmark/canonical 17/17；LAT 8/8；clean isolated committed tree Kernel 261/261 + Agent 17/17；双轴 review PASS
  evidence: evidence/2026-09-10-wmp-001-world-model-performance.md
```

## Residual risks
- revision-local occurrence 全量替换意味着其索引存在不可消除的 O(O) 构建成本；
  只在每 revision 有足够消费查询时回本，需用小/中/大场景报告拐点。
- persistent collection 的常数项与枚举成本可能在小输入上高于现有数组/Frozen
  collection；必须同时报告不利场景，不以单一大场景概括。

## Status log
2026-09-10 · enter→understanding · 读取 AGENTS/UniFlow、LAT-001/FCR-001
  state+evidence；复验 DSE-003=db51140b、ADB-001=b7f430d1 均 CLOSED 且提交；
  主树仅 5 个无关 untracked，WorldModel/Slice/ConsumerViews/UiEntityModel 零
  dirty。`/tmp/wmp001-preflight.h468FY/repo` 独立克隆 HEAD=b7f430d1，
  `dotnet test UniClaw.Kernel.slnx` 248/248 GREEN（Kernel 231 + Agent 17）
2026-09-10 · understanding→resolved→persisted→planned · 热点定位为 revision
  整集合复制 + consumer/demand 全表扫描；D1-D6 与 A1-A6 冻结。Plan：索引
  RED→GREEN→独立验证；COW RED→GREEN；规模 benchmark；REVIEW；VERIFY；精确提交
2026-09-10 · planned→implementing · 先完成 revision/demand 索引与定向测试，后
  引入 persistent COW；发现直接暴露 ImmutableDictionary/ImmutableHashSet 会使
  canonical 枚举顺序漂移，改为 internal persistent storage + 基线同构 lazy
  Frozen publication，逐 revision 与 real-asset 哈希恢复全等。
2026-09-10 · implementing→reviewing · focused 语义/规模测试 GREEN；主树排除他人
  `AdbEnvironmentTests.cs` 后 Kernel 258/258 + Agent 17/17。双轴 REVIEW 启动；
  Standards 首轮发现 state 状态滞后与一处内部 lazy-freeze 形状重复，Spec 首轮
  发现 index copied 计数、Slice scoped-claim 索引和非尾 demand 删除覆盖缺口，
  当前逐项修复后再复审。
2026-09-10 · reviewing→verifying · 修复 index copied 归因、scoped-claim 增量索引、
  中段 demand 删除覆盖；复审再发现 claim-before-container 漏索引风险，改为保留
  unresolved dotted prefix 并新增 delayed-container 回归。Standards=PASS，
  Spec=PASS；仅保留 private lazy-freeze 两处形状重复的 non-blocking judgement。
2026-09-10 · verifying · optimized-off/on public probe 在 8/64/512 revisions、
  real-asset cold/warm/partial/grounding 与 64 scoped claims 上 canonical hash 全等；
  确定性扫描/复制与 5 次中位数记录见 evidence。主树 full run 唯一失败来自他人
  untracked ADB real-device test（`emulator-5554` 不存在）；排除该文件后 278/278 GREEN。
2026-09-10 · verifying→closed · WMP 精确提交树在 clean isolated checkout 中
  `dotnet test UniClaw.Kernel.slnx` 全量 278/278 GREEN（Kernel 261 + Agent 17）；
  A1-A6 满足，Standards/Spec 双轴复审均 PASS，无 WMP-owned dirty。
