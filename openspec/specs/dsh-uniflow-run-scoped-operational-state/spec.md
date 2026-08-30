# dsh-uniflow-run-scoped-operational-state Specification

## Purpose

为未来 UniFlow 执行提供按 Session/Run 隔离、身份明确且兼容既有证据的 DSH operational state，使事件、派发记录和回执核验不会跨 Run 混淆，同时保持历史状态只读可追溯。

## Requirements

### Requirement: 新 Run 使用版本化 Session/Run 状态布局

DSH UniFlow adapter MUST 将新 dispatch 的 Run 状态写入 `sessions/<session_id>/runs/<run_id>/`，并在该目录内保存 Run 事件和 dispatch records。`session_id`、`run_id`、`correlation_id` MUST 来自 dispatch 输入及其 Envelope；adapter MUST NOT 从 Host session 目录名推断 UniFlow `run_id`。

#### Scenario: 新 Run 状态相互隔离

- **WHEN** 两个不同 Run 使用相同 WorkItem id 派发
- **THEN** 两个 Run MUST 写入不同目录且任一记录 MUST NOT 覆盖另一 Run

#### Scenario: Host session 目录不冒充 Run 身份

- **WHEN** receipt 从一个 Host session 目录重建
- **THEN** Host session id MAY 作为独立证据字段记录，但目录名 MUST NOT 覆盖 dispatch record 中的 UniFlow `session_id` 或 `run_id`

### Requirement: Run 事件携带完整关联身份

持久化的 Run-scoped 事件 MUST 携带 `scope=run`、`session_id`、`run_id`、`correlation_id`，并在事件适用时携带 `work_item_id`。现有 12 个事件名保持不变；Profile source 校验、加载和冲突等无 Run 输入的事件 MUST 记录为 `scope=system`，不得复制到任意 Run 目录。

#### Scenario: dispatch 事件与 Envelope 一致

- **WHEN** 一个合法 WorkItem 通过 Envelope 派发
- **THEN** 对应 `work_item.dispatched` 事件的 Session、Run、Correlation 与 WorkItem 身份 MUST 与 Envelope 完全一致

#### Scenario: System 事件不污染 Run

- **WHEN** adapter 仅执行 Profile 校验或加载且尚无 Run 输入
- **THEN** 产生的事件 MUST 进入 system scope 且任意 Run 目录 MUST NOT 被创建

### Requirement: v2 receipt 精确查找并保留 v1 只读兼容

receipt lookup MUST 在给定 `session_id` 与 `run_id` 时优先读取对应 v2 dispatch record，并核对 record、Envelope、requested binding 与 receipt 中的 Session/Run/WorkItem/owner 身份。若 v2 record 不存在，adapter MAY 只读回退到既有 flat v1 record；回退记录的内嵌身份与请求不一致时 MUST 返回 `RECEIPT_LOST` 或 `RECEIPT_MISMATCH`，不得猜测。

#### Scenario: v2 精确 receipt 通过

- **WHEN** v2 record、Envelope、Host receipt 与请求的 Session/Run/WorkItem/owner 及 binding 全部一致
- **THEN** receipt 核验 MUST 成功

#### Scenario: 跨 Run receipt 被拒绝

- **WHEN** receipt lookup 指向另一个 Run 的 dispatch record 或 receipt
- **THEN** adapter MUST fail-closed 且不得接受 WorkResult 或 ModuleContext Delta

#### Scenario: v1 record 继续可读

- **WHEN** 历史记录仅存在于既有 flat dispatches 目录且其内嵌身份匹配请求
- **THEN** receipt lookup MUST 通过只读 fallback 使用该记录且 MUST NOT 重写或迁移它

### Requirement: 历史状态在本 change 中保持冻结

本 change MUST NOT 迁移、重写、截断或删除既有 `.dsh/profile-adapter/state/events.jsonl`、flat dispatch records、ModuleContext、LeaderCheckpoint 或 OpenSpec/archive 证据。历史归档、保留期与删除 MUST 由后续独立 change 和 Human Gate 决定。

> **例外（Human Gate, 2026-08-29）**：对既有 `state/events.jsonl`
> 的一次性 legacy **复制**拆分已获授权——system 类事件复制入
> `system/events.jsonl`；可通过既有 flat dispatch records 回溯 Run 身份的事件
> 复制入对应 `sessions/<session_id>/runs/<run_id>/events.jsonl`；其余复制入
> `legacy/events.jsonl`。该拆分只新增复制文件，不删除、不改写、不移动原文件；
> 原文件与 flat dispatch records、ModuleContext、LeaderCheckpoint、
> OpenSpec/archive 证据仍受本条 MUST NOT 约束。授权记录：
> `docs/work/active/dsh-uniflow-v1-events-legacy-migration-gate.md`。

#### Scenario: 启用 v2 不改变 v1 字节

- **WHEN** v2 dispatch、receipt 或验证命令运行
- **THEN** change 开始前存在的 v1 历史文件内容与 mtime MUST 保持不变

### Requirement: 状态路径与写入 fail-closed

Session、Run 与 WorkItem 身份在形成路径前 MUST 通过单段安全校验；绝对路径、分隔符、`.`、`..` 或 traversal 输入 MUST 被拒绝。dispatch record MUST 继续使用同目录临时文件加原子替换；拒绝路径不得产生部分目录、事件或 record。

#### Scenario: 非法身份零副作用

- **WHEN** Session、Run 或 WorkItem 身份包含 traversal 或路径分隔符
- **THEN** adapter MUST 在任何状态写入前拒绝并保持 v1/v2 状态不变

### Requirement: 验证命令不污染持久运行状态

仅执行 adapter 安装/Profile 校验的命令 MUST 使用非持久事件 sink 或隔离临时状态，不得向仓库默认 operational state 追加事件。

#### Scenario: validate 零持久副作用

- **WHEN** 用户执行 adapter `validate` 命令
- **THEN** 默认 v1 与 v2 事件文件内容和 mtime MUST 保持不变
