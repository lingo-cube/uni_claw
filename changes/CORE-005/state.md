# CORE-005 — World Evidence 到 Claim 的责任对齐切片
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent
parent_change: CORE-004

## Intent

以现有 Kernel Evidence/World seam 为唯一事实来源，完成 Evidence、Segment、Slice、
Claim 的 Core 责任对齐和只读语义验证；不迁移旧类，不建立第二套事实权威。

## Acceptance

1. Evidence provenance、Segment continuity、Slice history/scope、Claim disposition
   和 Evidence basis 均可通过现有 seam 表达。
2. Slice 可重叠、包含、重复覆盖和 partial；新 Slice 不改写旧 Slice。
3. Claim revision 保留旧 Evidence basis；latest/current view 不改写历史。
4. omission、missing response、empty observation 保持 Unknown，不生成负 Claim。
5. Core projection 为只读，不新增 World/Evidence/Claim writer。
6. Segment/occurrence/reference 不被解释为已证明的现实身份。
7. 不引入新的 Event、Adapter、Runtime authority 或旧模型迁移。

## Verification

```yaml
level: DETERMINISTIC
primary_seam: existing Kernel CoreSemanticProjection + Core.Tests
expected: world alignment and history/unknown tests pass; canonical owner remains Kernel
actual: existing Core projection, World/Claim evolution, terminal, simulation and Core-only
  tests satisfy the slice; no new writer, project, Adapter, or source migration introduced.
evidence: evidence/2026-09-19-core-005-world-alignment.md
```

## Status log

- 2026-09-27 · verified→closed · Owner final closure PASS（联合 OWNER_GATE
  审阅：acceptance 7/7 机械证据；其冻结的边界——Kernel 唯一 World/Evidence
  authority、admission 判据零改动、provenance 通用透传——已被 PER-013
  （HierarchyCaptureDescriptor 经此缝，EvidenceLedger.cs 注释在案）与
  CSC-001/002（Space 字段同一通道）事实上当作 upstream authority；
  CoreSemanticProjection 生产侧零调用符合「只读对齐、不迁移」承诺范围）。
2026-09-19 · planned · 根据 CORE-004 矩阵和 codebase-design seam 评估，选择 World Evidence→Claim 作为第一条对齐切片；尚未实现。
2026-09-19 · planned→verified · 现有最高 seam 和测试已验证 Evidence→Segment/Slice→Claim 责任对齐；确认 Kernel 保持唯一 World/Evidence authority，Core 仅只读投影；Effect/Attempt gates 保留后续。
