# Phase 2.5 Graduation Review — Result

DocumentType: `GRADUATION_REVIEW`
Decision: `PROJECT_LEADER_UNIAGENT_EMULATOR_PHASE25_GRADUATION_REVIEW_RESULT`
Change: `openspec/changes/uniagent-emulator-validation-harness/`
Date: 2026-08-26
Reviewer: DSH coding agent (Sol role) — 独立核验，未信任 tasks 勾选、实现自述或先前建议
Authority: Runtime Architecture Contract I-1..I-14 不变；本评审不新增架构权威。

---

## 1. Executive Decision

**GRADUATE（建议）** —— `PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`。
独立核验重建了 Spec → Symbol → Test → Evidence 全链映射并重新执行了全部机器验证；
评审过程中发现并修复了两处文档级缺陷（checkbox 状态漂移、设备层措辞漂移），
未发现任何证据缺口需要补推论。

## 2. Graduation Claim（冻结声明）

**PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED**，含义（六条）：
1. RuntimeAgent 可以被外部抽象 Strategy 驱动（run.strategy.start + 冻结只读面）。
2. Runtime 单 Run 内保持执行与完成 authority（GoalEvidence + 终态路径不变）。
3. 上层 Agent 不需要 Run 内 UI 微操（Tier A/B 双层 zero-intervention 证据）。
4. Runtime failure 可以 evidence-backed bounded fail-closed（S2 真实异常处置）。
5. Runtime Result 可以作为上层下一 Run Strategy 决策输入（S3 插入点在 Runtime 边界外）。
6. Real Emulator 上完整 S1/S2/S3 buyer chain 已验证（Tier C 物理层 WAIVED_BY_HUMAN）。

## 3. Spec → Implementation → Test → Evidence Mapping（独立重建）

| Spec Requirement | Symbol | Test | Executed Evidence |
|---|---|---|---|
| 工具性非能力 | 项目拓扑 + HarnessSourceShapeGuardTests(5) | guard e (零反向引用) | 61/61 arch 绿 |
| 冻结表面 byte-identical | 7 文件 SHA-256 常量 | guard d | 绿（Runtime diff 为 Phase-2 在途，mtime 08-25 先于本 change 全部工作） |
| Emulator driver 边界（3 场景） | StrategyDirectiveValidator/EmulatorDriver/EmulatorCallLog | EmulatorDriverTests（含 7 类禁载 Theory） | 56/56 harness 绿 |
| 三场景入口 | SettingsExploration/ExceptionDisposition/CrossRunAdaptationScenario | ScenarioRunnerTests + ExceptionDispositionScenarioTests + ViewportNormalizationEquivalenceTests(A–E) | 同上 |
| Collector 真相性（含 UnavailablePartial） | ResultField 分类三值 + Wire/TierAReadSurface | ResultCollectorTests（分类走查/digest 稳定/不可解析如实） | 同上 |
| Tier-scoped coverage | CompileExplorationLedgerView 只读调用 | TierA digest 稳定 + wire-tier Unavailable 断言 | 同上 |
| 边界验证派生 | BoundaryVerifier（4 禁止证明） | BoundaryVerifierTests 8/8 | 同上 |
| Gate 可执行 G1–G4 | ValidationGates | 强制失败不弱化用例 | 同上 |
| 失败分类 | FailureOwner×8 + ProtocolFailureClassifier（构造守卫） | 9 分类用例 | 同上 |

**Tier B 执行证据**（docs/work/active/，14 个 tierb-* 文件）：
- S1：`tierb-s1-8of8-PASS.json` — accepted=true（run-1）；calls=[run.strategy.start×1,
  Outcome=Accepted]；events 51 条含 27×ActionDispatched（真实 ADB tap）；**GoalEvidenceProduced
  idx=49 < RunCompleted idx=50**；terminal Completed；Scenario Acceptance observedCoverage=8/8
  pass=true（独立读 fixture 外部态）；runlog + 截图在库。
- S2：`tierb-s2-postS1remediation.json` + runlog 含 `anomaly injected: fixture force-stop`；
  单次 start；Runtime 自主 Failed，理由明确（viewport exhaustion Unresolved——force-stop 后
  的真实后果），零 redispatch、零介入、无伪造 recovery。
- S3：`tierb-s3-postS1remediation.json` — run-1/run-2 distinct RunId，双 Completed，
  calls 恰 2，adaptationFact=evh3-51-events 仅入 Run 2 strategyId。

## 4. S1 Independent Verdict：**PASS_PHYSICAL_8_OF_8 → Tier B 层 PASS @ Real Emulator 8/8**

§3 十二项逐条独立确认（不信任自述）：accepted/单 Run（evidence 文件字段复核）；零微操
（call log 恰 1 start）；真实 UI 执行（27 dispatches + 设备终态截图）；viewport/等价/不误合并
（ViewportNormalizationEquivalenceTests A–D 独立复跑绿，且 E 断言重复帧仍 fail-closed）；
无假阳性 unresolved（修复链每步 FDP 定位在案）；8/8（JSON + uiautomator 双源）；
GoalEvidence→RunCompleted 顺序（事件索引独立计算 49<50）；Scenario PASS；Runtime 零修改
（git diff 7 文件 mtime 全部先于本 change；guarded SHA 基线后无 harness 改动）。
**双层判定成立**：RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS 在 3/8 证据
（`tierb-s1-3of8-hardened-proof.json`：Runtime Failed + Scenario FAIL_INSUFFICIENT…）与
8/8 证据两端都被执行过。

## 5. S2 Independent Verdict：**PASS_BOUNDED_FAIL_CLOSED**（声明边界准确）

