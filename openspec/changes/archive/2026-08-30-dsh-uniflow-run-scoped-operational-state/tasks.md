## 1. RED-first 状态布局与身份测试

- [x] 1.1 为持久 EventLog 增加 System/Run 分流、完整 Run 身份、跨 Run 隔离和非法 path component 零副作用测试
- [x] 1.2 为 CLI dispatch 增加默认 v2 路径、相同 WorkItem 跨 Run 不覆盖、显式 `--record-dir` 兼容测试
- [x] 1.3 为 CLI receipt 增加 v2 精确查找、Session/Run 不匹配 fail-closed、v1 flat fallback 与 Host session id 独立记录测试
- [x] 1.4 增加 `validate` 不修改默认 v1/v2 operational state 的测试

## 2. v2 Operational State 实现

- [x] 2.1 实现安全 path component 校验、不可变 Run event context 和 v2 state path resolver
- [x] 2.2 将 Profile 类事件写入 system scope，并将 WorkItem/Worker/WorkResult 事件按显式 context 写入对应 Run，保持原 12 个事件名
- [x] 2.3 将默认 CLI dispatch record 改为 v2 Session/Run 路径，同时保留显式 `--record-dir` flat 兼容和原子写
- [x] 2.4 实现 receipt v2-first/v1-fallback、Session/Run/WorkItem/owner/binding 核验，并停止从 Host session 目录名推断 Run id
- [x] 2.5 让 CLI `validate` 使用非持久事件 sink，证明默认 operational state 零副作用

## 3. 兼容与文档

- [x] 3.1 更新 `.dsh/profile-adapter/README.md`，说明 v2 布局、身份来源、v1 fallback、回滚和历史零删除边界
- [x] 3.2 保留现有 required-Skill 传播改动、`module-context.json`、`leader-checkpoint.json` 与所有历史 state 文件字节不变
  - 2026-08-29 Human Gate 授权对既有 `state/events.jsonl` 的一次性 legacy 复制拆分（spec 例外条款 + 独立 gate 记录）；拆分后原文件 sha256 不变（`2fa1ca74…`），dispatch records / `module-context.json` / `leader-checkpoint.json` 未触碰。

## 4. 验证与知识同步

- [x] 4.1 运行定向 AgentWorkflow 测试并确认 RED→GREEN 证据
- [x] 4.2 运行完整 `tests/AgentWorkflow`、OpenSpec strict validation、`scripts/check-consistency.sh` 与 `git diff --check`
  - 受 profile source pin drift 阻塞的 20 个旧用例已迁移到动态 HEAD（新增 `_pin_to_head` helper，与 CLI 套件同模式），完整套件 **165 passed / 3 subtests passed**；`profile-source.yaml` pin 按"协议变更同 commit"规则推进到 `3986d3…`，生产 `validate` 通过（`VALIDATION_PASS 1@3986d3d…`）；check-consistency C1–C15 全 PASS；`git diff --check` 通过。
- [x] 4.3 完成 DOCUMENTATION_SYNC 检查：架构/Runtime/当前 projection 为 NO_CHANGE，successor change 文档已更新，decision/main-spec 同步延后到 archive

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|---|---|
| `tools/dsh_profile_adapter.py` | `openspec/changes/dsh-uniflow-run-scoped-operational-state/design.md` |
| `tests/AgentWorkflow/` | `openspec/changes/dsh-uniflow-run-scoped-operational-state/design.md` |
| `.dsh/profile-adapter/` | `openspec/changes/dsh-uniflow-run-scoped-operational-state/design.md` |
