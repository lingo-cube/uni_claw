# Phase 2.5 Graduation Confirmation — Decision Record

DocumentType: `GRADUATION_DECISION`
Decision: `PHASE25_GRADUATION_CONFIRMED`
Capability: `PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`
Change: `openspec/changes/uniagent-emulator-validation-harness/`
Review: `docs/decisions/uniagent-emulator-validation-harness-graduation-review.md`（2026-08-26 独立评审）
Human Authorization: `PROJECT_LEADER_PHASE25_GRADUATION_CONFIRMATION`（2026-08-26，Phase25Graduation: APPROVED）
Date: 2026-08-26
Authority: Runtime Architecture Contract I-1..I-14 与 Architecture v1 不变；本决策只收口生命周期事实。

---

## 1. Final Graduation Decision

**Phase 2.5 — UniAgent Emulator Validation：GRADUATED / ACTIVE / NOT_ARCHIVED**（Human
APPROVED 2026-08-26；依据独立毕业评审的 GRADUATE 建议与全部机器证据）。

## 2. Exact Graduated Capability

`PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`：

1. 外部 Agent Loop 可以生成受支持的抽象 StrategyDirective 驱动 RuntimeAgent
   （`run.strategy.start` + 冻结只读面）。
2. RuntimeAgent 单 Run 内独立拥有 Observation → Grounding → Authorization → Execution →
   Verification → Completion；上层不需要 Run 内 UI 微操。
3. Runtime 无法安全继续时可以 evidence-backed bounded fail-closed（零 redispatch storm、
   零介入、无隐藏 fallback）。
4. Runtime Result 可被上层读取并用于下一次跨 Run Strategy 决策（插入点
   `Historical Result → Strategy` 在 Runtime 边界外）。
5. Real Emulator 上 S1/S2/S3 buyer chain 完整验证：S1 **PASS @ required coverage 8/8**；
   S2 **PASS_BOUNDED_FAIL_CLOSED**；S3 **PASS**。
6. Runtime production source 在整个 Phase 2.5 validation 中未因该 capability 修改
   （冻结 wire/DTO SHA 守护 + git diff mtime 实证）。
7. Tier C Physical Device validation：**WAIVED_BY_HUMAN** —— 毕业结论不含任何
   physical-device claim。

## 3. Explicit Non-Claims

不得扩展为：UniAgent implemented；Planner capability；Memory capability；universal Android
traversal；universal recovery；generic Trap/Recovery capability；physical-device validation；
dynamic planning；Runtime cross-run intelligence；Runtime MultiRun orchestration。

特别保留的不等式：
- `AUTONOMOUS_EXCEPTION_DISPOSITION != UNIVERSAL_RECOVERY`
- `RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS`

## 4. Lifecycle State（本决策后生效）

| Phase | 状态 |
|---|---|
| Phase 0 Execution Reliability | COMPLETED |
| Phase 1 Exploration Model | GRADUATED |
| Phase 2 Exploration Runtime | GRADUATED / ACTIVE |
| **Phase 2.5 UniAgent Emulator Validation** | **GRADUATED / ACTIVE / NOT_ARCHIVED** |
| Phase 3 Exploration Memory | READY_FOR_SEPARATE_HUMAN_GATE / NOT_APPLIED |
| Phase 4 General Exploration Intelligence | NOT_AUTHORIZED |

## 5. Tier C Waiver Record

Tier C（Physical Device）= **WAIVED_BY_HUMAN**（路径 B，2026-08-26）：执行时无物理
Android 设备（adb devices + ioreg USB 双证据，Environment blocked 于被测代码之前），
Human 裁定 Real Emulator 层（Tier B）保真足够。记录：
`docs/work/active/uniagent-emulator-tierc-physical-device-validation-result.md`。
**Tier B 全部证据的执行层为 Real Emulator（scroll-test AVD）；任何文档不得将其表述为
真机 / physical device。**

## 6. Documentation Consistency

本决策同步更新（仅生命周期事实，Roadmap 方向不变）：
- 本毕业决策记录（docs/decisions/）
- `docs/work/active/current-gates.md`（gate 注记）
- `docs/snapshots/latest.md`（Next 投影）
- `docs/decisions/runtime-exploration-roadmap.md`（§2 Current Capability Baseline 的
  Phase 2.5 状态行）
措辞纪律：Tier B=Real Emulator；Tier C=Physical Device WAIVED_BY_HUMAN（无 drift）。

## 7. Phase 3 Memory Compatibility Record

Phase 2.5 buyer evidence **没有推翻** `uniagent-local-exploration-memory` 草案四项基线：

| 基线 | 状态 |
|---|---|
| Buyer：UniAgent pre-Run Exploration Plan advisory | 未变 |
| Owner：UniAgent-local Memory | 未变 |
| Scope：UNIAGENT_PRIVATE_CROSS_SESSION | 未变（仍 conditional on 下一 Human Gate） |
| Influence：PRE_RUN_ADVISORY_ONLY | 未变 |

**新的实证支持**（Tier B S3）：`Historical Result → Upper Agent interpretation → New
Strategy → New independent Runtime Run` 已在 Real Emulator 上操作性地执行——Memory 插入点
继续位于 Runtime boundary 外部，且无需 Runtime 参与。注意：这只是 compatibility
evidence，**不等于 Phase 3 implementation authorization**（Phase3Apply: NOT_AUTHORIZED）。

## 8. Archive Status

**DEFER**（Human 决定）：与当前 Phase 2 在途 7 文件（工作树中 `runtime-exploration-*`
收尾 diff）的收尾和归档时机统一处理。本 change 保持 ACTIVE / NOT_ARCHIVED。

## 9. Remaining Human Gates

1. Phase 2 在途工作树收尾 + 本 change 与 Phase-2 相关 change 的统一 archive 时机。
2. Phase 3 Exploration Memory 的独立 Human Gate（apply 授权；草案兼容性已记录于 §7）。
3. Phase 4 维持 NOT_AUTHORIZED。

## 10. AuthorityDelta

`NONE`。

## 11. ArchitectureDelta

`NONE`（零生产代码改动；仅生命周期/决策/投影文档）。
