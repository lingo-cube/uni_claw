# UniAgent Emulator Validation Harness — Implementation Result

DocumentType: `IMPLEMENTATION_RESULT`
Decision: `PROJECT_LEADER_UNIAGENT_EMULATOR_VALIDATION_HARNESS_IMPLEMENTATION_RESULT`
Change: `openspec/changes/uniagent-emulator-validation-harness/`
Base revision: `e2d8dd44214632f50777992d58fb4fe318ad45f0`（Human Apply 授权记录于 tasks 1.4）
Date: 2026-08-26
Authority: Runtime Architecture Contract I-1..I-14 与 Architecture v1 不变；本结果不新增架构权威。

---

## 1. Implementation Summary

Phase 2.5 验证工具 `src/UniClaw.Runtime.ValidationHarness/` 已实现并通过全部门禁（S2 场景除外，见 §4/§9）。
Runtime 生产源码零修改（仅 `src/UniClaw.Runtime.sln` +14 行注册）；冻结 wire/DTO/协议文件
byte-identical（SHA-256 守护，基线后零 harness 编辑）。

验证目标达成度：**"RuntimeAgent 是否已具备被未来 UniAgent 驱动的条件"——在 S1/S3 覆盖的
能力面上，答案为 YES（证据级）；S2 覆盖的恢复能力面存在协议级缺口，需 Human 裁决。**

## 2. WorkItem Execution Record

| WorkItem | tasks | 结果 | 独立验收 |
|---|---|---|---|
| WI-EVH-001 脚手架+Tier-A 托管 | 2.1–2.4 | ✅ | build 0/0、往返测试绿、生产 diff 空 |
| WI-EVH-002 Emulator Driver | 3.1–3.4 | ✅ | 21 测试 + Leader 修复 attestation 竞态 |
| WI-EVH-003 Result Collector | 4.1–4.4 | ✅ | Leader 接手收尾（3 编译错+部分真相映射+值类型 RawValue）；25 测试 |
| WI-EVH-004 Report+Boundary Verifier | 6.1–6.4 | ✅ | 33 测试；三偏差记录（placeholder 消费、vacuous-positive 显式） |
| WI-EVH-005 Scenario Runner | 5.1–5.4 | S1✅ S3✅ **S2 BLOCKED** | 35 测试；S2 阻塞经 Leader 源码复核确认 |
| WI-EVH-006 分类+守护 | 7.1–7.3 | ✅ | 49 测试；5 源形态守护 + 7 冻结文件 SHA 基线 |

全部 WorkItem 经 `agent_profile_validator.py work-item` 校验 + M0 CLI dispatch record 落盘，
单播执行，绑定 `opencode-go/deepseek-v4-flash/high`。三次 worker 通道死亡由 Leader 在
UniFlow §4 交接豁免下收尾（一次纯读阶段死亡直接重派）。

## 3. Component Mapping（Spec → 符号 → 测试）

| Spec Requirement | 实现符号 | 测试证据 |
|---|---|---|
| 工具性非能力 | 项目拓扑 + `HarnessSourceShapeGuardTests`（零反向引用/零场景 token 白名单外） | guard (c)(e) |
| 冻结表面 byte-identical | 7 文件 SHA-256 常量基线 | guard (d) |
| Emulator driver 边界 | `Emulator/StrategyDirectiveValidator` + `EmulatorDriver` + `EmulatorCallLog` | `EmulatorDriverTests`（合法传输/禁载前拒/DIRECTIVE_REQUIRED/不可变 log） |
| 三场景入口 | `Scenarios/{SettingsExploration,Recovery,CrossRunAdaptation}Scenario` | S1/S3 全断言绿；S2 BLOCKED |
| Collector 真相性 | `Results/{ValidationResult,ResultCollector,IRuntimeReadSurface,WireReadSurface,TierAReadSurface}` | `ResultCollectorTests`（分类走查/wire-tier unavailable/Tier-A digest 稳定/不可解析引用如实记录） |
| 边界验证派生 | `Reporting/BoundaryVerifier` | 4 禁止正向证据 + 3 违规检出 |
| Gate 可执行 | `Reporting/ValidationGates` | G1–G4 报告字段 + 强制失败不弱化 |
| 报告分层 | `Reporting/ValidationReport` | JSON+MD 八节渲染 + unavailable/partial 语义 |
| 失败分类 | `Classification/{FailureOwner,ProtocolFailureClassifier}` | 9 分类测试 + 构造守护（裸 "Runtime failed" 不可表示） |

## 4. Scenario Validation Status

