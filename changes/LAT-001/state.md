# LAT-001 — Product Runtime Latency Baseline（P0：RawArtifact → Grounding 派生路径源码级延迟基线）
lifecycle_state: closed · disposition: none · depth: standard · base: 0b2cf4ef

## Intent（WHAT/WHY）
为 GREENFIELD Product Runtime 的 canonical 派生路径（RawArtifact →
FastPerception → ObservationProposal → Evidence admission → WorldModel.
Reconcile → occurrence/Slice 派生 → ResolveCurrent grounding）建立可复现的
源码级延迟/规模基线：真实执行缝的阶段耗时、调用次数、输入规模；事实基线
+ 热点排序，不设无数据支持的性能门槛。前置 CTL-001 / DSE-001 / CDS-001
均已 CLOSED（0b2cf4ef 实况复验通过）。

## Scope
- Trace 词表扩展（additive）：`TraceReferenceKind.Artifact`（第 11 成员）+
  5 个新 binding SpanDefinition：`perception.observe` /
  `perception.strategy` / `perception.emit-proposal` /
  `world.derive-slice` / `world.resolve-current`。
- `RuntimeStageMetrics`（Product Runtime 观察组件，Trace/ 家族）：per-stage
  invocations / elapsed ticks / input & output size 聚合 + 命名计数
  （admissions accepted/rejected、reconcile new/idempotent、artifact
  presentations/distinct）。非权威：null 注入 = 零开销。
- 缝绑定：FastPerception.Observe（可选 IRunTrace + metrics；strategy 计时/
  计数、proposal emission 计时/计数、artifact intake）；UniKernel（admit /
  reconcile 计时与结果计数——现有 span 结构不变；DeriveSlice 与
  ActViaCurrentGrounding 内 ResolveCurrent 新 span + 计时 + 规模）。
- occurrence derivation：经 IUiObservationStrategy 装饰器在缝上记 metrics
  （fixture 组合根提供装饰器；WorldModel 零改动）。
- 固定 benchmark fixture（tests）：PER-002 corpus 真实资产场景全集（10
  scenario：golden/scroll01 v1+v2+partial/nav03×2/popup×3/degraded），分相
  驱动 cold / warm / partial-degraded / slice+grounding；确定性计数断言 +
  原始耗时输出（ITestOutputHelper）。
- 非侵入性测试：trace on/off × metrics on/off canonical 全等；ThrowingRunTrace
  在新缝同样吸收。
- evidence 文档：测试环境、场景、原始测量、热点排序、测量限制。

## Out of Scope（禁止）
- 不设 p50/p95 门槛或任何性能断言阈值；只建立事实。
- 不实现缓存/memoization（P1）、batch reconciliation、World Model 索引、
  结构共享、post-action observation orchestration、模型预热。
- 不改 Observation/Evidence/World/Assurance/EB 的 Owner/Authority/Lifecycle；
  WorldModel / EvidenceLedger / EffectBoundary / RuntimeAssurance 零改动。
- 不引入 Development Harness / WorkItem / Worker / OpenSpec / legacy
  uni-agent 概念；不伪造当前不存在的运行路径（slow escalation 无实现，
  如实记"不存在"）。
- timing 不进 trace span 结构（TraceModel/InMemoryRunTrace 零改动；
  Deferred ⑪ canonical clock 议题不动）。

## Decisions
- D1 双观察面：trace = 结构因果（span 词表 additive 扩展）；
  RuntimeStageMetrics = 量化（耗时/次数/规模）。wall-clock（Stopwatch.
  GetTimestamp）只作非权威观测元数据落 metrics，不进 canonical、不进
  RunTraceArtifact、不参与任何决策——与 Deferred ⑪（无 canonical clock）
  相容。
- D2 occurrence derivation 不单独造 span：其真实拓扑在 world.reconcile
  span 内部（WorldModel 无 trace 注入、无 parent context 可用）；造根级
  span = 伪造拓扑。经 strategy 装饰器在真实缝记 metrics，evidence 如实
  说明该阶段耗时含于 reconcile span、独立耗时来自装饰器测量。
- D3 raw artifact capture 在 v0.1 是 host-owned seam（runtime 无 capture
  编排组件；RawArtifact.Capture 为静态纯函数）：fixture 以 metrics 的
  CaptureArtifact 阶段包裹真实 Capture 调用测量（真函数、真成本），不伪造
  runtime 绑定；作为测量限制记录。
