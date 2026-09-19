# TRW-001 — Real Async Trace Writer & Sealed Artifact Lifecycle（Phase 3 trace 半边）

lifecycle_state: closed · disposition: APPROVED_WITH_FOLLOWUPS  # Human 复审通过 2026-09-13；非阻塞 follow-ups 见 residual risks 尾部五项 · depth: decision-heavy · base: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 · pin: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 (2026-09-13)

## Intent（WHAT/WHY）

**WHAT**：实现真实异步 Trace writer——bounded capture sink + 后台持久化 consumer
+ drain/seal/integrity 完整生命周期——并把 P26 Scenario Importer 的消费面扩展
到 async 产出的 sealed artifact。

**WHY**：RFS-001 review 裁定「Trace async 未实现」并授权延期至 Phase 3
（rescope 记录）；已锁语义（ADR-0013、baseline §24.9、协议 P26、roadmap §7.4）
只有行为等价证据（三臂 + 故障吸收 + sealed-only 门），没有真实异步持久化、
背压/丢弃可诊断性、drain/seal/integrity 生命周期的执行面。G16 的 trace 半边
（async writer、drain/seal/integrity）仍未闭合。

## 上游已锁语义（不重锁，只执行）

- ADR-0013：Trace 非权威、reference-oriented、不参与决策/恢复；writer 延迟/
  失败/背压不得进入 Runtime critical path。
- baseline §24.9：capture/persistence 异步；只有 drain/seal/integrity 完成的
  artifact 可交 Importer；未 sealed/Quarantined/integrity-unknown 一律 fail closed。
- 协议 P26：sealed RunTraceArtifact → Importer → ScenarioStimulus；Trace Event
  永不是 command。
- roadmap §7.4：bounded sink、显式可诊断丢弃/降采样策略、Run 结束时由测试/
  运维侧执行 drain/seal/integrity。

## Scope

- `src/UniClaw.Kernel/Trace/` 新增 internal 异步 writer（见 D1–D5）：实现既有
  公共 `IRunTrace` + internal `IRunTraceSink`；bounded channel；后台持久化
  consumer（文件目标，路径注入）；drain/seal API；integrity（canonical
  rendering SHA-256 随 artifact 携带）
- `tests/UniClaw.Simulation.Tests/`：四臂 canonical 等价（sync/async/disabled/
  failing）、writer 减速不影响 kernel critical path、queue-full 丢弃可诊断、
  mid-write 崩溃 → CaptureFailed、未 drain/unsealed/integrity-bad → importer
  fail-closed、async artifact 派生 stimuli → re-drive、artifact 内容确定性
- 更新本 state、status log、verification 四元组

## Out of Scope

- OTel export adapter、跨进程/网络 transport、采样/降采样策略调优
- retention/redaction/tombstone、在线 registry（roadmap Phase 3 治理另立 change）
- 修改任何 Owner/Authority/协议语义（全部已锁）；修改 UniKernel 组合面
- 新增/冻结任何公共 Interface（writer 为 internal realization）
- Scenario bundle 治理 / baseline promotion（roadmap Phase 3 其余半边）
- 提交 commit

## Decisions

| # | 决策 | 状态 |
|---|---|---|
| D1 | 异步 writer = internal 产品基础设施 realization：实现既有公共 `IRunTrace` + internal `IRunTraceSink`；UniKernel 组合面零改动；无新公共 surface（RFS-001 review 教训：public 类型即事实 Interface） | candidate / 本 change |
| D2 | capture path = O(1) 有界 enqueue（`System.Threading.Channels` bounded channel，显式容量）；队列满/写入故障 → 显式 drop 策略（丢弃计数 + `TraceDiagnostic` 记录），绝不阻塞、绝不改变 canonical output | candidate |
| D3 | 后台 consumer + durable 文件持久化（目标目录注入）；drain = bounded wait 排空；seal = emission 观测 ∧ flush 完成 → `Finalized`；无 emission → `Quarantined`；writer 故障 → `CaptureFailed`（现有预留态的首次真实触发路径） | candidate |
| D4 | integrity = sealed artifact canonical rendering 的 SHA-256，随 artifact 携带；Importer 扩展：async 产物 integrity 校验失败/缺失 → fail closed（P26「drain/seal/integrity 完成」的执行面） | candidate |
| D5 | 放置 `src/UniClaw.Kernel/Trace/`（与 internal `InMemoryRunTrace` 同域同可见性先例）；测试经 InternalsVisibleTo 消费；持久化目录由 Host 注入 | candidate |
| D6 | 验收以「行为不变量 + 生命周期」为准：四臂 canonical 等价；writer 人为减速（可注入延迟/调度器）时 kernel 各 stage latency 不劣化；两遍同 run artifact 内容 hash 一致（TraceModel 无 timing 字段，天然可确定） | candidate |
| D7 | 确定性测试纪律：后台任务的 flush 只经显式 drain/seal 调用观察；不在测试中 sleep-race | candidate |