- **S1 Settings Exploration Depth 2：PASS**——单 Run、admission 后零 driver 调用、record-only
  叶零派发、Tier-A ledger 完整（全 scope pending=unresolved=frontier=0）、
  `GoalEvidenceProduced` 先于 `RunCompleted`、G1–G4 全过。
- **S2 Runtime Autonomous Recovery：BLOCKED_FOR_SPEC**
  （`STOPPED_AT_S2_RECOVERY_EVIDENCE_UNOBTAINABLE_ON_STRATEGY_RUN_SURFACES`）。
  静态证据：`run.strategy.start` 仅走 open-world 路径（`StrategyExecution.RunAsync →
  RunStrategyOpenWorldAsync`），`Agent.OpenWorld.cs` 零 trap/recovery 机制（trap 词汇仅存于
  Plan 路径）。实证：外部导航异常 → `RunFailed "…fail closed"`，事件
  `[ActionDispatched, RunFailed]`，trap/recovery 快照全 false；admission 时不可分类节点 →
  零可观察效应。spec S2 场景要求的 `TrapRaised`/`RecoveryStarted` + trap/recovery 快照在既有
  表面上不可得，且不得以 Runtime 源码修改换取。
- **S3 Cross-Run Adaptation Simulation：PASS**——两个单 Directive 单 Run（异 runId/strategyId/
  digest）；Result 1 事实仅进入 Run 2 directive（payload diff = strategyId）；Memory 插入点
  `Historical Result → Strategy` 在 Runtime 边界外（Run 1 证据重读不变）；双 Run G1–G4 全过。

## 5. Evidence Summary

- 测试：ValidationHarness 49/49；全量确定性 2101/2101 + Semantic 32/32
  （排除 RealDevice/RealEmulator/RealityBaseline）。
- 构建：`dotnet build src/UniClaw.Runtime.sln` 0 error（新文件 0 warning）。
- 治理：`openspec validate --strict` PASS；`scripts/check-consistency.sh` ALL PASS；
  `git diff --check` PASS。
- 冻结面：7 个 wire/DTO/协议文件 SHA-256 守护绿（工作树上 StrategyContract.cs 的既有
  Phase-2 在途 diff 为会话开始前状态，非 harness 编辑，已记录为上下文）。
- 设备层：7 个 RealDevice/RealEmulator 测试因无 ADB 设备 fail-closed（环境前置，非缺陷）。

## 6. Regression Result

全量确定性回归从基线 2073 增至 2101（+28 全部为 harness 能力测试），零既有测试被修改或
放宽；S2 相关零新增零放宽。Real-device/emulator 层限制如实记录（§5）。

## 7. AuthorityDelta

`NONE`。未新增 wire method、Runtime API、协议版本、状态系统或 Evidence owner；
未触碰 Phase 2 冻结契约、Agent/FSM/Traversal ownership、Memory、Planner。Harness 为纯
消费者/验证工具；其全部能力经源形态守护机械证明不含权威表面。

## 8. ArchitectureDelta

`ADDITIVE_TOOLING_ONLY`。新增 `src/UniClaw.Runtime.ValidationHarness/`（Emulator/Results/
Reporting/Scenarios/Classification/Fixtures/Hosting/Wire 八区）+ 测试目录
`tests/UniClaw.Runtime.Tests/ValidationHarness/`；`src/UniClaw.Runtime.sln` +14 行。
`modules.json` runtime-integration owned/test paths 增补（治理动作，validator PASS）。

## 9. Remaining Human Gates

1. **S2 裁决（阻塞，三选一）**：
   - **选项 1**：将 recovery 机制引入 strategy/open-world 路径 —— Phase 2 契约变更，Large
     Change，需新 OpenSpec + 实现；
   - **选项 2（Leader 推荐）**：修订 S2 spec 场景，接受 bounded fail-closed reason 作为
     "自治处置异常"的真实验证证据（Runtime 已自治 fail-closed，缺的只是 trap 证据词汇）——
     spec 文本修订 + 本 change 内补一个 S2 场景测试；
   - **选项 3**：S2 标记 deferred，Phase 2.5 结论如实记录"恢复能力面未经本 harness 验证"。
2. **生命周期结论**（tasks 9.3）：Phase 2.5 outcome、Phase 3 Memory resume、
   archive 时机——全部 Human-owned，本 change 不自决。
3. Real-device/emulator 层（Tier B/C）执行需 Human 批准的设备访问（设计如此，非本 change 缺口）。

---

*本结果由 Leader 生成；所有验收均独立重跑，未采信任何 worker 自述。S2 阻塞结论经
Leader 直接源码检查复核（非转述）。*
