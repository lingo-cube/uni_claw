## Context

现有 adapter 在构造 `DshWorkflowRuntime` 时把所有事件写入一个仓库级 `state/events.jsonl`，CLI dispatch 默认把 record 写成 `state/dispatches/<work_item_id>.json`，receipt 也只按 WorkItem id 查找。Envelope 和 dispatch record 已分别保存 Session/Run/Correlation，但事件发射没有传递这些身份；Host receipt 重建还把 Host session 目录名当作 Run id。当前目标文件同时存在未提交的 required-Skill 传播改动，本 change 必须做增量适配并保留这些修改。

既有 `dsh-uniflow-profile-adapter` change 已完成并明确记录单文件事件布局，因此本 change 作为 successor 冻结新能力，不回写其历史设计。

## Goals / Non-Goals

**Goals:**

- 新 Run 默认写入隔离的 v2 Session/Run 状态目录。
- 事件身份与 dispatch Envelope 使用同一显式来源。
- receipt 精确定位 v2 record，同时保留 v1 flat record 只读 fallback。
- 任何失败不覆盖历史、不产生半写记录，并保留一条配置回滚路径。
- 定向和全量 AgentWorkflow 测试继续使用临时 state，不污染仓库状态。

**Non-Goals:**

- 不迁移、归档、截断或删除任何 v1 历史文件。
- 不改变 WorkItem/WorkResult schema、事件名集合或 RuntimeAgent 权威。
- 不移动 `module-context.json` 或 `leader-checkpoint.json`。
- 不修改 C# Runtime、Perception、Replay 或 Golden Run 测试资产。
- 不声称 Host session 日志能够证明其中不存在的 UniFlow Run 身份。

## Decisions

### 1. 显式身份是唯一 Run 路径来源

`session_id`、`run_id`、`correlation_id` 来自 `dispatch_work_item()` 输入并进入 Envelope、Run event context 与 dispatch record。新增单段 path-component validator，拒绝空值、`.`、`..`、路径分隔符、绝对路径和 traversal。Host session 日志中的 session id 单独记为 `host_session_id`；Host session 目录只定位证据文件，不产生 UniFlow Run 身份。

选择该方案而不是从目录名推断，因为现有 `session_dir.name → run_id` 混淆了 Host 与 UniFlow 两层身份，且无法证明两者等价。

### 2. 默认 v2，新写单路；reader v2-first/v1-fallback

默认 state root 下使用：

```text
system/events.jsonl
sessions/<session_id>/runs/<run_id>/events.jsonl
sessions/<session_id>/runs/<run_id>/dispatches/<work_item_id>.json
module-context.json
leader-checkpoint.json
```

新 dispatch 只写 v2，不双写 v1。receipt 在显式 Session/Run 可用时先查 v2，再查既有 `state/dispatches/<work_item_id>.json`；fallback 必须校验 record 内嵌身份。显式 `--record-dir` 继续表示调用方管理的 flat 目录，以保持现有脚本和测试兼容；默认路径才启用 v2。

选择 reader fallback 而不是历史预迁移，避免一次性修改已跟踪审计证据，也让回滚只需恢复旧 writer。

### 3. EventLog 采用不可变 event context，不使用可变全局 Run

新增小型 event context/value object 或等价不可变映射。Runtime 在 dispatch 入口构造一次并显式传给 Scheduler、ModuleContext load、Host dispatch 和 WorkResult gate；后续 acceptance 从按 WorkItem 保存的 dispatch context 取回。持久 EventLog 对 Run-required 事件缺少 context 时 fail-closed；无文件 sink 的单元测试 EventLog 仍可只检查事件名。

选择显式传递而不是在共享 EventLog 上 `bind_run()`，避免并发或串行多 Run 时上下文串线。

### 4. System 与 Run 事件分开，但不扩大事件 taxonomy

Profile source 类事件进入 `system/events.jsonl` 并携带 `scope=system`。WorkItem/Worker/WorkResult 类事件进入 Run 目录并携带完整四元身份。`leader.fallback.started` 与 `checkpoint.updated` 在没有冻结 Session checkpoint 模型前保持 system scope；本 change 不把 checkpoint 移入 Session。

事件名仍限制为原 12 个，避免修改消费者的 taxonomy 契约。

### 5. validate 使用非持久 sink

CLI `validate` 只证明安装与 Profile 完整性，不是一次 Run，也不应修改 operational state。它使用非持久 EventLog 或临时 state 完成相同校验，消除重复验证持续污染仓库 `events.jsonl` 的行为。

### 6. receipt 身份核对为向后兼容的增强

in-process Host receipt 继续从 Envelope 获取 Session/Run。CLI 从 Host session 日志重建模型配置时，从已选 dispatch record 取得 UniFlow Session/Run，并把日志里的真实 Host session id 保存为独立字段。核验增加 expected Session/Run 参数；旧调用未提供时维持既有 binding/WorkItem/owner 门，不伪造更强证明。

## Risks / Trade-offs

- [CLI 调用未传 Session/Run 时只能使用 v1 fallback] → 保留旧命令行为，同时在 v2 record 场景明确要求两个身份参数并给出可执行错误。
- [目标文件已有并行 required-Skill 改动] → Luna 只添加局部功能，不回退、重排或覆盖现有差异；Leader 最终用 scoped diff 复核。
- [持久 EventLog 的并发写风险仍存在] → Run 文件天然分片，单个 record 使用原子替换；跨进程日志锁与历史 compaction 留给后续 change。
- [v1/v2 reader 增加分支] → 用最小版本化 fixtures 覆盖 v2-first、v1 fallback、身份冲突和零副作用。
- [历史删除继续占用少量空间] → 稳定性优先；删除必须等 successor 验证、引用迁移和独立 Human Gate。

## Migration Plan

1. 合入 successor spec 与 v2 writer/reader，不触碰现有 v1 文件。
2. 在临时 state 中验证 System/Run 分流、相同 WorkItem 跨 Run 不覆盖和 v1 fallback。
3. 运行完整 AgentWorkflow 与一致性门；失败时保持 v1 reader 可用并回滚 v2 writer 代码即可。
4. 后续真实 Run 使用 v2，观察期内保留所有 v1/v2 数据。
5. 历史迁移、归档和删除另建 change，依据终态、receipt、引用和 checksum 决定。
