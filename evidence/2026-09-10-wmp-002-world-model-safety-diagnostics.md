# WMP-002 — World Model Optimization Safety Diagnostics Evidence

## 1. Baseline / isolation

- Base: `687b6e7b4b95d4d9175153a70f1f912530c4a09d`（MRB-001 提交后的 uni-harness
  HEAD）；WMP-001 `88e959a3` 为其祖先；src/tests/schemas 零 dirty。
- 入口隔离检出 `/tmp/wmp002-preflight.avdIyL/repo`：Kernel 262/262 +
  Agent 17/17 = 279/279 GREEN。
- 动手前复验 WMP-001 五个 canonical hash 在该 HEAD 全部复现（与 evidence §3
  截断记录一致），才允许固化为 golden。

## 2. Canonical oracle（test-only，公共 interface 差分）

- 位置：`tests/UniClaw.Kernel.Tests/WorldModelCanonicalOracle.cs` +
  `WorldModelCanonicalOracleTests.cs`（9 tests）。生产零改动参与比较：expected
  = 同一 immutable revision 公开 canonical 集合的朴素全表扫描（Occurrences
  线性过滤 / WorldState 扫描 / Conflicts 扫描 / demand registry 扫描），actual
  = 优化路径（WorldRevisionIndex + persistent COW）经公共 `WorldModel` 接口输出。
- First-Divergence 输出（稳定单行 + 人读 context，可 grep/parse）：
  `WMP-DIVERGENCE schema=wmp-canonical-oracle/1 op=<op> owner=<owning-seam>
  rev=<id> key=<lookup> expectedCount=<n> actualCount=<m>
  firstDivergentPosition=<i> expected=<identity|value> actual=<identity|value>`。
  值仅稳定标识/短值（identity 与 value 均 96 字符截断），无 secret/原始
  artifact/对象地址。owner 按 op 族映射到最小 owning seam
  （CanonicalOracle.OwnerFor），直接对齐 uniclaw-debug-evidence 的
  Expected/Observed/Gap/FDP/Owner 语义。
- Red-capability 常驻自检：`Oracle_SelfCheck_ReportsParseableFirstDivergence`
  用构造的分歧序列验证 oracle 本身每次全量运行仍能 RED 且输出行逐字段可
  parse（sabotage 矩阵属性的永久重证，不破坏生产代码）。
- 覆盖矩阵（全部逐项比较，非 count/hash-only）：container append/Matched
  replace、claim-before-container、occurrence replacement、role/container/
  descriptor scope projection、LogicalItem append + referent termination、
  demand register/幂等 update/middle-delete/tail-delete、conflict append、
  Revise 痕迹链、continuity-only revision（含 Assert.Same 引用共享）、ordinary
  revision advance、stale occurrence、stale binding（IsSliceValid + 内容冻结）、
  missing key（fail-closed/NoCandidate/false）、重复查询、历史 replay（第二世界
  全量重放逐 revision 比较）、EvidenceBasis 内容+顺序、公开集合内容与顺序、
  小/中/大（8/64/512）+ real-asset cold/warm/partial/grounding（corpus 三场景
  多轮）。
- WMP-001 五个 canonical hash 固化为 golden 常量并每轮断言。

## 3. 顺序语义发现（对任何后续 World Model 工作重要）

1. `WorldState` / `EvidenceBasis` 公开枚举顺序不是插入序：`FrozenDictionary`
   按内部 bucket 布局输出（rev-6 首位是 claim-3，不是 claim-0）。WMP-001 的
   lazy Frozen projection 忠实复现 baseline 的 frozen 布局，golden hash 因此
   全等。
2. frozen 布局**不是 key set 的纯函数**：n=8/64 顺序无关，n=20/512 对构造
   输入序列敏感（reversed/sorted/shuffled 输入产生不同布局）。
3. n=512 时**增量链布局 ≠ 任何 fresh 重建**（chain 输入 = parent 布局 + 新键，
   逐级 re-freeze）。推论：任何「从 storage / spine 直接重建视图」的实现都会
   改变公开顺序；canonical 顺序只能由「parent materialized 布局 + 新键」的
   构造序列定义。
- Oracle 的顺序模型据此实现：测试侧用 BCL frozen 集合按驱动已知 delta 独立
  维护同一条增量链（同 overload、同输入序列），与生产链逐项比较。

## 4. Sabotage RED→GREEN 矩阵（scratch 检出执行，不提交）

