# OUT-003 — Terminal / Outcome Vertical Slice

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 4620594c

## Intent

**WHAT**: 在 E2B-001（Evidence→Belief）与 C2E-002（Control→Effect 闭环）之上，
把终局链一次串成最短可验证语义：Accepted Evidence + Current WorldBelief +
Run-level Proof Obligation State → Assurance Outcome Proof（四分类）→ Run
Model 单次 terminal Outcome State 转移（exact-prior / single-winner）→ Uni
Kernel 唯一 immutable Runtime Outcome emission（exactly once）→ terminal 后
external effect 通道关闭。终局 truth 一旦 terminal，不得被 receipt / evidence /
provider claim / control 声明重写或恢复。

**WHY**: Target v0.1（§3.4/§3.5/§3.8/§13/§15/§17/§19/§20.4）的运行时骨架已由
E2B/C2E 闭合到 effect 回流；但「执行最终如何终止、如何被证明、如何只发出一
次、如何不再产生新动作」仍是空白。本切片证明不变量 37-39、42（Assurance
judge Outcome Proof；Run Model record terminal Outcome State；Uni Kernel emit
immutable Runtime Outcome；terminal 后 Effect Boundary 关闭 delivery），并给出
success / failure / safe-stop 各自独立的 evidence-backed terminal proof。

## Scope

- Assurance：Effect Verification 消费面 + Outcome Proof（Completion / Failure /
  SafeStop / Escalation 四分类）judgment（§15.2，不变量 37）
- Run Model：Proof Obligation State discharge（D3 占位落地）、terminal Outcome
  State typed transition（exact-prior CAS、single-winner、不可恢复 active）
- Uni Kernel：terminal 编排 + immutable Runtime Outcome emission（exactly once，
  带 Run identity / classification / obligations / proof ref / evidence refs）
- Effect Boundary：delivery closure latch（§17 / 不变量 42）
- 终局反例确定性覆盖（任务第九节 A-J 全项）

## Out of Scope

- Goal Evaluation（UniAgent 消费 Runtime Outcome 属未来 slice，不建壳）
- Memory System、Capability Plane 框架化
- 新 Traversal 能力、新 Recovery 架构、新 Effect 模型、新 Capability
- 多 Run / continuation / correction-after-terminal 语义（本 slice 不发明）
- legacy（uni-agent）迁移或依赖；真机/外部环境（DETERMINISTIC only）
- F2「L2 直连执法集中组合缝」：carry forward，除非本 slice 被其真实阻塞

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | 终局链 = 任务十五唯一链；逐环节 Owner 严格按 §3.4/§3.8/§19：Assurance judge / Run Model record / Kernel emit | 任务二/三 |
| D2 | Run-level obligations 来自 contract 的**可选结构化字段**（ExecutionContract 增第 7 参，默认 null；null 时从 ProofCriteria 派生不可判定的占位 obligation）。C2E 既有单参构造/with 表达式零改动；admission 检查行为不变 | 任务五；C2E Accepted1 兼容 |
| D3 | 四分类 enum = `TerminalClassification { Completion, Failure, SafeStop, Escalation }`（baseline §13.2「terminal classification」词汇；§15.2 的 Safe-Stop/Escalation 一个 bullet 展开为两个枚举值——enum 名属 L4 开放项 §22） | 任务四；baseline §15.2 |
| D4 | obligation 满足判定 = **accepted Evidence 支持的 subject=value claim**（current revision 的 WorldState 或 Conflicts 携带该值，backing EvidenceId ∈ basis）。MaterialEffect 额外要求 backing record 的 producer 前缀 `effect.boundary`（D7 自产观察约定）——区分「provider 说」与「我们观察到」 | 任务四.1/九.A；E2B conflict 词汇承载 |
| D5 | classification 优先级（确定性）：mandatory Failure 满足 > mandatory SafeStop 满足 > mandatory Escalation 满足 > 全部 mandatory satisfied → Completion > **证据不足 → 无 proof（non-terminal，不猜测）**。reason 携带判断依据 | 任务四/九.E |
| D6 | terminal transition = `RunModel.TransitionToTerminal(OutcomeProof, expectedPrior)` exact-prior/引用等同 CAS；多 proposal 竞争 single-winner（任务六.4/九.F）。terminal 后 RecordCycle/RecordAction 抛 InvalidOperationException（§17 不可恢复 active） | 任务六 |
| D7 | emission 编排 = `UniKernel.EvaluateTerminal()`：Assurance judge → RunModel record（CAS）→ accepted 后 `EffectBoundary.CloseDelivery()` → 从 terminal OutcomeState 投影 immutable RuntimeOutcome（exactly once；Kernel 不重判、不解析 Evidence、不改 classification） | 任务七/十四；§7/§17 |
| D8 | effect channel closure = EffectBoundary delivery latch（§17 不变量 42）+ kernel.Act / SelectIntent terminal fail-closed；直接 Dispatch（F2 直连）也被 latch 拒绝 | 任务八/九.11 |
| D9 | late receipt / late evidence 可记录：经既有 E2B 回流/Process 路径（attempt evidence 与新 observations 仍可 admitted）；terminal Outcome / RunState 不被改写、Run 不恢复。不发明晚到 receipt 新通道 | 任务九.G/H/10 |

