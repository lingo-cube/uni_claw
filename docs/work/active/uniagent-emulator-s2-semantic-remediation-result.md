# UniAgent Emulator S2 Semantic Remediation — Result

DocumentType: `IMPLEMENTATION_RESULT`
Decision: `PROJECT_LEADER_UNIAGENT_EMULATOR_S2_SEMANTIC_REMEDIATION_RESULT`
Change: `openspec/changes/uniagent-emulator-validation-harness/`（S2 修订，Human Decision REVISE_SPEC_WITHOUT_RUNTIME_CHANGE）
Date: 2026-08-26
Authority: Runtime Architecture Contract I-1..I-14 与 Architecture v1 不变；本结果不新增架构权威。

---

## 1. Human-readable Reality Analysis

修订前的 S2 把"Runtime Autonomous Recovery"预设为 strategy Run 必有 trap/recovery 证据面。
实证与静态核查（上一轮，经 Leader 源码复核）证明：`run.strategy.start` 只走 open-world 路径，
该路径无 trap 机制——但**并非没有自治处置能力**：Runtime 在无上层介入下，对能力边界内可处理
的问题自主处理，对不可恢复的异常产出 bounded、evidence-backed、fail-closed 终态
（含明确的 zero-redispatch 策略声明）。原 S2 的语义错位在于把"自治处置"等同于"恢复必须成功"。

## 2. Original S2 Mismatch

- 原 spec 场景要求 `TrapRaised`/`RecoveryStarted` 事件 + trap/recovery 快照数据——该词汇仅存于
  Plan 路径（`Agent.Recovery.cs`），strategy 路径零实现。
- 为测试给 strategy path 新增 Trap/Recovery = 新 Runtime capability，超出 Phase 2.5 buyer。

## 3. Revised S2 Contract

S2 = **Runtime Autonomous Exception Disposition**：

- Expected：Emulator 启动 Run 后零介入；Runtime 遇异常自行处置。
- `AUTONOMOUS_HANDLING != RECOVERY_ALWAYS_SUCCEEDS`；
  `FAIL_CLOSED_TERMINAL != RECOVERY_FAILURE_OF_ARCHITECTURE`。
- 允许结果 A `PASS_RECOVERED`：真实 recovery evidence + 继续执行 + 零介入。
- 允许结果 B `PASS_BOUNDED_FAIL_CLOSED`：Runtime 起源终态失败 + 明确 FailureReason +
  EvidenceRef/lifecycle 支撑 + 无无限 retry + 无隐藏 fallback + 零介入。
- 禁止：bounded failure 标为 recovery success；伪造 recovery event；放宽 terminal 断言；
  改 Runtime 过测试。

## 4. Runtime Capability Gap Preserved

每个 S2 结果逐字记录：`STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5`。
Phase 2.5 只证明"上层无需 Run 内介入"；不证明"所有 Strategy 异常可恢复继续"。未来
recovery-and-continue buyer 必须单独建 Runtime Recovery capability → OpenSpec → Human Gate，
不得从本 harness 偷渡。

## 5. OpenSpec Changes（仅本 change 内）

| Artifact | 变更 |
|---|---|
| `specs/.../spec.md` | S2 场景整体改写为 Autonomous Exception Disposition（双 pass 结果 + gap 记录条款 + 禁止伪造条款）；需求标题同步 |
| `design.md` | 增补 S2 semantic revision 段（Human 决策、两结果语义、gap 保留、未来 buyer 路径） |
| `proposal.md` | Scenario Runner 行同步新名 |
| `README.md` | Status 更新（APPLY + S2 修订语义摘要 + gap 声明） |
| `tasks.md` | 5.2 改写为新契约并附实现证据；保留旧 BLOCKED 证据为 superseded note |

未创建新 OpenSpec；未修改任何已毕业 Phase 2 specs；strict 校验 PASS。

## 6. Tier A Validation

实现：`Scenarios/ExceptionDispositionScenario.cs`（双结果分类器 + gap 常量）+
`ExceptionDispositionScenarioTests`（能力断言，零脚本断言）。修正一处真实缺陷：异常注入偏移
+1 落在启动观测（表现为 foreground mismatch 而非 Run 内异常）→ 调至 +4，实证落点为
post-action 阶段。

| 项 | 结果 |
|---|---|
| Harness targeted tests | 51/51 |
| **S1** | **PASS**（ScenarioRunnerTests 绿） |
| **S2** | **PASS_BOUNDED_FAIL_CLOSED**——终态 Failed，明确理由 "post-action transition did not settle within 3 fresh observations；fail closed（composition policy；zero redispatch）"，事件 [ActionDispatched, RunFailed] + 快照诊断支撑，零 Emulator 介入，recovery 证据如实缺席（不伪造），gap 逐字记录 |
| **S3** | **PASS**（ScenarioRunnerTests 绿） |
| Runtime deterministic full suite | 2103/2103 |
| Semantic suite | 32/32 |
| Architecture guards | 61/61 |
| check-consistency | ALL PASS |
| git diff --check | PASS |
| OpenSpec strict | PASS |
| Runtime 源码 | 零修改（工作树 Runtime diff 为既有 Phase-2 在途状态，与本次修订前 byte-identical） |

## 7. Claim Boundary（Tier A 全绿下允许声明）

✅ **允许**："现有 RuntimeAgent 可以接受外部抽象 Strategy，并在一个 Run 内自主执行；对当前
能力可处理的问题自主处理，对能力边界外的问题可以 evidence-backed bounded fail-closed，无需
上层在 Run 内微操。"

❌ **禁止**："Strategy path 已具备通用 Trap/Recovery"；"Runtime 可以恢复所有探索异常"。

## 8. AuthorityDelta

`NONE`。零新增 Trap/Recovery/wire/API/ownership；harness 仍为纯验证工具（源形态守护继续生效）。

## 9. ArchitectureDelta

`NONE_RUNTIME / SPEC_ALIGNMENT_ONLY`。变更限于本 change 的 OpenSpec 文本对齐 + harness 内
S2 场景实现；Runtime、DriverHost、Harness、冻结 wire/DTO 全部未动（SHA 守护基线不变）。

## 10. Remaining Human Gates

1. **Phase 2.5 lifecycle 结论**（CONTINUE_VALIDATION / NOT_GRADUATED 状态下）：Tier A 已全绿，
   Tier B/C（NOT_AUTHORIZED_YET）执行与最终 Phase 2.5 outcome / graduation 仍 Human-owned。
2. **Phase 3 Memory**：REMAIN_PAUSED（无动作）。
3. **Archive**：NOT_AUTHORIZED（无动作；工作树未提交未归档）。
4. 未来 recovery-and-continue buyer：独立 Runtime Recovery capability OpenSpec + Human Gate。
