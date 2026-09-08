# 0013 — Run Trace 是 reference-oriented non-authoritative projection；OTel 仅 adapter；显式 parent

uni-agent legacy Trace 的代码级分析
（docs/analysis/uni-agent-trace-replayability-analysis.md，Authority: NONE）
证实：span 属性为 string 袋、parent 经 ambient Activity.Current、词表在
Runtime 与 DriverHost 双份硬编码、CaptureRecord 内嵌 domain 对象副本、
Reason 字符串前缀解析决策。同时第一真实 buyer 已经出现：Runtime
diagnosis / First-Divergence 定位（uniclaw-debug-evidence 工作流，E0-E4）
与 UIW-001 建立的 replay 对照能力，都需要一个 Primary Run 的结构化因果
投影。人工裁决（2026-09-09，五道 Human Gate 闭合）授权 TRC-001 与本
ADR：只做语义冻结 + tracer bullet，扩张需真实消费证据。

## Decision

```text
RunTraceArtifact
= ONE Primary Run 的 immutable / non-authoritative / structural causal projection
→ 只记录经批准的 operation occurrence + 指向各 Owner 已产生 immutable facts 的 typed reference
→ MUST NOT 复制 / 解释事实；MUST NOT 参与任何 Runtime 决策
→ projection 不可回写 canonical source；observability surface ≠ command surface（baseline §21.2）

Correlation root
= Primary Run / RunId（canonical）
≠ TraceId / SpanId（technical identity，无 domain authority）
≠ Product Session（可选 reference，非 root）

Lifecycle
= caller-owned RunTraceScope：驱动 Primary Run 执行的那一个组件
≠ 任何 L2 Owner；≠ ambient / 全局静态 / nullable fallback
→ 禁用 tracing 必须显式 DisabledRunTrace

Parenting
= explicit parent（StartOperation 显式传入）
→ Activity.Current / wall clock / random / 全局 mutable state MUST NOT 承载语义正确性

Reason codes（封闭 disposition）
= 封闭 enumerable union · typed/冻结常量 · 按 SpanDefinition 声明 · 随 SchemaVersion 版本化
= 码只来自/一一映射 owner 已发布词汇（version-conflict / concurrent-terminal-proposal 等）
≠ 自由文本；≠ trace 发明的 domain 语义
→ 存在 durable owner record（如 AssociationDecision）⇒ TraceReference 优先，code 禁止重复携带

OpenTelemetry
= 可替换 export adapter（未来）；永不成为 Target domain model
```

## Considered Options

- **OTel 数据模型直接作为 Target domain model**：被拒——自由 tags、
  ambient context、传输级时间语义会把 string-bag 与隐藏输入带入
  domain；Target 先定义更窄的 typed contract，OTel 降级为未来 export
  adapter。
- **Run Model 拥有 trace projection**：被拒——baseline 权威表明示
  trace projection 非 Run Model 所有；L2 不得因 observability 新增
  canonical concern，也不得被 trace 读取形成决策回路。
- **ambient Activity.Current parenting / 全局静态 recorder**：被拒——
  legacy 反例（RuntimeObservability 全局静态、tag stop 时才可读、进程级
  listener 互斥）。
- **严格 refs-only（拒绝点零信息）**：被拒——admission / gate 拒绝常无
  durable artifact（P16 显式 absence 先例：证据不足 = 无 proof 对象），
  拒绝点不可见将直接摧毁 First-Divergence buyer 的核心用例。
- **Product Session 作为 trace root**：被拒——提前购买 pre-run / Goal
  revision / 多 Run 等未实现生命周期。
- **trace 自动成为 Evidence**：被拒——Observability Artifact ≠ Evidence
  Record；未来若有 buyer 需另立 admission 语义与 gate。

## Consequences

- TRC-001（decision-heavy）承载语义冻结与 tracer bullet；bullet 阻塞于
  RUN-001（真实 RunId 注入，独立 change，确定性派生方案由其自行裁决）。
- NET_NEW_MUTABLE_TRUTH = 0：六个 L2 Owner / Authority 不变，六个 L2
  均不读 trace；Trace Recorder 为唯一新增非 canonical mutable state
  （terminal = Finalized | CaptureFailed | Quarantined）；architecture
  guard 证明无 trace→decision 回路。
- buyer 授权至 tracer bullet 为止；read model / persistence / OTel
  export 的扩张需 ≥1 次真实诊断会话消费过 artifact 的证据。
- canonical clock 维持 Deferred ⑪：trace timing = 可选、非权威观测
  元数据，replay / freshness / authority 不得依赖。
- 词表策略：SpanDefinition 唯一 catalog（Operation × ObservedTargetOwner
  × AllowedReferenceKinds × AllowedEvents），bullet 子集
  （evidence.admit / world.reconcile）binding，其余 9 项 provisional
  随 slice 落地逐项冻结。
