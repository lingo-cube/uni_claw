# PER-010 Plan — Compatibility Explore → FROZEN

版本：v0.1.1；本轮仅修订 checked capability 语义，保持 design-only/FROZEN。

## 目标

在不触碰 Product code 的前提下，把 Android hierarchy 的版本、采集器和能力差异收敛为 `UiHierarchyObservation v1` 设计，并用 fixture/matrix 证明字段和失败语义没有把缺失误写成 false。

## 垂直切片

1. **事实 inventory**：API 28/29/30+/34/35/36、采集器、字段、Compose/WebView/multi-window/OEM、现有 XML fixture；输出 compatibility/capability/source/version matrix。
2. **稳定协议**：CaptureMetadata、Windows、NodeOccurrence、ObservedValue、CheckedState、Capabilities、Coverage、CaptureResult；冻结 absence、identity、correlation 和 failure 语义。
3. **边界检查**：对照 PER-009、Product baseline、UWorld baseline，确认 raw XML、belief、grounding、AGT/RUN 没有越界。
4. **Grill/disposition**：本次 v0.1.1 只对 F1 checked ambiguity 做 focused re-grill；必要修改只限本文档；完成后保持 design_status = FROZEN。
5. **后续 implementation slices（未授权）**：adapter contracts → normalization fixtures → bounded acquisition → Evidence Ledger ingress → WorldModel mapping。这些不在本 change 内执行。

实现前置：当前 PER-009 realization 与本前向 contract 的语义迁移不一致已记录；任何消费 PER-010 contract 的 PER-011 实现，必须先完成独立的 migration decision。该前置不重开 PER-009，也不在本 change 内实现迁移。

## Acceptance

- A1 inventory 覆盖 API 28/29/30+/34/35/36 及要求字段、Compose、WebView、multi-window、OEM 事实；每格有 evidence path 或明确 `UNKNOWN`。
- A2 stable contract 明确 CaptureMetadata、Windows、Nodes、Capabilities、Observed/Unknown/Unsupported、Checked 三态和 capability-aware boolean mapping。
- A3 明确 `Empty`、`SourceUnavailable`、`Malformed`、`Partial` 互不折叠，且 missing ≠ false、unsupported ≠ unknown。
- A4 明确 occurrence-local identity、correlation/freshness boundary、bounded acquisition、Agent/Grounding boundary。
- A5 无 Product implementation、无 baseline/authority 修改、无 AGT/RUN 冲突。
- A6 Grill checklist 全部 PASS，design_status = FROZEN，并列出后续 slices。

## Verification（设计 change）

```yaml
level: CONTRACT
method: >-
  对 spec/plan/inventory 做 required-section grep；检查 changes/PER-010 与
  plans/PER-010 之外无工作区改动；git diff --check；逐项复读 PER-009 frozen
  mechanism 与 Product/UWorld baseline 的 authority boundary。
expected: A1–A6 满足；无 Product code 或 frozen boundary 改动。
actual: PASS，inventory 与 stable contract 已建立；v0.1.1 checked capability amendment 已纳入；focused re-grill 通过；待最终 exact-path audit。
evidence: plans/2026-09-26-per-010-compatibility-inventory.md；本目录文件。
```