## Alternatives

1. **test-side-only writer**：拒绝——sealed artifact 生命周期须可移植到 Product
   Host 诊断路径；且 P26 importer 只消费产品语义 artifact，test-only 会割裂。
2. **改造 InMemoryRunTrace 加后台线程**：拒绝——同步 recorder 语义已被
   TRC-001/RUN 系测试依赖；新类型并行存在，Host 二选一。
3. **公共 IAsyncTraceWriter 接口**：拒绝——单 realization 无 buyer 证据
   （skeleton §5 门槛）；internal 先行。
4. **integrity 放 import 侧计算**：拒绝——seal 时计算并随 artifact 携带才能
   证明「seal 时完整性成立」；import 侧重算+比对。

## Owner-Authority impact

- 零新增 Authority；Trace 维持非权威（ADR-0013）。
- 六个 L2 Owner、Run/Effect/Outcome 语义零接触；UniKernel 组合面零改动。
- writer 故障域完全隔离于 canonical output（fail-closed 吸收已存在，本 change
  证明其在 async 形态下成立）。

## Acceptance

1. capture 不阻塞：async 臂下 kernel stage latency 与 disabled 臂同量级
   （enqueue-only）；writer 注入减速不放大任何 Runtime stage 耗时。
2. 四臂 canonical 等价：sync/async/disabled/failing 四臂 semantic digest 与
   owner records 完全一致。
3. 队列满：显式丢弃计数 + diagnostic 可观察；canonical output 不变。
4. mid-write 崩溃注入 → artifact `CaptureFailed` 终态；canonical output 不变。
5. drain/seal/integrity：seal 仅在排空后完成；未 sealed / Quarantined /
   integrity 校验失败 → Importer fail-closed（含 async 产物路径）。
6. async sealed artifact 经 Importer 派生 stimuli → re-drive 复现任一既有
   场景语义（复用 RFS-001 ImportReDrive 模式）。
7. 同一 bundle 两遍运行的 async artifact 内容 hash 一致。
8. 产品闭包维持：writer internal、无 Simulation 依赖（closure 测试继续 GREEN）。
9. 无新公共 Interface；UniKernel/IRunTrace 公共面零改动。
10. 全 solution 回归 GREEN + git diff --check + 精确路径 status。

## Constraints

- TDD（RED→GREEN）；Leader 决策/设计 + SubAgent 编码分工延续。
- 不改并行 dirty；无 destructive git；不提交 commit。
- roadmap Phase 3 其余半边（asset governance/baseline promotion）不在本 change。

## Residual risks

- **canonical rendering 双实现**：并行未提交工作在 `TraceModel.cs` 增加了
  `RunTraceArtifactIntegrity`（IntegritySha256 字段 + Seal/IsValid）；本 change
  的 `SealedTraceStore.CanonicalRendering` 与其字段方案刻意对齐（IsValid 互
  持），但存在两份实现——并行 change 落地后应合一（一方委托另一方），否则
  漂移即 integrity 破裂。
- **emission marker 经 bounded queue 传递**：queue 溢出时可被诚实丢弃 →
  Quarantined（Trace lag/drop 只损诊断完整性，不改 canonical output——符合
  ADR-0013；测试 3 因此不断言 terminal）。
- **丢弃策略为计数+diagnostic 的最小实现**：降采样/优先级策略（roadmap
  §7.4 的扩展空间）未做；OTel export、跨进程 transport、retention/redaction
  均未做（out of scope 维持）。
- **DrainTimeout 默认 10s**：极慢 writer 下 Finalize 可能超时返回未排空
  artifact（CaptureFailed/Quarantined 语义吸收）；生产调参属后续运维证据。
- 持久化为单文件 append + JSON 封存；无 fsync 策略（进程级崩溃窗口仍在——
  CaptureFailed 崩溃测试证明的是消费路径故障，非 OS 级持久性）。
