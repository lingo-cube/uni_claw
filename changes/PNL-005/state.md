# PNL-005 — Runtime Run 状态、事件与查询投影

lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

把 PNL-004 已验证的真实 Runtime/DSH/Kernel 执行链工程化为稳定的查询边界：Runtime Host 负责 Run 生命周期和事件真相，Workbench 通过版本化业务接口读取状态、事件和产物引用。目标是进程重启、执行膨胀和来源异构时仍保持边界清楚、结果可解释、前端不依赖目录结构。

## Scope

- Runtime Host-owned RunStore 的状态快照与不可变事件记录。
- `RuntimeRun`、`TaskInstance`、`ProductSession`、`DshHostSessionRef` 的领域关系和查询投影。
- Run 创建、列表/状态查询、事件分页和产物引用查询契约。
- `starting → running → completed|failed|interrupted` 生命周期。
- `source`、`authority`、`schema`、`runId`、`productSessionId`、`observedAt`、`revision` 和 cursor 语义。
- 统一 HTTP 状态码与业务对象响应。

## Out of Scope

- 恢复/继续执行接口和恢复策略。
- 认证、远程 Host、多租户权限。
- OTel exporter 或把 DSH event 转换成 OTel Span。
- Workbench 新的视觉设计；本 Change 只提供稳定 projection。
- Host 侧 Project/Test Set 编辑、归档和权限管理。

## Decisions

1. Runtime Host 是 Run lifecycle 的唯一 authority；Workbench 只保存 Task Instance 关联并投影 Runtime 状态，DSH 只负责 Host Session/Agent realization。
2. `TaskDefinition → TaskInstance → Primary RuntimeRun → ProductSession + DshHostSessionRef` 是显式关系；身份不能从文件名、目录名或 DSH Session 推导。
3. RunStore 同时保存 append-only 事件和当前快照。快照服务查询，事件服务审计；状态不能反向覆盖事件。
4. 可见生命周期固定为 `starting`、`running`、`completed`、`failed`、`interrupted`。没有终态证据时显示“已中断”，内部 outcome 可为 `unknown`。
5. Runtime HTTP 使用 `POST /runs`、`GET /runs`、`GET /runs/{runId}`、`GET /runs/{runId}/events`；恢复接口本 Change 不实现。
6. HTTP 状态码表达协议层结果，响应体表达业务对象；错误使用统一的业务错误对象，不返回裸字符串。
7. 查询结果默认摘要和逻辑 `detailEndpoint`，事件按 cursor 分页；大 JSON、trace、evidence 和 journal 不直接塞入默认响应。
8. Projection 允许最终一致，但必须返回 `revision`、`observedAt`、`lastEventSequence` 和 `consistency`，不能静默声称 current。
9. DSH、UniClaw Runtime、Kernel 等来源保留 source-native 结构，只共享包络字段，不伪造 parent、span 或 duration。

## Assumptions

- PNL-004 的 Runtime Host 仍是本地 Host 组合根，RunStore 第一版可使用 Host-owned 本地文件存储。
- `runId`、`productSessionId` 和 Host session 映射由 Runtime/Host 实际创建流程提供。
- 前端会通过现有 Query capability 和新增 Runtime adapter 消费 projection。

## Alternatives rejected

- 让 Workbench 直接读取 `runs/`：会把物理目录变成共享契约，并复制 Runtime truth。
- 用 DSH Session 作为 Run 主键：混淆 Host realization 与 Product execution。
- 只保存快照：无法审计状态变化，也无法解释中断前最后一次观察。
- 把所有事件归一成 OTel Span：丢失 DSH/Kernel source-native 语义。

## Owner / Authority impact

- Runtime Host：Run identity、lifecycle、event sequence、terminal outcome 和 artifact association。
- Kernel：执行语义、证据和效果判断；不直接管理 Workbench projection。
- DSH Host：Agent realization、DSH session 和 DSH-native event。
- Workbench：Task Instance 关联和只读 projection，不升级来源 authority。

## Acceptance

1. 合法创建请求返回 `202` 和完整 Runtime Run identity；不生成本地 fallback identity。
2. Run 查询能返回 `starting → running → completed|failed|interrupted` 和一致性元数据。
3. Run 列表支持状态/产品会话过滤和 opaque cursor；事件查询支持 source filter、cursor 分页和摘要/明细分离。
4. Runtime/DSH/Kernel/Device 真实执行可以形成一个可查询 Run Projection。
5. facts、trace、evidence、journal 只通过逻辑 detail reference 暴露，绝不泄漏绝对路径。
6. Workbench 不读取本地目录，且大 payload 不进入默认列表响应。
7. schema、HTTP status、业务错误对象和 revision/cursor 均有契约测试。
8. `interrupted`、`failed`、`completed` 的显示和原因可由真实证据区分。

## Verification

```yaml
level: CONTRACT
method: python3 tools/validate-workspace-schemas.py; Runtime Host contract tests; real 3083→3090→emulator scenario
expected: schema and business responses are valid; state/event/projection behavior matches decisions
actual: RuntimeRunStore and HTTP query surface implemented; contract/unit/smoke checks pass; direct Runtime and Ego Lite Workspace launches both completed against DSH `3083` and `emulator-5556`, with Runtime `3090` projections and DSH conversation/trace evidence
evidence: evidence/pnl-005/runtime-run-store.md
```

## Status log

- 2026-10-04 · UNDERSTAND → RESOLVE · 完成 Runtime authority、状态、事件、projection、HTTP 业务对象和边界 grill。
- 2026-10-04 · RESOLVE → PERSIST · 冻结领域关系、RunStore、查询接口、分页、一致性和残余边界，进入 schema/plan 阶段。
- 2026-10-04 · PERSIST → IMPLEMENT · 提交 schema、ADR、实施计划和正负样例；校验器通过 11 个 schema。
- 2026-10-04 · IMPLEMENT · RuntimeRunStore 完成文件 adapter、append-only event、snapshot、幂等查找、Run 列表过滤和 opaque cursor；Workbench 增加 host-neutral Runtime HTTP adapter，页面不依赖 DSH 或 runs 目录。
- 2026-10-04 · IMPLEMENT → VERIFY（局部） · schema、RunStore、Host build、Workspace adapter tests 和 list HTTP smoke 通过；真实 DSH/ADB 环境当前未启动，端到端证据待补。
- 2026-10-04 · IMPLEMENT → VERIFY（局部） · HTTP smoke 通过 health、404 business error、400 business error；真实 DSH/ADB 环境当前未启动，端到端证据待补。
- 2026-10-04 · VERIFY · 真实 `emulator-5556`、DSH `3083`、Runtime `3090` 链路通过；直接 POST 和 Workspace 页面发起均得到 Runtime `202 → completed`，事件为 accepted/started/completed，页面可读 Uni-Agent 请求→决策→结果和 22 条 DSH trace。
- 2026-10-04 · VERIFY · 修复 DSH launch 对 Runtime 中性 `hostSessionRef` 的读取，避免 Runtime 已绑定 session 时重复创建 DSH session；新增回归测试通过 66/66。
