# Protocol Violation Records

DocumentType: `PROCESS_VIOLATION_RECORD`
Authority: `NONE`
GeneratedAt: `2026-08-25`
RecordedBy: `DSH coding agent (Sol Leader role, UniFlow session)`

本文件记录已发生的执行协议违规，供治理审计使用。它不改变架构权威、
OpenSpec 生命周期或任何 gate 结论；引用时以被指出的权威协议原文为准。

---

## PV-2026-08-25-01 — Tool Only 配置用于源码写入任务

- **Status**: RECORDED（未追溯修复；违规产物已由后续独立核验覆盖评估）
- **Protocol violated**:
  - `.ai/workflows/codex-coding-workflow.md` §3："`tool-only` 不启动 Agent"，
    Tool Only 仅用于确定性读取/命令（§4 step 1、§7 路由表"确定性读取/命令"）。
  - `.ai/profiles/execution.json` `tool-only` profile：`source_write: forbidden`。
- **What happened**: `runtime-exploration-ledger-and-depth-control` Apply 阶段的
  旧派发把写入生产源码的任务（`src/UniClaw.Runtime/Model/ExplorationLedger.cs`、
  `ExplorationLedgerCompiler.cs`、`Agent.cs`、`Agent.OpenWorld.cs` 及对应测试）
  标记为 Tool Only 执行。源码写入超出 Tool Only 的授权边界。
- **Impact**: 产物真实性不受影响 —— 产物由本次独立核验（Spec → 符号 → 测试 →
  证据映射）重新验证；本记录只针对派发协议违规本身。
- **Corrective rule going forward**（本次会话已执行）:
  1. 确定性操作由 Leader 直接使用工具，不创建 Tool Only Subagent；
  2. 任何 Worker 派发必须生成完整 JSON WorkItem、通过
     `tools/agent_profile_validator.py work-item` 校验，并使用匹配的
     Role/Execution/Module Profile 与唯一 `worker_owner`；
  3. 源码写入仅允许 `development` ExecutionProfile。
