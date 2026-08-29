# UniFlow v1 events.jsonl Legacy Migration — Human Gate

DocumentType: `HUMAN_GATE_PACKET`
Date: 2026-08-29
Authority: `NONE`
Change: `openspec/changes/dsh-uniflow-run-scoped-operational-state/`
Disposition: `AUTHORIZED_IMPLEMENTED_VERIFIED`

## Human Decision

2026-08-29，Human 授权对既有 `.dsh/profile-adapter/state/events.jsonl`
执行**一次性 legacy 复制拆分**（推荐方案）：

- system 类事件复制入 `state/system/events.jsonl`；
- 可通过既有 flat dispatch records 回溯 Run 身份的事件复制入对应
  `state/sessions/<session_id>/runs/<run_id>/events.jsonl`；
- 其余无身份历史事件复制入 `state/legacy/events.jsonl`；
- 原 `state/events.jsonl` **原地保留（零删除、零改写）**。

该授权不扩展到 flat dispatch records、`module-context.json`、
`leader-checkpoint.json` 或 OpenSpec/archive 证据，它们仍受本 change spec
`Requirement: 历史状态在本 change 中保持冻结` 的 MUST NOT 约束。

## Why

v1 `state/events.jsonl` 是平铺共享文件（1740 行 / 244KB）。v2 实现后新的
EventLog 只写 `system/events.jsonl` 与 `sessions/<sid>/runs/<rid>/events.jsonl`，
v1 文件（与 flat dispatch record 不同）**没有只读 fallback 读取路径**，
若不处理将成为无主数据孤岛。拆分使其按新 scope 语义可检索，同时不违反
"零删除"边界。

## What changed or was discovered

- 拆分结果（行数守恒 1457 + 12 + 271 = 1740）：
  - `system/events.jsonl`：`profile.source.validated`(803) + `profile.loaded`(652)
    + `checkpoint.updated`(2)；
  - `sessions/session-9f2d0100-aa0d-4ee5-b6f5-4151648bb46a/runs/WI-EVH-00{1..6}/events.jsonl`：
    12 行 `work_item.dispatched`，按 `state/dispatches/WI-EVH-*.json` 内嵌
    Session/Run 身份回溯（每个 run 恰好 2 行）；
  - `legacy/events.jsonl`：其余 271 行（`work_item.dispatched` 175 /
    `worker.context.loaded` 94 / `workflow.route.selected` 1 /
    `work_result.accepted` 1）——历史事件未记录 Session/Run 身份且无 dispatch
    record 可回溯，故不作推断、整体只读归档。
- 历史事件普遍缺失 run 身份：1740 行中 0 行含 `session_id`/`run_id`/`correlation_id`；
  仅有 `work_item_id` 的行 188 行，其中仅 12 行可经 dispatch records 精确关联。
- 拆分只读复制，不改写任何来源行；原文件 sha256 前后一致
  （`2fa1ca744ac9bf6d…`）。DshWorkflowRuntime 仍使用 `state_dir` 根下的
  `module-context.json` 与 `leader-checkpoint.json`，均未触碰。

## Validation

- 行数守恒：1457 + 12 + 271 = 1740 ✓
- 原文件字节不变：`sha256` 相同 ✓；每个目标文件与来源行逐字节一致 ✓
- 目标路径全部为 v2 布局合法路径（单段组件、无分隔符/traversal）✓
- 脚本拒绝覆盖任何已存在目标文件 ✓
- 相关文档已同步：spec 例外条款、`.dsh/profile-adapter/README.md`、
  `tasks.md` 3.2/4.2；`git diff --check` 通过。
- 全局 `check-consistency.sh` 的 C15 FAIL 来自另一 change 的 evidence 文件
  （`runtime-iterative-full-traversal-acceptance/evidence/…` 引 `WI-NORM-*`），
  与本迁移无关且未在本授权范围内。

## Owner

`engineering-governance`：历史运行状态的一次性归档分类与文档同步，不改变
Runtime / Perception 权威，不改变 OpenSpec 生命周期结论。