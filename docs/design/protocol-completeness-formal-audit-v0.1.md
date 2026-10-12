# 协议设计完备性形式审计 v0.1 —— 生命周期与流程全走查（数学 + 哲学）

> Status: `DRAFT / FORMAL-AUDIT`
> Authority: `NONE`（审计稿；结论供 Owner 裁决，不直接修改基线）
> Date: 2026-10-12
> 与既有工作的关系：`scenario-reasoning-v0.1.md` 已在**场景层**做公理实例化（T1–T6 × 150 场景，数学+哲学双审）。本审计在**协议图与状态机层**做结构检查（可达性/终止性/闭合性/认识论循环），两者互补、结论互不覆盖。

---

## 1. 形式模型

**状态机 M**（生命周期轴）：`Session → Goal → Contract(admitted) → [P24] activated →
Run(cycle*: observe→decide→act→verify | WaitingForInput | recovering) → terminal(4) →
[emission] → closed → Evaluation`。Run 内部状态取 `RunDriveStatus` 12 值（代码实测）。

**协议图 G**：结点 = {User, UniAgent, 六域, CapabilityPlane, Memory, Host}；
边 = P1–P26（有向、带幂等/absence 语义）。

**性质分类**：Safety（坏事情不发生）/ Liveness（好事终发生）/ Reachability（状态可达）/
Consistency（幂等·原子）/ Epistemic soundness（认识论健全）/ Authority closure（权力闭合）。

## 2. 数学走查

### 2.1 Safety ✅（抽样全过）

47 条编号不变量均有执法点（基线 §20 + 场景层已证）；本轮补验三个薄弱执法点：
① Desired-State skip（ADR-0017）走 occurrence 携带的 revision-local state——**验证点**：
skip 路径是否经过与 act 路径同等的 currentness 判定（建议补一条锚定测试）；
② provider confidence 不入 Assurance 输入签名（P11 View 不携带 confidence 字段即可闭）；
③ P22 只影响 association ranking（不产 claim）——代码 `ExportTransitionContext` 语义符合。
结论：**无新增 Safety 违约**。

### 2.2 Liveness

**定理 L1（终止性，条件成立）**：Run 到达 terminal ⟸ 证明成立 ∨ 预算耗尽（DeferRounds/
consult/steps，耗尽→确定性诚实终局，T6）。前提：**终止测度（预算）存在**。

> **发现 D1（条件性缺陷）**：P1 准入不要求契约携带终止测度；活性由 Contract View 端的
> **缺省预算**（16/64，consultation-protocol D3）承重，且代码自记「budget 级入口校验
> 显式 deferred——尚无 owning model」。缺省若被绕过/漂移，终止性失去结构保证。
> 建议：P1 admission 增加预算存在性检查（或协议基线把"终止测度存在"列为 admission
> 必要条件）。

**定理 L2（等待有界）✅**：WaitingForInput/Defer 均有配额+耗尽语义（`KernelRunDriver`
代码验证：DeferRoundsExhausted 终问、consult-budget-exhausted、budget-exceeded:steps）。

> **发现 D2（宿主侧孤儿 Run）**：WaitingForInput 的出口依赖外部**再次调用** `Drive()`；
> 协议图无超时/watchdog 边（与 deferred ⑪ 无 canonical clock 同源）。宿主崩溃或永不
> 回调 → Run 停留合法非终态，无人问责。定级：已知缺口的图论表述，建议随 ⑪ 一并裁决。

**定理 L3（无死锁）✅（以 D1 为条件）**：act 链每步皆有拒绝出口且拒绝回流 Control
（短路语义）；observe 循环受预算约束。

### 2.3 Reachability

- Completion / Failure / SafeStop：可达（场景层已实例化）。
- **Escalation**：仅经**契约预声明的升级条件**（obligation 型）可达 ✅；

