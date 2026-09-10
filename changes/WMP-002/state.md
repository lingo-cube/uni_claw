# WMP-002 — World Model Optimization Safety Diagnostics（canonical oracle + First-Divergence + lazy materialization 处置）
lifecycle_state: closed · disposition: implemented · depth: decision-heavy · base: 687b6e7

## Intent（WHAT/WHY）
WMP-001（88e959a3）为 World Model 引入 revision-bound 索引与 COW persistent
collections，其等价性证据是跨提交 probe hash。本 change 补上**常驻、可重复、
确定性**的安全网：test-only canonical oracle 证明优化路径不会静默改变 canonical
行为，且任何索引/COW 分歧都能在**第一次分歧处**被机器定位。目标不是增加优化
开关，而是让"WMP-001 行为 == canonical 行为"成为每次全量测试自动重证的命题。

## Scope
- `tests/UniClaw.Kernel.Tests` 新增 oracle 测试：expected 由同一 immutable
  `WorldBeliefRevision` 的**公开 canonical 集合**朴素全表扫描独立计算；actual 经
  公共 `WorldModel` interface（ResolveCurrent / DeriveSlice / DeriveBindingView /
  DeriveActionAssuranceView / DeriveOutcomeAssuranceView / ResolveContinuity /
  demand registry / RevisionHistory）取得；逐项全等比较，不只比 count/hash。
- 所有 mismatch 生成稳定、可机器解析的 First-Divergence 报告（见 D2）。
- 单独调查 `PersistentRevisionDictionary` / `PersistentRevisionSet` 的 lazy
  canonical materialization 副作用（首次枚举沿 parent chain 全量 materialize）；
  确认后以最小 owner-internal 方案修复（见 D4），不改变公开内容/顺序/相等性。
- 新 evidence 记录：canonical differential、测量、sabotage RED→GREEN 矩阵、
  WMP-001 evidence 中"optimized-off/on"措辞的更正说明（跨提交 probe、非运行时
  开关）。

## Out of Scope（禁止）
- 禁止：public `WorldModel` 构造参数、产品配置、环境变量、运行时
  optimized/reference 双实现、在线热切换、正常运行时 shadow execution、跨
  revision view/binding 缓存、新的权威状态。
- 禁止：诊断逻辑参与 WorldBelief / revision / replay / Assurance / Effect 决策。
- 禁止：为测试暴露 `WorldRevisionIndex` 或扩公开接口；公共 interface 构造不出
  red-capable 回路时先记录 seam gap 回 RESOLVE（当前评估：无 gap）。
- 禁止：修改历史 WMP-001 evidence 文件；禁止把 oracle 逻辑放进 src/。
- 禁止：修改 AGENTS.md / uniflow / diagnosing-bugs / uniclaw-debug-evidence
  （Skill 层属 DBG-001）。

## Decisions
- D1 oracle 只做三角验证：expected = 同 revision 公开集合的朴素扫描（测试侧
  独立实现，非生产 reconciliation 复制品）；actual = 生产优化路径输出。生产
  仍只有一个行为实现，默认运行路径零额外 canonical scan。
- D2 First-Divergence 输出格式（稳定、单行、可 grep/parse）：
  `WMP-DIVERGENCE schema=wmp-canonical-oracle/1 op=<op> rev=<revisionId>
  key=<lookupKey> expectedCount=<n> actualCount=<m> firstDivergentPosition=<i>
  expected=<identity|value> actual=<identity|value>`，随后附人读 context 行。
  值仅取稳定标识/短值（长度截断），不含 secret、原始大体积 artifact、对象地址。
  字段覆盖 uniclaw-debug-evidence 的 Expected/Observed/Gap/FDP/Owner 语义。
- D3 sabotage 验证矩阵（人工 RED→GREEN，全部在 scratch 检出执行、不提交）：
  顺序颠倒、条目遗漏、stale revision 索引、错误 bucket、错误 identity、
  conflict bucket 遗漏；每项须使 oracle RED 且 FDP 指向首分歧，恢复后 GREEN。
