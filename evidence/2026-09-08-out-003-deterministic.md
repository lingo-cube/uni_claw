# Evidence — OUT-003 DETERMINISTIC verification

- Date: 2026-09-08
- Change: `changes/OUT-003/state.md`
- Command: `dotnet test UniClaw.Kernel.slnx`（.NET SDK 10.0.400；net10.0）
- Level: DETERMINISTIC（纯内存 fake world，scripted driver / observation
  provider / policy，零 IO/设备/时钟依赖）

## Result

```text
已通过! - 失败: 0，通过: 36，已跳过: 0，总计: 36 [dotnet test, 2026-09-08]
```

36 = E2B-001 8 用例（零改动）+ C2E-002 10 用例（零改动）+ OUT-003 18 用例。
全部用例列表（detailed verbosity 实跑）：

```text
E2B（8）: EvidenceToBeliefTests.Accepted1..8 —— 零改动保持 GREEN
C2E（10）: ControlToEffectTests.Accepted1..10 —— 零改动保持 GREEN
OUT（18）: TerminalOutcomeTests.Outcome1..Outcome18 —— 全部 GREEN
```

构建：clean rebuild 后零 error；CS 警告清单 = 基线 HEAD 已存在的 6 条
CS1591（C2E-era 公共构造器/接口，`RuntimeAssurance`/`ControlLoop`/
`IControlPolicy`/`EffectBoundary`/`IEffectDriver`/`RunModel`），OUT-003
新增文件零新警告（全部公共成员带 XML 文档）。环境级 NU1900 NuGet 漏洞库
缓存不可写，与 E2B/C2E 记录一致，与代码无关。

## TDD 过程（失败尝试是证据，保留）

- RED（桩 + 测试先行）：失败 16 / 通过 18（通过项 = E2B 8 + C2E 10 回归，
  零改动）。16 个 OUT 用例全部失败，原因为 `NotImplementedException`
  （Outcome Proof / obligation 满足判定桩，14 个经 JudgeOutcome、1 个经
  EvaluateObligations、1 个经二者联动的组合断言）——关键终局场景可见失败。
- GREEN（最小实现）：失败 3 / 通过 31。剩余 3 项为测试预期/实现边界问题，
  非语义缺陷：
  - Outcome10：immutability 断言误用「无 setter」；record 的 init accessor
    即带 setter 形状——改为行为断言（`with` 产新实例、原实例不改写）。
  - Outcome9：exact-prior 守卫顺序——「concurrent-terminal-proposal」应优先
    于「already-terminal」（stale prior 的 proposal 在任何情况下都先被拒绝）。
    实现调整守卫次序后一致。
  - Outcome16(a)：record 编译器生成的公共 `<Clone>$` 拷贝方法干扰
    entity-scan——过滤后唯一产出点 = `RuntimeAssurance.JudgeOutcome`。
- REVIEW 修复后：失败 0 / 通过 36。

## REVIEW（fresh SubAgent，轴 A1-A8）与修复

独立评审结论：**APPROVE**（无 authority 违规、无 frozen-surface 漂移、无
scope creep）。关键门禁复验：34/34 GREEN（当时 16 OUT）；no-drift diff
（Evidence/ World/ Control/ E2B/C2E 测试文件）EMPTY。发现与处置：

| # | 级别 | 发现 | 处置 |
|---|---|---|---|
| F1 | minor | Escalation terminal 分类已实现但无专用测试（四分类需各自证据） | 已修：新增 Outcome17_EscalationTerminalFromEvidenceBackedProof |
| F2 | minor | 空 mandatory 集时 `mandatory.All(Satisfied)` vacuous truth → 零证据 completion | 已修：Completion 要求 `mandatory.Count > 0`；新增 Outcome18_VacuousCompletionIsPreventedWithoutMandatoryObligations |
| F3 | nit | 5 文件缺 EOF 换行 | 已修：全部 11 个新/改 .cs 文件补换行 |
| F4 | nit | AdmitContract 检查清单 6→7（obligations-wellformed；null 时恒过） | 接受：决策级行为不变（C2E 10 用例零改动 GREEN）；检查清单内容是诚实留痕 |
| F5 | nit | Outcome10 immutability 断言部分地断言 record 语义 | 接受：与 exactly-once / 投影断言同处，语义意图清晰 |

修复后复跑：失败 0 / 通过 36（2026-09-08）。

## 验收 ↔ 用例映射（四元组 Requirement → Symbol → Test → Evidence）