> **发现 D3（用户中途边缺失，图论表述）**：G 中 User 的出边仅（经 UniAgent 的）初始
> 意图与 P19 前的授权链；mid-run 无任何用户入边。形式后果：交互式 Escalation、
> cancel（机械可表达：driver input + SafeStop 义务路径，但非协议边，⑯）、运行中澄清
> 均为**不可达状态**。政治哲学表述见 §3.3。

> **发现 D4（悬挂点）**：admitted-never-activated 的 Contract View 无失效/GC 语义
> （⑰ 子问题）：图中是入度 1、出度 0 的悬挂结点，可无限累积。

### 2.4 Consistency ✅（幂等/原子矩阵全过）

P1 同版本幂等复用 · P24 同 key at-most-one · P25 每 boundary 一次 · P16/P17 exact-prior
single-winner · P19 确定性 id 幂等 · binding 消费失效防双发 · latch absorbing +
delivery closure。**未发现违约**。

### 2.5 图完备性

> **发现 D5（观察致动边缺号）**：Observation Control 决定"何时、以何 scope 观察"，
> 但 Control → CapabilityPlane（acquisition 请求）**不是编号边**；P2 只锁回流。
> 系统的感知实际是目标导向（goal-directed）的，协议图却只建模了"认知回流"、未建模
> "感知指向"本身——目前该编排藏于 driver realization（P2 注记自认"acquisition 编排
> 归属不在 P2 中锁定"）。建议：登记为 P27 候选（Observation Request）， buyer = 未来
> 的感知策略调度/多设备观察。

其余配对检查：UniAgent↔Kernel（P1/P18/P25 ✅，cancel=⑯）、Memory（占位显式 ✅）、
Assurance→Control 拒绝回流（经组合缝，进程内成立；跨进程缺边，非当前范围）。

## 3. 哲学审计

### 3.1 认识论

- **E1 桥接公理**（现实只经 admitted observation 可及）与 **E2 单一写入**（belief 只经
  Reconciliation）成立 ✅——形而上学谦逊被制度化。
- **E3 期望渗透观察（新发现 D6，设计弱点）**：P22 prior（"我刚点击了 X"）→ association
  ranking 偏置 → 对确认性证据的解释 → 该证据又被用于验证引发 prior 的那次动作。这是
  理论负载观察（theory-ladenness）的协议级闭环：**系统用自己的行动期待为自己的行动
  作证**。已有缓解：四值判别诚实（Ambiguous/Insufficient 不强制）、Conflict 保留、
  MaterialEffect 要求内容 claim 而非仅身份连续、ProducerTrust/trust grade（ADR-0028）。
  缺：**对 MaterialEffect 承重观察的 prior-无关佐证政策或降级规则未成文**。定级：非
  违约、是循环论证风险敞口；建议登记为验证政策扩展项（与 ④ freshness policy 同批）。
- **E4 反身性处理 ✅**：Outcome Proof 是系统对自身运行的证明——"证据不足≠失败分类"
  正确处理了证明论极限（不可自证的诚实退出）。
- **E5 条件健全性（⑦ 升格表述）**：origin/producer 可伪造（场景 8、SR-024/126）⇒
  数学上应记为**公理前提**（生产者诚实）而非可证性质。系统的健全性是 conditional
  soundness——建议协议基线把此前提显式写入 P2 的前提节，替代目前"无核验手段"的
  纯缺口表述。

### 3.2 证明论与信息论

append-only = **信息只增不灭**（历史可被重新解释、不可重写）——认识论宪法条款 ✅；
串行屏障 = 验证义务的**因果序强制**，等价于把"未验证不动"做成偏序约束 ✅。

### 3.3 政体哲学

- **三权分立闭合 ✅**：Control 提案不执法、Assurance 裁决不立法（不签发 intent）、
  Effect 执法不释法（Gate 不重判）——无自裁通道；Capability 无论多强零权威（能力
  不扩权 = 出身不授职）。
- **修宪缺位（=② 的政治表述）**：任期中契约不可修正 → 范围错误以整次 Run 重启为
  代价。是刻意的宪政稳定换经济成本，登记即可。