- D4 materialization 处置：先测量（8/64/512 revisions 下 reconcile、首次
  Current.WorldState/EvidenceBasis 枚举、重复枚举、全 RevisionHistory replay 的
  allocation/ticks/materialized entry 数，allocation 为确定性主证据、ticks 仅
  辅助）；确认后以最小 owner-internal 方案修复。
  【2026-09-10 实证修正】frozen 枚举布局不是 key set 的纯函数：小规模呈
  顺序无关，n=20/512 时对构造输入序列敏感，且 512 时增量链布局 ≠ 任何
  fresh 重建——因此「结构共享 canonical order 序列/spine 直接重建」会改变
  公开顺序（golden 会破），不可行；跨 revision Consumer View cache 亦被
  任务禁止。已实施的最小修复 = **transient 祖先计算**：子 revision 视图仍由
  parent 的 materialized 布局构建（输入序列与 overload 与原实现逐字节一致，
  布局不变），但祖先视图只临时计算、不永久缓存——保留量与「实际被枚举的
  revision」成正比。修复不能消除的剩余风险：首次枚举长链的 O(N·n) 链式
  工作内在存在于 lazy 布局链（eager 替代 = WMP-001 已拒绝的 reconcile 回归），
  详见 evidence。
- D5 golden hash：沿用 WMP-001 evidence 记录的 canonical hash（8/64/512 与
  real-asset）作为 COW/顺序不变性的固定 golden 值固化进测试；修复前后必须
  逐字节一致。

## Assumptions
- Public 集合可观察枚举顺序（WMP-001 Assumption）继续按兼容面保留；oracle 对
  顺序敏感（顺序漂移 = 分歧）。
- GC.GetAllocatedBytesForCurrentThread 差分在同线程同代码路径下确定，可作
  RED 门槛；不设任何 wall-time 绝对阈值。
- 测试可见 internal（既有 InternalsVisibleTo）但不用于 oracle 断言本身。

## Alternatives（含被拒）
- 运行时 reference 双实现 + shadow diff：拒绝（第二行为实现 + 常驻扫描成本）。
- 只比 count / 最终 hash：拒绝（无法定位首次分歧，任务明令禁止）。
- 把 oracle 放 src/ 供生产自检：拒绝（诊断逻辑进入决策邻域的风险）。
- materialization 修复用跨 revision Consumer View cache 规避：拒绝（任务明令）。
- materialization 修复用纯共享 spine 直接枚举（无 frozen）：拒绝（见 D4，枚举
  每键 AVL 查找，热路径回归风险大于收益）。

## Owner-Authority impact
零变更。World Model 仍是 sole authority；oracle 位于测试，不进产品；D4 修复是
`PersistentRevisionCollections` owner-internal realization 变更，公开接口、
枚举顺序、相等性、replay 语义不变。

## Acceptance
- A1 生产只有一个行为实现；默认运行路径无额外 canonical scan（diff 证明无
  src 侧 scan/shadow 逻辑）。
- A2 oracle 能捕获人工构造的顺序、遗漏、stale revision、错误 bucket、错误
  identity 分歧（sabotage 矩阵全 RED→GREEN），失败报告精确指向首次分歧。
- A3 WMP-001 全部 canonical 场景继续全等（含 golden hash、real-asset
  cold/warm/partial/grounding、replay、EvidenceBasis、集合顺序）。
- A4 覆盖矩阵：container append/replace、claim-before-container、occurrence
  replacement、role/container scope projection、LogicalItem append/termination、
  demand register/update/middle-delete/tail-delete、conflict append、
  continuity-only revision、ordinary revision advance、stale occurrence/binding、
  missing key、重复查询、历史 replay、EvidenceBasis、公开集合内容与顺序、
  小/中/大三档 + real-asset 四场景。
- A5 materialization：首次枚举与 retained-memory 风险有确定性证据与处置
  （修复或 Human Gate），公开行为不变。
- A6 clean isolated checkout 全量测试通过；REVIEW（code-review 双轴）与 VERIFY
  分离；只提交 WMP-002 精确文件。

## Constraints
- 测试限 `tests/UniClaw.Kernel.Tests`；产品修改限
  `src/UniClaw.Kernel/World/PersistentRevisionCollections.cs`（仅当 D4 确认）；
  文档限本 state 与新 evidence。
