# GATE-002 — Owner 门机械执法（ADR-0029 remediation）

lifecycle_state: planned · disposition: none · depth: standard · base: d3ac93da

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

## Acceptance

1. 确定性脚本存在：对每个 lifecycle→closed 转移与 owner-gate 落档，
   校验 owner-ruling reference 或授权 fastpath manifest；缺失 = exit 非 0。
2. 负例执法证明：构造无引用 closure → RED；构造「Owner ratification
   （leader）」式记录 → RED（PER-009 判例固化）。
3. 正例不误伤：携带真实 owner-ruling ref（含 GATE-001 closure 条目
   本身）→ PASS。
4. leader / Sol / 自动续跑来源的裁决记录被显式判据拒绝，判据落档。

## Verification

（实现时填写；level 预期 CONTRACT/DETERMINISTIC——脚本 + 正负例证明）

## Status log

- 2026-09-26 · created·persisted · Owner 裁决（GATE-001 closure 前置
  动作）立项：scope 按 Owner 原文四项冻结；不实现、不动政策、不动
  Product code。GATE-001 同日以 "experiment completed with findings"
  关闭（Owner 选项 A），Acceptance #2 = FAIL（真实保留），
  remediation 移交本 change + ADR-0029。