- **主权者中途话语权缺位（=D3）**：主权者（用户）只在"选前"（goal 授权）与"任期后"
  （evaluation 消费）出场；弹劾（cancel）、中期质询（clarification）、罢免重选
  （contract revision mid-run）皆无边。对一个宣称 fail-closed 的系统，**对人的
  fail-open**：系统不会伤害用户，但用户也无法中途拯救系统。
- **法治典范 ✅**：Gate 只执行既有判决、"拒绝可观察有 reason"——程序正义被结构化。

## 4. 缺陷汇总与处置映射

| # | 发现 | 级别 | 处置建议 | 与既有台账关系 |
|---|---|---|---|---|
| D1 | 终止性依赖 View 端缺省预算；P1 不强制终止测度 | 条件性缺陷 | P1 admission 加预算存在性检查 | 补强 H14/1469 行自记缺口 |
| D2 | WaitingForInput 出口依赖宿主再入；无 watchdog 边 | 已知缺口的图论表述 | 随 ⑪（canonical clock）裁决 | =⑪ 派生 |
| D3 | mid-run 用户边缺失（cancel/澄清/交互式升级不可达） | 结构缺号 | 立一条 UserConsultation/Clarification 候选边 | =⑯+§24.7 的图论收口 |
| D4 | admitted-never-activated 悬挂点无 GC | 结构缺号 | 随 ⑰ 补失效语义 | =⑰ 派生 |
| D5 | 观察致动（Control→感知 acquisition）不是编号边 | 结构缺号 | 登记 P27 候选（Observation Request） | **新登记项** |
| D6 | 期望渗透观察的验证循环（P22 prior → 验证） | 设计弱点 | MaterialEffect 承重观察的 prior-独立佐证/降级政策 | **新登记项**（与④同批） |
| E5 | 健全性以生产者诚实为前提 | 前提显式化 | P2 前提节写入 conditional soundness | ⑦ 升格表述 |
| D7 | 契约预算可空 + View 静默缺省（16/256），意图-行为背离无留痕 | 条件性缺陷（D1 精确化） | admission 记录预算来源（valueOrigin）或必填 | 本轮字段级新增 |
| D8 | 验证输入无 ExpectedEffect 载荷；非状态型 effect 靠特设布尔代偿（滚动验证硬编码） | 字段级缺口 | PostActionEffectVerificationInput 增加 expected-effect 字段 | 本轮新增（roadmap G6"expected change correlation"同源） |
| D9 | AdmissionRecord 对被拒输入无指纹/去重，重复坏输入膨胀留痕 | 轻微 | 拒绝记录加输入摘要（可选） | 本轮新增 |

**总评**：Safety/Consistency/权力闭合三项**结构性成立**（这在此类系统中罕见）；Liveness
为**条件成立**（D1）；图完备性缺三块（D3/D4/D5）；认识论一处循环风险（D6）。无一条
推翻既有基线；D5/D6 为本轮新增登记项。

## 5. 证伪条款（仿 scenario-reasoning §5）

1. 若任何 Run 在预算存在且被正确消费的情况下仍未终止且未进合法非终态 → L3 伪；
2. 若出现 P22 prior 直接产生/改写任何 WorldState claim 的实例 → E2 伪（升 D6 为违约）；
3. 若出现用户 mid-run 语义被某边隐式承接（未登记边）→ D3 的"缺号"表述伪（应为"漏登记"）。

## 6. 维护规则

本审计结论随协议基线修订同步复核；D1/D5/D6 处置后在本档登记闭合。

---

## 附录 A · 字段级落实清单（接口 ↔ 缺什么 ↔ 多什么）

依据：协议边 Minimal Payload × C# record 实测字段（2026-10-12 读码核对）。

### A.1 缺字段（协议已要求、类型上没有）

