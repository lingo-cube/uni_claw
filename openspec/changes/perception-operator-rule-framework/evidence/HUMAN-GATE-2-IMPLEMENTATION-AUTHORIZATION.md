# Human Gate #2 — Implementation Authorization (2026-08-27)

Verbatim ruling summary (three decisions):

## 1. Perception Framework — APPROVED_S1_S2_S4

```text
执行 UniFlow：按 perception-operator-rule-framework 已澄清并验证的 OpenSpec 实施 S1→S2→S4。
严格逐阶段执行并独立验收：S1 必须与保留候选零行为差异，任何差异立即停止；S2 仅消费原始
视觉区域、OCR 文本块及成对几何关系，必须通过 v1n 反例、四锚点无回退和跨 UI 回归，证据不足
保持 fail-closed，失败不得自动进入 S3；S4 的文本和结构化证据只能 veto 或降置信，不得生成
导航候选。禁止实施 S3、S5，禁止修改 Runtime、Agent/FSM/Traversal、CURRENT-ACTIVE 或
Phase 2.6 生命周期。
```

## 2. Runtime Normalizer Disposition — RETAIN_AS_RUNTIME_OWNED_CONTRACT_CONFORMANCE_REPAIR

- Owner: `RuntimeAgent / World normalization`；`AuthorityDelta: NONE`；
  `RuntimeBehaviorDelta: PRESENT`；`ArchitectureDelta: NONE`。
- Rationale (Human): no authority transfer — it only blocks non-authorization-eligible
  auxiliary occurrences from the completeness identity sequence when an explicit Primary
  Vision source exists; diagnostics still enumerate auxiliary; the source-less legacy
  compatibility path is retained. Consistent with the existing Vision-primary contract.
- Targeted test re-verification: 7/7 PASS.
- Documentation corrections REQUIRED (applied in this session's evidence):
  - No blanket "Runtime implementation unchanged" claims.
  - Phase 2.6's `0/216` means ONLY that the campaign itself made zero Runtime edits.
  - The diff is Runtime-owned (not Perception-owned) and must NOT be cited as
    tolerating duplicate visual menu items.
  - Runtime precision-overlap, uniqueness checks, and fail-closed rules unchanged.

## 3. Phase 2.6 Re-entry — CONDITIONALLY_PREAUTHORIZED_AFTER_S1_S2_S4_PASS

Conditions: S1+S2+S4 all pass; final composed pipeline yields exactly one candidate per
PROVABLE navigation visual row; non-navigation rows/descriptions/subtitles/local
controls/ambiguous rows are never fabricated into candidates; v1n + four-anchor +
cross-UI regressions all pass; no Runtime Contract/Authority/CURRENT-ACTIVE change.
Then: G/Stage A → H/Stage B → I/Stage C → J/2.6A Independent Acceptance; K/2.6B only
after J PASS. Frozen fixture v1 may serve as I.2's real conflict input — but
fresh-evidence-wins must be PROVEN by the new campaign's facts, not pre-declared.

## Final state

```text
PerceptionImplementationAuthorization: APPROVED_S1_S2_S4
RuntimeNormalizerDisposition: RETAIN_AS_RUNTIME_OWNED_CONTRACT_CONFORMANCE_REPAIR
Phase2_6Reentry: CONDITIONALLY_PREAUTHORIZED_AFTER_S1_S2_S4_PASS
S3: NOT_AUTHORIZED
S5: DEFERRED
AuthorityDelta: NONE
ArchitectureDelta: AUTHORIZED_PERCEPTION_INTERNAL_ONLY
```
