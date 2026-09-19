# UAP-001 证据报告 — Phase 4 统一异步 Perception 最小垂直 tracer

> Change: `changes/UAP-001` · 日期 2026-09-14 · pin `40f190c6`（工作树含并行
> 未提交内容，本 Change 增量见 §7）
>
> 验证等级：DETERMINISTIC + SCENARIO（无设备/无 live 模型环境臂）
>
> 真实 buyer：Kernel internal driver 的观察控制（`RunDriverInputs.NextInput`
> seam）。Trace 臂 = DisabledRunTrace（Trace 仅异步诊断原则的零耦合负证据；
> async trace 半边属 TRW-001，不在本 Change）。

## 0. 结论先行

1. **八组场景全部 GREEN**（15 个场景测试，含复审追加 S5b；RED 阶段
   15/16 + S5b（旧引擎）失败已留痕）：
   四种 realization（Fast-only / Fast→Slow / Slow-only / one-shot）对
   Runtime 顶层同构——观察只以 ObservationProposal 经 P2 admission → P3
   accepted Evidence → relevance/reconciliation 进入 belief；迟到/重复/
   乱序/partial/failure/timeout/cancel 全部受控 fail closed；Perception
   不直接改 WorldBelief；Fast/Slow 不出现在任何 Run/World 顶层模型或
   producer 字段（模型 stage 的 Observation-kind evidence producer 恒为
   `perception.runtime`；S3b/S8b-B2 按 D4 故意引入跨 producer
   `review.manifest` 以验证 Conflict 语义——该臂如实声明，非泄漏）。
2. **发现并修复一个已提交产品缺陷**（Human 授权，2026-09-14）：
   `PersistentRevisionDictionary/Set.BuildCanonicalView` 在「中途缓存视图
   + 之后仅 SetItem revision」链上以 null 为重建基，canonical 枚举静默
   丢失祖先键并把错误视图永久缓存。S2a 首先暴露（rev-13 `count=7,
   enum=0`），独立最小复现固定，2 处各 4 行修复 + 回归测试转正
   （`CanonicalEnumerationRegressionTests`）。
3. **事件时间完成语义（Human 复审要求，D13）**：原实现「先收齐所有
   ≤now 的到期结果再统一判定」被 RED 对照证伪——拉取节奏会改变交付批
   次（t6 拉取把 t6 迟到的错误 Fast 收进交付批）、迟到 failure 会翻转
   已完成 op。修复后：事件按时间逐项处理、转移即时发生；交付与拉取
   节奏无关；完成后迟到结果一律隔离。
4. **两个 tracer 级语义发现**（未升格产品语义，H9 留在 tracer）：
   - D9：整帧 occurrence 替换与 partial 交付不相容——partial 帧会把未
     覆盖 region 的既有 occurrence 抹掉（伪装 absence）；tracer double
     采用 descriptor 键合并（新帧条目替换同 descriptor，未覆盖
     carry-over）。
   - 顺序敏感：同 producer（Perception 统一观察流）的 CLE-001 Revise 使
     「最后到达」的同流再观察覆写值/景观——纠正与再犯错都对 arrival
     顺序敏感（S8c 决策时上下文如实断言）；跨 producer 分歧按 Conflict
     保持既存 + Uncertainty 增长（latest 永不获胜，S8b-B2/S3b）。

## 1. 场景验收四元组（method · expected · actual · evidence）

证据引用格式：`test:name`（xunit；全部在 `tests/UniClaw.Simulation.Tests/`，
命令见 §5）。所有虚拟时间 T0=2026-09-14T09:00:00Z；零 sleep/竞态；零
owner-state 直注（观察只经 feed → `NextInput` → P2）。

### ① Fast 漏目标 → 定向 Slow 找回 — `S1_FastMissesTarget_TargetedSlowRecovers_OneEffectOnRealTarget`

- method：root-top 子页；fast（真 FastPerception+LiveVisionStrategy，录
  制 provider response）漏 Network & internet 行；targeted-slow（truth
  double）补覆盖；虚拟时钟 T0→T0+2→T0+8→T0+33 三段 Drive。
