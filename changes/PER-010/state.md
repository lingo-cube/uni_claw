# PER-010 — Android UI Hierarchy Compatibility Layer

版本：v0.1.1（narrow amendment；design-only）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 2f633b04
design_status: FROZEN

## Intent（WHAT/WHY）

以 API 28 为兼容下限，把 legacy XML、AndroidX UiAutomator、AccessibilityNodeInfo / richer source 归一化为稳定 typed observation，避免 Product 绑定采集器或 Android 版本，并冻结 missing/unknown/unsupported、checked 三态、occurrence identity、freshness/correlation、bounded acquisition 与 Agent/Grounding 边界。

## Scope / Out of Scope

范围和排除项见 `spec.md`。本 change 只产生设计、matrix、fixture inventory 和 Grill disposition，不写 Product implementation。

## Decisions

- API 28 是 v1 floor；compatibility band 只用于 coverage。
- capability-driven > acquirer-driven > Android-version-driven。
- `ObservedValue<T>` 的 `Observed | Unknown | Unsupported` 三态不可折叠。
- checked 的值轴为 `Checked | Unchecked | Partial`；`checkedTriState` 完整表达；`checkedBooleanExact` 仅在二态 domain 已证明时表达 false→Unchecked；`checkedBooleanCollapsed` 在可能存在 Partial 时 false→Unknown(`partial-unrepresentable`)。
- hierarchy node/window 都是 capture-local occurrence；任何 provider key 只能作为 association feature。
- Capture timestamp/correlation 是 provenance 输入；freshness 由 Assurance 在具体消费时判断。
- raw XML 不进入 Agent；bounds 不绕过 Grounding；acquisition 必须 bounded。
- PER-009 的字段权威和验证机制保持不变；未发现 upstream design conflict。

## Acceptance

1. API/source/capability/field/failure inventory 完整并可追溯。
2. `UiHierarchyObservation v1` contract 覆盖 metadata/windows/nodes/capability/coverage/checked/identity/failure。
3. absence、unknown、unsupported、partial、malformed、unavailable 语义可判定。
4. Agent/Grounding/WorldModel authority boundaries 无越界。
5. v0.1.1 focused re-grill 的 F1 checked ambiguity PASS，后续 implementation slices 明确。
6. `design_status: FROZEN`；没有写 Product code 或修改 frozen baseline。

## Verification

```yaml
level: CONTRACT
method: >-
  required-section lint + exact-path git status + git diff --check；逐项核对
  PER-009 mechanism、Product baseline、UWorld baseline；inventory evidence path
  复核。
expected: Acceptance 1–6 满足；工作区既有 dirty 文件保持不变。
actual: PASS（设计稿与 inventory 已建立；PER-009/基线边界复核通过；无 Product code 改动）。
evidence: changes/PER-010/spec.md; changes/PER-010/plan.md; plans/2026-09-26-per-010-compatibility-inventory.md。
```

## Grill findings disposition

- 原冻结设计的 capture outcome、field state、capability、occurrence 和 temporal correlation 保持不变。
- v0.1.1 focused re-grill 只攻击 F1：API 36 tri-state-capable environment 中，legacy XML `checked=false` 只有在 `checkedBooleanExact` 已证明二态时才是 `Unchecked`；`checkedBooleanCollapsed` 必须是 `Unknown(partial-unrepresentable)`。
- F1 结果 `PASS`；没有 `UPSTREAM_DESIGN_CONFLICT`。
- Freeze：PER-010 design FROZEN；PER-011 可在本 change 的 contract 之上开始。

## Status log

- 2026-09-26 · understanding→persisted · 读取用户路线、AGENTS.md、UniFlow、PER-009、Product/UWorld baseline；确认本 change 为 design-only。
- 2026-09-26 · persisted→planned · 完成 stable observation contract、failure/capability/identity/grounding 边界草案；委派 Luna 做事实 inventory。
- 2026-09-26 · planned→verified · inventory、required-section lint、exact-path audit 和 focused re-grill 通过；design_status 置为 FROZEN。
- 2026-09-26 · verified→closed · v0.1.1 narrow amendment 已证明 checked capability 语义；PER-010 继续 FROZEN；只修改设计文档与 inventory，既有 Product dirty 文件未触碰。
