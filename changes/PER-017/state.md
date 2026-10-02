# PER-017 — Slow Perception orchestration 与模型管理基础
lifecycle_state: implemented · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

把 Slow Perception 从“Deep VLM 后置概念”收敛为 Perception Capability Plane
中的一等能力，并建立首个可实施但不依赖真实模型的内部编排闭环：

```text
Control authorization
→ Perception orchestration
→ logical profile
→ Model Management binding snapshot
→ Slow adapter / replay realization
→ SlowResult
→ ObservationProposal[]
→ P2 / Evidence Ledger
→ WorldModel reconciliation
```

本 change 先证明请求、上下文、预算、异步、迟到和 authority 边界；真实 Text
LLM / Vision VLM provider 作为后续独立 slice，不因配置存在而宣称可用。

## Scope

- 内部 `SlowPerceptionRequest` / `SlowResult` orchestration seam。
- `Normal → Focused → Slow` route hint；`ObservationDepth.Slow` 不携带模型细节。
- RequiredClaim、BuyerRef、AttemptKey、Capture/Fusion provenance 与 bounded budget。
- Element/Layout Context 与 Semantic Reasoning Context 的组合、裁剪、原子冲突 basis
  保留和 `ContextInsufficient` fail-closed 规则。
- Product logical profiles：`slow.semantic.text`、`slow.semantic.visual`，状态为
  `experimental`。
- Model Management capability 的窄职责：logical profile resolve、availability、
  concrete binding snapshot、显式 fallback、`ROUTING_UNAVAILABLE`。
- 复用既有 provider identity：`modelId`、`configId`、`pipelineRevision`、
  `deploymentId`、`variantId`；不建立第二套 identity owner。
- Text/Visual 两种 deterministic replay realization；真实 provider 接入不在本
  slice 内。
- Control-owned keyed ephemeral attempt ledger：实际调用前原子占位；按
  target/cycle/claim 限制 in-flight 与一次性尝试；不产生持久 authority/state。
- Effect-critical bounded wait、non-effect-critical continue、late result 的原
  capture provenance 与 P2 admission 规则。

## Out of Scope

- 真实 `deepseek-v4.1-flash` 或 `deepseek-v4-flash-vision-exp` provider 接入、
  availability 声明或 production eligibility。
- DSH session/workflow/profile state 进入 Product Runtime；`.dsh/model-bindings.yaml`
  不作为 Product registry。
- 下载、安装、热切换、自动调参或模型生命周期平台。
- 新增公共 `Ixxx`、扩展 `ObservationNeed`、`ObservationProposal`、`EvidenceRecord`
  或 `AgentDecisionContext` 语义。
- Whole-ledger/world dump、provider raw JSON/tensor/OCR DTO 进入 Slow context。
- Effect Gate 直接调用或等待 Slow。
- Focused crop/coordinate-remap 的真实设备实现；该 prerequisite 必须由其独立
  change 完成并在 Slow production slice 前通过 gate，本 change 仅保留 route/contract
  依赖。
- 新的 reliability/confidence schema 或全局 confidence voting。

## Decisions

1. **Capability boundary**：Slow 是 Perception Capability；具体模型/provider 是
   realization；Perception pipeline 可以编排 Fast、Focused、Slow 和可用的
   hierarchy capability。
2. **Authority closure**：Slow 只产 ObservationProposal；唯一证据入口仍是 P2，
   WorldModel 仍是 belief/reconciliation owner，Slow 不拥有 identity、grounding、
   assurance 或 effect authority。
3. **Orchestration ownership**：Control 授权 Slow；Perception orchestration 选择
   logical profile 并组装 request；Model Management resolve concrete binding；
   adapter 执行；Effect Gate 永不调用或等待 Slow。
4. **Input modes**：Text profile 使用 Product-normalized/admitted evidence context；
   Visual profile 使用同一 RawArtifact/screenshot，并可附带 bounded context；
   RawArtifact.ArtifactId 与 CaptureId 保持双标识。
5. **Context protocol**：Element/Layout 与 Semantic Reasoning context 可组合，按
   RequiredClaim/Reason bounded；冲突/歧义最小 evidence basis 原子保留，无法保留
   时为 `ContextInsufficient`；evidence-backed exclusion 只作用于当前 reasoning/
   capture context，不删除 evidence、不成为 canonical truth。
6. **Model management**：Product 只拥有 logical profile；realization adapter 拥有
   concrete binding。模型 identity 复用现有 provider identity，启动冻结 snapshot，
   不静默降级或任意热切换。
7. **Attempt semantics**：keyed attempt ledger 在实际调用前原子占位；同一
   target/cycle/claim 最多一个 in-flight attempt；late result 不重新消耗 attempt，
   但可按原 provenance 进入 P2，并不得满足已过期 admission 或追溯授权 effect。
8. **Effect timing**：effect-critical required claim 由 Control 在 bounded budget 内
   等待；timeout/failure 保持 `Unknown`/`Conflicted` 并 fail closed；
   non-effect-critical workflow 可以继续。
9. **Required claim**：内部 `RequiredClaim` 只表达 `Subject + Field`，不携带
   expected value、confidence、authority、reliability 或 model hint。
10. **Context projection**：Evidence Context 使用 typed internal records，复用
    现有 repository types；`ConflictBasisBundle` 原子保留，无法整体保留时为
    `ContextInsufficient`；`ExclusionDisposition` 只在当前 capture/reasoning scope
    生效，不删除 canonical evidence。
11. **Correlation**：一个 `AttemptKey` 只允许一次 semantic invocation；transport
    retry 只能发生在该 invocation 内。terminal AttemptKey 不得语义重试；新的
    observation cycle 必须生成新的 AttemptKey；RequestId 与 EvidenceId 独立。

