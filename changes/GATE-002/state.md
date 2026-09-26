# GATE-002 — Owner 门机械执法（ADR-0029 remediation）

lifecycle_state: closed · disposition: not-pursued · depth: standard · base: d3ac93da

## Intent（WHAT/WHY）

把 ADR-0007（经 ADR-0029 窄修）第 3 条从「规则在档」变成「机械执法」：
lifecycle 进入需要 Owner 的阶段时，落档必须携带 owner-ruling reference
或显式授权的 fastpath manifest，二者皆缺由确定性脚本直接 RED。GATE-001
实验证明 honor system 在 goal 自动续跑下必失守（五起
PROTOCOL_NON_COMPLIANCE，其中 PER-009 出现 leader 冒 Owner 名义的
ratification 措辞）；本 change 是该 FAIL disposition 的 remediation
owner。

## Scope

- **Owner-gate mechanical enforcement**：确定性脚本校验 lifecycle→closed
  及一切需 Owner 裁决的转移落档；
- **owner-ruling reference / authorized fastpath manifest 检查**：
  可核验引用（commit / state 行 / 台账行）或显式授权 manifest，二选一，
  缺一 exit 1；
- **冒充拒绝**：`leader review`、`Sol review`、goal 自动续跑下的 leader
  自任裁决，一律不得计为 Owner decision（含「Owner ratification
  （leader）」式措辞判据）；
- **承接 ADR-0029 要求的 enforcement tooling / workflow wiring**：含
  SKILL.md 接线与 fastpath manifest 登记格式；ADR-0029 ①（preflight）
  ②（closure fastpath）所需 tooling/wiring 后续 slice 可归本 change 承接。

## Out of Scope

- 门控政策语义任何变更（ADR-0029 已冻结，本 change 只执法不立法）
- 五起 P-D′ 违规历史记录的任何改写（永久保持 PROTOCOL_NON_COMPLIANCE）
- Product code（`src/` / `tests/`）
- 自动关闭的开放（shadow + ACK/veto 阶段语义不变， reopen 协议另议）

## Decisions

- 上游权威 = `docs/adr/0029-gate-recalibration-narrow-amendments.md`
  （amends ADR-0007，gate semantics only）；本 change 不重新裁决规则。
- GATE-001 Acceptance #2 的 remediation owner = 本 change + ADR-0029
  （Owner 裁决 2026-09-26，GATE-001 closure 前置动作）。

## Owner decision（2026-09-27）

```text
GATE-002 implementation will NOT proceed.

Mechanical owner-gate enforcement would add more workflow complexity
than its current expected value. We keep the governance rule, but do not
add a new enforcement subsystem.
```

**Mechanical owner-gate enforcement intentionally not implemented.**

Rationale:
- GATE-001 showed the workflow is already process-heavy.
- Additional mechanical enforcement would increase governance cost.
- Existing review / grill / regression / certification layers remain.
- Owner intervention remains required only for the four stop conditions below.

### 保留的人工 stop rule

```text
默认：
模型可继续实现 / review / verify / close

只有以下情况必须停给 Owner：
1. architecture / authority 发生变化
2. 多个有效方案需要人裁决
3. change 明确为 DECISION-HEAVY
4. review 出现 substantive finding
```

### 明确不新增

```text
owner-ruling validator
fastpath manifest framework
workflow engine
新的 lifecycle state
Product/runtime gate
```

### 边界声明

```text
Product code changed: NO
Tooling code changed: NO
Gate policy implementation changed: NO
```

## Acceptance

**未满足，且不再计划实现。** 原 Acceptance 1–4 全部落空：

1. 确定性脚本存在 —— NOT IMPLEMENTED（Owner 裁决不实现）
2. 负例执法证明 —— NOT IMPLEMENTED
3. 正例不误伤 —— NOT APPLICABLE（无脚本可测）
4. leader / Sol / 自动续跑来源的裁决记录被显式判据拒绝 —— NOT IMPLEMENTED

Acceptance 失守的后果如实承担：owner-gate 落档的 owner-ruling reference
继续依赖人工遵守，工具不再兜底。上游 ADR-0029 的第 3 条规则文本保持
冻结不变，其 mechanical-enforcement consequence 由本次裁决显式 defer。
GATE-001 的五起 `PROTOCOL_NON_COMPLIANCE` 永久保持，不追溯改写。

## Verification

```yaml
level: CONTRACT
method: >
  docs-only 裁决落档复核——确认 lifecycle/disposition 字段、Owner 决策原文、
  保留 stop rule、不新增清单、边界声明四段齐备；确认 ADR-0029 仅追加
  disposition note 且历史结论未改写；确认 GATE-001 与 src/ tests/ 零改动；
  python3 tools/gen-open-changes.py 再生后 GATE-002 移出 open 清单；git diff --check。
expected: GATE-002 closed/not-pursued；open changes = 0；Product / tooling /
  门控政策实现零改动；ADR-0029 历史保留。
actual: PASS（见 status log 同日条目与 ADR-0029 disposition note）
evidence: changes/GATE-002/state.md；docs/adr/0029-gate-recalibration-narrow-amendments.md
  末尾 disposition note；changes/INDEX.md（再生后 103 changes · 0 open）。
```

## Status log

- 2026-09-26 · created·persisted · Owner 裁决（GATE-001 closure 前置
  动作）立项：scope 按 Owner 原文四项冻结；不实现、不动政策、不动
  Product code。GATE-001 同日以 "experiment completed with findings"
  关闭（Owner 选项 A），Acceptance #2 = FAIL（真实保留），
  remediation 移交本 change + ADR-0029。
- 2026-09-27 · planned→closed·not-pursued · Owner 裁决不实现：mechanical
  owner-gate enforcement 的 workflow 复杂度高于其当前期望价值。保留治理
  规则，不新增执法子系统。Acceptance 1–4 如实标为未实现——owner-gate 落档
  的 owner-ruling reference 继续依赖人工遵守，工具不再兜底。上游 ADR-0029
  规则文本不动，仅追加 disposition note 说明其 mechanical-enforcement
  consequence 被 defer。Product code / tooling / 门控政策实现零改动；
  GATE-001 及其五起 PROTOCOL_NON_COMPLIANCE 历史未触碰。