- 并行 dirty（Assurance/PostActionEffectVerification.cs、RuntimeAssurance/
  DisabledRunTrace/InMemoryRunTrace 修改、TraceModel IntegritySha256）非本
  change 所有，未触碰。

## Verification

```yaml
verification:
  level: DETERMINISTIC + SCENARIO
  method: >
    四测试项目全量回归 + TRW-001 七项专项测试 + grep 无新 public 类型 +
    dotnet build + git diff --check
  expected: >
    Acceptance 1–10：四臂 canonical 等价；慢 writer 不阻塞 Runtime；
    queue-full 显式丢弃且 canonical 不变；崩溃→CaptureFailed 且 canonical
    不变；未 sealed/Quarantined/integrity 坏→import fail closed；async
    sealed→派生→re-drive；两遍 artifact hash 一致；closure 维持；零新公共
    surface；全绿
  actual: >
    （minimal-fix 后 2026-09-13 二轮）Kernel.Tests 382/382、Agent.Tests 17/17、
    Simulation.Tests 74/74（+4：DroppedRecords_ThenSuccessfulEmission_NotFinalized /
    EmissionEnqueued_FlushStalledPastDrainTimeout_NotImportable /
    SealPersistFailure_NoThrow_NotImportable / HeavyDropping_DiagnosticsBounded；
    QueueFull 臂强化为显式 Quarantined 断言；RED 5/5→GREEN 证据在
    Agent-6 报告）；
    AsyncArm_CanonicalEquivalent_FourArms / SlowWriter_DoesNotBlockRuntime /
    QueueFull_ExplicitDrops_Diagnostics_CanonicalUnchanged /
    WriterCrash_CaptureFailed_CanonicalUnchanged /
    UnsealedOrBadIntegrity_ImportFailsClosed /
    AsyncSealedArtifact_Imports_AndReDrives /
    ArtifactContent_Deterministic_TwoRuns 全 GREEN（套件 3×复跑稳定；
    minimal-fix 四项各有具名 RED→GREEN 测试）；src/UniClaw.Kernel/Trace 新增类型全 internal；
    git diff --check 干净
  evidence: >
    可复现命令：dotnet test tests/UniClaw.Kernel.Tests；dotnet test
    tests/UniClaw.Agent.Tests；dotnet test tests/UniClaw.Simulation.Tests；
    grep -n "public" src/UniClaw.Kernel/Trace/AsyncFileTraceWriter.cs
    src/UniClaw.Kernel/Trace/SealedTraceStore.cs（仅接口实现/嵌套私有成员）；
    git diff --check
```

## Status log

- 2026-09-13 · enter→understanding·resolving · Human 授权进入 Phase 3（scope =
  真实异步 Trace writer + sealed artifact 生命周期；明确不含 roadmap Phase 3 治理
  半边）；Entry/Resume 复验 RFS-001/WRC-001 closed、462/462 全绿、diff-check
  干净；上游语义全部已锁（ADR-0013/§24.9/P26/§7.4），本 change 为纯 realization，
  无需新的架构 amendment；D1–D7 候选决策与 4 项被拒替代方案落档