## Acceptance（10 条）

1. **Assurance 独占 Outcome Proof judgment**：全 kernel 程序集唯一产出
   OutcomeProof 的路径 = `RuntimeAssurance.JudgeOutcome`；Kernel / Run Model /
   Control / Effect 无第二产出点（不变量 22/37）
2. **Run Model 只记录、不判断**：terminal OutcomeState 是 proof 的纯记录快照
   （proof ref + classification + statuses + evidence refs + uncertainty）；
   Run State 对象图不含 Assurance 类型（不变量 38）
3. **Completion requires satisfied mandatory Run obligations**：全部 mandatory
   obligation satisfied 才可能 CompletionProof；任一 mandatory 未满足 → 不得
   completion（任务 九.C）
4. **Receipt success 不得证明 completion**：最后 receipt 成功但无 post-action
   accepted Evidence → 无 completion proof（不变量 35，任务 九.A）
5. **Verified Effect 不得证明 completion**：局部 effect 已验证而另有 mandatory
   pending → 不得 terminal success（任务 九.B）
6. **Failure / Safe-stop 可由独立 evidence-backed Outcome Proof terminal**：
   failure / safe-stop 证据充分 → 相应分类 proof → 合法 terminal non-success 状态
   （任务 九.D；不变量 37）
7. **terminal transition exact-prior / single-winner**：一个 Primary Run 至多成功
   进入一次 terminal；竞争 proposal 一个成功其余拒绝（任务 六.4/九.F）
8. **Runtime Outcome immutable + exactly once**：只发出一次；envelope 纯由
   terminal OutcomeState 投影，携带 Run identity / classification / fulfilled /
   unfulfilled obligations / proof ref / evidence refs / effect & situation refs
   （任务 七/十四）
9. **terminal 后 effect channel closed**：全部真实 effect 入口 fail-closed——
   kernel.Act、直接 Dispatch（latch）、late candidate binding、late authorization、
   late driver callback（不变量 42，任务 八）
10. **late receipt / evidence / provider claim 不能重写 terminal truth**：可记录
    attempt / 历史 evidence；不得改 OutcomeState、不得恢复 Run、不得二次 emit
    （任务 九.G/H/I）

## Verification

```yaml
level: DETERMINISTIC   # 纯内存 fake world，scripted driver / observation provider / policy
method: >
  RED（桩 + 测试先行，关键终局场景可见失败）→ GREEN（仅本 slice 最小实现）→
  REVIEW（fresh SubAgent，六轴）→ VERIFY（验收 10 条 + 反例 A-J 逐项 +
  E2B/C2E 回归零改动 + no-drift 检查 + 四元组映射）
expected: >
  验收 10 条全 GREEN；15±2 个终局用例全 GREEN；E2B 8 + C2E 10 用例零改动保持
  GREEN；E2B Evidence/World 文件 byte-level 无漂移；terminal exactly-once 与
  effect closure 有确定性证据；success/failure/safe-stop 各有代表性 terminal proof
actual: >
  2026-09-08 dotnet test（UniClaw.Kernel.slnx；SDK 10.0.400；net10.0）：
  失败 0 / 通过 36 / 跳过 0——OUT-003 18 用例全 GREEN，E2B 8 + C2E 10
  零改动全保持。RED 阶段（桩 + NotImplementedException）失败 16 / 通过
  18；GREEN 后失败 3 / 通过 31（3 项为测试预期/守卫次序/编译器生成成员
  过滤问题，非语义缺陷，见 evidence）；REVIEW 修复后失败 0 / 通过 36。
  REVIEW（fresh SubAgent）APPROVE；F1/F2 已修（Escalation 测试 +
  vacuous-completion 守卫），F3-F5 处理见 evidence。no-drift：
  E2B/C2E 冻结面 diff EMPTY。terminal exactly-once 与 effect closure 有
  确定性证据（Outcome8/10/11）；success/failure/safe-stop/escalation 各
  有代表性 terminal proof（Outcome1/5/6/17）。
evidence: evidence/2026-09-08-out-003-deterministic.md
```

## Constraints

- Target v0.1 §3.4/§3.5/§3.8/§13/§15/§16/§17/§19/§20.4 为本切片直接权威；
  不变量 1-3、7、22、35-39、42 不可违反
