# UAP-001 — Phase 4 统一异步 Perception 最小垂直 tracer 与场景验证

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 · pin: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 (2026-09-14, 工作树含并行未提交内容, 见 §并行边界) · Human closure 2026-09-19（docket: evidence/2026-09-16-abg001-uap001-human-review-docket.md）

## Intent（WHAT/WHY）

**WHAT**：以最小垂直 tracer 证明 Phase 4「统一异步观察消费语义」（roadmap §10
Phase 4，`TRACER_HYPOTHESIS`）：Fast-only / Fast→Slow / Slow-only / one-shot
full-model 四种 Perception realization 对 Runtime 顶层同构——观察结果全部以
ObservationProposal 经 P2 admission → P3 accepted Evidence → World Model
relevance/reconciliation 进入当前 belief；late / duplicate / out-of-order /
partial / failure / timeout / cancel-then-late 受控且 fail closed；Perception
不直接改 WorldBelief；Fast/Slow 标签不进入 Run/World 顶层模型。包含新增
「小标题/副标题误作菜单项」场景（复用 FSV-001 的 `type-truth.json` 与
`type-dual-local.json`）。

**WHY**：roadmap Gap G3/G4/G5（Phase 4）——异步观察的 capture/session
correlation、completion、乱序/重复/迟到、partial/failure 语义目前无统一执行
面；「Fast 错、Slow 纠正」「Slow 迟到不污染」「subtitle/static_title 误判不
形成第二可点击目标」只在边界场景账本（§9）中列举、未被可执行场景证明。真
实 buyer = Kernel internal driver 的观察控制（`RunDriverInputs.NextInput`
seam，RFS-001 已落地）。

## 上游已锁语义（不重锁，只执行）

- baseline/协议：Evidence Ledger 是唯一 admission 面（P2）；Perception 是
  proposal producer，无 EvidenceId / admission / belief 权（不变量 9/11）；
  accepted ≠ belief-relevant（P3 / 不变量 10）；Reconciliation 幂等，同
  subject 不相容 claim → Conflict 不静默覆盖；Fast/Slow 是 Perception 内部
  realization，不是两条 ingress / 两份 belief（§2.2）。
- roadmap §10 Phase 4：Runtime 不看 Fast/Slow 标签，只看 observation
  contract 状态；accepted Evidence 可不增 revision；basis/conflict/
  uncertainty 变化可增 revision；Slice 只在 buyer request 时派生；stale
  Slow 不污染 current revision；failure ≠ OK_EMPTY；Fast/Slow 不进入
  Run/World top-level model。
- H9（OPEN_GATE）：observation session/capture correlation 是否成为产品
  协议未决——默认安全行为 = 只在 tracer 内具体实现，不升格公共模型。
- H10（HUMAN_SELECTED）：preliminary 结果不自动授权；风险规则未定时按
  current evidence/belief/assurance 判断 fail closed。
- ADR-0013：Trace 仅异步非权威诊断，不作 owner truth 或命令。

## Scope

- 新增 `tests/UniClaw.Simulation.Tests/AsyncPerceptionTracer.cs`：虚拟时钟、
  观察操作 correlation（capture/operation id、purpose/scope/budget）、
  typed completion（Pending/Complete/PartialAtDeadline/Empty/Failed/TimedOut/
  Superseded/Cancelled）、确定性合并投递（canonical ordering + ResultId
  去重 + hold-until-complete）、stale 隔离、组装真实 L2 的
  AsyncPerceptionHost（真实 UniKernel/KernelRunDriver/EvidenceLedger/
  WorldModel/ControlLoop/RuntimeAssurance/EffectBoundary + 确定性外部缝）。
- 新增 `AsyncPerceptionFixtures.cs`：`type-truth.json` /
  `type-dual-local.json` 加载与真值分类核对；golden-run-v1 真实 provider
  response JSON 复用（真 FastPerception + LiveVisionStrategy 代码路径）；
  菜单 fixture 的录制 provider response / Slow double 输出合成。
- 新增 `AsyncPerceptionScenarioTests.cs`（八组场景）与
  `AsyncPerceptionRealizationTests.cs`（四 realization 同构 + live/recorded
  adapter 接入证明）。
- 新增 `evidence/2026-09-14-uap001-phase4-async-perception-tracer/report.md`。
- **授权例外（Human 2026-09-14）**：修复已提交产品缺陷
  `src/UniClaw.Kernel/World/PersistentRevisionCollections.cs`（2 处
  BuildCanonicalView 重建基，见 D11）+ 回归测试转正
  `CanonicalEnumerationRegressionTests.cs`。