- 不改 `RuntimeStageMetrics` 词表、既有 metrics 定义、trace 词表、对外序列化
  形状；不破坏既有 WMP-001 测试断言。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >-
    oracle 10 tests + materialization probe 5 tests + 全量 dotnet test
    UniClaw.Kernel.slnx（主树与 clean isolated 精确提交树 a026c11）+
    sabotage 矩阵 6/6 RED→GREEN（scratch 检出）+ WMP-001 golden 5 项复现
  expected: >-
    A1-A6 全满足；优化路径与 canonical scan oracle 逐项全等；golden 不变；
    修复后保留 ≈0、reconcile 无 eager 回归（guard 常驻）；isolated 全量 GREEN
  actual: >-
    主树 Kernel 276/276 + Agent 17/17；isolated 精确提交树 Kernel 276/276 +
    Agent 17/17；sabotage 6/6 RED（FDP 首分歧定位）→恢复 GREEN；512 首枚举
    124.7×→139.9×（链式工作内在，已文档化）、保留 +12.1MB→≈0、reconcile
    2,690,720B（guard <12.3MB）；Standards/Spec 双轴 PASS（处置记录见 §7）
  evidence: evidence/2026-09-10-wmp-002-world-model-safety-diagnostics.md
```

## Status log
2026-09-10 · enter→understanding · 复核基线：HEAD=687b6e7（MRB-001 提交后），
  WMP-001=88e959a3 为祖先，src/tests/schemas 零 dirty，仅两无关 untracked；
  /tmp/wmp002-preflight.avdIyL 隔离检出全量 Kernel 262/262 + Agent 17/17。
  读毕 AGENTS/uniflow/diagnosing-bugs/uniclaw-debug-evidence/WMP-001
  state+evidence、World 全部实现与 WMP 测试。确认公共 interface 无 seam gap。
2026-09-10 · understanding→resolved→persisted→planned · D1-D5 与 A1-A6 冻结。
  Plan：oracle 引擎+场景（S/M/L+real-asset+golden pinning）→ sabotage 矩阵
  → materialization 测量→裁决→（若可行）最小修复→全量→REVIEW→VERIFY。
2026-09-10 · planned→implementing · oracle 9 tests GREEN。过程中三项顺序语义
  发现（frozen 布局非插入序、布局对构造序列敏感 n=20/512、增量链布局≠fresh
  重建），oracle 顺序模型改为测试侧 BCL frozen 增量链（同 overload 同序列）；
  ExpectedScopedClaims/ExpectedOutcomeClaims 顺序模型改为 BCL frozen 投影。
  WMP-001 五 golden hash 在 HEAD 复现后固化。
2026-09-10 · implementing（materialization）· 测量确认两危害（512 首枚举
  124.7×/120×ref ≈ 48.7MB；触碰后永久保留 +12.1MB）。spine/storage 重建经
  实证否定（改变公开顺序）；实施 transient 祖先计算修复（输入序列与 overload
  逐字节一致，布局不变；祖先视图不缓存）。修复后主树全量 275/275+17/17，
  golden/oracle 全绿；保留 ≈0，replay-after-touch 回到未触碰链固有 O(Σ)。
2026-09-10 · implementing→reviewing · sabotage 矩阵 6/6 RED→GREEN（顺序/
  遗漏/stale revision/错误 bucket/错误 identity/conflict bucket 遗漏；S5 首轮
  漏捕暴露 fixture 盲区，已补 RichRotatingObservationStrategy；S6 首轮落在
  不可达分支，改破坏增量分支）。双轴 REVIEW 启动（Standards/Spec 并行
  subagent）。注意：主树另出现并发会话 dirty（CONTEXT.md、changes/PER-004/、
  docs/analysis/uni-agent-perception-implementation-analysis.md、
  show-me-perception-arch-diff.html），非 WMP-002 所有，提交时精确排除。
2026-09-10 · reviewing→verifying · Standards=无硬违规（judgement calls 全部
  处置：真实 reconcile 测量+eager guard、迭代式 transient 构建、golden 表
  去重、DivergenceFact 聚合、OracleEntry 简化、参数更名）；Spec=处置 owner
  字段、删除未用 helper、identity 截断、全 (role,descriptor) 探测、red-
  capability 常驻元测试，另有两点 reviewer 结论经核实后部分驳回并书面说明。
  修复后主树全量 Kernel 276/276 + Agent 17/17。VERIFY：clean isolated
  checkout 提交树全量 + 精确提交。
2026-09-10 · verifying→closed · 精确提交树（a026c11）隔离检出全量 Kernel
  276/276 + Agent 17/17 GREEN；A1-A6 满足；仅提交 WMP-002 六文件，并发会话
  dirty（CONTEXT.md、changes/PER-004/、两无关 untracked 文档）全部保留未动。
