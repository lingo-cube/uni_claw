# AGT-003 — Task-scoped DSH session reuse and decision correlation
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

让一个 UniClaw Product task 在同一个 Product Run 内保持一个稳定的 DSH
session，并让每一轮咨询的 `DecisionId` 明确绑定到该 session 上的当前
consultation。当前握手会返回 `DshSessionId`，但后续 consult 没有把这个
realization-private identity 带入 wire contract；同时服务端只校验
`ProductSessionId`，没有完整校验 Product Run 映射。这使多轮真实 Agent
咨询可能落入错误或不连续的 DSH context，并表现为 `decision-id-mismatch`。

## Scope

- DSH HTTP peer 在握手后保存并在每次 consult 显式携带 `dshSessionId`。
- DSH plugin 对显式 session、Product Session、Product Run 三者做映射校验。
- 保持 task/run 维度 session 复用；不为每次 consultation 创建新 DSH session。
- 补齐 `DecisionId` 当前轮次的诊断信息与 wire-level / plugin 回归测试。
- 保持 Product seam、Kernel authority、跨重启恢复语义不变。

## Out of Scope

- 跨进程或跨重启恢复 DSH session/transcript。
- 修改 `DecisionId` 的 Product 语义或新增 ConsultationId。
- 用确定性咨询替代真实 DSH Agent。
- Settings-specific coordinate shortcut 或 effect authority 迁移。

## Decisions

1. `ProductSessionId` 是任务维度；`ProductRunId` 是一次执行；`DshSessionId`
   是 realization-private 的物理会话；`DecisionId` 是每轮咨询的 Product
   semantic correlation，四者不互相替代。
2. DSH session 在握手创建一次，并在同一 attachment 的所有 consultation
   中复用；consult 必须显式携带该 session identity，服务端必须拒绝错配。
3. 每个返回的 AgentDecision 必须精确回带当前 context 的 `DecisionId`；
   mismatch fail closed，并保留 expected/received 诊断。
4. detach 只释放 attachment mapping，不承担 Product Run 终止或跨重启恢复。

## Acceptance

1. HTTP peer 的 handshake → 两次 consult wire body 包含同一个非空
   `dshSessionId`；没有 handshake 的直接 peer fixture 仍 fail closed/保持测试语义。
2. plugin 拒绝错误的 `dshSessionId`、`productSessionId` 或 `productRunId`，并且
   不调用 model prompt；正确映射允许多轮 consult 使用同一 DSH session。
3. deterministic Agent.Dsh tests cover session reuse, mapping mismatch, and
   DecisionId correlation diagnostics; existing suites remain green.
4. Real DSH retry uses one task-scoped session and produces a trace that records
   the session identity per consultation; model capacity failures remain explicit.
5. Host configuration names the DSH workspace project and Product/Slow
   conversations through the real Workspace and Session-title services.
6. Agent-produced act plan, policy, noAction and defer outputs are schema-valid,
   bounded, DecisionId-correlated, and traceable to Product Session/Run through
   the consultation response diagnostics; invalid bounds fail closed.
7. No Product authority, Kernel lifecycle, or cross-restart semantics are changed.

## Constraints

- AGT-001/AGT-002 frozen Product seam remains authoritative.
- Preserve unrelated dirty files and existing PER-017/PER-018 work.
- No silent model fallback; retry only at the configured realization boundary.

## Verification

```yaml
level: ENVIRONMENT
method: focused .NET and DSH plugin tests; full solution build/test; authenticated dedicated-port real DSH multi-turn retry; live Android traversal when environment is available
expected: explicit session mapping is preserved, wrong mappings fail closed, and real multi-turn consultation no longer loses the task session
  actual: "DSH plugin tests 19/19; solution tests 1104/1104 (Agent.Dsh 131, Kernel 697, Host 52, Simulation 184, Core 14, Agent 17, FileSystem 9); solution build 0 errors; model binding 4/4; scenario certification 29/29. Dedicated authenticated live E2E on port 3088 used zai-coding-cn/glm-5.3-flash, one task-scoped session, three consultations, two real taps with DeliveryCompleted receipts, and ended Completed/terminal-emitted with Wi-Fi checked. The plugin rejected an invalid noAction completion payload and the model corrected it in the same physical turn. The owner service on 3080 was untouched; the dedicated 3088 service was stopped after verification."
  evidence: evidence/real-settings-traversal-20260930/; evidence/real-settings-traversal-20260929/; dsh/uniclaw-decision-channel/tests/plugin.test.mjs; tests/UniClaw.Agent.Dsh.Tests/ProtocolFoundationTests.cs; tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerTests.cs; tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerE2eTests.cs; tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs; tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverPolicyValidationTests.cs
```

## Status log

- 2026-09-28 · understanding→resolved · Confirmed `DecisionId` is per-consultation Product correlation and identified missing explicit DSH session propagation plus incomplete Product Run mapping validation.
- 2026-09-28 · resolved→persisted · Persisted implementation and verification boundary as AGT-003.
- 2026-09-29 · persisted→implemented · Added explicit DSH session propagation and Product Run/session mapping checks; serialized physical turns before reuse; added wire/plugin regression coverage.
- 2026-09-29 · implemented · Real DSH + API 35 traversal confirmed one task session over four consultations and two completed effects; no session/correlation transport failure remained.
- 2026-09-29 · implemented · DSH host configuration now names the persistent workspace project and task-scoped conversation through `workspaceRegistry` and `sessionController.rename`; Slow sessions receive a separate configured title.
- 2026-09-29 · implemented · Added output acceptance coverage for act plans, policy rules, noAction and defer: generated bounds are in the shared schema, invalid bounds fail closed, and successful responses expose Product Session/Run, DecisionId, generation and schema hash for traceability.
- 2026-09-29 · implemented→hold · Final device hierarchy showed Wi-Fi checked, but Kernel terminal proof remained evidence-insufficient; retained the exact live evidence and did not widen Product authority or claim traversal PASS.
- 2026-09-29 · hold→implemented · Added native resource-id checked-state association with existing-evidence fallback, projected typed `ElementSummary.state`, and made completion-anchor validation corrective within the same physical turn. The dedicated 3088 run now emits `Completion` after two delivery-completed effects.
- 2026-09-30 · implemented→reviewed→verified→closed · Re-ran solution tests and build, explicitly re-certified all 29 scenarios under AGT-003, verified the configured model/session/workspace behavior, and recorded durable live evidence plus a human-readable report.

## Gate disposition

AGT-003 的 DSH 会话复用、模型容量重试、配置化项目/对话命名和输出边界校验已完成；真实设备上也验证了一条两步导航与最终 Wi-Fi typed 状态判断。Host closure 测试、solution 级验证和 29 个场景的显式认证均通过；本 change 的会话/关联验收证据齐备，进入 CLOSED。该窄范围结果不等于原始 Real Android Settings Traversal 任务书的完整 PASS：本次 Fast 不可用、Focused/Slow 未触发、导航 step-level post-action verification 未通过。完整 E2E 仍需独立复测与 Final Gate 裁决，见 `evidence/real-settings-traversal-20260930/README.md`。