- D4 规模计数全部经公共面观察（WorldModel.Current 的 Occurrences/
  WorldState 计数、Slice.Occurrences 计数、CurrentGroundingView.Candidates
  计数、Provenance 无关）——零 L2 内部改动。
- D5 per-artifact 归因 = fixture 顺序确定性驱动 + before/after 快照差分；
  metrics 本体不做 artifact-keyed 归因（避免第二套 correlation authority；
  trace 侧由 perception span 的 Artifact 引用承载因果）。
- D6 cold = 同 kernel 首次观察（新 revision）；warm = 同 artifact 再观察
  （EvidenceLedger 内容去重 admission + reconcile 幂等路径——两条既有真实
  路径）；完整/局部 = 完整 detection set vs partial/degraded（corpus 现成
  scenario）；fast/slow escalation = 当前运行时不存在 slow path，不伪造。
- D7 兼容：既有 evidence.admit / world.reconcile span 定义与绑定零改动；
  FastPerception 构造兼容（新增可选参数，null = 禁用观察）；UniKernel 新增
  可选 metrics 参数（null = 零开销）；全部既有调用点零迁移。
- D8 TraceReferenceKind 10 项冻结词表的第 11 成员扩展（Artifact）：P0
  验收要求 per-artifact 因果；枚举注释明示 10 项冻结于 TRC-001，本扩展随
  perception slice 落地启用（与"其余随各自 slice 落地启用"同一演化路径，
  仅种类为新增而非启用预留）——在此显式留痕。
- D9 OTel 裁决（Human 决策 2026-09-10：先不接，但设计须考虑可接）：
  - 动机校准——用户诉求是「组件可插拔的观察集成契约」（换
    strategy/driver 实现后观察自动跟上、实现可自报内部阶段），不是采集
    管线。该诉求的 BCL 标准载体是 ActivitySource/Meter（DiagnosticSource
    体系，零 NuGet）；OTel SDK 只是宿主侧可选路由。
  - 两层观察模型定型：TraceCatalog（封闭词表 / 确定性 id / 深冻结）= run
    结构真相，不与 OTel 数据模型互译；ActivitySource/Meter = 未来组件
    集成契约层；OTel SDK = 出进程时挂载，届时产品代码零改动。
  - 数据面适配结论：Metric 近 1:1（Histogram 严格更强）；Trace 只能经
    实时桥（冻结 artifact 无时间戳，离线导出不诚实；确定性 id × collector
    去重冲突需导出边界裁决）；Event 无损；生命周期语义（Quarantine/
    StructuralOutcome/单次 finalize）不映射、永远留在本侧。
  - 重开触发器（任一成立）：T1 长驻 runtime host；T2 真实诊断需求
    （跨 run 对比 / 时序分布）；T3 多进程拆分。路径：先 BCL 桥（候选
    后续 change，如 OBS-001），出现独立 collector 部署才评估 OTel SDK。
  - 「做的时候要考虑到」的实现约束：①新观察埋点保持在缝级（实现无关
    ——换实现零埋点改动）；②IRunTrace / RuntimeStageMetrics 接口面保持
    窄且稳定（未来桥接只加不改）；③组件接口（IFastPerceptionStrategy
    等）未来扩展时，评估可选观察上下文通道（显式传入，不用 ambient 进
    domain）；④组件自报信号永远非权威、不进 canonical、不参与 replay
    判定。

## Acceptance
A1 固定场景稳定输出各阶段 invocations / elapsed / 规模计数（invocations
   与规模为确定性断言；elapsed 仅原始记录）
A2 fixture 输出可直接回答：一次 artifact → 多少 strategy invocation、
   proposal、evidence admission、world revision
A3 trace × metrics 四象限开关下 canonical 产出全等（EvidenceRecord id
   集、admission decisions、revision 结构链、Slice、CurrentGroundingView、
   receipts）
A4 既有全量测试通过 + 新增非侵入性测试（含 ThrowingRunTrace 新缝吸收）
A5 evidence/ 文档：环境、场景、原始测量、热点排序、测量限制；静态推断
   与实测分离表述
A6 只提交本 change 精确文件；ESO-001 in-flight 与无关 untracked 不纳入

