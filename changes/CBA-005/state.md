# CBA-005 — Canonical-Binding Assurance Cutover

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 4d949839

## Intent

**WHAT**: 把 Act pipeline 从当前 realization `Judge(intent, candidate) →
Bind → Gate` 迁移到目标协议序 `Bind → Judge(canonical binding) → Gate`
（ADR-0009）：CandidateBinding 退出 Assurance 输入签名；AssuranceJudgment
绑定 IntentId + BindingId + RevisionId 三元组（correlation key）；Gate 对
三元组 fail-closed；补协议压力测试场景 13（两 case）。

**WHY**: 协议基线 v0.1（docs/architecture/protocols/）§5 裁决的首个
implementation change——授权必须针对最终真正执行的 target；当前实现序是
ADR-0009 记录的 known deviation，本 change 将其收敛到目标协议。

## Scope

- `UniKernel.Act` 组合序迁移（Bind → Judge → Gate → Dispatch）
- `RuntimeAssurance.Judge` 签名：`(ControlIntent, CanonicalBinding,
  ExecutionContractView, WorldBeliefRevision)`
- `AssuranceJudgment` 增 BindingId + RevisionId（RevisionId = Binding
  .RevisionId）
- `EffectBoundary.Dispatch` 增 `judgment-binding-match` /
  `judgment-revision-match` 两条 gate 检查
- `EffectBoundary.Bind` 增 `no-candidate` 拒绝路径（candidate 缺失 =
  显式 decision，非异常）
- 测试迁移：C2E Accepted2/3/4/10、OUT 两处 ctor 适配；新增
  PressureScenario13 独立用例（两 case）

## Out of Scope（红线）

- ingress 显式 kind 字段（P2 known gap）
- freshness 双维度评估（P4 known gap）
- P5 known leak / P11 potential overexposure 曝射面收缩
- Memory、F2（L2 直连执法集中组合缝）、legacy cutover
- 协议基线文档改动（本 change 是执行而非修订）

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | 目标序 Bind→Judge→Gate；锁**语义阶段**（顺序不可颠倒/绕过、三阶段 Authority/decision 可区分、任一阶段拒绝 → 无 effect 副作用）不锁物理调用形状 | ADR-0009 + 立项 Q1 |
| D2 | candidate 缺失 = 第四种 Bind 拒绝语义 `no-candidate`（显式 record，无异常路径） | 立项 Q2 |
| D3 | Judge 检查集：删恒真检查（代码原名 `binding-sufficient`，立项拟名 binding-present）；新增 `binding-intent-correlation`、`binding-revision-currentness`；`intent-is-act` / `effect-class-allowed` / `effect-class-not-forbidden` / `target-declared` / intent basis currentness（原 `freshness` 检查更名）/ `no-unresolved-conflict` / `no-blind-retry` 全部保留；null binding = API contract violation，不产生 judgment | 立项 Q3 + Review F1 对齐 |
| D4 | `Judgment.RevisionId = Binding.RevisionId`（三元组语义 = "审的是这个 binding"）；与 current 相符由 check 验证 | 立项 Q3 |
| D5 | Gate 新增两条 correlation 检查，落地词汇 `judgment-binding-id-mismatch` / `judgment-revision-mismatch`（立项拟名 `judgment-binding-match` / `judgment-revision-match`，Review F1 对齐为实际落地词汇——拒绝 reason 词汇不锁属 realization）；保留 `binding-stale` 传递锚定 current | 立项 Q4 + Review F1 |
| D6 | judgment 拒绝 → Gate **执行 enforcement 并拒绝**（非跳过）；binding validity 不因此消失（validity ≠ authorization） | 立项 Q5 |
| D7 | 场景 13 独立用例、两 case 分别独立断言（wrong BindingId / wrong RevisionId 不同时改） | 立项 Q5 |
| D8 | Route=Direct——不可拆的单一语义切片（Canonicalize→Judge→Gate），高内聚 invariant 一起改；非执行便利；depth=decision-heavy；TDD RED 先行 | 立项 Q6 |

