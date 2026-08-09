# Tasks — phase3-s0-capstone-settings-traversal

> 实施前必读: `proposal.md` + `design.md` + `specs/s0-capstone-settings-traversal/spec.md` + `scenarios/SC-S0-CAPSTONE-001-settings-traversal.md` + `docs/system/scenarios/06-s0-capstone-settings-traversal.md`。
> 本 change 已获 HUMAN Gate `ACCEPT_S0_BASELINE_READY_AUTHORIZE_CAPSTONE_OPENSPEC`（2026-08-09）授权创建；**实施（Task 2.1 起的任何执行）必须等待 HUMAN Semantic Gate `PROJECT_LEADER_SEMANTIC_GATE_SC_S0_CAPSTONE_001` 批准**。完成一项立即勾选 `- [x]`；一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> Production Delta Budget = **exactly zero**：model types/fields/enums/interfaces/components/new mutable-state fields/new mutable-state owners 全部 = 0；Ownership Delta = NONE；Authority Delta = NONE。全部交付物为 test-side fixture/harness/evidence。
> 任一执行者若发现需要任何生产代码变更、扩大预算、创建 graph/stack/nav manager/safety manager/risk enum/progress framework、reopen 任何 frozen capability、改变 ownership/authority、需要 Harness H4-4、或执行中暴露 frozen 组合无法表达的新 Reality Distinction，立即停止并返回对应 Semantic/Architecture/Human Gate（新 Reality Distinction → stop → EXTRACT_BOUNDED_CANDIDATE）。

## Dependency Order

```text
1.1 Fake S0 World Fixture
→ 2.1 Integration Run Harness
→ 3.1 Formal Capstone Proof
→ 4.1 Independent Validation
```

> 1.1 属 test-side 准备，可在 Semantic Gate 批准前建立并提交 Gate 评审；2.1–4.1 需要 Gate 批准后执行。

## 1. Deterministic S0 World Fixture (test-side)

- [x] 1.1 **构建 external-world-only 的 four-level Settings Fake fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE（frozen SC-P1/P2/P3 既有 Fakes 与 06 注册文档只读复用）
  - **Scenario Receipt:** SC-S0-CAPSTONE-001
  - **Goal:** 在 `tests/UniClaw.Runtime.Tests/Scenario/` 建立确定性 S0 世界 fixture：approved semantic navigation tree 至少四级、safe reachable pages、可见的 destructive mutation candidate（reset/delete/uninstall 等价物）、exactly one local Popup/Overlay obstruction、exactly one external Launcher/desktop drift（均为 deterministic schedule 点）、depth bound 4、允许 scope/safety 约束输入；fixture 不得编码 Container identity、Recovery authority、progress completion 或 Goal success，也不得预编码 concrete route。
  - **Required Semantic:** 世界只决定 external evidence 与 dispatch outcome；world 数据全部 deterministic 且可 replay；visible candidate != approved executable action 是 fixture 性质而非 production 结论；Popup/drift 各自恰好一次。
  - **Approved Production Purchase:** zero（本任务只购买 test-side fixture）。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` 新 fixture/helper/基础 fixture tests；只读复用 frozen 场景 Fakes、`docs/system/scenarios/06-s0-capstone-settings-traversal.md`、分类证据；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**` 任何改动；production model/field/enum/interface/component/state；在 fixture 中编码 production 结论；reopen frozen capability；Task 2.1+ 行为；Capstone 实施。
  - **Required Assertions:** 树至少 4 层且 approved 分支可在 bound 内完整遍历；dangerous candidate 可见但非可执行；Popup 与 drift 各恰好一次且时刻确定；相同输入 replay 相同世界；fixture 不引用任何 production 未公开语义。
  - **Verification:** 基础 fixture tests；`dotnet build src/UniClaw.Runtime.sln`；frozen 回归不受影响；production-delta audit = zero。
  - **Deferred Boundary:** integration harness、正式 proof、Gate 决策、任何 production 行为。
  - **Return Contract:** `TASK_RESULT`；success 需要 `Status: DONE` 且 production delta = 0。完成后停止并由 H4-3 重读 repository truth；**继续执行 Task 2.1 前必须确认 Semantic Gate 已批准**。

## 2. Integration Run Harness (test-side)

