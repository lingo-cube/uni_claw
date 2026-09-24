# AGT-002 — DeepSeek Harness Realization

## Intent

实现 AGT-001 冻结的 DSH Product UniAgent realization：`.NET` 产品协议记录是唯一
authority，DSH 只能通过版本化 sidecar seam 提交一个结构化 `AgentDecision`。

## Scope

- Slice A：协议元数据、由 `.NET` records 生成的 JSON Schema、schema hash、capability
  manifest、adapter handshake、fake/replay provider，以及 Act/Policy/NoAction/Defer
  roundtrip。
- Slice B：local stdio sidecar skeleton、Product Session 与 DSH Session 显式映射、
  一次 consultation 对应一个 turn、`submit_decision` 唯一正式输出面、DecisionId
  校验、transport generation/request-id、AbortCurrentTurn、timeout、late/stale/
  duplicate response 丢弃，以及每个 Run 至多一个 active consultation。
- Slice C：Luna 机械测试、fixtures、文档同步；由 Sol review 后才算完成。

## Out of scope

- 修改 AGT-001、RUN-005、Product baseline 或任何 authority/lifecycle 语义。
- DSH shell/filesystem/browser/device/workflow/subagent/ask-user/approval tool。
- 新增 Product owner、durable consultation journal、跨进程 exactly-once、Agent
  continuation、Policy vocabulary 或 effect tool。
- 真实 provider 权限扩大；DeepSeek Flash E2E 仅在协议闭合后进行。

## Acceptance

1. `AgentDecisionContext`、`AgentDecision`、`Policy*` 等现有 `.NET` records 作为唯一
   schema 输入；生成 JSON Schema、TypeScript 检查类型和 submit_decision schema 不得
   另有手写 union/source。
2. handshake 在 protocolVersion、schemaVersion、schemaHash 或 capability 不匹配
   时拒绝启动；产品 manifest 只允许 `submit_decision`。
3. Act/Policy/NoAction/Defer 可经 JSON roundtrip，错误 correlation fail closed。
4. 一个 Product Run 同时只能有一个 active consultation；每次 consultation 只启一
   个 turn，transport retry 不形成新的 semantic consultation。
5. timeout/abort 后的 late response、stale generation response、duplicate response
   均只记录 diagnostic，不回流 Kernel、不生成 effect、不消耗第二次 consultation。
6. fake/replay provider 能在不启动 DSH 的情况下覆盖四种 decision，并与真实 adapter
   使用同一 Product seam。
7. 全量 .NET 测试通过；实现不得触及 AGT-001、RUN-005、Product baseline。

## Frozen decisions

- Kernel decides WHEN; UniAgent/DSH decides WHAT。
- DecisionId 是唯一 Product semantic correlation；request-id/generation/turn-id
  只属于 realization transport。
- `AbortCurrentTurn` 只是机械中断，不拥有 Product cancel/preemption/lifecycle
  authority。
- DSH sidecar headless-only，正式输出面只有 `submit_decision`。
