# Proposal: DSH UniClaw Control-Plane Protocol Baseline

| 属性 | 内容 |
|------|------|
| Change ID | `dsh-uniclaw-control-plane-protocol-baseline` |
| 状态 | Proposed |
| 类型 | **SOURCE-FIRST ARCHITECTURE AUDIT + OPENSPEC**（零实现：无 plugin、无 Shadow、无 Advisory、无 UI、无 transport 采购） |
| 日期 | 2026-08-15 |
| 分支 | `uni-agent` |
| 模式 | `SOURCE_FIRST_ARCHITECTURE_AUDIT_AND_OPENSPEC` |
| 前置 | `dsh-kernel-read-only-observability`（已 GRADUATED / archived） |

## Why

UniClaw Kernel 已具备只读可观测性（`RuntimeEvent` / `RunSnapshot` / `EvidenceRef`，已毕业冻结），
但"UniClaw 如何与 DeepSeek Harness 集成"从未被系统审计过。此前所有讨论都假设存在一个平行的
UniClaw↔DSH 协议（DecisionRequest / DecisionResponse / HealingRequest / DiagnosisRequest……），
却从未用 **DSH 自己的源码**验证过哪些语义本来就有 DSH 原生 seam。

本 change 回答唯一的问题：**根据 DSH 当前源码真相（pinned commit），UniClaw 作为认知/控制平面集成进
DeepSeek Harness 时，应该使用 DSH 自身的哪些 protocol / plugin / runtime 表面？**

约束：不得发明平行的 UniClaw↔DSH 协议，除非审计证明某个必需语义确实没有 DSH 原生 seam。
输出是一个**冻结的兼容性基线**（`UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859b…`），
不是插件、不是 Shadow、不是 transport。

## What Changes

- **DSH 兼容性 pin 冻结**：`UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859bef60e4160492346772ded9b24f765a`
  （DSH `0.1.0-rc.5`，pre-release，pinned checkout 无 git tags，release commit `abe560f81e` 在历史中；
  remote `deepseek-ai/deepseek-harness`，branch `master`）。所有映射决策基于 pinned source，不用最新文档。
- **SourceEvidenceMatrix**（`source-evidence-matrix.md`）：43 行 DSH 表面证据（事件/命令/工具/slots/
  Typert Remote/投影/持久化/preset/组合/权限……），每行有 source file、Type/API/Event、语义、
  持久性、生命周期、稳定性、UniClaw 用途；附 7 条 docs-vs-source 差异。
- **IntegrationMatrix**（`integration-matrix.md`）：全部必需映射行（RuntimeEvent、RunSnapshot、
  EvidenceRef、10 个事件 kind、11 个人类控制操作、7 类认知操作、Shadow 插入点、控制平面 UI），
  每行含方向/持久性/模型参与/权威/新鲜度/适配器/状态/DSH 证据；附 DecisionTable 与硬禁路径。
- **OpenSpec 变更**（本目录）：proposal / design / spec / tasks，含 DSH pin、扩展点审计、
  observability/control/cognition/UI 四类映射、DriverHost+plugin 边界、权威平面、transport 决策、
  进程生命周期决策、token 经济约束、协议缺口策略、falsifiers、毕业标准、未来实施序列。
- **未来变更序列修正**：`dsh-shadow-cognition` 被本 change 取代为第 4 步；正确序列为
  2. 本 baseline → 3. `dsh-uniclaw-control-plane-plugin-implementation` → 4. `dsh-shadow-cognition`
  → 5. `dsh-advisory-cognition` → 6. 仅在后续有依据时才做 bounded blocking seams。

## Capabilities

### New Capabilities

- `dsh-uniclaw-control-plane-protocol-baseline`: 冻结的 DSH↔UniClaw 控制平面协议基线 —
  DSH 原生 seam 识别、DriverHost 与 dsh-plugin-uniclaw 角色冻结、权威边界冻结、
  transport/进程生命周期决策或延后、Shadow 插入点冻结。**零 Runtime 变更、零 DSH 变更、零实现。**

### Modified Capabilities

- 无（`dsh-kernel-read-only-observability` 已毕业的 RuntimeEvent / RunSnapshot / EvidenceRef 契约
  保持不变；本 change 只消费它们，不改写）。

## Impact

- `src/UniClaw.Runtime/`：**无任何变更**。Kernel 语义、事件发射、只读投影、authority 全部不动。
- `src/UniClaw.Runtime.Harness/` 与 DriverHost 方向：无变更；只读投影继续作为唯一事实源。
- DSH（`/Users/fran/Documents/Code/dk-harness`）：**只读审计**，一个文件都不改。
- 本仓库：仅在 `openspec/changes/dsh-uniclaw-control-plane-protocol-baseline/` 新增文档。
- 测试：无 Runtime 构建/测试；验证 = `openspec validate --strict` + `scripts/check-consistency.sh`。

## Non-Goals (deferred, out of scope for this change)

- 任何 DSH 插件实现（`dsh-uniclaw-control-plane-plugin-implementation`，第 3 步）
- Shadow cognition（第 4 步）、Advisory（第 5 步）、Blocking seams（第 6 步，仅后续有依据）
- 任何 C-class Kernel 发射器采购（`DecisionProposed` / `DecisionAccepted` / `ActionAuthorized` /
  `RecoveryVerified`）——无 concrete buyer，只记录为 PROTOCOL_PRESSURE
- 任何 transport 采购（默认 `TRANSPORT_DEFERRED`，见 design.md §18）
- 任何 DSH UI / 客户端模块设计（只冻结宿主 seam：slots / Typert Remote / projection push）
- 任何模型面向的 `uniclaw.*` tool 预批准（design.md §13 只做 buyer 评估，不批准）
- persistent EvidenceRef 解析（F14：绝不宣称完成）
