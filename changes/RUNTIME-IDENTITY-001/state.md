# RUNTIME-IDENTITY-001 — TaskInstance / ProductSession 最小持久化存储
lifecycle_state: implemented · disposition: none · depth: standard · base: f3aeb6f

## Intent（WHAT/WHY）

ADR-0044/0045/0046 已冻结身份架构，但 Runtime 侧没有独立铸造和持久化
TaskInstance / ProductSession 的存储——当前 RuntimeRunStore 把所有东西混在
内存里，HTTP 一个请求全建。本 change 建最小 append-only 文件存储，为后续
分阶段 HTTP 和 UniAgent 契约生成提供上游 identity 来源。

## Scope / Out of Scope

In：`src/UniClaw.Host/Runtime/TaskInstanceStore.cs`（新）、
`src/UniClaw.Host/Runtime/ProductSessionStore.cs`（新）、单元测试。
Out：HTTP 接口拆分（后续 change）；UniAgent 契约路径接线（后续 change）；
ExecutionContract 加字段（后续 change）；Workbench instanceId 统一（后续 change）。

## Decisions

- D1：append-only JSON 行文件（`FileExecutionJournal` 先例，零外部依赖）。
- D2：幂等——同 idempotencyKey（TaskInstance）或同 taskInstanceId
  （ProductSession）重入返回已有实例，不产生第二条记录。
- D3：1:1 基数（ADR-0045 当前范围）；BindingRun 为可选追加字段。
- D4：存储路径由构造注入（测试用临时目录，产品用 `runs/<runid>/` 同级）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | Create 幂等：同 key 重入返回同一 id、不追加第二行 |
| A2 | 重启恢复：Dispose 后重建，从文件恢复全部实例 |
| A3 | 1:1 强制：同 taskInstance 的第二次 CreateSession 返回已有 session |
| A4 | BindRun 可选且幂等 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet build + dotnet test --filter TaskInstanceStore|ProductSessionStore"
  expected: "编译零错；A1-A4 测试全绿"
  actual: "待填"
  evidence: "待填"
```

## Status log

- 2026-10-12 · UNDERSTAND → RESOLVE → PERSIST → IMPLEMENT · Owner 批准直接干；
  架构裁决已齐（ADR-0043/0044/0045/0046），无需 grill；代码落地。