## Out of Scope

- 不修改 `src/` 产品代码、协议、基线文档、ADR、并行 Change 的任何文件。
- 不冻结 observation session/correlation 公共 Interface（H9 不升格）。
- 不实现真实 Slow/full-model 产品 adapter（Slow = executable double；只声
  明验证异步时序/失败语义，不宣称真实 Slow 质量或第二个真实产品 adapter）。
- 不做 Preliminary→action 风险分级授权表（H10 按既有 fail-closed 路径执
  行并记录）；不做产品模型选择、在线 model switch、Contract 模型注入。
- 不做真实设备 live 采集（无环境；live one-shot/fast 路径以真
  FastPerception + LiveVisionStrategy + 录制 live provider response 接入）。

## Decisions

- D1 **tracer 全部落测试程序集**：Phase 4 语义在 `UniClaw.Simulation.Tests`
  内实现（`InternalsVisibleTo` 既有 seam），`src/` 零改动——H9「tracer 内
  correlation」的结构性执行。
- D2 **真实 buyer = `KernelRunDriver.NextInput`**：观察控制经 driver 既有
  pull seam 消费；Perception realization 选择（Fast→Slow 升级等）是 feed
  adapter（Perception 侧）内部决策，Runtime 只看完成后的合并
  ObservationProposal 批次（context 纪律不变）。
- D3 **hold-until-complete 投递**：结果按录制虚拟时间到达、按 operation
  correlation 合并；operation 完成（Complete/PartialAtDeadline）才投递一
  个 canonical-ordered 批次；Pending 期间 driver 合法等待
  （WaitingForInput）。纠正因此在 decision boundary 之前落 belief。
- D4 **unified producer + stage-scoped provenance**：Runtime 面向的
  producer 恒为 `perception.runtime`（realization 不泄漏为 producer）；
  stage（fast/slow/oneshot/review）只进 provenance scope/lineage → 同流
  再观察按 CLE-001 Revise 语义替换值；跨 producer（如 reviewed manifest）
  分歧按 Conflict 保持既存值并增长 Uncertainty。exact duplicate（同
  ResultId → 同 EvidenceId）幂等零 revision。
- D5 **菜单 typing→occurrence role 映射**：row_title→`menu.row`、
  row_subtitle→`menu.subtitle`、static_title→`menu.static`、section_label→
  `menu.section`——四类真值分类映射四个互异 role，不合并；grounding 只
  对 `menu.row` 消费。occurrence 景观 = 最后 reconcile 的 live.frame 记
  录（revision-local 替换语义），typing claim 冲突不自动消除歧义 →
  MultipleCandidates/NoCandidate fail closed 零 Effect。
- D6 **stale/superseded/cancelled 隔离在 correlation 层（宿主驱动）**：
  页面/采集推进是宿主可观察事实——隔离由宿主经 feed 外部缝
  （`SupersedePending`）显式触发：pending operation → Superseded；run
  terminal 或 cancel → Cancelled；迟到结果记入诊断清单，零 admission、
  零 belief 变化、零 Effect（fail closed）。不删除、不静默。「新 capture
  自动迁移旧 operation」的自动语义未被声明也未实现（Review 2026-09-14
  措辞修正）。
- D7 **failure/timeout/empty 不投递、不伪造 absence**：failed/timed-out/
  empty 的 operation 不产生任何 proposal（§18：missing detection 不产
  observation）；typed completion 状态面区分四者；partial 只携带已覆盖
  region 的真实 claims，未覆盖 region 零声明。
- D8 **live 路径接入声明**：fast/one-shot stage 经真实 `FastPerception` +
  `LiveVisionStrategy`（产品 live one-shot/fast strategy 代码路径）消费录
  制 live provider response（golden-run-v1 真资产 + 菜单 fixture 合成
  response）；Slow stage = recorded executable double（truth 对齐或错误
  臂），只证异步语义。

- D9 **occurrence 景观合并（tracer 发现）**：整帧替换与 partial 交付不
  相容（partial 帧会把未覆盖 region 的既有 occurrence 抹掉——伪装
  absence）。MenuFrameObservationStrategy double 采用 descriptor 键合并：
  新帧条目替换同 descriptor 既有 occurrence，未覆盖 carry-over。tracer
  级假设，非产品语义。
- D10 **空结果的语义**：真空 = 对已覆盖 scope 的显式负观察（live.frame
  `{"detects":[]}` 随完成投递），与 failure（零投递零 admission）严格
  区分；partial 到期投递已覆盖 region 的真实 claims，未覆盖 region 零
  声明。修正原 D7 中「empty 不投递」的表述（显式负观察应投递；
  failure/timeout 仍零投递）。