## Acceptance（10 条，立项 grill 定稿）

1. **语义阶段序**：Bind → Judge → Gate 语义顺序不可颠倒、不可绕过；
   三阶段 Authority / decision 必须可区分；任一阶段拒绝 → 无 dispatch /
   receipt / belief / run-progress 副作用（decision record 留痕不属于
   副作用）
2. **Candidate 退出**：`RuntimeAssurance` 全部 public 输入签名无
   `CandidateBinding`（反射负向断言）
3. **Judgment 三元组**：IntentId + BindingId + RevisionId（RevisionId =
   Binding.RevisionId）；Judge 侧 `binding-intent-correlation`
   fail-closed；null binding = API contract violation，不产生 judgment
4. **Gate 三元组 fail-closed**（场景 13，独立用例两 case）：同 IntentId +
   异 BindingId → 拒绝；同 IntentId + 异 RevisionId → 拒绝；各零
   dispatch 零 receipt
5. **Bind 拒绝短路**：stale / ambiguous / unknown-target / no-candidate →
   BindingDecision 留痕，无 CanonicalBinding、无 AssuranceJudgment、
   无 Gate
6. **C2E 上层 invariants 保持**：Candidate≠Canonical、Gate 只执法
   （judgment 拒绝时执行 enforcement 并拒绝，binding validity 不因此
   消失）、Attempt≠Effect、E2B 零穿透、recovery 无 blind retry
7. **E2B 冻结面**：`src/UniClaw.Kernel/Evidence/`、`World/` 源文件 +
   E2B 8 用例零改动 GREEN（git diff EMPTY）
8. **OUT 18 + GEV 17 回归 GREEN**：仅 AssuranceJudgment ctor 适配，
   行为断言不变
9. **no-blind-retry + revision-currentness 行为保持**（不使用 freshness
   措辞，避免与 P4 known gap 混淆）
10. **全量 GREEN**：迁移/替换用例在本文档逐一留痕，无静默删除

## Constraints

- ADR-0009（目标序 + validity ≠ authorization）与协议基线 P9/P10/P13
  语义为直接权威
- Target v0.1 不变量 21-27、32-34 不可违反；E2B/C2E 已定型语义零改动
  （C2E 验收语义不变，用例允许迁移/替换——保护架构事实与验收语义，
  不保护旧测试本身）
- 测试验证行为，不验证实现细节

## Verification

```yaml
level: DETERMINISTIC   # 纯内存，无 IO / 真机依赖
method: >
  RED（目标签名桩 + 迁移/新增用例先行，关键场景可见失败）→ GREEN（最小
  实现：Act 重排 / Judge 签名与检查集 / Judgment 三元组 / Gate 两检查 /
  Bind no-candidate）→ REVIEW（fresh SubAgent：三元组 invariant / 短路
  语义 / C2E 上层 invariants / E2B 冻结 / 意外改动）→ VERIFY（验收 10
  条逐条 + 全量 dotnet test + E2B no-drift diff + 迁移台账核对）
expected: >
  验收 10 条全 GREEN；全量测试 GREEN（总数 ≥ 基线语义覆盖）；E2B 冻结面
  diff EMPTY；场景 13 两 case 各自独立证明两条 correlation 生效
actual: >
  2026-09-09 dotnet test（UniClaw.Kernel.slnx；SDK 10.0.400；net10.0）：
  RED 失败 22 / 通过 34（Act/Judge 依赖场景可见失败）→ GREEN 失败 0 /
  通过 56（Kernel 39 = E2B 8 + C2E 10 + 新增 3 + OUT 18；Agent 17）→
  Review 修复批后复跑 56/56。E2B 冻结面 diff 0 行；改动面 = plan After
  表 7 文件零意外；红线零触碰。验收 1-10 逐条证明映射见 evidence
evidence: evidence/2026-09-09-cba-005-deterministic.md
```

## Assumptions