- expected：hold 期 Pending 合法等待；覆盖并集完成才合并投递；omission
  ≠ absence（漏检元素零声明）；找回后唯一接地；恰一 Effect 落真值行
  （cy 867.5/2400=0.3614 中心）。
- actual：`WaitingForInput → WaitingForInput(op Pending, res-fast 已到达)
  → WaitingForInput(act 完成,等 post) → Completed(Completion)`；revisions
  =14、canonical=15、effects=1、虚拟投递延迟 8s；binding locator 中心
  =0.361458；模型 stage 的 Observation-kind evidence producer=
  `perception.runtime`（admissions 计数为描述性输出，非测试断言）。
- evidence：`test:S1_...`（断言含 hold 状态、DeliveredResultIds=[res-fast,
  res-slow-targeted]、locator 中心、producer 同一性）。

### ② Fast 误判状态 → decision 前纠正 / Fast-only 对照 / 旧绑定失效 — `S2a` / `S2b` / `S2c`

- S2a method：fast 误判 Colors 状态 enabled（真相 disabled）+ targeted-slow
  全 scope 纠正；agent 步骤 DesiredState=enabled。
- S2a expected：纠正先于 decision boundary——agent consultation 上下文
  含 `ui.state.colors=disabled`；纠正后 Unsatisfied → 执行动作；同流再
  观察 = Revise 非 Conflict。
- S2a actual：`Completed(Completion)`，1 effect，agent
  `CurrentWorldClaims["ui.state.colors"]=disabled`，ClaimEvolutionLog 含
  `Revise(ui.state.colors)`。
- S2b（对照）expected：Fast-only 无人纠正 → desired-state 已满足 →
  policy 不签发 act → fail closed，MaterialEffect 义务无 post-action 证
  据不得伪装 Completion。actual：`GroundingFailed(control-issued-non-act-
  intent)`，0 effect，非终态、无 Outcome。
- S2c（旧绑定失效）expected：两步串行各 fresh grounding；step1 binding
  的 occurrence 在纠正后的 current revision 不复存在，stale UiTarget →
  DeriveBindingView fail closed。actual：两个 canonical binding 的
  TargetOccurrenceId/RevisionId 互异；`DeriveBindingView("menu.row:Colors",
  c1).HasTargetOccurrence=false`；两步 locator 中心分别 = Colors/Color
  contrast 真值行；2 effects。
- evidence：`test:S2a_...`、`test:S2b_...`、`test:S2c_...`。

### ③ 补充性 Slow：零增长 vs 增长 — `S3a` / `S3b`

- S3a expected（复审修订：精确重复须在完成事件之前到达——D13）：rA
  @t1（部分覆盖 + scope 外 `ui.text.ocr7`）、逐字节重发 @t1.5、rB final
  @t2（完成事件）→ 重发与原件同 EvidenceId → ledger/reconcile 幂等。
- S3a actual：relevance 两条均 false（重发与原件各判一次）；admissions
  =8（全 accepted）、canonical=5（重发全幂等去重）、revisions=4
  （rA frame+typing、重发零新增、rB frame Revise+typing）。
- S3b expected：basis 增长（同值再观察 Reaffirm）→ 新 revision；跨
  producer（review.manifest）异值挑战 → Conflict 保持既存 + Uncertainty
  增长 → 新 revision。
- S3b actual：revisions=9；`WorldState[typing.color]=row_title`（既存保持）；
  Conflict{Established=row_title, Challenging=static_title}；
  uncertainty=2、conflicts=2；Reaffirm 留痕存在。
- evidence：`test:S3a_...`、`test:S3b_...`。

### ④ 旧 capture 的 Slow 迟到 — `S4_StaleSlowAfterPageAdvance_Quarantined_ZeroBeliefPollution`