- 2026-09-13 · implementing·delegated · Agent-5 完成 TRW-001 实现（Leader REVIEW 通过）：产品侧 internal `AsyncFileTraceWriter`（bounded channel TryWrite-only capture + 显式丢弃计数/diagnostic + 单后台 consumer 批量落盘 + bounded drain/seal + CaptureFailed 首个真实触发路径）与 `SealedTraceStore`（LoadVerified：缺文件/缺 hash/反序列化失败/integrity 重算失配 → SealedTraceIntegrityException fail closed）；sim 侧 TraceArm.AsyncFile + DeriveFromPersisted；七项验收测试 1:1 落地。Leader 抽查：capture 路径 lock+TryWrite+NoOp-after-finalize 非阻塞语义正确；LoadVerified 四重门完整。发现并行会话 dirty（TraceModel IntegritySha256/RunTraceArtifactIntegrity、Assurance/PostActionEffectVerification 等）——Agent-5 未触碰且 canonical 方案刻意对齐（IsValid 互持），双实现合一记入 residual risks
- 2026-09-13 · verify·awaiting-human-review · Leader VERIFY：382/382 + 17/17 + 70/70（共 469）第一手复跑；git diff --check 干净；验收 1–10 全有具名测试/构建/grep 证据。lifecycle 置 verifying/awaiting-human-review，不自封 CLOSED
- 2026-09-13 · review·minimal-fix→implementing · Human review 四项最小修法，全部接受（无反驳；判定：Finalized 证明完整、已刷盘、可校验的录制，不只是看见了运行结果）——(1) Drain 先关队列入口再有界等待 consumer 完成 flush 并退出，返回显式结果（完成/超时/故障）；Finalized ⟺ drain 完成 ∧ 无 writer 故障 ∧ DroppedCount==0 ∧ 已观测 emission；丢片/超时→Quarantined，writer/持久化故障→CaptureFailed；成功排空前不取消 consumer。(2) _journal.Add 移至整批 Flush 成功之后（超时快照只含已刷盘记录）。(3) PersistSealed 失败捕获：返回内存 CaptureFailed 诊断产物，磁盘不留可导入 seal 对；临时文件写完再原子发布 .trace.json/.trace.sha256；读取端同时核对 sidecar 与 artifact 内嵌摘要；诊断改累计计数+有上限样本。(4) 以可控 consumer 闸门（非 Task.Delay）补三项确定性 RED 测试：先丢早期 record 再成功入队 emission 仍不得 Finalized；emission 已入队但 flush 卡至 drain 超时不得导入；seal 写盘失败不抛出且不可导入；另加大量丢弃时诊断条数有界断言。不重开架构决策
- 2026-09-13 · implementing·minimal-fix · Agent-6 完成四项最小修法（Leader REVIEW 通过，RED→GREEN 证据核实：inert seams 先行 → 新测试 5/5 RED → 行为修复 → 全 GREEN）：(1) Drain 显式结果枚举 + TryComplete 先关入口 + 有界等 consumer 退出（成功路径不取消）+ Finalized ⟺ drain 完成∧无故障∧零丢弃∧已观测 emission；(2) journal 移至整批 Flush 后（_pendingUnflushed 记账，超时快照只含已刷盘）；(3) PersistSealed 捕获（不抛、内存 CaptureFailed、临时文件 File.Move 原子发布、失败清残留）+ LoadVerified 双核对（sidecar ∧ RunTraceArtifactIntegrity.IsValid 内嵌摘要）+ 诊断封顶（样本 27 + finalize 关键诊断保留位，artifact 级上限 32 + capped 汇总 + 累计计数）；(4) ManualResetEventSlim 闸门 + DrainTimeout 界定，无 Task.Delay 竞态。deviation 采纳：QueueFull 臂强化断言 Quarantined；cap 预留席位设计有注记
- 2026-09-13 · verify·awaiting-human-review · 二轮 Leader VERIFY 第一手：382/382 + 17/17 + 74/74（共 473）、git diff --check 干净；Drain 顺序/原子发布/诊断封顶抽查确认。seal 语义判定落地：Finalized 证明完整、已刷盘、可校验的录制。lifecycle → verifying/awaiting-human-review
- 2026-09-13 · closed·APPROVED_WITH_FOLLOWUPS · Human 复审通过，正式关闭。关闭证据：473/473（Kernel 382 + Agent 17 + Simulation 74）第一手复跑、git diff --check 通过、minimal-fix 四项各有具名 RED→GREEN 测试。按 Human 指示记录五项非阻塞 follow-ups（不继续修改 Trace Writer）：(1) 双文件发布非事务原子——.json/.sha256 两次 File.Move 间存在窗口，跨文件事务性需目录级方案或清单文件；(2) 非 fsync——File.Move/Flush 无 fsync 选项，OS 级崩溃窗口仍在（现行为 CaptureFailed 语义吸收，但持久性保证未达 fsync 级）；(3) 诊断极端上限——finalize 关键诊断（drain-timeout/unflushed 计数等）理论无界于极端故障风暴，artifact 级 cap 32 仅约束常规样本；(4) 资源释放——consumer Task/StreamWriter 的确定性释放依赖 Finalize 路径，未 finalize 的 writer 实例生命周期由 GC/进程退出兜底（无显式 Dispose 契约）；(5) sidecar 异常统一包装——LoadVerified 的 IO/JSON 异常路径部分仍以底层异常类型逃逸，建议统一包装为 SealedTraceIntegrityException 家族。以上均为后续 change 材料，不阻塞本 change
