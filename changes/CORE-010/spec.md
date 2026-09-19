# CORE-010 — 可靠执行记录首个受控实现切片（规划）

## Problem Statement

CORE-009 契约已通过，但仓库现有 `EffectBoundary` / Runtime 记录尚不能证明发送前准备、
无 Receipt 未决和恢复发现满足契约。需要规划首个受控实现切片，优先复用现有记录，不把
诊断 Trace 提升为恢复权威。

## Solution Boundary

本 Change 只提交实现规划，不实施代码。规划以 `EffectBoundary.Dispatch` 为最高现有 seam，
围绕发送前可靠登记、已核验请求/绑定一致性和 Receipt/未知反馈关联组织垂直切片。

候选实现可复用现有 BindingLog/ReceiptLog 及 Runtime 组合，但必须先证明可靠提交、故障
范围内读取和无 Attempt ID 的未决发现；否则另行设计满足契约的 Runtime 执行源。两者都不
改变 Core 对象集，也不让 Trace 成为唯一来源。

## Acceptance

1. 计划明确行为、模型/契约、Runtime seam、测试和证据的垂直切片。
2. 计划区分本地准备提交重试、外部投递重试和补偿 Effect。
3. 计划覆盖提交成功/失败/未知、两处发送前崩溃窗口、无 Receipt、迟到反馈和无 ID 发现。
4. 计划明确不修改 Trace 权威、不新增 Core 事实模型、不触发真实外部操作。
5. 实施入口、执行源 Owner、可靠提交机制和持久性范围在实现前经过评审。

## Out of Scope

- 本 Change 不改 `src/`、`tests/`、Runtime、Trace 或存储实现。
- 不冻结新的公共接口、数据库表、序列化格式或跨节点恢复。
- 不实现机器人、文件、API 或其他 realization。