| 接口（边 ↔ 类型） | 缺的字段 | 对应 |
|---|---|---|
| P1 ↔ `ExecutionContract` | `taskInstanceId`、`productSessionId`、contract reference（ADR-0046 最小载荷） | 已登记 gap |
| P1 ↔ `ExecutionContract` | `MaxConsultations`/`MaxTotalSteps` 可空（View 端静默缺省 16/256） | **D7**（D1 精确化） |
| P2 ↔ `ObservationProposal` | **`Origin`**（external vs post-action 自产；现靠 Context+Producer 代偿） | ADR-0047 gap，E5/D6 根源 |
| P3 ↔ `EvidenceRecord` | `Origin`（与 P2 同源） | 同上 |
| P15 ↔ `EffectReceipt`（回流 AttemptReport） | 独立 origin（同源缺口）；attempt 关联为约定级（subject 约定）非字段级 | 同上（轻） |
| P18 ↔ `RuntimeOutcome` | `productSessionId`（信封只有 RunId） | ⑨ 派生 |
| P25 ↔ 咨询缝 | `productSessionId`、`contractViewRef`、`consultationCorrelationId`（每边界幂等键） | ADR-0046 自记 |
| 验证输入 ↔ `PostActionEffectVerificationInput` | **`ExpectedEffect`**（期望效果载荷；滚动验证以 `PriorRoute/CurrentRoute/ScrollContentChanged` 特设布尔代偿=缺口证据） | **D8**（新） |

### A.2 多字段（类型有、协议不需要 → 曝射面过大）

| 接口 | 多的字段 | 说明 |
|---|---|---|
| P10 ↔ `CanonicalBinding` | `TargetLocator`/`TargetNative`/`TargetSpace` | 投递材料（P14 层）提前固化进绑定载荷；P10 收件人是 Assurance，裁决不需要执行坐标——EXP-008 收敛了 view 方向、binding 方向未点名（**新发现**） |
| P10 ↔ `CanonicalBinding` | `TargetSubject`/`TargetValue` UI 通道恒 null（工厂 `null!` 压制） | 双通道共用 record 的形状税（⑯⑮ 关联） |
| P14 ↔ `DeliveryTarget` | `OccurrenceReference` | 注释自认"仅溯源永不参与执行"，仍在 driver 请求面（声明式收窄而非移除，轻） |
| P2 ↔ `Provenance` | `Hierarchy`（第 5 位） | PER-013 自记"通用透传不做判定"——收面>查面（已自记，轻） |

### A.3 缺整个接口（边上没有）

| 缺的边 | 对应 |
|---|---|
| 观察指令边（Control→感知 acquisition；P27 候选） | D5 |
| 用户中途边（cancel/澄清/交互升级；cancel 驱动层有形状、协议无户口） | D3 |
| 看门狗/超时边（宿主失联唤醒） | D2 |

## 附录 B · 流程完备性二次走查（字段链级）

方法：沿 F1–F8 每条流程逐步核对"该步输入字段是否由上游产出、输出是否有人消费"。

| 流程 | 字段链结论 | 断点/缺口 |
|---|---|---|
| F1 契约与激活 | ◐ | P1 身份三缺（A.1）；激活钥匙无类型载体（⑰）；D7 预算来源无留痕 |
| F2 观察 | ◐ | `Origin` 缺（A.1）；`Hierarchy` overexposure（A.2，已自记） |
| F3 控制→效果 | ◐ | `ExpectedEffect` 缺（D8）；binding locator overexposure（A.2）；`Parameters` 无类型通道（声明式已知，buyer 待） |
| F4 连续性 | ✅ | P23 进程内 verified；DemandHandle 幂等键在载荷 |
| F5 终局 | ✅ | 仅 `RuntimeOutcome` 缺 `productSessionId`（⑨ 派生） |
| F6 咨询 | ◐ | ADR-0046 三字段缺；budget/risk/capability 入口校验 deferred（H14） |
| F7 记忆 | 显式占位 | —（P20/P21） |
| F8 仿真/查询 | ✅ | schema 面齐（workspace/runtime-http/run-report） |