- method：capture A（旧页）fast 部分覆盖、slow 排在 t+20；宿主在 t+2 后
  页面推进（`SupersedePending`）；capture B 完整覆盖；t+20 Drive。
- expected：op A Superseded；迟到 slow 记入 stale 隔离；零 capture-A
  证据 admission；belief 无 network typing；动作只来自 B 流。
- actual：`Superseded`，`StaleQuarantinedResultIds=[res-a-slow]`，
  DeliveredResultIds=[]；canonical records 全部无 `capture:{captureA}`
  lineage；`Completed(Completion)`，1 effect，binding 中心 = Colors 真值行。
  负对照臂（Review 建议后补）：不隔离的平行臂中 capture-A 证据确实
  可见——证明零污染断言具备检出能力（非构造性通过）。
- evidence：`test:S4_...`。

### ⑤ 同 capture 乱序 + 精确重复 — `S5_OutOfOrderAndExactDuplicate_Deterministic_NoDuplicateAction`

- method：control（r1@t3,r2@t5）vs treatment（到达时间互换 + r2 异
  ResultId 精确重发 + r1 同 ResultId 重投）双臂对照。
- expected：feed 同 ResultId 去重；异 ResultId 精确重复 → EvidenceId 幂
  等；规范化世界结论（语义 subjects）与 occurrence 景观一致；Effect 不
  重复。
- actual：treatment `DuplicateResultIds` 含 res-r1；两臂语义 claims 与
  occurrences（role/descriptor/centerY）逐项相等；两臂各恰 1 effect、同
  一真值目标。container identity 与 live.frame 字符串值合法不同（由首
  个处理证据/最后到达帧派生——规范化显式排除，见测试注释）。
- evidence：`test:S5_...`。

### ⑤b 拉取节奏不变性（事件时间完成，Human 复审追加）— `S5b_EventTimeCompletion_PullCadenceInvariant_LateArrivalsQuarantined`

- method：同一 operation、同一批 ArrivalTime（slow final @t2 已满足
  coverage；错误 fast @t6 才到）；三臂：t3 拉取 / t6 拉取 / t6 拉取 +
  「完成后才到的 failure @t5」反例。
- expected：两拉取节奏交付同一批次（仅 res-slow）、同一规范化世界结
  论、CompletedAt 同为完成事件 t2；t6 错误 fast 作为完成后迟到结果隔
  离（不覆写纠正）；迟到 failure 不把已完成 op 翻转为 Failed。
- actual：RED（旧引擎）证实两缺陷——t6 拉取交付批含 res-fast-late、
  迟到 failure 使 op=Failed 零交付；修复后 GREEN：两节奏
  DeliveredResultIds=[res-slow] 一致、CompletedAt=T0+2 一致、语义
  claims 与 occurrence 景观逐项相等、各恰一 Effect 落真值行
  （0.917708）、typing 保持纠正值 static_title；
  StaleQuarantinedResultIds 含 res-fast-late / res-failure-late、
  op 状态 Complete、FailureReason=null。
- evidence：`test:S5b_...`。

### ⑥ partial / 真空 / failure / timeout 严格区分 — `S6_FourTerminalCompletionStates_StrictlyDistinct`

- expected：四态互异；partial 未覆盖 region 零声明且 occurrence 景观不
  含该元素；真空 = 完成的显式空帧 claim（`{"detects":[]}`，对已覆盖
  scope 的合法负观察）；failure = 零 admission 零 belief（不产生空帧）；
  timeout = 零 admission 零 belief；failure ≠ OK_EMPTY。
- actual：`PartialAtDeadline / Complete(空帧) / Failed / TimedOut` 四态
  互异断言通过；partial belief 含 color-header typing、无 Colors typing/
  occurrence；empty belief 的 live.frame=`{"detects":[]}`、occurrences=0；
  failure/timeout belief=null、admission log=0/0（FailureReason=provider
  transport failure）。
- evidence：`test:S6_...`。

### ⑦ 等待期 cancel → 结果迟到 — `S7_CancelDuringWait_ThenLateResult_NoRevivalZeroLateEffect`