- [x] 2.1 **以 frozen capability 组合驱动一次完整 integration run**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE + `PROJECT_LEADER_SEMANTIC_GATE_SC_S0_CAPSTONE_001` HUMAN 批准
  - **Scenario Receipt:** SC-S0-CAPSTONE-001
  - **Goal:** 用 Task 1.1 fixture 与 frozen Agent 行为组合一次端到端 run：traversal intent + scope + depth 4 + safety 约束 → 发现（CAND-008）→ 分支进度（CAND-004）→ dangerous zero-dispatch（CAND-006）→ Popup（P3-002）→ drift（P2-001 + CAND-005/009）→ viewport（P3-003 + CAND-007）→ GoalEvidence 完成判定；断言 completion evidence 1–7 全部成立、零 dangerous dispatch、re-entry 不重复计分、retained progress 不丢弃不伪造。
  - **Required Semantic:** Capstone 只组合、不购买；GoalEvidence 是唯一完成权威；production delta 保持 zero。
  - **Approved Production Purchase:** zero。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` harness/集成测试；只读复用 production 既有 surface 与 frozen Fakes。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`；任何 production 语义变更；graph/stack/manager/planner/FSM；reopen frozen capability；H4-4；新 Reality Distinction 不停止而继续。
  - **Required Assertions:** 断言 1–9（route 不预编码、depth<=4 全覆盖、零 dangerous dispatch、无 unresolved 分支、Popup 后 verified continuity、drift 后 verified reconciliation、progress 不伪造不丢弃、verified 工作不重复计分、单独 exhaustion/dispatch 不完成 Run）成立；completion evidence 1–7 全部由 GoalEvidence 独立满足。
  - **Verification:** 集成测试全绿；frozen 13 切片回归全绿；`dotnet build`/test；guards；consistency；strict OpenSpec validation；production-delta audit = zero。
  - **Deferred Boundary:** formal proof、独立验收、任何 production 行为、S1/S2/S3。
  - **Return Contract:** `TASK_RESULT`；若暴露新 Reality Distinction 或需要 production 变更，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` / `EXTRACT_BOUNDED_CANDIDATE`。完成后停止并由 H4-3 重读 repository truth。

## 3. Formal SC-S0-CAPSTONE-001 Proof

- [x] 3.1 **Prove integration completion evidence, replay, and boundary**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-S0-CAPSTONE-001
  - **Goal:** 建立 formal end-to-end Capstone evidence：Required Assertions 1–12 全部成立（含 equal-input replay 至 equal progress/ActionHistory/Observations/journal/Trace/GoalEvidence/final RunState）；完成证据 1–7 逐条断言；zero production delta 审计；新 Reality Distinction 的 stop/extract 路径以 fixture 表达。
  - **Required Semantic:** 完成 = GoalEvidence 七项合取；单独 exhaustion/dispatch/Recovery/viewport/局部完成均不构成完成；replay 全量相等。
  - **Approved Production Purchase:** zero。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` formal tests/replay；必要时最小 test-side helpers；Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`（除非 explicit 实现回归阻塞已批准 proof，此时先回 Gate）；任何生产 artifact/state；其他 Scenario/Capstone 扩展。
  - **Required Assertions:** Scenario Required Assertions 1–12 全部成立；frozen 13 切片回归全部通过；production delta = 0。
  - **Verification:** targeted formal/replay tests；全量 build/test；Architecture Guards；consistency；strict OpenSpec validation。
  - **Deferred Boundary:** 独立验收、S0_GRADUATED 声明、Phase 完成。
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。
  - **Notes (2026-08-09, evidence repair):** `CapstoneSettingsFormalProofTests.cs` 的 Assertions 1–12 全绿；full suite 411/411。修复后的 Recovery 证明非空：Recovery 边界的 `CompletedSiblingEvidence` 非空，Network 历史完成于 seq 18；唯一 external drift 在 seq 20（Expected=19/Observed=20）后，verified Agent Recovery 取得 fresh recovered root seq 21，CAND-009 criterion 实际求值为 `true`，Network revalidation 为 18→21，Network 零 redispatch，未解决的 Display/System 继续。最终 progress `{ Network=21, Display=27, System=34 }`，root viewport 35→36，final GoalEvidence 于 seq 36 诚实完成七项合取。Assertion 12 仍以 edge schedule `S0DisturbanceSchedule(8, WifiPrefsScreen, 9)` 表达：run 停在 frozen Select-failure 词汇（`目标「Dismiss」…无匹配候选`），exactly one bounded Candidate 注册 sketch（`CapstoneCandidateExtraction`，PreApproved=false），无静默吸收。Assertion 11 的 positive/negative/edge replay Theory 及 unequal-inputs negative 仍证明 replay 合取 load-bearing。production manifest/hash audit：31 files，pre/post SHA-256 `50644a4326ffe6a95f3c68c0153f35dc5c376633b8d156c6372c4f44b7ba35f4`，equal=true，Actual Production Delta=0；check-consistency ALL PASS；`openspec validate --strict` valid。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent Capstone slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-S0-CAPSTONE-001
  - **Goal:** fresh reload repository truth，独立审计 1.1/2.1/3.1 actual diff、completion evidence 1–7、required assertions 1–12、deterministic replay、zero production delta、frozen 13 切片回归、S0 world boundary（fixture 不编码 production 结论）、无 H4-4/无 S1/S2/S3/无 S0_GRADUATED claim。
  - **Required Semantic:** Capstone 只组合 frozen capabilities；GoalEvidence 唯一完成权威；任何新 Reality Distinction 必须已经 stop + EXTRACT_BOUNDED_CANDIDATE，不得被 2.1/3.1 悄悄吸收。
  - **Approved Production Purchase:** zero（validation 自身 delta = 0）。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict validation；all PASS 后 only mark Task 4.1 complete。
  - **Forbidden Scope:** repair production/tests/spec；accept coder summary instead of fresh evidence；add artifact/state/dependency；change frozen architecture；start 下一 Phase/其它 Scenario。
  - **Required Assertions:** assertions 1–12 all satisfied；evidence 1–7 逐条独立成立；replay 全等；production delta 恰为 zero；frozen 13 切片 PASS；S0 world boundary 无泄漏；deferred capabilities 全部缺席。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-s0-capstone-settings-traversal --strict`；production-delta/ownership/authority/determinism audit。
  - **Deferred Boundary:** 任何 production 购买与 frozen 语义变更均缺席；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 可创建 Capstone closeout（`S0_GRADUATED` 需额外 authority），然后停止。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `docs/system/scenarios/06-s0-capstone-settings-traversal.md` | Capstone registration（权威语义） |
| `docs/system/scenarios/s0-roadmap-coverage.md` | Roadmap §5 matrix、§8 `S0_BASELINE_READY`、§11 boundary |
| `docs/decisions/s0-baseline-ready-capstone-authorization.md` | HUMAN gate decision（授权） |
| `tests/UniClaw.Runtime.Tests/Scenario/` | Capstone fixtures/harness（本 change） |
