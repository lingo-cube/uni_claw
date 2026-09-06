# Plan — C2E-002 Control-to-Effect Belief Closed Loop Vertical Slice

Change State: `changes/C2E-002/state.md`（resolved → 本计划为 PLAN 态输出）
Route: **Direct**（语义密度高、不变量 21-27/32-34 约束密集、延续 E2B-001 已定
约定与代码风格；委派打包成本 > 收益）
Date: 2026-09-07

## 已决实现入口决策（PLAN 态补齐，兑现 state.md 两项 Residual Risks）

| # | 决策 | 依据 |
|---|---|---|
| P1 | act-intent 与 Tactical Hypothesis **零附着**：hypothesis 只存在于 ControlLoop 内部 log（可废弃），intent 不携带任何 hypothesis 引用（连 id 都不带） | Residual Risk 1；验收 7 锁死不外溢 |
| P2 | post-action observation 时机契约 = **dispatch 产生 receipt 后、下一 control cycle 前**，由 scripted observation provider 构造；producer 前缀 `effect.boundary`、lineage 携 `dispatch:<receiptId>`（D7） | Residual Risk 2 |
| P3 | Attempt evidence 回流：`kernel.Act` 在 receipt 后自动 `ExportAttemptEvidence` → 走既有 `Process`（admission→relevance→reconciliation）；claim subject `attempt.<target>` 在 relevance scope 外 → admitted 但零 revision（验收 5/8 同时成立） | Scope「Effect Evidence 回流」 |
| P4 | blind-retry 记忆 = Assurance action-local 状态（target → 失败时 revision number）；act 可接受当且仅当 current revision number **大于**失败时 revision number | 验收 10；§15 action-local 只在 judgment lifecycle 内 |
| P5 | `kernel.Act` 只处理 ControlLoop **已签发**的 intent（`IsIssued` fail-closed 检查）；forged intent 无路径进 dispatch | 验收 2「必经四者」在组合边执法 |
| P6 | 六 L2 组合缝 = ctor 注入（E2B 两 L2 参数保持可选默认，E2B 测试零改动）；未注入 L2 时对应组合 API fail-closed 抛出 | Scope「两 L2 → 六 L2 组合缝」 |

## 垂直切片（tracer bullet）

```text
行为(10 条验收) → 模型(Run/Control/Assurance/Effects 四组最小类型)
              → 运行时(UniKernel 六 L2 组合缝) → 测试(xUnit 10 用例)
              → 证据(测试输出落 evidence/)
```

### 1. Scaffold（`src/UniClaw.Kernel/` 新增四目录，E2B 两目录零改动）

```text
src/UniClaw.Kernel/
├── Run/                        # Run Model — Canonical Run State Recording Authority (§13)
│   ├── ExecutionContract.cs    #   contract 候选 + ContractAdmission/ContractCheck
│   ├── ExecutionContractView.cs#   immutable canonical view（acceptance 1/9）
│   ├── RunState.cs             #   ObjectiveState / ProofObligationState(占位) / ProgressState
│   └── RunModel.cs             #   AdmitContract / RecordCycle / RecordAction（typed transition）
├── Control/                    # Control Loop — sole Control Intent Authority (§14)
│   ├── ControlIntent.cs        #   kind=Observe|Act|Recovery + BasisRevisionId
│   ├── TacticalHypothesis.cs   #   disposable，仅 ControlLoop 内部
│   ├── ControlPolicy.cs        #   IControlPolicy 注入缝 (D9) + ControlDecision + ControlState
│   └── ControlLoop.cs          #   SelectIntent / NoteDispatchOutcome / recovery 边 (D10)
├── Assurance/                  # sole Runtime Assurance Judgment Authority (§15, action-local)
│   ├── AssuranceJudgment.cs    #   judgment + 逐项 check 留痕（immutable，§17）
│   └── RuntimeAssurance.cs     #   Judge / NoteOutcome（blind-retry 记忆）
├── Effects/                    # Effect Boundary — sole Binding + Delivery Authority (§16)
│   ├── TargetBinding.cs        #   CandidateBinding / CanonicalBinding / BindingDecision（D8 三态）
│   ├── EffectDispatch.cs       #   IEffectDriver(scripted) / GateDecision / DispatchOutcome / EffectReceipt
│   └── EffectBoundary.cs       #   Bind / Dispatch(Gate 只执法) / IsBindingValid(派生) / ExportAttemptEvidence
└── UniKernel.cs                #   扩展：AdmitContract / SelectIntent / Act；Process 面零改动
tests/UniClaw.Kernel.Tests/ControlToEffectTests.cs   # 10 用例 ← 验收 1..10 一一对应
```