- D11 **产品缺陷修复（Human 授权 2026-09-14）**：
  PersistentRevisionDictionary/Set.BuildCanonicalView 在「中途缓存视图 +
  之后仅 SetItem revision」链上以 null 为重建基，canonical 枚举静默丢
  失祖先键并永久缓存错误视图（S2a rev-13 count=7/enum=0；最小复现
  count=3/enum=[s.late]）。修复 = 重建基改为 pending 链切断处的已缓存
  祖先视图（WMP 瞬态祖先策略不变）。授权依据：本 Change 验收
  （accepted Evidence → belief 链）依赖 canonical 枚举正确性；缺陷复
  现独立于 tracer 语义。原「不修改 src/」范围据此窄幅修订。
- D12 **顺序敏感性如实报告（2026-09-14 复审修订）**：同 producer（统一
  观察流）的 CLE-001 Revise 使完成窗口**内**最后到达的同流再观察覆写值/
  景观；跨 producer 分歧 Conflict 保持既存。安全网 = 精确 descriptor 接
  地 + 含糊 fail closed。持久 supersession 语义（协议 Deferred ⑧）为后
  续 Change 输入；跨操作仲裁（R6）未实现（Human 明示不得顺手实现）。
- D13 **事件时间完成语义（Human 复审要求 2026-09-14）**：feed 的事件推
  进按事件时间顺序逐项处理（结果到达事件 + deadline 事件，同刻按「到
  达先于 deadline、脚本序」稳定排序），状态转移在每个事件点及时发生
  ——完成判定不得滞后到拉取时刻。推论：①交付批次与拉取节奏无关
  （S5b：t3 与 t6 拉取同批、同规范化世界结论、CompletedAt 同为完成事
  件时刻）；②完成后才到的结果一律隔离（新 ResultId → stale；已交付
  ResultId 重投 → Duplicates），不得翻转已完成 op（「完成后才到的
  failure」反例）；③deadline 同为事件：预算耗尽在其事件时间生效。原
  「先收齐所有 ≤now 的到期结果再统一判定」的实现被 S5b RED 证伪并修
  正（旧实现下 t6 拉取会把 t6 迟到的错误 Fast 收进交付批、t5 迟到的
  failure 把已完成 op 翻转为 Failed）。

## Acceptance（验收 = roadmap Phase 4 Acceptance 的场景化）

1. 八组场景 GREEN（每组 method/expected/actual/evidence 四元组入
   evidence report）：
   ① Fast 漏目标 → 定向 Slow 找回（omission≠absence；新 proposal→新
   revision→fresh grounding；恰一 Effect 落真实目标）。
   ② Fast 误判状态 → decision/完成判定前纠正（agent consultation context
   含纠正后 claims）；Fast-only 对照臂 fail closed（VerificationFailed，
   零错误完成）；旧 revision occurrence 绑定失效（stale 绑定零 Effect）。
   ③ 补充性 Slow：irrelevant / exact-duplicate accepted Evidence 零
   revision；basis 增长 / Conflict / Uncertainty 变化 → 新 revision。
   ④ 旧 capture 的 Slow 迟到（页面/revision 已推进）→ Superseded 隔离，
   零 belief 污染、零动作。
   ⑤ 同 capture 乱序 + 精确重复投递 → 确定性合并、结果确定、Effect 不
   重复；⑤b（复审追加）拉取节奏不变性：事件时间完成语义——slow final
   @t2 已满足 coverage、错误 fast @t6 才到，t3 与 t6 拉取同批同结论、
   CompletedAt=完成事件时刻、迟到结果隔离；「完成后才到的 failure」反
   例不翻转已完成 op。
   ⑥ partial / 真空 / failure / timeout 四态严格区分；未覆盖 region 不得
   推断 absence；failure ≠ OK_EMPTY。
   ⑦ 等待期 cancel → SafeStop terminal；此后结果到达零复活、零 late
   Effect。
   ⑧ 小标题/副标题误作菜单项：真值分类差异核对（static_title ≠
   section_label ≠ row_subtitle ≠ row_title，clickable 差异）；纠正成功
   臂（副标题/分组标题不形成第二可点击目标；Colors 只接地真实菜单；
   含糊零点击）与仍错误/冲突臂（Slow 不因更慢成真值；歧义零点击）；
   交换到达顺序 + 重复投递不变量保持。
2. 四 realization（Fast-only/Fast→Slow/Slow-only/one-shot）同语义输入：
   规范化世界结论与安全行为比较（不比原始字节）；结论差异报首个分歧。
