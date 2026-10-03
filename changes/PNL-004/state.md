# PNL-004 — Observability Contract and Task Launch/Query Storage Seam

lifecycle_state: resolved · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

建立 UniClaw Workspace 下一阶段的可扩展只读数据接缝：通过一个明确的 Task Launch Seam 发起任务并建立 Project/Test Set/Task、Runtime Run/Product Session、Host Session 和本地 storage namespace 的关联；通过 Host-neutral Query capability 查询 Session、Conversation、Trace、Evidence、Metadata 和执行结果。

当前不建设完整的 Project/Test Set 管理系统。代码库中的 `testsets/` 目录只作为本地临时资产来源；文件夹和 JSON 是第一种 storage adapter，不进入共享前端契约。

## Scope

- `default` 测试集版本和可选 `sourceRevision` 的读取语义。
- Task Launch Seam 的运行关系写入与 `runId`/Session 显式绑定。
- 本地 filesystem storage adapter 与 Host-neutral Query Core 的关联读取。
- Project/Test Set/Task/Instance/Run/Session/Trace/Evidence/Metadata 的逻辑引用和局部失败状态。
- Observation envelope 的 source、authority、availability、claimKind、snapshot/observedAt 和 detail reference。

## Out of Scope

- Host 侧 Project/Test Set 正式持久化、编辑、归档和权限管理。
- Workspace 自有长期缓存、归档、保留或第二份 Product truth。
- 从目录名、artifact 文件名或 DSH Session 推导 Product Session/Primary Run。
- OTel 采集、完整 exporter、实时 websocket 和前端任务编辑器。

## Decisions

1. Host/Task Catalog 是 Project/Test Set 的长期 owner；当前 Host 能力缺失时，代码库 `testsets/` 提供声明式资产和 fixture 引用，统一对外使用 `default` 版本。
2. Task Launch Seam 是建立运行关系的唯一入口：它引用测试集资产，调用 UniClaw Runtime 取得 canonical `runId`/Product Session，绑定 primary Host Session，并创建或确保本地 storage namespace。
3. Query capability 只消费逻辑引用并读取 storage adapter；共享前端不直接访问目录或文件，未来可替换为 Host/远程 adapter。
4. 测试集资产是输入和查询引用来源，不是 Runtime Outcome、完成状态或 Evidence authority。
5. `availability` 与 `claimKind` 分开表达：前者描述有没有数据，后者区分实际观测和配置声明；缺失、未采集、未关联、不可用和权限拒绝不能折叠为空值。
6. UniClaw Trace、DSH Trace 和其他来源保留 source-native 结构，共享 envelope 和关联字段，合并视图不得伪造 Span、parent 或 duration。

## Acceptance

1. 任务发起接口能为一次任务建立可查询的 Project/Test Set/Task 引用、Task Instance、Runtime Run/Product Session、Host Session 和 storage namespace 关系；`runId` 由 Runtime 产生。
2. Query capability 能从本地 adapter 读取同一任务的对话、DSH/UniClaw Trace、Evidence、Metadata 和执行结果；页面不依赖目录结构。
3. 测试集默认显示 `default`，变更测试集内容时可通过 `sourceRevision` 定位来源；前端样式或查询实现变化不改变测试集版本。
4. 真实 Android fixture、无 artifact session、未关联 Host session 三类场景都能显示明确的关联或不可用状态，不静默拼接 Product truth。
5. Trace comparison 保留 source、authority、schema、run/session correlation、observedAt 和 truncation/cursor；DSH event 不被伪装成 OTel Span。
6. 现有 `TaskQuery/SessionQuery/TraceQuery/EvidenceQuery/DetailQuery` 兼容；新增关系和局部失败有契约测试、确定性测试及至少一条真实 3083 场景证据。

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