## Acceptance

1. Internal request/result seam 不扩展 `ObservationNeed`、P2/P3 records 或
   `AgentDecisionContext`；`ObservationDepth.Slow` 仅为 route hint。
2. Request 能表达 logical profile、RequiredClaim(Subject+Field)、BuyerRef、Reason、
   AttemptKey、Capture/Fusion provenance、RawArtifactRef、candidate/evidence refs 和
   bounded budget；ArtifactId/CaptureId 不混用。
3. Text context 只由 Product-normalized/admitted evidence 组成；Visual context 以
   RawArtifact 为主；whole-ledger/world、raw provider payload、tensor/DTO 均被拒绝。
4. Context composition 对 RequiredClaim/Reason 有界；冲突/歧义 basis 不可完整保留
   时输出 `ContextInsufficient` 且零 proposal；excluded disposition 可追溯但不删证据。
5. SlowResult status 与 semantic disposition 分离；失败/取消/超时/不可用/坏 schema
   零 proposal；partial 只接收 provenance 完整的逐条 proposal；成功空结果不产生
   absence claim。
6. Model Management 返回已冻结、可审计的 binding snapshot；未知 profile、缺模型、
   health/schema/identity 不匹配 → `ROUTING_UNAVAILABLE`，禁止 silent fallback。
7. Keyed attempt ledger 在调用前原子占位，阻止同一 target/cycle/claim 重复 in-flight；
   late result 可入 P2，但不能满足 expired attempt 或 retroactive effect authorization。
8. Replay realization 覆盖：Fast→Focused→Slow 顺序、effect-critical bounded wait、
   non-effect-critical continue、timeout/failure、cancel、late/duplicate/out-of-order、
   P2/Evidence/WorldModel ingress 和 Effect Gate non-invocation。
9. Two logical profiles remain `experimental`; no acceptance claim treats either real
   model as production-ready. Real Text/Vision provider slices require separate paired
   claim-domain evaluation and availability evidence.
10. RequiredClaim/Context/ConflictBasis/Exclusion/AttemptKey 的 typed internal record
    语义与边界测试通过；transport retry 不产生第二 semantic invocation。

## Assumptions

- Existing `FusionCapture`, `RawArtifact`, `ObservationProposal`, `EvidenceRecord` and
  provider identity are sufficient carriers; implementation may add internal records but
  must not invent a second authority or provenance path.
- Focused crop/coordinate-remap is a prerequisite dependency, not silently substituted by
  subject filtering.

## Alternatives rejected

- Slow as model-only escalation: rejected; Slow is a first-class capability.
- Agent/DSH direct Slow invocation: rejected; Control owns authorization.
- Full-ledger context or provider-private payload: rejected; bounded Product-normalized
  evidence is required.
- Reusing `.dsh/model-bindings.yaml` in Product: rejected; Product/Development Harness
  boundary remains separate.
- Silent model fallback or runtime hot switch: rejected; fail closed with explicit routing
  status.

## Owner / Authority impact

```text
Control                  authorization + bounded wait
Perception orchestration request/profile/context/attempt coordination
Model Management         binding/availability/identity snapshot
Slow adapter             provider execution
Evidence Ledger (P2)     sole evidence admission
WorldModel                sole belief/reconciliation
Effect Gate               consumes current view; never invokes/waits Slow
```

## ADR refs

- `docs/adr/0030-slow-perception-first-class-capability-bounded-async-consumption.md`
- `docs/adr/0020-live-perception-determinism-anchor-is-response-json.md`
- `docs/adr/0021-perception-provider-black-box-variants-selected-not-carried.md`
- `changes/PER-011/spec.md`

## Residual risks / Human Gates

- Exact internal record field types and context serialization remain implementation-grill
  work is now constrained by Q46–Q50; no public schema is frozen by this state.
- Real provider capability, model availability and claim-domain reliability are unverified.
- Focused crop prerequisite and target/cycle enforcement need deterministic tests before
  any production Slow provider is enabled.

## Constraints

- Product Runtime / Development Harness remain separate.
- No Product code changes in this persistence step.
- Shared `model-routing.yaml` remains provider/model-free; concrete bindings stay in
  realization adapters.

## Verification

```yaml
level: CONTRACT
method: repository evidence audit + decision alignment + exact-path diff check
expected: Q6–Q45 decisions represented without changing Product runtime, P2/P3 authority,
          PER-009/010/011 frozen semantics, or DSH/Product boundary
actual: Internal contracts and replay/orchestration seams are implemented without
public authority protocol expansion; focused/kernel tests pass 688/688,
Simulation tests pass 184/184, full solution builds with 0 errors, certification
check passes 29/29, and git diff --check is clean.
evidence: this state.md; docs/adr/0030; CONTEXT.md; PER-009/011; provider baseline
```

## Status log

- 2026-09-27 · understanding→resolved · Owner grill Q1–Q45 closed; Slow capability,
  model-management boundary, context protocol, attempt semantics and effect timing aligned.
- 2026-09-27 · resolved→persisted · first implementation slice persisted as decision-heavy
  Change State; Product runtime untouched; implementation and real-provider gates remain open.
- 2026-09-27 · persisted→planned · Q46–Q50 closed；typed context、atomic conflict basis、
  exclusion scope、AttemptKey semantic invocation 与 transport retry boundary 已进入计划。
- 2026-09-27 · planned→implemented · Added internal Slow contracts, bounded context
  builder, keyed ephemeral attempt ledger, deterministic text/visual replay,
  model-binding resolution, bounded orchestration and P2-only projection. Kernel
  tests 688/688, Simulation tests 184/184, scenario certification 29/29.
