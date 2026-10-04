# CAP-005 — Task-scoped capability injection seam
lifecycle_state: closed · disposition: none · depth: standard · base: f3953033

## Intent（WHAT/WHY）
补足 Capability Hub 的第二种装配形态：部分能力不是进程级常驻实例，而是在任务
下发时按任务上下文创建、绑定和释放。当前 Registry 能注册和解析全局实例，但如果
直接把任务专属实例放入全局 Registry，可能造成跨任务状态泄漏和错误复用。

## Scope
- 明确 Descriptor registration、task dispatch 和 task-scoped instance binding 的分层。
- 设计任务注入所需的最小 context、correlation、生命周期、取消和 teardown 语义。
- 确定常驻能力与任务注入能力在 Hub/Composition Root 中的职责分配。

## Out of Scope
- 本 Change 暂不实现具体 task-scoped API。
- 不实现 DSH Tool/Skill projection 或新的真实能力。
- 不改变现有 `CapabilityRegistry.Resolve()` 的全局解析语义。

## Current finding
- `CapabilityScope` / `TrustDomain` 表达注册域，不表达实例生命周期。
- `CapabilityRegistry.Resolve()` 返回注册时保存的实例，不能直接承担任务级隔离。
- 仿真能力当前是测试夹具实例，不能作为任务注入接口的证据。

## Acceptance
1. 文档明确能力注册域和实例注入生命周期是两个独立维度。
2. 任务专属实例不会被描述为全局 singleton。
3. 后续实现可以在不改变 Descriptor trust-domain 语义的前提下增加 task binding seam。

## Verification
```yaml
level: CONTRACT
method: >-
  inspect Capability ownership/index and task-injection documentation;
  python3 -m unittest -v evidence/cap-005/task_binding_model.py;
  dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore
  --filter 'FullyQualifiedName~DocsMetadataTests'; git diff --check
expected: >-
  task-scoped capability is represented as a future Host binding seam, not a
  global Registry instance; two-task isolation, cancellation, rollback,
  correlation rejection and late-result handling are executable design cases.
actual: >-
  Design document separates registry domain from instance lifecycle and assigns
  binding creation/teardown to Host/Composition Root. The model passes 9/9;
  DocsMetadataTests pass 4/4; diff check passes. No production task-binding
  API or runtime behavior was changed.
evidence: >-
  docs/design/task-scoped-capability-binding-v0.1.md;
  evidence/cap-005/task_binding_model.py; command output from the above checks.
```

## Status log
- 2026-10-04 · UNDERSTAND → RESOLVE · 用户指出部分能力只在任务下发时注入。
- 2026-10-04 · RESOLVE → PERSIST · 记录全局注册与任务级实例绑定的职责差异，暂不冻结具体 API。
- 2026-10-04 · PERSIST → PLAN · 固定两个真实买方（Language Inspector、Operation Measurement）、Host owner、Runtime identity、局部取消与 teardown 验收；生产接口暂缓。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY → CLOSED · 完成任务级注入设计文档和 9 个可执行隔离/收尾模型场景；未改 Registry.Resolve()、Runtime 主循环或 DSH Tool/Skill。