对 `WorldRevisionIndex` 定向破坏，每次仅一类，oracle 必须在首次分歧处 RED，
恢复后 GREEN（已全部验证，恢复后 9/9 GREEN）：

| # | 破坏类 | 实现方式 | RED 捕获点（FDP） |
|---|---|---|---|
| S1 | 顺序颠倒 | role bucket `Insert(0,·)` | `ResolveCurrent` pos 0：expected `occ-…-0` actual `occ-…-2` |
| S2 | 条目遗漏 | `OccurrencesById` 丢末条 | `ResolveCurrent` expectedCount=3 actualCount=2 pos 2 expected=`<save/submit>` actual=`<none>`；real-asset golden RED |
| S3 | stale revision | `ForContinuityRevision` 对 +1 增长复用 parent 索引 | continuity 场景：SameReferent 被降级 Insufficient（gate 在 stale 索引上找不到新 item） |
| S4 | 错误 bucket | `OccurrencesByRole` 以 descriptor 为键 | `ResolveCurrent` expectedCount=3 actualCount=0 pos 0 |
| S5 | 错误 identity | `OccurrencesById` off-by-one 映射 | `BindingView.Locator` expected X1=0.1 actual X1=0.3（fixture 补强后捕获；原 fixture 的 occurrence 全部 locator/native=null 时该类破坏不可观测——已补 owner/locator/native/state 可区分 fixture） |
| S6 | conflict bucket 遗漏 | 增量 SetItem 丢弃 priorEntries | `OutcomeView.Conflicts` expectedCount=3 actualCount=2 pos 1 expected `b-v2` actual `b-v3` |

S5 的首轮漏捕本身是发现：oracle 对「全 null fact」的 occurrence 缺乏 identity
分辨率，已用 `RichRotatingObservationStrategy` 补强。S6 首轮破坏落在正常流
不可达的 full-rebuild 分支（conflicts 始终 +1 增量），改破坏增量分支后 RED。

## 5. Lazy canonical materialization 调查与处置

### 测量（allocation 为主证据 = GC.GetAllocatedBytesForCurrentThread 差分；
ticks 仅辅助；reference = 同键数一次 fresh frozen 构建）

修复前（WMP-001 实现首枚举沿 parent chain materialize 并永久缓存全部祖先）：

| size | 首枚举 WorldState | 首枚举 EvidenceBasis | 衍生物化视图数 | replay-after-touch | 触碰 Current 后保留 |
|---:|---:|---:|---:|---:|---:|
| 8 | 9,664 B | 8,040 B | ~0.1×ref | 360 B | — |
| 64 | 446,600 B（24.1×） | 392,960 B（21.2×） | ~24 / ~21 | 2,600 B | — |
| 512 | 24,828,680 B（124.7×） | 23,904,016 B（120.0×） | ~125 / ~120 | 20,520 B | **+12,091,888 B**（强制 GC 后仍保留） |

结论：确认两个危害——(a) 首次枚举 Current 在一次调用内做 O(N·n) 链式
materialization（512 时 ~48.7MB 分配 / 28.6M ticks 辅助）；(b) 触碰一个
revision 即永久缓存全部祖先 Frozen views（persistent storage + 全量 Frozen
copies 同时保留，revision history append-only 因而不释放）。

### 方案裁决（依据 §3 发现）

- spine / storage 直接重建：**否**——改变公开顺序（golden 破），见 §3.3。
- eager 逐 revision 物化：**否**——把 O(Σ) 成本移回 reconcile，等价回退
  WMP-001 的核心收益（其 evidence 记录 baseline reconcile alloc 43.5MB → 2.7MB）。
- **已实施：transient 祖先计算**——子视图仍由 parent materialized 布局构建
  （输入序列、overload、comparer 与原实现逐字节一致 ⇒ 布局与公开顺序不变），
  但祖先视图仅在构建过程中临时计算，不写入祖先字段。保留量 ∝ 实际被枚举的
  revision。生产 diff 仅 `PersistentRevisionCollections.cs` 两个 CanonicalView
  getter（+文档）。

### 修复后

| size | 首枚举 WorldState | 首枚举 EvidenceBasis | replay-after-touch | 触碰后保留 |
|---:|---:|---:|---:|---:|
| 8 | 10,488 B | 7,776 B | 8,184 B | ≈0 |
| 64 | 454,520 B（28.3×） | 391,728 B（24.4×） | 444,440 B | ≈0 |
| 512 | 24,894,968 B（139.9×） | 23,896,592 B（134.3×） | 24,814,072 B | ≈0（-4.3MB 读数为 GC 噪声，量级信号：12.1MB → 0） |

