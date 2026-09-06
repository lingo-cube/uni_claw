# CBA-005 — Canonical-Binding Assurance Cutover · DETERMINISTIC Evidence

> changes/CBA-005/state.md · 2026-09-09 · base 4d949839 · 工作树未提交 diff（7 文件）
> 复现命令：`dotnet test UniClaw.Kernel.slnx`

## 四元组

```yaml
level: DETERMINISTIC
method: >
  TDD：RED（Act/Judge stub + 迁移与新增用例先行）→ GREEN（最小实现：
  Act 重排 Bind→Judge→Gate / Judge(canonical binding) 九检查集 / 三元组
  record / Gate 两条 correlation 检查 / Bind 第四态 no-candidate）→
  REVIEW（fresh SubAgent 六轴 + /code-review 双轴 Standards/Spec）→
  修复批（Review F1 文档对齐、F2 doc 注释、Standards 术语澄清、验收 3
  null-throw 断言、验收 2 反射负向扩面至全部 DeclaredOnly public 方法）→
  复跑全量 + E2B 冻结面 no-drift
expected: >
  验收 10 条全 GREEN；全量 56/56（Kernel 39 = E2B 8 + C2E 10 + 新增 3 +
  OUT 18；Agent 17 零改动）；E2B 冻结面 diff EMPTY；场景 13 两 case 各自
  独立证明 BindingId / RevisionId 两条 correlation
actual: >
  RED：失败 22 / 通过 34（Act/Judge 依赖场景全部可见失败，含 GoalEval17
  真实 emission 链）。GREEN：失败 0 / 通过 56（Kernel 39 + Agent 17）。
  Review 修复后复跑：失败 0 / 通过 56。E2B 冻结面（Evidence/ World/
  EvidenceToBeliefTests.cs）diff 0 行。改动面恰为 plan After 表 7 文件，
  红线（ingress kind / freshness / P5 / P11 / Memory / F2 / legacy /
  协议语义修订）零触碰
evidence: 本文件
```

## 验收 ↔ 证明映射

| # | 验收 | 证明 |
|---|---|---|
| 1 | 语义阶段序（Bind→Judge→Gate 不可颠倒；拒绝 → 零 effect 副作用） | Accepted2（四 log 留痕 + 新序短路）；Accepted3 四态 `Judgment==null`；UniKernel.cs Act 重排 |
| 2 | Candidate 退出 Assurance 全部 public 输入签名 | Accepted7 反射负向（DeclaredOnly 全方法含泛型参数，Review 后扩面） |
| 3 | 三元组 + 产者侧 correlation + null=API violation | Judge_RejectsCanonicalBindingOfDifferentIntent（`binding-intent-correlation` 拒绝 + 三元组记被审 binding + ArgumentNullException 断言） |
| 4 | Gate 三元组 fail-closed（场景 13 两 case） | PressureScenario13_WrongBindingId（`judgment-binding-id-mismatch`）/_WrongRevisionId（`judgment-revision-mismatch`），各零 dispatch 零 receipt |
| 5 | Bind 四态短路 | Accepted3（stale/ambiguous/unknown-target/no-candidate，均无 judgment 无 gate） |
| 6 | C2E 上层 invariants（含 Gate 只执法 + validity≠authorization） | Accepted2 迁移（binding 存在 + Gate not-authorized + `IsBindingValid` 仍 true）；Accepted4/5/8/10 |
| 7 | E2B 冻结面 | diff 0 行；E2B 8 用例零改动 GREEN |
| 8 | OUT 18 + GEV 17 回归 | TerminalOutcomeTests 仅 ctor 适配（行为断言不变）18 GREEN；Agent 17 GREEN |
| 9 | no-blind-retry + revision-currentness 行为保持 | Accepted10 迁移（Bind→Judge 直调拒 `no-blind-retry`，零二次 dispatch）；`freshness` 检查更名 `intent-basis-currentness` |
| 10 | 全量 GREEN + 迁移台账无静默删除 | 56/56；台账：迁移 Accepted2/3/4/7/10、适配 OUT ×2、新增 3 facts（Scenario13 ×2 + 产者侧 correlation）、零改动 E2B 8 + GEV 17 |

## Review 记录

- 六轴（fresh SubAgent）：**APPROVE**——A1 三元组 invariant / A2 短路语义 / A3 C2E 十条 / A4 E2B 冻结 / A5 协议对齐 P9·P10·P13+ADR-0009 / A6 意外改动，全 PASS；F1/F2/F3 均 nit，F1/F2 已修，F3（`.tmp-hf-intake/` 遗留 scratch）与本 change 无关、维持已知项注记
- /code-review Standards 轴：0 硬违规；术语/结构纪律符合并改善基线（`freshness`→`intent-basis-currentness` 纠正了 Freshness 词条 _Avoid_ 违反）；doc 澄清建议已采纳；Data Clumps / Repeated Switches 明确不做（文档化标准覆盖）
- /code-review Spec 轴：0 scope creep；(a)1 D5 词汇漂移已对齐 state.md；(a)2 null-throw 断言已补；(a)3 反射扩面已做；(c) 三项均判定可接受（近恒真检查在直调路径体现约束力；no-candidate 次序为低风险可辩护选择；IsBindingValid 为既有 API）

## 台账核对（验收 10）

8 处迁移/新增全部落盘且在测试文件中可定位；无一删除既有用例：C2E 10 → 10（5 处迁移），OUT 18 → 18（2 处 ctor 适配），E2B 8 → 8，GEV 17 → 17，新增 3。总数 53 → 56。