- expected：cancel 优先交付 → SafeStop obligation → terminal；此后结果
  到达零复活、零 late Effect、零新 admission；op Cancelled 隔离。
- actual：`Completed(SafeStop)`，0 effect；t+30 `PumpNow` 后
  `AlreadyTerminal`、revisions/admissions 不变、0 effect；
  `CancelledQuarantinedResultIds=[res-slow-late]`、Delivered=[]；
  ScriptedAgent 纪律 0 consultation 违规为零。
- evidence：`test:S7_...`。

### ⑧ 小标题/副标题误作菜单项 — `S0` / `S8a` / `S8b` / `S8c`

真值核对（`S0_TruthClassification_StaticsAndSubtitlesDistinct_NotMerged`）：
- canonical truth（type-truth.json）：`Color`=static_title（clickable=
  **false**，cy 2078.5）≠ `Settings`=section_label（clickable=false）≠
  `Mobile, Wi‑Fi, hotspot`=row_subtitle ≠ `Colors`=row_title（clickable=
  true，cy 2202.5）；四类 → 四个互异 occurrence role（menu.static /
  menu.section / menu.subtitle / menu.row），不合并。
- dual probe（type-dual-local.json）的 3-class truth 列与 canonical
  4-class 存在粒度差（Color 在 dual 中记 section_label，canonical 为
  static_title）——显式断言差异，fixture 以 canonical 为准。
- 历史预测错误如实加载：`Color` pred=row_title（分组标题误判）、
  `Mobile, Wi‑Fi, hotspot` pred=row_title（副标题误判）、`Colors`
  pred=row_title（正确）。

S8a（纠正成功臂，`S8a_...`）：
- expected：targeted-slow（truth double）纠正后——副标题/分组标题不形
  成第二个可点击目标（menu.row 恰 = 真值行集）；请求 Colors 唯一接地
  真实菜单行；agent 上下文含纠正后 typing；role-only 含糊 →
  MultipleCandidates 零点击；请求 "Color" → NoCandidate 不点标题。
- actual：menu.row descriptors 恰为 `[Brightness level, Color contrast,
  Colors, Connected devices, Network & internet]`；Network 行恰 1 个
  menu.row + 1 个 menu.subtitle；Color=menu.static、Settings=menu.section；
  agent 上下文 typing 全纠正；1 effect、binding 中心 = 2202.5/2400 =
  0.917708（Colors 真值行）；`Completed(Completion)`；seam 断言
  role-only=MultipleCandidates、singular=NoCandidate。
- evidence：`test:S8a_...`。

S8b（仍错误/冲突臂，`S8b_...`）：
- B1（slow 仍错误）：role-only → `GroundingFailed(grounding:
  MultipleCandidates)`、0 effect（Color 与 Colors 均被误判为 menu.row →
  含糊 fail closed）；"Colors" 精确请求仍唯一接地真实行（不点 Color）。
- B2（跨 producer 冲突）：slow 先立误判（row_title）、review（truth,
  producer review.manifest）后到挑战——claim 层不覆写（`WorldState=
  row_title` 保持，Conflict{Established=row_title, Challenged=
  static_title}，Uncertainty≥1）；occurrence 景观跟随最后 reconcile 的
  review 帧（Color=menu.static）→ 请求 "Color" 无 act →
  `GroundingFailed`、0 effect。Slow/挑战不因更晚自动成为真值。
- evidence：`test:S8b_...`。

S8c（交换到达顺序 + 重复投递 + 迟到错误结果，`S8c_...`，复审修订）：
- slow（纠正）在完成窗口内到齐（partial @t1 + 逐字节重发 @t1.5 +
  final @t2 → 完成事件 t2）；错误 fast @t6 完成后迟到。不变量保持：
  恰 1 effect（重复零放大）、请求 Colors 仍只接地真实行（中心
  0.917708）、role-only 含糊零点击、DeliveredResultIds=
  [res-slow-partial, res-slow-copy, res-slow-final]、同 ResultId 重投
  入 Duplicates、res-fast 入 stale 隔离。