- 单线程内存模型（引用等同 / append-only log 足够，§22 开放项延续）
- C2E 迁移点限于 ENTRY 实测的 6 处（Accepted2/3/4/10 + OUT ×2）；无其他
  编译依赖面（grep 实测 E2B/GEV 零接触）
- GateDecision 在 judgment 拒绝路径新增留痕条目属语义增强（执法留痕），
  无消费者受影响（ActResult.Gate 语义本就可空→现该路径非空）

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| 保留 Judge(candidate) 旧序 | ADR-0009 裁决否决（授权须针对最终执行 target） |
| judgment 拒绝时跳过 Gate（旧实现行为） | 立项 Q5 否决——C2E 验收 4 原文即"Effect Gate 拒绝 dispatch"；跳过使组合面丢失"Gate 只执法"证明 |
| 保留 `binding-present` 恒真检查 | 立项 Q3 否决——无业务价值，Assurance 承担无意义职责 |
| Act 签名 candidate 改 non-null | 立项 Q2 否决——把可判定状态降级为类型约束，破坏 C2E 调用形状 |

## Owner / Authority Impact

- 无 Owner 迁移、无 Authority 变更：Effect Boundary canonicalize /
  Assurance judge / Gate enforce 三职责不变，仅组合序与 correlation
  表达迁移（这正是 D8 Direct 路由的依据）

## ADR Refs

- docs/adr/0009-assurance-judges-canonical-binding.md（本 change 的裁决
  来源；迁移完成后其 known deviation 条款自然闭合）
- docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md
  §5（first change 定义）、§4-13（场景 13）

## Residual Risks

- `ActResult.Judgment` 由恒非空变为可空（Bind 拒绝路径）——当前无生产
  消费者，测试适配即止；未来组合缝消费者需感知该 absence 语义
- F2（非 terminal 直连的 issuance/provenance 校验）仍挂起——本 change
  不扩大直连面

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-09 | →resolved | ENTRY/UNDERSTAND 核验（base 4d949839、53/53 基线、6 处测试迁移点 grep 实测）+ 立项 grill 两轮（6 问 + 4 修正）定稿 acceptance 10 / D1-D8 |
| 2026-09-09 | resolved→planned | PLAN 落盘 plans/2026-09-09-cba-005-canonical-binding-assurance-cutover.md；Route: Direct（D8） |
| 2026-09-09 | planned→implemented | TDD RED（22 失败：Act/Judge stub + 迁移/新增用例先行，GoalEval17 真实链可见失败）→ GREEN（56/56：Kernel 39 + Agent 17；Judge 检查集 / 三元组 / Gate 两检查 / Bind no-candidate / Act 重排）；E2B 冻结面 diff EMPTY；改动面 = plan After 表 7 文件零意外 |
| 2026-09-09 | implemented→reviewed | 三重独立审查：六轴 fresh SubAgent APPROVE（A1-A6 全 PASS，F1/F2/F3 nit）；/code-review Standards 轴 0 硬违规（术语/结构对齐且改善基线）；Spec 轴 0 scope creep（(a)1/(a)2 收尾建议） |
| 2026-09-09 | reviewed→implemented | 修复批：F1 state.md D3/D5 词汇对齐实际落地；F2 Bind doc 四态；Standards doc 澄清（freshness vs currentness）；补验收 3 null-throw 断言；验收 2 反射负向扩面至全部 DeclaredOnly public 方法；复跑 56/56 GREEN |
| 2026-09-09 | implemented→verified | 验收 10 条逐条对照 evidence 全 GREEN；E2B 冻结面 diff 0 行；四元组 actual/evidence 完整；台账核对 8 处全落盘（53→56，无删除） |
| 2026-09-09 | verified→closed | 范围完成 + acceptance 被证明 + 无未授权改动；A9 文档同步：协议基线 P9/P10/P13 状态标注由 known deviation 更新为 cutover 已闭合（仅状态标注，语义零改动）+ ADR-0009 deviation 闭合注记；无阻塞 Human Decision |