### 2. 关键语义映射（验收 → 机制）

| # | 验收 | 机制 |
|---|---|---|
| 1 | 契约准入 fail-closed | `RunModel.AdmitContract`：version/objective/scope/allowed-effects/proof-criteria 逐项检查；任一失败 → 拒绝 + 零 Run State（无 View、无 State、History 空） |
| 2 | 四产出独立留痕、次序不可合并 | intent/judgment/binding/receipt 各在**四个 Owner 的 append-only log**（IntentId 关联）；短路证明次序：judgment 拒 → binding/receipt log 零条目；binding 拒 → receipt 零条目；`IsIssued` 挡 forged intent |
| 3 | Candidate ≠ Canonical | `Bind` 三态拒绝（stale-revision / ambiguous / unknown-target，D8）→ 无 canonical 即无 dispatch；canonical 绑定 current revision（id 相等）；失效为派生判定 `IsBindingValid`（复用 Slice 模式） |
| 4 | Gate 只执法 | `Dispatch(binding, judgment, current)`：非 admissible / intent 不匹配 / binding 失效 → 拒绝，无 receipt；无任何重判或改 target 路径 |
| 5 | Attempt ≠ Effect | receipt 留痕 + attempt evidence 经 `Process` admitted（subject `attempt.*` → irrelevant → **零 revision**，WorldState[target] 不变）；post-action observation（producer `effect.boundary` 前缀 + lineage dispatch 引用）→ 新 revision 才携带新值 |
| 6 | Run State 边界 | `RecordCycle` typed transition：Progress.Cycles 随 SelectIntent 推进；反射断言 RunState 对象图零 Assurance 类型 |
| 7 | plan/hypothesis 负向封闭 | 反射遍及 `JudgeRelevance/Reconcile/Judge/Bind/Dispatch` 参数类型图（含递归属性）→ 无 TacticalHypothesis/Plan 类型 |
| 8 | E2B 权威零穿透 | ledger/world 变化只经 `Process`（kernel.Act 的 attempt 回流也走它）；admission log ↔ 提交观察 1:1、每 revision 必有 relevance 留痕、basis 全部可溯源到 canonical record |
| 9 | Contract View immutable | 同 version 重复 admit → 同一 View 实例、History 不增长；不同 version → fail-closed 拒绝（显式取代不在本片） |
| 10 | Recovery 重新入环 | dispatch 失败 → ControlLoop 记 pending recovery（target+revision）→ 同 revision 下 SelectIntent 必发 Recovery intent；Assurance blind-retry 检查拒同 target 无新 revision 的 act（P4）；re-observe → 新 revision → 可再 act |

### 3. TDD 顺序（A6）

RED（桩 + 测试先行；类型表面可编译、行为体 `NotImplementedException`）→
最小实现 → GREEN → 仅结构性 refactor。测试验证行为不验证实现细节
（Constraints）；E2B 既有 8 用例保持零改动、全 GREEN。

### 4. 验证与收口

- `dotnet test` 18/18 GREEN（E2B 8 + C2E 10）→ 输出存
  `evidence/2026-09-07-c2e-002-deterministic.md`
- REVIEW（fresh SubAgent：意图对齐 / 范围 / 不变量 21-27、32-34 逐条 /
  意外改动）→ 发现即回 IMPLEMENT
- 回填 state.md verification 四元组 + Status Log → VERIFY → 单个
  `feat(kernel)` commit（E2B 惯例）→ CLOSED

## Out-of-plan（不做）

Outcome Proof / Proof Obligation discharge / Effect Verification / Runtime
Outcome emission（→ OUT-003）、UniAgent L1 壳、Memory、Capability Plane
框架化、FSM、持久化、多 Goal/多 Run、真机环境、E2B 既有类型任何语义改动。