3. 真实 live one-shot/fast 策略路径 + recorded deterministic adapter 均在
   场景中真实执行（成分与边界写入 report）。
4. 每场景记录 observation/admission/reconciliation/revision/Effect 计数
   与关键路径虚拟延迟；Trace 仅诊断。
5. 三套回归（Kernel.Tests / Agent.Tests / Simulation.Tests）实际通过数报
   告；`git diff --check` 干净；新增未跟踪文件空白检查等价通过。
6. H9：correlation 保持 tracer 内（无产品 Interface 变更——结构性断
   言）；H10：无 preliminary 自动授权路径（fail closed 断言）。

## Constraints

- 不 reset / 不清理 / 不覆盖并行未提交内容；不改 `src/`、`schemas/`、
  `model-routing.yaml`、docs、csproj、slnx。
- 可控虚拟时间 + 录制输入；禁 sleep 竞态、禁直接注入期望 owner state、
  禁测试逐 cycle 驱动 Kernel（只经 Drive/NextInput 合法面）。
- 测试只观察公共/internal seam 与 owner 只读投影，不读私有字段伪造证明。

## Verification

逐场景 method/expected/actual/evidence 四元组 + 计数/延迟/首个分歧见
`evidence/2026-09-14-uap001-phase4-async-perception-tracer/report.md`（§1–§6）。

```yaml
verification:
  - level: DETERMINISTIC + SCENARIO
    method: >
      dotnet test UniClaw.Kernel.slnx（三套全量）；
      RED 留痕：实现前 AsyncPerception* 15/16 失败
      （S0 前置真值核对按设计通过）
    expected: >
      八组场景全 GREEN；四 realization 同构比较 + live 路径接入 +
      产品缺陷回归通过；既有三套回归零回归
    actual: >
      Kernel.Tests 382/382 · Simulation.Tests 112/112 · Agent.Tests
      17/17（共 511，0 失败）；AsyncPerception*+回归 18/18 GREEN
      （含复审追加 S5b）；RED 阶段 15/16 + S5b（旧引擎节奏依赖/迟到
      failure 翻转）失败已留痕
    evidence: >
      evidence/2026-09-14-uap001-phase4-async-perception-tracer/report.md §5
  - level: DETERMINISTIC（产品缺陷）
    method: CanonicalEnumerationRegressionTests（缓存中途视图+SetItem 链）
    expected: 修复后 canonical 枚举 = storage（修复前 count=3/enum=[s.late]）
    actual: 通过（枚举全量、Revise 值正确、basis 一致）
    evidence: report §4；修复授权记录见本文件 D11
  - level: CONTRACT
    method: git diff --check；新增未跟踪文件空白/UTF-8/EOF 检查
    expected: 全部干净
    actual: diff-check exit 0；7 文件 PASS（含 report）
    evidence: report §5
  - level: CONTRACT（H9/H10 纪律）
    method: >
      结构检查：correlation/completion 类型全部 internal 且仅存在于测试
      程序集；场景断言全部 admitted Observation-kind evidence producer =
      perception.runtime；Fast-only/未纠正臂零 effect 不伪装 Completion
    expected: H9 不升格公共模型；H10 无 preliminary 自动授权路径
    actual: 满足（S2b/S6/S8b fail closed 断言通过；src/ 唯一改动为 D11 授权修复）
    evidence: report §7/§8；test:S1/S2b/S8b/S6
```

## Owner-Authority impact

零产品 Owner 变更：Evidence/World/Run/Control/Assurance/Effect 所有权与
协议 P2/P3 消费路径不变；tracer 是测试程序集内的 Perception realization
编排证据，不是第二 Runtime/World/Effect authority；不新增公共 Interface。

## Assumptions

- A1 `type-truth.json` 为 Human-reviewed Ground Truth（FSV-001 资产），
  仅作测试参照与 Slow double 的录制输出源，不回写 Evidence/Belief/Control
  ——与 WRC-001 同纪律。
- A2 `type-dual-local.json` 的 `pred` 是真实模型历史预测（FSV-001 probe），
  作为录制错误臂输入；本 Change 不重算模型、不报告模型准确率结论之外的
  质量声明。
- A3 golden-run-v1 provider response JSON 是真实 live provider 录制响应
  （RFS-001 已用作 determinism anchor）；经真实 strategy 代码路径消费即
  视为 live one-shot/fast 只读采集路径接入（无设备环境的诚实边界）。

## Alternatives（被拒）

