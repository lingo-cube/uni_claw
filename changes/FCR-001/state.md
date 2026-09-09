# FCR-001 — Frame Computation Reuse（P1：FastPerception 内部有界版本化确定性计算复用）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 9f3feb68

## Intent（WHAT/WHY）
相同 RawArtifact（同 ArtifactId = 同内容）被同一 strategy 以同一 version 重复
呈现时，strategy computation 只执行一次；LAT-001 warm fixture 的 strategy
invocation 4→1 为核心验收。缓存是 FastPerception 所有权内部实现细节；
canonical outputs（ObservationProposal 业务内容、Evidence、WorldRevision、
WorldBelief、Occurrence、Slice、ResolveCurrent/Grounding）与关闭缓存时
完全一致。前置 LAT-001 CLOSED（fb8e45b ∈ 历史 ✓，基线可复现：隔离
worktree 224/224 复核）。

## Scope
- `Perception/FastPerception.cs`：新最小 seam
  `IVersionedFastPerceptionStrategy`（StrategyIdentity + StrategyVersion，
  实现者契约：identity 跨 run 稳定；version 在模型/规则/配置/实现语义变化
  时必须变化；禁进程随机值/对象引用/时间）；缓存集成（可选容量参数）。
- `Perception/StrategyObservationCache.cs`（新，internal）：键 =
  (StrategyIdentity, StrategyVersion, ArtifactId) ordinal；值 = 防御性拷贝
  的 ArtifactObservation[]（唯一缓存物）；有界 LRU（容量显式，默认 256）；
  single-flight（同键并发只算一次）；失败/取消不写缓存；命中返回新拷贝。
- `Diagnostics/RuntimeStageMetrics.cs`：additive 计数 CacheLookups /
  CacheHits / CacheMisses / CacheEvictions / CacheSingleFlightWaits（不改
  既有 stage/计数定义，LAT-001 语义保持）。
- 测试 `Perception/FrameComputationReuseTests.cs`（新）+
  `PerceptionDoubles.cs`（CorpusFastPerception 实现 version seam）。
- evidence 文档：四相 cache off/on 对比（strategy 阶段节省与端到端变化
  分开报告）。

## Out of Scope（禁止）
- 缓存或跨 step/revision 复用 EvidenceRecord、Freshness、WorldRevision、
  ObservationOccurrence、GroundingView、CanonicalBinding、EffectReceipt、
  AttemptReport、验证结论（缓存物仅 ArtifactObservation 快照）。
- occurrence/association/continuity strategy 缓存（不同缝，不动）。
- batch reconciliation、World Model 索引、结构共享、post-action
  observation acquisition、模型预热、delta capture、binding reuse、
  OTel adapter（LAT-001 D9 维持）。
- UniKernel / EvidenceLedger / WorldModel / Consumer View 公共接口扩大。
- 修复并行 change 的任何问题（9f3feb68 干净检出断裂归 RVR-002/DSE-003
  会话，只报告不代修）。

## Decisions
- D1 seam 形状：`IVersionedFastPerceptionStrategy : IFastPerceptionStrategy`
  （两个 string getter）；strategy 未实现 ⇒ 缓存整体禁用（fail-safe，行为
  与今日逐字节一致——无 version 就无失效依据，宁可不缓存）。version 契约
  无法被接口强制，由实现者义务 + 测试 double 示范；接口注释写死禁止项
  （进程随机值 / 对象引用 / 时间）。
- D2 缓存物与隔离：cache 内存 = `ArtifactObservation[]` 快照（插入时防御
  拷贝）；命中每次返回新数组（调用方改不动内部值）；strategy 原始返回
  list 被 caller 改动也不影响已入缓存快照。
- D3 淘汰：容量上限显式（ctor 参数，<1 fail-closed）；LRU 按访问序
  （插入或命中刷新 recency）——调用序列确定 ⇒ 行为确定；满载插入前淘汰
  尾部；被淘汰键下次 miss 安全重算。生命周期 = FastPerception 实例
  （组合根持有），非静态全局。
- D4 single-flight：lock 分段——查缓存/登记 inflight 在锁内，computation
  在锁外；同键并发后来者等待 holder（计数 CacheSingleFlightWaits），
  成功共享同一结果、失败共同收到同型异常且缓存零污染（inflight 移除，
  后续调用重算）。产品运行时单线程，但并发契约成立并被测试。
- D5 provenance 重建：emission 阶段每次调用照旧构造全新
  ObservationProposal（当前 CaptureTime / Scope / Context / Producer /
  lineage）；缓存只省 strategy 计算，不省任何 provenance 工作。命中时
  **不**记 perception.strategy span、**不**计 FastPerceptionStrategy
  invocation（不伪造调用）；perception.observe / emit-proposal span 照常。
- D6 ArtifactId 语义边界：命中只证明输入内容相同 ⇒ 复用确定性观察结果；
  不证明世界状态/目标存在/binding 有效/effect 结果（本缓存不触碰任何
  下游面，边界由构造保证）。
- D7 验证策略：隔离 worktree @ fb8e45b（FCR 全部关联文件在 fb8e45b 与
  9f3feb68 逐字节一致，diff=0）+ FCR 文件拷入运行；脏主树结果单独报告
  归因（当前 232/232 GREEN，DSE-003 in-flight 已自行迁移其测试）。
- D8 metrics additive 面：五个 cache 计数独立于既有 stage 聚合；缓存
  禁用（未实现 seam 或容量未配置）时零计数零开销。

## Acceptance
A1 warm 4× 同 artifact：strategy invocation 4→1；且不止 mock 计数——
   proposals 内容、EvidenceIds、revision 链同等于关闭缓存态