可声明：Runtime Autonomous Exception Disposition——已有能力内自主处理；无法安全继续时
bounded fail-closed（理由明确、evidence-backed、零 redispatch storm、零介入）。
不可声明（且现有证据不支持）：strategy-path 通用 Trap/Recovery（Agent.OpenWorld.cs 零
trap 机制的静态事实在案）；可恢复所有异常；fail-closed=recovery success（S2 结果逐字
记录 STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5）。

## 6. S3 Independent Verdict：**PASS**（插入点证明成立）

两 StrategyId/两 RunId、恰 2 start、R1 影响仅限 Run 2 strategyId（payload diff=strategyId
only）、R1 不入 Runtime mutable state（Run-1 证据重读不变的操作性断言）、无 mid-run
mutation、无 Memory 实现、无 Runtime 隐藏跨 Run 状态。**足以证明**未来 Memory/UniAgent 的
合理插入点位于 Runtime 边界外（Historical Result → Strategy）。

## 7. Tier C Waiver Audit：**通过（含措辞修正）**

所有最终文档现明确：Tier C Physical Device = WAIVED_BY_HUMAN；原因 = 物理设备不存在，
Environment blocked 于被测代码之前（adb + ioreg 双证据）。未发现把 Tier B 描述为
Physical validation 的结论性声明。

## 8. Documentation Claim Drift：**发现 2 处，均已修正**

1. **Checkbox 状态漂移**（重要发现）：候选状态称 "20/20 complete"，实际 3/30——回填脚本
   只写了 Evidence 行未勾选框。按「Evidence 行存在且属实」逐项核对后修复至 30/30，并在
   tasks.md 记录 `Checkbox-state repair` 注记（内容与 Evidence 零改动）。这正是"不信任
   tasks 勾选"原则的价值实证。
2. **设备层措辞漂移**：4 份 Tier B 结果文档使用"真机/真实设备"表述 Tier B（Real Emulator）
   结果——按 §6 标准构成 DOCUMENTATION CLAIM DRIFT。已全部修正为"Real Emulator（非物理
   设备）"，Tier C 文档同步澄清。修正仅措辞，无证据杜撰。

## 9. Authority / Contract Audit：**干净**

Phase 2.5 全程零新增：Runtime API / wire / Strategy Contract mutation / Agent-FSM-Traversal-
GoalEvidence authority / Memory / Planner / UniAgent / dynamic depth / multi-Run 编排。
Harness wire 面 = 恰好 {run.strategy.start, run.snapshot.get, run.events.after/drain,
run.trap.get, evidence.get}（源扫描确认闭合集合）。Harness 定位仍是 client-side
validation tool（源形态守护 5 项持续生效）。

## 10. Regression Verification（本轮全部独立重跑）

build 0 err · harness 56/56 · deterministic **2109/2109** + Semantic 32/32 · arch guards
61/61 · consistency ALL PASS · diff-check PASS · strict PASS。真实环境调试未污染基线。

## 11. Exact Graduated Capability Boundary

即 §2 六条；来源限定：策略词汇=冻结 StrategyDirective 契约；读面=冻结五方法；Tier B 证据
= Real Emulator（scroll-test AVD / API 15 级）；异常处置=现有能力语义（非新增 recovery）。

## 12. Explicit Non-Claims

不包含：UniAgent 实现；Planner；Memory；通用动态规划；universal recovery；**physical-device
validation**（WAIVED）；universal Android traversal；Runtime cross-run intelligence；
strategy-path trap/recovery capability。

## 13. Phase 2.5 Lifecycle Recommendation

**GRADUATE → Phase25Status: GRADUATED / ACTIVE / NOT_ARCHIVED**

## 14. Archive Recommendation

暂缓（与既有决定一致）：建议在毕业结论被 Human 确认后，与 Phase-2 在途工作树收尾
（同根 7 文件 diff 的归属处理）一并执行归档，避免两次快照 churn。

## 15. Phase 3 Memory Compatibility

毕业后建议：`Phase3Memory: READY_FOR_SEPARATE_HUMAN_GATE`（非 AUTO_RESUME）。
对在库草案 `uniagent-local-exploration-memory` 的四项核对：Buyer（UniAgent 侧 pre-Plan
advisory）未变；Owner（UniAgent-local，非 Runtime）未变；Scope（provenance-bearing 历史
知识咨询）未变；pre-Run advisory 假设——S3 已实证该插入点（Result → 下一 Strategy）
位于 Runtime 外且无需 Runtime 参与而被**加强**。**Phase 3 Memory draft remains
semantically compatible**（且获得 S3 实证支撑）。

## 16. Remaining Human Gates

1. 确认毕业结论（本评审建议 GRADUATE）。
2. Archive 时机（建议与 Phase-2 工作树收尾合并处理）。
3. Phase 3 Memory 的独立 Human Gate（草案兼容性已核对）。

## 17. AuthorityDelta

`NONE`（评审只读 + 文档措辞修正 + checkbox 状态修复，均零架构语义）。

## 18. ArchitectureDelta

`NONE`（零代码改动；文档级修正 5 文件）。

---

### 最终问题的回答

**"现有证据是否足以正式冻结——未来 UniAgent 可以把 RuntimeAgent 当作独立执行底座，
而不需要在 Run 内接管它？"**

**是。** 三层证据（确定性 56 测试、Real Emulator 三场景含真实 8/8 与真实异常处置、
跨 Run 插入点实证）在冻结契约（零 Runtime 改动、SHA 守护、wire 闭合集）下互相印证，
且反例路径（3/8、异常、重复帧）都被证明走 bounded fail-closed 而非需要接管。
唯一未覆盖的物理设备层已被 Human 显式豁免并如实记档。