- E2B-001 已定型语义（Admission/Relevance/Reconciliation/Revision/Slice）零改动
- C2E-002 已定型语义零穿透：Control Intent authority、action-local judgment、
  Candidate→Canonical Binding、Effect Gate 只执法、Receipt != Effect、
  post-action evidence 回流路径全部不变；C2E 用例文件不因本 slice 而修改
- 禁止 Run Model 解释 Evidence / 判断完成；禁止 Kernel 重判 completion；
  禁止 Effect Receipt 直接写 terminal state
- 测试验证行为，不验证实现细节

## Assumptions

- obligation 的 subject/value 由 contract author（测试脚本）显式提供；belift 的
  WorldState/Conflicts 词汇可承载 obligation 满足判定（E2B Assumption 3 延续）
- scripted 世界下 post-action 观察（producer 前缀 `effect.boundary`）是 material
  effect 的唯一合法证据来源（D7 延续）
- 单线程内存模型下「exact-prior」用引用等同 CAS 表达已满足并发守卫需求；未来
  多线程/持久化部署另立（§22 开放）

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| obligations 走 AdmitContract 平行参数 | 语义上 obligations 属于 contract 的 proof criteria（§3.3），且会分裂 admission 签名；可选字段 + 默认 null 保持 C2E 单参调用兼容 |
| Completion 与 success 同义、只做 Completion Proof | 任务明确要求 failure / safe-stop / escalation 独立 proof；baseline §15.2 三分类 |
| 冲突（uncertainty）阻塞 completion | C2E 世界 post-action 观察必然产生 E2B 冲突（不静默覆盖）；若冲突必阻塞则成功路径不可达；改为「记录 unresolved uncertainty，不阻塞满足判定」 |
| 证据不足时猜测 blocked 并 terminal | 任务 九.E 明确禁止伪装分类；证据不足 = 无 proof = 保持 non-terminal |
| implementation 顺序先做完再做 Guard | transition CAS + emission latch + delivery latch 必须在同一 slice 内形成不变量 42，否则留下「terminal 后仍可 effect」的窗口 |

## Owner / Authority Impact

- Assurance：新增唯一 Outcome Proof Authority 面（§15.2；不变量 22）
- Run Model：新增 Outcome State 记录面 + Proof Obligation discharge（§13.2；
  不变量 36/38；C2E D3 占位落地）
- Uni Kernel：新增 terminal Runtime Outcome emission boundary（§3.5/§3.8；
  不变量 39）；不成为任何判断 Owner
- Effect Boundary：新增 delivery closure（不变量 42）；不改 binding/gate/dispatch
  语义
- UniAgent：本 slice 不出现（Runtime Outcome consumer 为未来 slice）

## ADR Refs

- 产品架构基线：`docs/architecture/product-architecture-baseline-l0-l3.md`
  （Target v0.1 §3.4/§3.5/§3.8/§13/§15/§16/§17/§19/§20.4 直接权威；原
  docs/analysis/ 路径由 ARCH-DOC-013 relocation 收口）
- 既有：E2B-001（changes/E2B-001/state.md）、C2E-002（changes/C2E-002/state.md）

## Residual Risks

- F2「L2 直连执法集中组合缝」：carry forward；本 slice 用 EffectBoundary
  delivery latch 覆盖 terminal 后直连 Dispatch，但非 terminal 直连的
  issuance/provenance 校验仍待开放直连前的 change（不阻塞 OUT-003）
- obligation 与 ProofCriteria 的关系仅按 id 约定（无强制绑定）；结构化 obligations
  缺失时 contract 永不可完成（显式占位，行为诚实）
- RuntimeOutcome 的多线程原子性（emission latch 单线程内存模型）待存储/部署
  slice（§22 开放）
- 「correction / continuation after terminal」语义（任务 九.H 尾句）明确留给
  未来独立 change，本 slice 不发明——若未来需求出现，需显式新 change

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-08 | →planned | ENTRY 复验（E2B/C2E closed、基线 18/18 GREEN、working tree 干净）后按任务规格定稿 |
| 2026-09-08 | planned→implemented | TDD RED(16 失败，关键终局场景可见)→GREEN(31)→修正 3 项后 34/34；最小实现仅限本链（Assurance judge / RunModel record / EffectBoundary latch / Kernel emit） |
| 2026-09-08 | implemented→reviewed | fresh SubAgent APPROVE（轴 A1-A8）；F1（Escalation 无测试）/F2（vacuous completion）已修，F3-F5 处置见 evidence |
| 2026-09-08 | reviewed→verified | 验收 10 条逐条对照 evidence 全 GREEN；反例 A-J 逐项 GREEN；E2B/C2E no-drift EMPTY；四元组映射落 evidence |
| 2026-09-08 | verified→closed | 范围完成 + acceptance 被证明；无第二 Run State Owner / 无第二 Outcome Proof Authority / 无 Kernel completion 重判；working tree 仅 `.tmp-hf-intake/`（已记录无关项） |