A2 相同内容不同 CaptureTime：命中缓存 + 新 provenance proposal
   （EvidenceId 因 CaptureTime 变化而不同 → 新 revision，证明 proposal
   未被复用）
A3 ArtifactId / StrategyIdentity / StrategyVersion 任一变化 ⇒ miss 重算
A4 返回集合与缓存内部值不可被调用方修改（含 strategy 原始 list 事后改动）
A5 并发同键 single-flight：实际计算恰一次，全部调用者等价结果，
   waiter 计数正确
A6 strategy 抛错：异常上抛、缓存零污染、后续调用可重算；等待者同型失败
A7 容量上限 LRU 淘汰：确定性、可安全重算、eviction 计数
A8 四相（cold/warm/partial/grounding）cache off vs on：canonical 全等
   （EvidenceIds/relevance/revision 链含 occurrences/Slice/GroundingView）；
   仅 strategy invocations、cache 计数、trace 拓扑（命中无 strategy span）、
   ticks 允许不同
A9 null metrics/trace + 缓存启用：低开销非干扰；未实现 seam 的 strategy：
   缓存自动禁用、行为与今日一致
A10 全量测试（隔离基点）GREEN；evidence 文档分开报告 strategy 阶段节省
   与端到端变化；只提交 FCR 精确文件

## Constraints
- 产品 diff 面 = Perception/ 两文件 + Diagnostics additive；UniKernel 及
  其余 L2 零改动。
- 确定性：同驱动序列同缓存行为；零 random/GUID/wall-clock 进键或版本。
- corpus 只读；真实资产场景优先。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: 隔离 worktree @ fb8e45b + 仅 FCR 文件（dotnet test 全量 ×2 +
    FCR 压测 ×8）+ 脏主树全量（外部归因分离）+ R8 四相对比输出
  expected: A1–A9 GREEN；隔离基点 224/224 零回归 + 9 新测试全绿
  actual: >
    实现前基线：fb8e45b worktree 224/224；脏主树 232/232（DSE-003 已自迁
    allowlist）。实现后：隔离 worktree（fb8e45b + FCR）全量 233/233 GREEN
    ×2（Kernel 216 = 207 基线 + 9 FCR + Agent 17）；FCR 压测 ×8 全绿
    （并发稳定性）。核心验收 warm 4→1 双环境复现：attempts off=4 on=1、
    hit=3 miss=1；cold/partial/grounding attempts 不变（partial detection
    set 键分离生效，无误共缓存）；每相 admissions/newRevisions off/on 全等；
    R8 断言每相 canonical 投影逐字节全等；R2 同 bytes 异 CaptureTime 命中
    +全新 provenance（EvidenceId 互异→新 revision）。REVIEW 修复三项：
    ①并发缝崩溃点（Record/CountArtifactPresentation 内部锁 + 缓存计数
    Interlocked——single-flight 契约下 perception 缝可并发，观察面不得成
    崩溃点）；②identity/version 构造期捕获→逐调用读取（策略热变化即时
    失效）；③测试侧 miss 不变量（misses=1+waits）与类型面。端到端诚实
    报告：corpus double 下 all-stages 单次观测无不可声明改善（warm on 侧
    反现 +0.4ms 级噪声），reconcile 热点未触碰。脏主树后段被 DSE-003
    in-flight 新文件（AdbLiveEffectDriver.cs 编译错）打断——外部归因，
    非本面；本 change 验证以隔离 worktree 为权威
  evidence: evidence/2026-09-10-fcr-001-reuse.md（对比表/限制/语义边界）；
    隔离 worktree 两次全量 + 压测输出；R8 detailed console 双环境誊录
```

## Status log
2026-09-10 · understanding→resolved · 入口复验：fb8e45b ∈ 历史（现 HEAD
  = 9f3feb68，RVR-002 于会话间提交；其 EffectDispatch 引用 DSE-003 未提交
  的 NativeLocator → 干净检出断裂，归并行会话，不代修）。并行 dirty 面
  逐文件记录（DSE-003 19 文件 + RVR-002 state + 2 无关 untracked），FCR
  文件面与其零交集。实现前基线：隔离 @fb8e45b 224/224；脏主树 232/232
  （DSE-003 已自迁 allowlist）。D1–D8 裁决如上
2026-09-10 · resolved→persisted→planned · state.md 建立。切片：cache
  组件 → seam+FastPerception 集成 → metrics additive → doubles+R1–R9
  测试 → 隔离验证+四相对比 → evidence → REVIEW/VERIFY 分离
2026-09-10 · planned→implemented · StrategyObservationCache（LRU +
  single-flight + fail-safe）+ IVersionedFastPerceptionStrategy seam +
  FastPerception 集成（命中不伪造 span/invocation）+ 5 个 additive 缓存
  计数 + R1–R9；R8 四相对比数据双环境产出
2026-09-10 · implemented→reviewed · REVIEW（Direct）：发现并修复 ①并发缝
  崩溃点（metrics Record/CountArtifactPresentation 内部锁——R5 八线程压测
  暴露的隐性竞争）②identity/version 构造期捕获→逐调用读取 ③测试侧 miss
  不变量与类型面。词表/LAT-001 语义/边界 4（禁下游缓存）逐条复核通过
2026-09-10 · reviewed→verified→closed · 隔离 worktree（fb8e45b+FCR）全量
  233/233 ×2 + FCR 压测 ×8 全绿；warm 4→1 + canonical 全等双环境复现 →
  DETERMINISTIC_FRAME_REUSE_ESTABLISHED。P1 收口