- 保留危害消除：12.1MB → ≈0（512 revisions）。
- 首枚举分配不变（+0.3%，工作本质未变）——这是该修复的**有意边界**：链式
  布局依赖使 lazy 链的 O(N·n) 首触工作不可消除，除非回退 WMP-001 的
  reconcile 收益（已拒绝）。正常稳态（每新 revision 触碰一次）成本与修复前
  相同（parent 已缓存 → 单次 O(n)）。
- replay-after-touch 从 20.5KB（祖先已全部缓存）回到 ~24.8MB ≈ 未触碰链的
  固有 O(Σ) 成本。说明：forward replay 中每个 revision 首次枚举即自缓存
  （`_canonicalView ??=` 作用于被枚举对象自身），因此全历史正向重放每个视图
  恰好构建一次（总量 Σ），不存在「反复重建」；20.5KB 的旧便宜以永久保留
  12.1MB 为代价。内容/顺序/相等性不变（oracle + golden + 全量测试证明）。
  这是用「永久保留 12.1MB」换取的一次性工作，非新引入的渐进阶。
- 修复含确定性 eager 回归 guard：reconcile 每 revision 分配上限 24KB
  （实测 ~5.3KB/rev；WMP-001 eager baseline 在 512 时 ~85KB/rev——若未来
  重新引入逐 revision eager 物化，此断言 RED）。
- 重复枚举断言（确定性边界）：≤ 64 + 16·entries 字节，修复前后均成立。

## 6. 验证

- 主树全量：Kernel 275/275（新增 oracle 9 + materialization probe 4）+
  Agent 17/17。
- Sabotage 矩阵 6/6 RED→GREEN（§4）；恢复后 oracle 9/9 GREEN。
- WMP-001 全部 canonical 场景（golden 5 项、replay、invariance、引用共享）
  在修复后保持全等。
- clean isolated checkout 全量与双轴 review 结果见 §7（CLOSED 前补记）。

## 7. REVIEW / VERIFY

- Standards review（subagent，base 687b6e7 工作树 diff）：无硬违规
  （Product/Harness 分离、surgical scope、注释语言均合规）。judgement calls
  已处置：无效 reconcile 测量块 → 真实测量 + eager 回归确定性 guard
  （512 实测 2,690,720B，上限 24KB/rev；WMP-001 记录 2,695,880B）；递归
  BuildCanonicalView → 显式栈迭代（深度免疫，append-only 历史不再受栈深
  约束）；golden 映射表去重（单一来源）；Divergence 11 参数 →
  DivergenceFact 聚合；OracleEntry 投机字段删除；ConflictProposal 参数更名。
  保留 non-blocking：两个 Builder 的镜像结构（沿用既有形状，泛型抽象收益
  不足）；两个 probe 文件各自携带 Proposal helper（仓库先例：自包含 probe）。
- Spec review（subagent）：已处置——FDP 增加 owner 字段；删除未被调用的
  ExpectedEvidenceBasis；identity 输出截断（Render 统一 Sanitize）；real-asset
  battery 的 descriptor 探测从每 role 首个扩展为全部 (role,descriptor) 组合；
  red-capability 固化为常驻元测试；eager reconcile guard 落地。对 reviewer
  两点结论的部分不同意并说明：(1)「重放时每个祖先视图被反复重建」不成立
  ——forward replay 中每个 revision 枚举即自缓存，全历史重放每视图恰好构建
  一次（Σ），与未触碰链的固有成本相同；(2)「D4 逐字节一致被违反」不成立
  ——构造输入的元素序列与 ToFrozenDictionary overload 均与原实现一致
  （懒生成 IEnumerable），布局不变由 golden/oracle 全绿证明；措辞已改为
  「元素序列与 overload 一致」。
- 修复复审后主树全量：Kernel 276/276（含自检元测试）+ Agent 17/17。
- clean isolated checkout（提交树）：见下方 VERIFY 行。

## 8. 对 WMP-001 evidence 的更正说明（不改动原文）

WMP-001 evidence 中「optimized-off/on」措辞的实际语义是**跨提交 probe**：
同一 public-interface-only probe 源文件分别在 baseline 提交（b7f430d1）与
WMP-001 提交运行并比较 hash——不存在任何运行时优化开关、配置或双实现。
WMP-002 之后该语义进一步固定：生产只有唯一行为实现，等价性由常驻 oracle
测试在每次全量测试中自动重证。
