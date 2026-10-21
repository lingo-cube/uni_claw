# Protocol Map（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml`（PROTOCOL-MACHINE-001 单源）。
> 本文件是生成产物，手改会被 `--check` 漂移检测抓出；协议语义权威是
> COMPONENT-BASELINE 文档 `inter-component-protocol-baseline-l1-l3.md`，
> 本视图不引入边表之外的协议事实。

## 机器概览（5 台）

| 机 | initial | 节点数 | 转移数 | 终态 | 悬挂白名单 |
|---|---|---|---|---|---|
| activation-record | reserved | 4 | 3 | blocked, failed, finalized | ∅ |
| attempt | committed-pending | 5 | 4 | completed, failed, unknown-outcome | ∅ |
| binding | candidate | 5 | 4 | consumed, derived-invalid, rejected | ∅ |
| consultation-journal | pending | 3 | 4 | completed, rejected | ∅ |
| contract-view | admitted | 3 | 2 | frozen | ∅ |
| judgment | pending | 3 | 2 | consumed | ∅ |
| run | created | 10 | 18 | closed, terminal | ∅ |

## 协议边表（45 条；P## 协议边 / O## wire·宿主边 / G## known gap）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G1 | gap | track | control-loop → capability-plane | — | 观察致动（无编号边 · 审计 D5；交互模型步骤 3） |
| G2 | gap | track | user-session → uni-kernel | — | 任务定义（交互模型前奏 0a：Task Catalog，无编号边） |
| G3 | gap | track | uni-kernel → uni-kernel | — | Task Instance 创建（交互模型前奏 0c：Runtime 铸造，无独立协议边） |
| G4 | gap | track | uni-kernel → uni-kernel | — | Product Session 分配（交互模型前奏 0d：Runtime 铸造 + 显式 Binding，无编号边） |
| G5 | gap | track | uni-agent → run-model | — | 契约 Authoring（交互模型前奏 0e：UniAgent 形成，P1 上游，无编号边） |
| O1 | edge | track | user-session → uni-kernel | schema v1 | O1 · task-launch-request（调用方 → Runtime HTTP；交互模型前奏 0b） |
| O2 | edge | track | uni-kernel → user-session | schema v1 | O2 · task-launch-ack（Runtime 铸造；交互模型前奏 0b/0c） |
| O3 | edge | track | uni-kernel → user-session | schema v1 | O3 · launch-stage-record（阶段记录：valueOrigin + availability 分离） |
| O4 | edge | keep | uni-kernel → user-session | schema v1 | O4 · runtime-run-event（source-native 事件流：DSH / Kernel / 设备 / Runtime） |
| O5 | edge | keep | uni-kernel → user-session | schema v1 | O5 · runtime-run-projection（Runtime Run 查询投影，唯一 authority：Runtime Host） |
| O6 | edge | keep | uni-kernel → user-session | schema v1 | O6 · runtime-run-response（单 Run 查询响应） |
| O7 | edge | keep | uni-kernel → user-session | schema v1 | O7 · runtime-run-events-response（事件列表响应，nextCursor 分页） |
| O8 | edge | keep | uni-kernel → user-session | schema v1 | O8 · runtime-run-list-response（Run 列表摘要投影） |
| O9 | edge | keep | uni-kernel → user-session | schema v1 | O9 · run-report（全链路单 run 报告只读投影，PNL-006） |
| O10 | edge | keep | user-session → uni-kernel | schema v1 | O10 · capability（五类只读 capability 请求/响应公共封套） |
| O11 | edge | keep | uni-kernel → user-session | schema v1 | O11 · query-error（结构化查询错误，不返回裸字符串） |
| O12 | edge | keep | uni-kernel → user-session | schema v1 | O12 · record-envelope（跨来源记录公共封套） |
| O13 | edge | keep | uni-kernel → user-session | schema v1 | O13 · local-storage-namespace（不透明逻辑 namespace 引用） |
| P1 | edge | keep | uni-agent → run-model | record UniClaw.Kernel.Run.ExecutionContract | ### P1 Execution Contract Admission（UniAgent → Run Model） |
| P2 | edge | keep | capability-plane → evidence-ledger | record UniClaw.Kernel.Evidence.ObservationProposal | ### P2 Observation Ingress（观察生产者 → Evidence Ledger） |
| P3 | edge | keep | evidence-ledger → world-model | record UniClaw.Kernel.Evidence.EvidenceRecord | ### P3 Accepted Evidence Record（Evidence Ledger → World Model） |
| P4 | edge | keep | world-model → control-loop | record UniClaw.Kernel.World.Slice | ### P4 Slice（World Model → Control Loop） |
| P5 | edge | track | run-model → control-loop | record UniClaw.Kernel.Run.RunState | ### P5 Run Snapshot（Run Model → Control Loop） |
| P6 | edge | keep | run-model → control-loop | record UniClaw.Kernel.Run.ExecutionContractView | ### P6 Execution Contract View（Run Model → Control Loop / Assurance） |
| P7 | edge | keep | run-model → assurance | record UniClaw.Kernel.Run.ProofObligationState | ### P7 Obligation View（Run Model → Assurance） |
| P8 | edge | keep | control-loop → assurance | record UniClaw.Kernel.Control.ControlIntent | ### P8 Control Intent（Control Loop → Assurance / Effect Boundary） |
| P9 | edge | keep | capability-plane → effect-boundary | record UniClaw.Kernel.Effects.CandidateBinding | ### P9 Candidate Binding（Grounding Provider → Effect Boundary） |
| P10 | edge | keep | effect-boundary → assurance | record UniClaw.Kernel.Effects.CanonicalBinding | ### P10 Canonical Binding（Effect Boundary → Assurance） |
| P11 | edge | keep | world-model → assurance | record UniClaw.Kernel.World.ActionAssuranceView | ### P11 Current WorldBelief View（World Model → Assurance / Effect Boundary） |
| P12 | edge | keep | evidence-ledger → assurance | record UniClaw.Kernel.Evidence.EvidenceRecord | ### P12 Canonical Records Lookup（Evidence Ledger → Assurance） |
| P13 | edge | keep | assurance → effect-boundary | record UniClaw.Kernel.Assurance.AssuranceJudgment | ### P13 Assurance Judgment（Assurance → Effect Boundary） |
| P14 | edge | keep | effect-boundary → capability-plane | record UniClaw.Kernel.Effects.DispatchRequest | ### P14 Dispatch Request（Effect Boundary → Capability Plane） |
| P15 | edge | keep | capability-plane → effect-boundary | record UniClaw.Kernel.Effects.DispatchResult | ### P15 Dispatch Result（Capability Plane → Effect Boundary） |
| P16 | edge | keep | assurance → run-model | record UniClaw.Kernel.Assurance.OutcomeProof | ### P16 Outcome Proof（Assurance → Run Model） |
| P17 | edge | keep | run-model → uni-kernel | record UniClaw.Kernel.Run.OutcomeState | ### P17 Terminal Outcome State（Run Model → Uni Kernel） |
| P18 | edge | keep | uni-kernel → uni-agent | record UniClaw.Kernel.Outcome.RuntimeOutcome | ### P18 Runtime Outcome（Uni Kernel → UniAgent） |
| P19 | edge | track | uni-agent → user-session | — | ### P19 Goal Evaluation（UniAgent → user/session，未来） |
| P20 | edge | track | uni-agent → memory-system | — | ### P20 Memory Persist（placeholder，→ Memory System） |
| P21 | edge | track | memory-system → control-loop | — | ### P21 Memory Recall（placeholder，Memory System → Agent / Control / World Model） |
| P22 | edge | keep | effect-boundary → world-model | record UniClaw.Kernel.World.TransitionContext | ### P22 Runtime Transition Context（Runtime Effect Flow → World Model） |
| P23 | edge | keep | effect-boundary → world-model | record UniClaw.Kernel.World.UiRealization.CurrentGroundingView | ### P23 Continuity Request Seam（Effect / Obligation path ↔ World Model） |
| P24 | edge | keep | uni-agent → uni-kernel | record UniClaw.Kernel.Runtime.ActivationResult | #### P24 Legal Run Activation（Runtime activation path → Uni Kernel internal run driver） |
| P25 | edge | keep | uni-kernel → uni-agent | record UniClaw.Kernel.Runtime.AgentDecision | #### P25 Agent Decision Consultation（Uni Kernel internal driver → UniAgent realization，外部 seam） |
| P26 | edge | keep | uni-kernel → simulation-host | record UniClaw.Kernel.Trace.RunTraceArtifact | #### P26 Sealed Trace → ScenarioStimulus（测试 / 治理侧，非产品内边） |
| P27 | edge | keep | world-model → control-loop | record UniClaw.Kernel.World.ObservationNeed | ### P27 Observation Need（World Model → Control Loop） |
