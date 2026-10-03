# PNL-004 — Observability Contract and Task Launch/Query Storage Seam

lifecycle_state: implemented · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

建立 UniClaw Workspace 下一阶段的可扩展只读数据接缝：通过一个明确的 Task Launch Seam 发起任务并建立 Project/Test Set/Task、Runtime Run/Product Session、Host Session 和本地 storage namespace 的关联；通过 Host-neutral Query capability 查询 Session、Conversation、Trace、Evidence、Metadata 和执行结果。

当前不建设完整的 Project/Test Set 管理系统。代码库中的 `testsets/` 目录只作为本地临时资产来源；文件夹和 JSON 是第一种 storage adapter，不进入共享前端契约。

## Scope

- `default` 测试集版本和可选 `sourceRevision` 的读取语义。
- Task Launch Seam 的带元信息请求、分阶段运行关系写入与 `runId`/Session 显式绑定。
- 本地 filesystem storage adapter 与 Host-neutral Query Core 的关联读取。
- Project/Test Set/Task/Instance/Run/Session/Trace/Evidence/Metadata 的逻辑引用和局部失败状态。
- Observation envelope 的 source、authority、availability、valueOrigin、snapshot/observedAt 和 detail reference。

## Out of Scope

- Host 侧 Project/Test Set 正式持久化、编辑、归档和权限管理。
- Workspace 自有长期缓存、归档、保留或第二份 Product truth。
- 从目录名、artifact 文件名或 DSH Session 推导 Product Session/Primary Run。
- OTel 采集、完整 exporter、实时 websocket 和前端任务编辑器。

## Decisions

1. Host/Task Catalog 是 Project/Test Set 的长期 owner；当前 Host 能力缺失时，代码库 `testsets/` 提供声明式资产和 fixture 引用，统一对外使用 `default` 版本。
2. Task Launch Seam 是建立运行关系的唯一入口：它引用测试集资产，从对应产品 authority 取得 Product Session，并从 UniClaw Runtime 取得 canonical `runId`，绑定 primary Host Session，并创建或确保本地 storage namespace。
3. Query capability 只消费逻辑引用并读取 storage adapter；共享前端不直接访问目录或文件，未来可替换为 Host/远程 adapter。
4. 测试集资产是输入和查询引用来源，不是 Runtime Outcome、完成状态或 Evidence authority。
5. `availability` 与 `valueOrigin` 分开表达：前者描述有没有数据，后者区分 `configured`（配置/manifest）、`generated`（创建 authority 生成）、`observed`（设备/Runtime/Host 实测）和 `derived`（由已有关联记录计算）。缺失、未采集、未关联、不可用和权限拒绝不能折叠为空值。
6. UniClaw Trace、DSH Trace 和其他来源保留 source-native 结构，共享 envelope 和关联字段，合并视图不得伪造 Span、parent 或 duration。
7. Launch metadata 按阶段生成：先记录声明式 request metadata，再按实际创建流程补齐 Task Instance/storage、Product Session、Runtime Run、Host Session、设备/Runtime 观察、Trace/Evidence 和 terminal outcome；这些是字段来源阶段，不预先规定跨组件调用顺序；任何阶段缺失都显式保留，不用空值伪装完成。

## Acceptance

1. 任务发起接口接受声明式 request metadata，并能为一次任务逐步建立可查询的 Project/Test Set/Task 引用、Task Instance、Runtime Run/Product Session、Host Session 和 storage namespace 关系；`runId` 由 Runtime 产生，接口可以先返回 launch/instance reference，后续查询可观察字段逐步补齐。
2. Query capability 能从本地 adapter 读取同一任务的对话、DSH/UniClaw Trace、Evidence、Metadata 和执行结果；页面不依赖目录结构。
3. 测试集默认显示 `default`，变更测试集内容时可通过 `sourceRevision` 定位来源；前端样式或查询实现变化不改变测试集版本。
4. 真实 Android fixture、无 artifact session、未关联 Host session 三类场景都能显示明确的关联或不可用状态，不静默拼接 Product truth。
5. Trace comparison 保留 source、authority、schema、run/session correlation、observedAt 和 truncation/cursor；DSH event 不被伪装成 OTel Span。
6. 现有 `TaskQuery/SessionQuery/TraceQuery/EvidenceQuery/DetailQuery` 兼容；新增关系、分阶段元信息和局部失败有契约测试、确定性测试及至少一条真实 3083 场景证据。
7. 同一 launch request 可安全重试或恢复，不重复创建 Task Instance/Run/Session；阶段失败保留明确的 partial/unavailable 状态和可诊断原因。
8. 至少一条验收场景证明同一任务同时包含 configured、generated、observed、derived 四类元信息，并能在界面上区分它们。
9. Launch request/ack、阶段记录和 Query response 的字段形状进入版本化 schema；写入 contract 与只读 contract 的权限、错误和幂等语义可分别验证。

## Plan / WorkItems

- Plan：`plans/2026-10-03-pnl-004-plan.md`
- Completed WorkItem：`workitems/WI-PNL004-001.json`（Contract）
- Completed WorkItem：`workitems/WI-PNL004-002.json`（Repository Test Catalog）
- Completed WorkItem：`workitems/WI-PNL004-003.json`（Task Launch + Local Storage）
- Pending WorkItem：`workitems/WI-PNL004-004.json`（Query Projection + UI metadata）
- Pending WorkItem：`workitems/WI-PNL004-005.json`（Integration / real verification）

## Status log

- 2026-10-03 · RESOLVE → PLAN · 完成 Task Launch、分阶段元信息、测试集资产和本地 Storage Adapter 的垂直切片计划；建立 WI-PNL004-001..005 DAG。
- 2026-10-03 · PLAN → IMPLEMENT → REVIEW → VERIFY（S1）· 完成 Launch request/ack、LaunchStageRecord、LocalStorageNamespace schema、正反例和 validator；S2–S5 仍待执行。
- 2026-10-03 · PLAN → IMPLEMENT → REVIEW → VERIFY（S2）· 完成两个本地测试集 manifest 与 fail-closed validator；S3–S5 仍待执行。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S3）· 完成 canonical Task Launch、Runtime-owned run、幂等复用和 partial recovery；S4–S5 仍待执行。

## Evidence targets

- `testsets/android-settings/`
- `testsets/workspace-contract/`
- `evidence/pnl003-real-task-android-settings-20261003/`
- `schemas/workspace/`
- `web/uniclaw-workspace/`
- `dsh/uniclaw-task-workbench/`
- `docs/adr/0031-uniclaw-workspace-host-neutral-query-core.md`
- `docs/adr/0032-host-owned-project-test-catalog.md`
- `docs/adr/0033-task-launch-and-local-storage-seam.md`
- `docs/adr/0034-staged-task-launch-metadata.md`
