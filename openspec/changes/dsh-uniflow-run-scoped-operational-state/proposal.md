## Why

UniFlow 当前把全部 DSH 适配器事件追加到一个仓库级 `events.jsonl`，dispatch record 也只按 WorkItem id 平铺；事件缺少稳定的 Session/Run 身份，重复验证会持续放大共享文件，并使不同 Run 的审计与回执关联容易混淆。需要在不扰动既有开发流程和历史证据的前提下，让未来 Run 使用可隔离、可回滚、可核验的运行状态布局。

## What Changes

- 为未来 UniFlow Run 增加 `Session → Run` 分层的 operational-state v2 布局，Run 事件与 dispatch record 只写入对应 Run 目录。
- 将 Profile 加载/校验等非 Run 事件保留为 system-scoped 日志，避免复制进每个 Run。
- 每条 Run 事件携带 `session_id`、`run_id`、`correlation_id` 与适用的 `work_item_id`；这些身份来自已校验的 Envelope/dispatch 输入，不从 Host session 目录名推断。
- receipt lookup 优先读取 v2 精确路径，并对既有 v1 flat dispatch record 提供只读 fallback；旧状态不迁移、不重写、不删除。
- 保持 `module-context.json`、`leader-checkpoint.json`、WorkItem/WorkResult schema、RuntimeAgent 权威及现有 12 个事件名不变。
- 增加 v1/v2 兼容、跨 Run 隔离、身份不匹配 fail-closed、路径安全和原子写测试；先通过真实/集成验证，历史归档与删除由后续独立 change 决定。

## Capabilities

### New Capabilities

- `dsh-uniflow-run-scoped-operational-state`: 定义 UniFlow v2 新写布局、System/Run 事件归属、Session/Run 身份、v1 只读兼容和历史零删除边界。

### Modified Capabilities

无。

## Impact

- `tools/dsh_profile_adapter.py`：事件路由、v2 dispatch record 路径、receipt 精确查找与 v1 fallback。
- `tests/AgentWorkflow/`：EventLog、Gateway、CLI dispatch/receipt 的隔离与兼容测试。
- `.dsh/profile-adapter/README.md`：v2 布局、身份来源、兼容和回滚说明。
- `openspec/changes/dsh-uniflow-run-scoped-operational-state/`：新能力规格、设计和任务。
- 不修改 `.ai/schemas/`、Runtime/Perception 生产代码、C# 测试资产、历史 `.dsh/profile-adapter/state/` 内容或 OpenSpec archive。