| 验收 | Implementation Symbol | Test | 确定性证据 |
|---|---|---|---|
| 1 Assurance 独占 Outcome Proof judgment | `RuntimeAssurance.JudgeOutcome`（唯一产出点） | Outcome16(a)：程序集级 reflection scan；Outcome14/15 行为负向 | 唯一 producer = RuntimeAssurance.JudgeOutcome；provider/control 声明零 OutcomeState 写入 |
| 2 Run Model 只记录不判断 | `RunModel.TransitionToTerminal` + `OutcomeState`（纯快照） | Outcome16(b)：RunState 对象图 reflection walk 零 Assurance 类型；Outcome10 投影等值断言 | Run/* 无 Evidence/World 引用；OutcomeState ≡ proof 数据 |
| 3 Completion requires satisfied mandatory obligations | `JudgeOutcome` mandatory 全满足分支 | Outcome1；Outcome4（C）；Outcome18（空 mandatory 负向） | 全 mandatory Satisfied → Completion；任一未满足 → null proof |
| 4 Receipt success ≠ completion | `ResolveBackingEvidence`（attempt.* 世界无关 + basis 校验） | Outcome2（A）；Outcome16(d)（receipt 后全 obligation 未满足） | Delivered receipt + 零 post-action evidence → 无 proof |
| 5 Verified Effect ≠ completion | 同上 + `RunObligationKind` 分类 | Outcome3（B） | effect obligation Satisfied 但另一 mandatory pending → 无 proof |
| 6 Failure / Safe-stop 独立 evidence-backed proof | `JudgeOutcome` situation 分支 | Outcome5（D-failure）；Outcome6（D-safe-stop）；Outcome17（D-escalation） | 各分类独立 proof + terminal non-success + RuntimeOutcome 相应 classification |
| 7 terminal transition exact-prior / single-winner | `RunModel.TransitionToTerminal`（引用等同 CAS） | Outcome8（F，kernel 面）；Outcome9（direct CAS + 不可恢复） | 一次 accepted；stale prior → concurrent-terminal-proposal；已 terminal → already-terminal；RecordCycle/Action throw |
| 8 Runtime Outcome immutable + exactly once | `UniKernel.EvaluateTerminal` + `RuntimeOutcome` | Outcome10；Outcome8（二次 EvaluateTerminal 零新 Outcome） | 唯一 emission；envelope ≡ OutcomeState 投影；`with` 不原地改写 |
| 9 terminal 后 effect channel closed | `EffectBoundary.CloseDelivery`（latch）+ `UniKernel.Act/SelectIntent` fail-closed | Outcome11（任务八 全入口枚举） | delivery-closed gate reason；Act/SelectIntent throw；ReceiptLog 不增长 |
| 10 late receipt/evidence/provider claim 不能重写 terminal truth | `UniKernel.Process`（E2B 路径不变）+ terminal latch | Outcome12（G）；Outcome13（H）；Outcome14（I） | late evidence 可 admitted/新 revision，但 OutcomeState Same-ref 不变、Run 不恢复、零二次 emit |

## 核心反例 A-J 覆盖核对

| 反例 | 语义 | 用例 | GREEN |
|---|---|---|---|
| A Receipt success ≠ Completion | 无 post-action accepted Evidence → 无 completion | Outcome2（+Outcome1 前半） | ✅ |
| B Verified local Effect ≠ Completion | 局部已验证 + mandatory pending | Outcome3 | ✅ |
| C Mandatory unresolved | 部分满足 + 至少一个 mandatory 未满足 | Outcome4 | ✅ |
| D Failure / Safe-stop terminal | 独立 evidence-backed proof | Outcome5 / Outcome6 / Outcome17 | ✅ |
| E Evidence insufficient | 不猜测分类；non-terminal | Outcome7（+Outcome18） | ✅ |
| F Concurrent terminal proposals | exact-prior single-winner；一个 Runtime Outcome | Outcome8 / Outcome9 | ✅ |
| G Late receipt after terminal | attempt evidence 可记录；不改 Outcome、不恢复 | Outcome12 | ✅ |
| H Late evidence after terminal | 历史可追加；terminal truth 不可改写；无 continuation 语义 | Outcome13 | ✅ |
| I Provider claims completed | 只能作 proposal / raw output；不得写 OutcomeState | Outcome14 | ✅ |
| J Control claims no more work | 不等于 completion；必须经 Outcome Proof | Outcome15 | ✅ |

## 回归与 no-drift

- `git diff 4620594c -- Evidence/ World/ Control/ tests/.../EvidenceToBeliefTests.cs
  tests/.../ControlToEffectTests.cs src/.../Effects/EffectDispatch.cs
  src/.../Effects/TargetBinding.cs src/.../Assurance/AssuranceJudgment.cs
  src/.../Run/ExecutionContractView.cs` → **EMPTY（byte-level）**
- E2B 8 + C2E 10 用例在 OUT-003 代码下零改动全 GREEN（36 总含）
- ControlToEffectTests.cs 与 EvidenceToBeliefTests.cs 字节级不动

## 残余风险（OUT-003 范围外，carry forward）

- F2「L2 直连执法集中组合缝」：本片用 EffectBoundary delivery latch 覆盖
  terminal 后直连 Dispatch；非 terminal 直连的 issuance/provenance 校验仍
  待开放直连前的 change（不阻塞 OUT-003）
- obligation 与 ProofCriteria 的关联按 id 约定（无强制绑定）；结构化
  obligations 缺失时 contract 永不可完成（显式占位，行为诚实）
- RuntimeOutcome 多线程原子性（emission latch 单线程内存模型）待存储/部署
- correction / continuation after terminal 语义留给未来独立 change