# TRC-001 — Run Trace Reference：非权威因果投影基线（语义冻结 + tracer bullet）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 10cd12f6

## Intent（WHAT/WHY）
为第一 buyer——Runtime diagnosis / First-Divergence 定位 / replay 对照——建立
Target 侧 Run Trace 基线：冻结语义（术语 / Owner / identity / lifecycle /
payload 分类 / 禁止项），以 ADR-0013 记录架构承诺，并用最小 tracer bullet
（真实 RunId + caller-owned root scope + evidence.admit → world.reconcile，
InMemory adapter）证明投影不改变 canonical 行为。

buyer 授权边界（G1）：仅至 tracer bullet；read model / persistence /
OTel export 等扩张需 ≥1 次真实诊断会话实际消费过 artifact 的证据。

## Scope
- S1 语义冻结：术语表（RunTraceScope / RunTraceArtifact / SpanDefinition /
  TraceSpan / TraceReference / StructuralOutcome / TraceEvent /
  TraceDiagnostic）；RunTraceScope / Artifact lifecycle 与 finalize 语义；
  SpanDefinition 唯一词表机制（Operation × ObservedTargetOwner ×
  AllowedReferenceKinds × AllowedEvents，生产与查询共享）；TraceReference
  封闭 union；StructuralOutcome 封闭词汇；reason-code 三边界。
- S2 ADR-0013（reference-oriented non-authoritative projection；OTel 仅
  adapter；显式 parent）。
- S3 tracer bullet（IMPLEMENT 阻塞于 RUN-001 真实 RunId 注入）：caller-owned
  root scope + evidence.admit / world.reconcile 两 operation；
  InMemoryRunTrace + DisabledRunTrace 显式禁用；tracing on/off canonical
  等价 + 归一化 causal graph 测试。
- S4 architecture guard：无 Trace→Control / Assurance / Effect / Run State
  依赖。

## Out of Scope（禁止）
- read model / persistence / wire / CLI / UI / remote exporter / 自然语言查询。
- Product Session 作为 trace root；多 Run / continuation 语义。
- trace 自动成为 Evidence 或任何 canonical 输入；trace 驱动
  retry / recovery / completion。
- act/terminal 链等其余 catalog operation 的实现（bullet 后按 buyer 证据
  另行裁决；词表项可先行登记为 provisional）。
- 修改六个 L2 公开 interface；TraceContext / Span / exporter 类型进入 L2。
- canonical clock 裁决（维持 Deferred ⑪；Timing = 可选非权威观测元数据）。
- 双份词表；ambient parent（Activity.Current）；nullable / 全局静态 /
  隐式 trace fallback。

## Decisions
人工裁决（2026-09-09，五道 Human Gate 闭合；详见 ADR-0013）：
- G1 第一 buyer = Runtime diagnosis / First-Divergence / replay 对照，
  授权至 tracer bullet；扩张需真实消费证据（可证伪，不永久授权）。
- G2 真实 RunId = 硬前置但并行：单独 RUN-001 承担 identity 注入（确定性
  派生方案由该 change 裁决，可参考 ContainerIdentity `ctr-` + 内容哈希
  先例）；TRC-001 语义工作不阻塞，S3 阻塞于 RUN-001。
- G3 RunTraceScope lifecycle = caller-owned："caller"钉死为驱动 Primary Run
  执行的那一个组件（当前 = kernel run 入口直接调用方；测试 = test host；
  未来 = host 层），写入 ADR 防漂移。
- G4 允许封闭 reason code，三边界：(i) 封闭 enumerable union、typed enum /
  冻结常量（禁裸 string）、按 SpanDefinition 声明、随 SchemaVersion 版本
  化；(ii) 码必须来自/一一映射 owner 已发布词汇（如 ContractAdmission
  "version-conflict"、OutcomeTransition "concurrent-terminal-proposal"），
  trace 不发明 domain 语义；(iii) 该负结果存在 durable owner record（如
  AssociationDecision）时 TraceReference 优先，code 禁止重复携带。

设计决策（v0.1 草案经 grill-with-docs 复核后采纳）：
- StartOperation(SpanDefinition, explicitParent, TraceReference[]) 取代
  StartSpan(name, layer, component)；生产与查询共享唯一 catalog，禁双份
  词表。
- 词表冻结策略：机制 + bullet 子集（evidence.admit / world.reconcile）为
  binding；全 11 项 catalog（run.execute … runtime.emit-outcome）登记为
  provisional，随各自 slice 落地逐项冻结。
- TraceReference 封闭 union：RunRef / EvidenceRef / WorldRevisionRef /
  AssociationDecisionRef / IntentRef / BindingRef / AssuranceJudgmentRef /
  ReceiptRef / OutcomeProofRef / RuntimeOutcomeRef；judgment 无独立 ID 时用
  既有 correlation tuple（如 (IntentId, BindingId, RevisionId)），不铸造
  canonical-looking identity。