- 在 `src/` 新增 ObservationSession/ICaptureCorrelation 产品模型——被拒：
  H9 未决，无两个真实 adapter 与 buyer 证据（§12.2 晋升门）；tracer 优先。
- 逐结果渐进投递给 driver——被拒：现 phase 机一次 pull 即进 decision，
  渐进投递会把未完成观察当充分证据；hold-until-complete 才能表达
  「Runtime 只看 observation contract 状态」。
- Slow double 直写 WorldBelief 修正——被拒：违反 P2/P3 唯一 ingress；纠正
  必须以 proposal 经 admission/relevance/reconciliation。

## Residual risks

- R1 Slow/full-model 无真实 adapter：只验证了异步时序/失败/相关性语义，
  未验证真实 Slow 模型质量与第二真实产品 adapter（如实声明）。
- R2 hold-until-complete 是 tracer 级投递策略（D3），非冻结产品语义；产
  品化时渐进投递 + sufficiency 判定仍待 H9/H10 裁决。
- R3 `KernelRunDriver` GroundingFailed 后无再观察循环（Phase 5/6 能力缺
  口）：① 的「定向 Slow」由 Perception realization 内部完成，非 Runtime
  re-observe 协议——语义边界已在 report 标注。
- R4 occurrence 景观跟随最后 reconcile 的 live.frame（revision-local 替
  换）：typing claim 的 Conflict 不自动撤销景观条目，歧义靠 grounding
  fail-closed 兜底；产品级 typing 仲裁语义待 UWM 后续裁决。
- R5 无设备 live 环境臂：真实采集/传输故障分类未执行（Phase 7）。
- R6 顺序敏感（D12，窗口限定）：完成窗口内同流纠正可被更晚的同流误
  判覆写（完成窗口外的迟到结果已被 D13 隔离）；跨 producer 挑战不阻
  止窗口内最后到达帧替换 occurrence 景观（S8b-B2 的安全依赖 review 帧
  最后到达）。conflict-aware grounding/assurance 是缺口，待 H9/后续
  Change（本 Change 明示不做）。
- R7 D9 descriptor 键合并依赖页内文本唯一（当前真值页满足）；重复文
  本元素的合并语义需 UWM 后续裁决。
- R8 产品缺陷修复（D11）虽经 510 项回归 + 独立回归测试，但
  PersistentRevisionCollections 的完整语义测试面属 WMP 谱系，建议
  Human review 时对 WMP 系测试做一次针对性目检。

## Status log

- 2026-09-14 · understanding→resolved · Entry/Resume 完成：HEAD/pin 核对、
  并行边界确认、P2/P3+Phase4/H9/H10+真值文件核对、buyer seam 确认。
- 2026-09-14 · resolved→persisted→planned · 本 state.md 建立；PLAN=D1–D8。
- 2026-09-14 · planned→implemented · RED（15/16 失败）→ GREEN（17/17）；
  发现并（Human 授权）修复 PersistentRevisionCollections canonical 枚举
  缺陷（D11）；D9/D10/D12 语义发现入档；三套回归 510/510；证据报告落
  盘。待独立 Review/Verify 后提交 Human review。
- 2026-09-14 · implemented→reviewed→verified · 独立 Review（mutation
  probe×3 + 真值实核）与独立 Verify（隔离副本复证 D11 修复前签名
  count=3/enum=[s.late]、恰 S2a 失败）均无 blocker/major；minor 落实：
  D6 宿主驱动措辞、S2b reason 断言、S4 负对照臂、tracer CS8625 消警、
  report 措辞/计数修正。提交 Human review（不 CLOSED、不 commit）。
- 2026-09-14 · Human 复审指令 · 暂不 CLOSED/不 commit；追加 RED 对照
  S5b（拉取节奏不变性 + 完成后迟到 failure 反例）→ 旧引擎 RED 证实两
  缺陷（迟到错误 Fast 入交付批、迟到 failure 翻转已完成 op）→ PumpAll
  重写为事件时间逐项推进（D13）→ S5b/S3a/S8c GREEN（S3a 精确重复改在
  完成事件前到达；S8c 迟到 Fast 隔离、纠正稳定）→ 三套回归 511/511。
  未实现 R6 跨操作仲裁、未触碰并行文件。待复审。
- 2026-09-19 · closing · Human 经 `evidence/2026-09-16-abg001-uap001-human-review-docket.md` 裁决批准关闭；三套件复跑 511/511 一致；R1–R8 residual（Slow 真实 adapter、H9/H10、GroundingFailed 再观察、WMP 目检等）按声明归后续 Phase 5/6/7