## Constraints
- 产品 diff 面 = Trace/（TraceReference、SpanDefinition 词表、新
  RuntimeStageMetrics）+ Perception/FastPerception + UniKernel；其余零改动。
- 确定性：计数断言可重复；零 random；corpus 只读。
- 低开销：metrics=null 时新代码路径 = 一次 null check / 阶段。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案，三次运行；Run2 = LAT-001 单独在树 224/224）+ benchmark 原始输出誊录
  expected: A1–A4 GREEN；既有全量零回归
  actual: >
    Run1 205/207（2 失败 = 本 change 迁移点：RunTraceBulletTests 词表计数
    11→16/2→7 + shape 卫兵要求 metrics 离开 Trace 命名空间——修后转绿）；
    Run2 224/224 GREEN（Kernel 207 + Agent 17；LAT-001 单独在树，两轮独立）；
    Run3 223/224——唯一失败 RuntimeViewExposureTests.N1 归因 DSE-003
    in-flight（其 NativeLocator 属性 vs allowlist，非本 change 面，其
    state.md 自行迁移）。F1–F8 全绿：计数/规模确定性断言（cold 8 场景
    attribution 1→N→N→N、warm 4 轮 55new+165idempotent、grounding
    4 slice + 6 grounding）、四象限 canonical 全等、ThrowingTrace 新缝
    吸收、span 拓扑零词表违规。原始测量与热点排序（reconcile 第一
    ≈17.6ms 全程、occurrence ≈5.6ms 含于前者、strategy=invocation 数
    与 presentation 数相等）誊录 evidence/2026-09-09-lat-001-baseline.md
  evidence: 三次 dotnet test 输出（2026-09-09/10）；F8 detailed console
    原始表；git diff 逐文件审查记录
```

## Status log
2026-09-10 · understanding→resolved · 前置复验：CTL-001/DSE-001/CDS-001
  全 CLOSED（HEAD 0b2cf4ef）；ESO-001 in-flight（untracked）与两份无关
  untracked 文件列入保护清单。侦察：trace 词表现状（binding 2 + provisional
  9，无 perception/slice/grounding 项；timing 刻意省略）、FastPerception /
  UniKernel / WorldModel 缝实况、corpus 10 scenario（含 partial/degraded）、
  CTL-001 nav03 grounding 场景形状。D1–D8 裁决如上
2026-09-10 · resolved→persisted→planned · state.md 建立。切片：RED 非侵入
  性+计数测试 → 词表+metrics 组件 → FastPerception/UniKernel 缝绑定 →
  fixture → 全量回归 → evidence → REVIEW/VERIFY 分离
2026-09-10 · planned→implemented（进行中）· 产品面四文件 + 测试 F1–F8
  落地；首跑 5 绿 3 红（F5 record-List 引用相等陷阱 / F7 emission
  diagnostic 语义 / F8 warm 首轮实为 cold——均为测试侧修正）。期间并行
  会话提交 ESO-001/002（HEAD → 142e1710），与本 change 文件面零冲突。
  OTel 讨论落 D9（先不接 + 两层观察模型 + 可接性约束）
2026-09-10 · implemented→reviewed · REVIEW（subagent 基础设施失败，按
  B2/DSE-001 先例 Direct）：S1–S7/T1–T5 逐条 PASS 零 blocker；迁移点
  二处（词表计数、Trace→Diagnostics 命名空间迁出以保 shape 卫兵语义）
  均最小机械适配；4 条 note 确认非缺陷
2026-09-10 · reviewed→verified→closed · Run2 224/224（LAT-001 单独在
  树）+ Run3 失败归因 DSE-003（非本面）+ evidence 誊录完成 →
  PRODUCT_RUNTIME_LATENCY_BASELINE_ESTABLISHED。P0 收口，P1 解阻
2026-09-10 · 基线复测（post-closure，fixture 对照组职责）· WMP-001
  （P2，revision-bound 索引 + COW）落地后零修改重跑 F8/R8：稳态 reconcile
  -63%（scroll01 55 obs 2.31→0.85ms）/-29%（warm），降幅随场景规模递增；
  occurrence/perception 面持平（符合 WMP 不触碰面）；FCR warm 4→1 与
  canonical 全等稳定复现；计数断言全绿 = WMP canonical 等价的独立旁证。
  原始数据与噪声边界：evidence/2026-09-10-latency-remeasure-post-wmp001.md