- 纠正对迟到错误结果稳定（复审修订）：决策时与最终 WorldState 的
  typing 一致为 static_title——原「后到 Fast 覆写纠正」路径被 D13 事件
  时间完成语义消除（完成窗口外同流结果不进入 belief）。
- evidence：`test:S8c_...`。

## 2. 四 realization 同构比较 — `FourRealizations_SameSemanticInput_NormalizedConclusionsAndSafetyCompared`

- method：同语义输入（display-child ⑧ 页 + 请求打开 Colors）× 四
  realization；观察臂（NoAction，终态 belief = 该 realization 交付世界）
  比较规范化结论；行为臂（真实 tap 链）比较安全行为。规范化 checkpoint
  有序：RoleOnly → Colors 请求 → Color 请求 → menu.row 计数 →
  subtitle 计数 → effect 计数/目标。
- expected：Fast→Slow / Slow-only / one-shot 结论互相一致；Fast-only 存
  在分歧；全部 realization 安全行为一致（恰 1 effect 落真实行）。
- actual（inspect 臂输出）：
  - FastOnly：`{RoleOnly=MultipleCandidates, Colors=UniqueCandidate,
    Color=**UniqueCandidate**, MenuRow=3, Subtitle=0}`
  - FastThenSlow=SlowOnly=OneShot：`{RoleOnly=MultipleCandidates,
    Colors=UniqueCandidate, Color=**NoCandidate**, MenuRow=2, Subtitle=0}`
  - **first divergence = ColorRequestResolution**（FastOnly=UniqueCandidate
    vs 纠正性 realization=NoCandidate）——Fast-only 会把 "Color" 请求唯
    一接地到不可点击分组标题（危险点），纠正性 realization 无此解析。
  - 行为臂：四者各恰 1 effect、同一真值目标（中心 0.917708）；全部
    Observation-kind evidence producer=`perception.runtime`。
- evidence：`test:FourRealizations_...`（含 inspect/act 双臂断言）。

## 3. live one-shot/fast 只读采集路径接入 — `LiveOneShotFastPath_RealGoldenResponse_DeterministicRealStrategy`

- method：golden-run-v1 真录制 live provider response
  （`perception/case-b-off.json`）→ 真实 `FastPerception` +
  `LiveVisionStrategy`（产品 live one-shot/fast 策略代码路径，只读）。
- expected：非空确定性 observations（两次调用逐字节一致）；契约形态
  （yolo→ui.detect.*.class+spatial、ocr→ui.text.*）；策略真实执行
  （RuntimeStage.FastPerceptionStrategy invocations=2）；空 yolo/ocr
  （OK_EMPTY）→ 零 observation。
- actual：observations=42、strategyInvocations=2、artifact=
  `art-624aeac7f81072c5`；空响应零 observation 通过。
- 边界（如实声明）：无设备环境——真实采集/传输臂未执行（Phase 7）；
  场景内的 fast/oneshot stage 以同一真实策略代码路径消费录制 response。

## 4. 产品缺陷（发现 + 授权修复 + 回归）

- 现象：S2a rev-13 `WorldState.Count=7` 而 canonical 枚举=0；
  `Count/TryGetValue` 走 storage 不受影响（既有 510 项测试未触发的原
  因）。
- 根因：`PersistentRevisionDictionary/Set.BuildCanonicalView` 的 transient
  重建基取 `_canonicalParent?._canonicalView`（最近祖先，pending 链存在
  时必为 null），等价于从 null 重建——丢失 pending 链切断处已缓存祖先
  的全部键；错误视图经 `??=` 永久缓存。触发：枚举过中间 revision（缓
  存视图，如 agent DecisionContext / Facts 消费侧）后出现 ≥1 个仅
  SetItem（无新增键）的 revision，再枚举最新 revision。
- 复现（修复前断言已固定于 git 历史的本报告描述）：
  `count=3, enum=[s.late]`（丢失 live.frame/s.early）。
