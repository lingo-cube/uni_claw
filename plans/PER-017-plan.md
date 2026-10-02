# PER-017 Plan — Slow Perception orchestration foundation

> Plan 只描述 HOW；WHAT/WHY/Acceptance 以 `changes/PER-017/state.md` 为准。
> 本计划不接入真实 Text/Vision provider，不改变 PER-009/010/011 authority。

## Entry gates

1. 复验当前 HEAD、工作树和 PER-017 state；若与 state/base 不一致，回
   UNDERSTAND，不带矛盾实施。
2. 确认 Focused crop/coordinate-remap prerequisite 的 owner 与 gate 状态。
   未通过时可继续 deterministic replay contract，但不得宣称 Slow production-ready，
   也不得以 subject filtering 冒充真实 Focused crop。

## Slice A — Typed internal request/context records

行为：Control 已授权、同一 capture/cycle、有 required claim 时，能够构造内部
Slow request；无 required claim、缺 provenance 或 context basis 不完整时 fail closed。

模型与 seam：

```text
RequiredClaim(Subject, Field)
BuyerRef
AttemptKey(target, cycle, claim, profile)
CaptureContext(existing FusionCapture)
RawArtifactRef(existing ArtifactId identity)
ElementLayoutContext
SemanticReasoningContext
ConflictBasisBundle
ExclusionDisposition
SlowPerceptionRequest
```

约束：typed internal records；复用 `ObservationClaim`、`EvidenceRecord`、
`FusionCapture`、`RawArtifact`、`ObservationContext` 的现有语义；不创建公共
`Ixxx`，不扩展 P2/P3/AgentDecisionContext。

测试：

- RequiredClaim 只有 Subject+Field；禁止 expected value/confidence/authority 字段。
- ArtifactId 与 CaptureId 不混淆；Capture/Cycle/Session provenance 完整性失败时
  不产 request。
- Element/Layout 与 Semantic context 可独立或组合；按 RequiredClaim/Reason 有界。
- ConflictBasisBundle 整体保留；无法整体保留 → ContextInsufficient、零 proposal。
- ExclusionDisposition 只影响当前 context，不删除 EvidenceRecord。

## Slice B — Model Management capability + experimental profiles

行为：logical profile resolve 到已声明 concrete binding snapshot；未知 profile、
缺模型、health/schema/identity 不匹配均显式 `ROUTING_UNAVAILABLE`。

profile：

```text
slow.semantic.text
slow.semantic.visual
```

两者均保持 `experimental`。Product 只持有 logical profile；realization adapter
持有 provider/model binding。绑定 identity 复用现有 provider 的
`modelId/configId/pipelineRevision/deploymentId/variantId`；不复用
`.dsh/model-bindings.yaml` 作为 Product registry，不做热切换或 silent fallback。

测试：启动冻结 snapshot、未知 profile、缺模型、失败 health、schema mismatch、
identity mismatch、显式 fallback 与 `ROUTING_UNAVAILABLE`。

## Slice C — Attempt ledger + route hint

行为：`ObservationDepth.Slow` 仅作为 route hint；Control 在真实调用前原子占位
keyed attempt ledger。

规则：

```text
same AttemptKey → one semantic invocation
transport retry → invocation-local only
terminal AttemptKey → no semantic retry
new cycle → new AttemptKey
```

全局 budget 可保留为摘要，但 keyed ledger 是执法面；ledger 为 ephemeral，不能
成为新的持久 authority/state owner。

测试：重复请求、并发占位、transport retry、timeout、cancel、terminal retry 拒绝、
new cycle 重置、late result 不重复扣预算。

## Slice D — Replay Slow adapter + SlowResult

行为：deterministic replay realization 支持 text/visual 两个 logical profile，
不绑定真实 provider。

输入：Text 读取 bounded Product-normalized/admitted Evidence Context；Visual
读取同一 RawArtifactRef + capture provenance，并可带 bounded context。

输出：

```text
SlowResult
├ AttemptKey / RequestId
├ execution status
├ ObservationProposal[]
├ ModelBindingSnapshot
├ diagnostic
└ original capture provenance
```

失败：失败/取消/超时/不可用/坏 schema → zero proposal；partial 逐条 provenance
完整才可进入 P2；成功空结果不产生 absence claim。

## Slice E — Control wait + P2/WorldModel integration

行为：

```text
Control authorizes
→ orchestration builds request
→ model binding resolves
→ attempt reserved
→ adapter executes
→ Control waits iff effect-critical
→ proposals → P2 → Evidence Ledger → WorldModel
```

Effect Gate 不调用或等待 Slow。非 effect-critical 可继续。late result 保留原
Capture/Cycle provenance，可在 freshness/currentness 允许时进入 P2/后续 reconciliation，
但不得满足 expired admission 或 retroactive effect authorization。

测试：effect-critical bounded wait、non-effect-critical continue、timeout/failure
fail-closed、late/duplicate/out-of-order、P2 provenance/lineage、Effect Gate
non-invocation。

## Gate F — Foundation acceptance

全部 Slice A–E focused tests 通过；replay 证明链完整；`git diff --check` 通过；
Product/DSH 边界、P2/P3 authority、PER-009/010/011 frozen semantics 无偏离。

Gate F 不等于真实模型可用。真实 Text/Vision provider 必须在后续独立 slice 中完成
availability、paired claim-domain benchmark、latency/timeout、reliability evidence
和 production eligibility。