**二次走查总结论**：八条流程**字段链全部可走通，无结构性断链**；缺口收敛为四类
**元字段欠账**——①身份关联（taskInstance/session/correlation）、②来源性质（origin）、
③期望载荷（expected-effect）、④预算来源（valueOrigin）。与图级审计结论一致：
结构成立，元数据完整性欠账。修法高度收敛：一个 `Origin` 字段同时闭合 ADR-0047
gap、D6 判定输入、E5 前提显式化；一个必填/留痕的预算字段闭合 D1/D7。

## 附录 C · Grill 记录（grill-with-docs，2026-10-12）

**Round 1**（六题全前沿；底稿=本审计附录 A/B；裁决=Owner）：

| 题 | 议题 | 裁决 | 备注 |
|---|---|---|---|
| Q1 | 契约预算：硬拒收 vs 留痕宽松 | **B（留痕宽松）+ 预算可续授**：后续可续步骤，决策者 = UniAgent 与规则共同决定 | 续授机制解锁 Round 2（Q7–Q9） |
| Q2 | Origin 值域 | **A**：封闭二值 External \| PostActionSelf | 已落 CONTEXT「Observation Origin」 |
| Q3 | Origin 判定权 | **A + 禁时间启发式**：显式 attempt 关联机械推导；自报仅诊断 | 已落 CONTEXT；ADR 候选（0048） |
| Q4 | 期望载荷形状 | **B**：验证输入侧独立期望载荷（状态型+变化型两族谓词）；滚动三布尔迁移为实例 | 已落 CONTEXT「Expected Effect」；ADR 候选（0049） |
| Q5 | P10 locator 曝射 | **A′**：CanonicalBinding 移除 locator 三字段；Core 投影改从 DeliveryTarget 取 | 消费面已查清（EB+Core 两处） |
| Q6 | 期望渗透佐证政策 | **B**：判定侧立法"prior 影响的 association 连续性不得作为 MaterialEffect 唯一依据"+ 锚定测试 | 与 ⑦ 解耦 |

**Round 2**（续授机制，由 Q1 补充解锁）：

| 题 | 议题 | 裁决 | 备注 |
|---|---|---|---|
| Q7 | 续授通道 | **A（P25 咨询）**+ 触发时机需定义：UniAgent 按实际情况（正常推进未走完→该续就续），阈值式自动发起 | 触发形状 → Round 3 Q10 |
| Q8 | 天花板语义 | **A（两层顶）**：契约预算=初始额度；policy 声明绝对硬顶（续授不可突破）；两层均留痕 | 终止性落在 policy 硬顶（有限） |
| Q9 | 记账归属 | **A（轻记账）**：driver 内部消耗态 + P25 decision 留痕 + trace；不进 canonical Run State；崩溃恢复语义随 ⑰ | |

**Round 3**（触发与额度范围，收口轮）：

| 题 | 议题 | 裁决 | 备注 |
|---|---|---|---|
| Q10 | 触发闹钟归属 | **A（policy 定阈值）**：driver 进决策边界检查剩余 ≤ 阈值 → 标记续授窗口；大脑看剩余额度决定续多少；**终问允许携带续授提案**（防"有步数没轮次开口"死锁角） | 阈值与硬顶同源 policy |
| Q11 | 额度范围 | **A（机制统一）**：步数与轮次皆可续，硬顶各自独立 | 一套机制两种额度 |

**Grill 收口（2026-10-12，frontier 清空）**：11 题全部裁决。固化产物——
ADR-0048（Origin 值域+判定权）· ADR-0049（Expected Effect 载荷）·
ADR-0050（Budget Grant：P25 通道+两层顶+轻记账）；CONTEXT 落词 3 条
（Observation Origin 判定权更新、Expected Effect 新增、Budget Grant 新增）；
实施切片见 `protocol-fix-001-proposal.md`。本审计 D1/D6/D7/D8/E5 五项发现
随上述裁决闭合路径确立，D2/D3/D4/D5 维持既有 OPEN GATE 轨道（⑪⑯⑰/buyer）。