- StructuralOutcome 只表达 Completed / Faulted / Cancelled / Incomplete；
  Admission rejected / Gate denied / Outcome Failure 仍是被引用的 domain
  结果，不映射为 trace failure。
- EffectReceipt → AttemptReport → Evidence admission 复用嵌套
  evidence.admit / world.reconcile，经 ReceiptRef 表达 causation，不另建
  重复事实链；Container Association 不建独立 span，只引用
  AssociationDecision append-only log。
- Recorder buffer = 唯一新增非 canonical mutable state，sole owner =
  Trace Recorder，terminal = Finalized | CaptureFailed | Quarantined；
  finalize 于 runtime.emit-outcome 记录之后、幂等；未关闭 span 显式标
  Incomplete，不伪造 duration / success；Recorder 自身故障只产生
  TraceDiagnostic，不伪装 Runtime failure。
- 禁用 tracing 必须显式 DisabledRunTrace（禁 nullable / 全局静态 / 隐式
  fallback）。
- G4 bullet 落地形态：事件词汇按 SpanDefinition 封闭（admitted /
  admission-rejected / reconciled / reconcile-idempotent）；ReasonCode =
  owner 已发布拒绝词汇透传（如 source-identity），trace 不铸造 domain
  语义；rejected 无 durable record → 只带 code；accepted / reconciled
  reference-first。
- 实现落点 src/UniClaw.Kernel/Trace/（assembly / namespace = realization）。

## Assumptions
- buyer 真实性：uniclaw-debug-evidence skill（E0-E4 / First-Divergence /
  失败分类学）证明运行时诊断是反复发生的工作流；UIW-001 S9–S12 使 replay
  对照成为现实能力。若 bullet 后持续无真实消费事件，收缩而非扩张。
- RUN-001 会在 S3 开始前交付确定性 RunId 注入。
- Session / 多 Run 生命周期非 v0.1 buyer 所需（G3 附带裁决）。

## Alternatives（含被拒）
- OTel 数据模型直接入 domain：拒（string-bag / ambient / 时间语义风险；
  OTel 只作未来 export adapter）。
- Run Model 拥有 trace：拒（baseline 权威表明示 trace projection 非
  Run Model 所有）。
- ambient Activity.Current parenting：拒（legacy 反例：全局静态
  RuntimeObservability、tag stop 时才可读）。
- 严格 refs-only：拒（G4——拒绝点不可见伤及第一 buyer；P16 显式 absence
  先例：负结果可无 artifact）。
- Product Session 作为 trace root：拒（提前购买未实现生命周期）。
- trace 自动升 Evidence：拒（authority 污染；未来另立 admission 语义与
  gate）。
- 全 11 项 catalog 立即冻结为 binding：拒（未行使的语义先冻结 = 重蹈
  read model 提前冻结 schema 的风险；改为 provisional 逐项冻结）。

## Owner-Authority impact
NET_NEW_MUTABLE_TRUTH = 0。Evidence Ledger / World Model / Run Model /
Control Loop / Assurance / Effect Boundary 的 Owner / Authority / lifecycle
完全不变；Trace Recorder 为新增非 canonical state 的唯一 owner；六个 L2
均不读 trace；无 trace→decision 回路（S4 guard 证明）。observability
surface 不得成为 command surface（baseline §21.2）。

## ADR refs
- docs/adr/0013-run-trace-is-reference-oriented-non-authoritative-projection.md
  （本 change 创建，随 S2 落盘）
- docs/adr/0011（owner-derived immutable projection 纪律参照）
- docs/analysis/uni-agent-trace-replayability-analysis.md（RESOLVE 输入；
  Authority: NONE）

## Residual risks
- runtime.emit-outcome 仍为 provisional：finalize-after-emission 目前是
  caller 纪律（bullet 测试如此执行），机械执法（emission span 存在性
  检查）随该词表项冻结时落地。
- owner reason 词表仍为 string 约定封闭（非 typed enum）：bullet 以
  AllowedEvents 封闭事件 + owner 词汇透传控制；typed enum 化随
  act/terminal 链扩展一并裁决。
- buyer 证伪窗口未定量：建议首个真实诊断会话（uniclaw-debug-evidence
  工作流）实际消费 artifact 后，再裁决 read model / persistence /
  OTel export 扩张。
- 工作树已于 2026-09-09 收口（ARCH-DOC-013 / UIW-001 / PER-002 /
  TRC+ADR 四分片提交，f014934f..10cd12f6）；base re-pin 至 10cd12f6
  （本 state.md 落库 commit）；基线引用已全部使用 docs/architecture/
  新路径。

## Acceptance
1. tracing enabled/disabled 时，所有 canonical outputs 与 Owner logs
   完全一致。
2. recorder / emitter / listener 抛错不改变 Runtime 行为（吸收 +
   TraceDiagnostic）。