- 修复（Human 授权 2026-09-14）：dict/set 两处把重建基改为「pending 链
  切断处的已缓存祖先视图」；瞬态祖先内存策略（WMP）不变。
- 回归：`CanonicalEnumerationRegressionTests
  .CachedMidChainView_ThenSetItemOnlyRevisions_EnumerationKeepsAncestorKeys`
  ——修复后枚举=storage、Revise 值正确、basis 集合一致。

## 5. 回归与命令（actual）

- `dotnet test UniClaw.Kernel.slnx`：
  **Kernel.Tests 382/382 · Simulation.Tests 112/112 · Agent.Tests 17/17
  （共 511，0 失败，0 跳过）**（含复审追加 S5b）。
- `git diff --check`：exit 0（干净）。
- 新增未跟踪文件空白检查（行尾空白/EOF 换行/TAB/UTF-8/遗留诊断引
  用）：7 文件 PASS（含本 report）。
- RED 留痕：实现前同套测试 15/16 失败（唯一通过 = S0 前置真值核对，
  按设计不依赖引擎）；复审追加 S5b 在旧引擎上 RED（迟到错误 Fast 入
  交付批、迟到 failure 翻转已完成 op——两缺陷留痕于 D13）。

## 6. 计数与延迟汇总（关键路径，虚拟时间）

| 场景 | revisions | canonical evidence | admissions | effects | 备注 |
|---|---|---|---|---|---|
| S1 | 14 | 15 | 15+reflux | 1 | 投递延迟 8s（op 打开→交付） |
| S3a | 4 | 5 | 8 | 0 | 重发全幂等去重；irrelevant 零增长 |
| S3b | 9 | — | — | 0 | uncertainty=2, conflicts=2 |
| S6 | 1(partial)/1(empty)/0/0 | — | >0/>0/0/0 | 0 | 四态互异 |
| ⑧-A | — | — | — | 1 | 目标中心 0.917708 |

## 7. 本 Change 增量文件（全部新增，除授权修复外零 src 改动）

- `changes/UAP-001/state.md`
- `tests/UniClaw.Simulation.Tests/AsyncPerceptionTracer.cs`（tracer 引擎
  + host 组合 + realization 构建）
- `tests/UniClaw.Simulation.Tests/AsyncPerceptionFixtures.cs`（真值/预测
  加载 + provider response 合成 + join）
- `tests/UniClaw.Simulation.Tests/AsyncPerceptionScenarioTests.cs`（八组
  场景）
- `tests/UniClaw.Simulation.Tests/AsyncPerceptionRealizationTests.cs`（四
  realization + live 路径）
- `tests/UniClaw.Simulation.Tests/CanonicalEnumerationRegressionTests.cs`
  （产品缺陷回归）
- `evidence/2026-09-14-uap001-phase4-async-perception-tracer/report.md`
- **授权修复**：`src/UniClaw.Kernel/World/PersistentRevisionCollections.cs`
  （Human 授权 2026-09-14；2 处 BuildCanonicalView 重建基修正 + 注释）

## 8. 诚实边界（不宣称）

- Slow/full-model 无真实产品 adapter：slow/review stage = recorded
  executable double（truth 对齐或录制错误臂）——只验证异步时序/失败/
  相关性语义与安全不变量，**不**宣称真实 Slow 模型质量或第二个真实产
  品 adapter。
- 无设备 live 环境：真实采集/传输故障分类未执行（Phase 7）。
- hold-until-complete、typed completion 状态机、descriptor 键合并、
  D5 typing→role 映射均为 tracer 级假设（H9 不升格）；产品化语义
  （观察会话/覆盖/supersession 协议）待 H9/H10 及后续 Change。
- H10：本 tracer 未引入任何 preliminary 自动授权路径——Fast-only/未纠
  正臂一律 fail closed（零 effect / 不伪装 Completion）；风险分级授权
  表未建（按既定 fail-closed 默认执行并如实报告）。