3. L2 公开 interface 无 TraceContext / Span / exporter 类型。
4. 唯一 typed catalog；生产与查询无重复词表。
5. artifact 不内嵌 domain aggregate / raw observation / prompt / 截图 /
   provider report。
6. parent 显式传递；语义正确性不依赖 Activity.Current / wall clock /
   random / 全局 mutable state。
7. 同一确定性场景归一化 technical IDs / timing 后 causal graph 相同。
8. runtime.emit-outcome 记录后才 finalize；finalize 幂等。
9. 未关闭 span 显式标 Incomplete，不伪造 duration / success。
10. architecture guard 证明无 Trace→Control / Assurance / Effect /
    Run State 依赖。
11. 零 persistence / wire / CLI / UI / remote exporter / 自然语言查询。
12. bullet 消费 RUN-001 注入的真实 RunId（非 "run-1"）。

## Constraints
既有测试零回归（当前 93 = Kernel 76 + Agent 17）；产品代码不进 harness
层；不修改 view allowlist / Accepted7 输入封闭 / N5 freshness 命名等既有
锁定面；不修改 L0-L3 基线语义（trace 落在 §21.2 投影纪律之内，不重开
基线）。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >
    dotnet test tests/UniClaw.Kernel.Tests + tests/UniClaw.Agent.Tests；
    RunTraceBulletTests（8 facts：canonical 等价 / throwing-trace 吸收 /
    归一化 causal graph + 真实 RunId / 词表执法 / finalize 幂等 +
    Disabled 显式空 / Incomplete / artifact 形状 guard / L2 架构 guard）
  expected: >
    新增 8 测试 GREEN；既有 112 零回归；Acceptance 1–12 bullet 子集满足
  actual: >
    RED（缺类型编译失败）→ 实现（Trace/ 8 文件 + UniKernel 组合缝显式
    IRunTrace 必选参数 + 15 处调用点显式 DisabledRunTrace.Instance）→
    120/120 GREEN（Kernel 103 + Agent 17；新增 8，既有 112 零回归）。
    A1 on/off 键字段投影一致；A2 throwing double 吸收；A3/A10 反射证明
    六 L2 无 Trace 类型引用；A4 TraceCatalog 唯一词表 + 执法（非法
    ref/event 丢弃 + TraceDiagnostic）；A5 形状 guard 证明 public 面仅
    string/int/enum/封闭集合组合；A6 TraceId/SpanId 确定性派生（RunId
    哈希 + 捕获序数），无 ambient/random/wall-clock；A7 归一化 causal
    graph 全等；A8 finalize 幂等（Equal 验证；emission-op 仍
    provisional，finalize-after-emission 为 caller 纪律，机械执法随
    runtime.emit-outcome 词表项冻结时落地——见 Residual risks）；
    A9 未关闭 span 标 Incomplete；A11 零 persistence/wire/exporter；
    A12 artifact.RunId = run-<hash12>（消费 RUN-001 派生身份）。
  evidence: dotnet test 输出（2026-09-09，103+17 全绿）；TRC-001 commit
```

## Status log
2026-09-09 · understanding→resolved · 五道 Human Gate 闭合（G1 buyer 两档 /
G2 RunId 并行前置 / G3 caller-owned / G4 封闭 reason code 三边界 / G5 授权
TRC+ADR）；v0.1 草案 + legacy 分析 + 基线权威表/§21.2 复验完成
2026-09-09 · resolved→persisted · state.md + ADR-0013 建立（base
a2bf82e9；树上并行用户变更待收口，见 Residual risks；S3 阻塞于 RUN-001）
2026-09-09 · persisted（base re-pin，非状态转移）· 工作树收口完成
（f014934f..10cd12f6 四分片）；RUN-001 落地（edb68d2d，S3 阻塞解除）；
待 S3 规划转入 planned
2026-09-09 · persisted→planned · S1 词表冻结（TraceCatalog：机制 +
bullet 子集 binding，其余 9 项 provisional）+ S3/S4 垂直切片计划定稿
2026-09-09 · planned→implemented · RED（缺类型编译失败）→ Trace/ 8 文件
（模型/catalog/adapters/scope）+ UniKernel 显式 IRunTrace 必选参数 +
Process 埋点（admit→reconcile 显式 parent causation）+ 15 调用点迁移
→ 4 处测试断言修正（record List 成员无深度相等 → 键字段/归一化投影；
形状 guard 限定 public 面）
2026-09-09 · implemented→reviewed · REVIEW：L2 公开 interface 零触碰
（架构 guard 证明）；无 persistence/wire/exporter；UniKernel 行为与
埋点前一致（既有 112 零回归）
2026-09-09 · reviewed→verified→closed · 120/120 GREEN（Kernel 103 +
Agent 17；新增 8，既有 112 零回归）；Acceptance 1–12 bullet 子集逐条
核验（A8 caller 纪律注记见 Residual risks）→
RUN_TRACE_REFERENCE_BASELINE_ESTABLISHED
