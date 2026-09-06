# EXP-008 — Runtime View Exposure Hardening（P5 known leak + P11 potential overexposure）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 79519a43

## Intent

**WHAT**: 收敛协议基线剩余两处跨 Owner 过曝，使 Consumer 依赖最小
语义 projection 而非 canonical aggregate：

- **P5**：实测 Control 对 `RunState` 字段消费为**零**（ControlLoop 不
  dereference；全部 5 个 `IControlPolicy` 实现忽略 inputs 全部成员）
  → 移除 `SelectIntent` / `ControlInputs` 的 RunState；P5 标
  **no-current-buyer / deferred**（"当前无 buyer，因此 dependency
  暂时消失"），不造零载荷协议、不造空 DTO。
- **P11**：`WorldBeliefRevision` 整传 → 三个 consumer-specific
  immutable view（`BindingView` / `ActionAssuranceView` /
  `OutcomeAssuranceView`），由 World Model（Owner）在消费点即时派生。

**WHY**: Owner 的内部 canonical aggregate 不应因调用方便成为跨组件
协议（基线 P5 known leak / P11 potential overexposure 台账要求真实
闭合）。目标状态：Owner canonical aggregate → Owner derive →
consumer-specific immutable view → Consumer。

## Scope

- Control：`SelectIntent(view, slice)`（RunState 参数移除）；
  `ControlInputs(ContractView, Slice)`
- World 侧新增三个 view 类型 + `ScopedClaim` 协议表示 + WorldModel
  具名派生方法（realization 形态：`DeriveBindingView` /
  `DeriveActionAssuranceView` / `DeriveOutcomeAssuranceView`）
- Effect Boundary：`Bind` / `IsBindingValid` / `Dispatch` 改收
  `BindingView`
- Assurance：`Judge` 改收 `ActionAssuranceView`；
  `EvaluateObligations` / `JudgeOutcome` 改收 `OutcomeAssuranceView`
- UniKernel 组合点改派生 view（Act：BindingView ×1 复用 +
  ActionAssuranceView；EvaluateTerminal：OutcomeAssuranceView）
- `Conflict` 显式升格 protocol vocabulary（基线 P11 改写注记）
- 结构断言测试：shape allowlist（×3）+ consumer 无持久 view 字段 +
  consumer 公开签名零 aggregate + correlation-mismatch stale 负向
- 测试 seam 迁移（CBA-005 式台账，无静默删除）
- 文档：基线 P5/P11 状态改写（迁移完成时）；ADR-0011 与 CONTEXT.md
  词条立项时已落档，VERIFY 核对一致

## Out of Scope（红线）

- WorldModel 内部 ContainerGraph 实现；UIWorld 组件拆分；Fast / Slow
  Perception；Observation Control；freshness policy；DispatchResult /
  EffectReceipt vocabulary（P15）；Session / Goal↔Run correlation；
  Memory；F2 authority hardening；legacy migration；docs/architecture
  全量物理迁移；WorldGraph schema；Control algorithm / FSM /
  traversal algorithm
- **尤其禁止**因"既然做 View"开始设计 WorldModel L4
- proof 载荷 `BasisEvidenceIds` 语义收窄（OUT-003 锁定语义；全量
  basis 留在 OutcomeAssuranceView 是既有真实 proof buyer）
- view currentness validation API（无异步持有 / 跨 cycle buyer，
  ADR-0011 不预先设计）
- `IsSliceValid` / Slice 语义（P4 verified，零触碰）

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | P5 = no-current-buyer / deferred：实测零字段消费 → 移除 run 侧输入；不是 CLOSED 成零载荷协议；未来真实 buyer（progress / obligations 的 Control 消费）出现再恢复载荷并立真正的 consumer view | R1-Q1 修正 |
| D2 | P11 拆三个 consumer-specific view（BindingView→EB / ActionAssuranceView→Judge / OutcomeAssuranceView→obligation 路径），不做并集 / 万能 View；Assurance 两条消费画像不相交（action-local 不读 WorldState/basis/uncertainty；obligation 路径不读 FreshnessBasis） | R1-Q2 |
| D3 | 协议 invariant = **Owner 唯一派生权**（canonical Owner 唯一有权派生 consumer projection；consumer 只消费 projection，不得从 aggregate 自行投影）；具名 `DeriveXxx` 方法是 realization 不是 invariant | R1-Q3 修正 |
| D4 | view 载荷边界 = **Owner-owned fact ✅ / consumer-owned judgment ❌**：belief facts（含派生事实 `HasTargetSubjectClaim` / `HasConflictOnTarget`）可携带；`BindingAllowed` / `ActionAdmissible` / `ShouldRetry` 等 consumer 裁决永不进 view。判据是裁决权归属，不是 bool vs record | R1-Q4 修正 |
| D5 | 类型复用判据：immutable + 单一语义 + 无 Authority 行为 + **已是 protocol/domain vocabulary**。`FreshnessBasis` 直接复用（ADR-0010 已锁）；`Conflict` 显式升格（概念已词汇化、五字段全消费）；`WorldClaim` 不外泄 → `ScopedClaim(Value, EvidenceId)` 协议表示 | R1-Q5 修正 + R2-Q4 |
| D6 | `BindingView = {RevisionId, RevisionNumber, HasTargetSubjectClaim:bool}`；EB 从 fact 推 UnknownTarget；不带 claim 值（Bind 从不读 value，binding TargetValue 来自 candidate）。命名定 `HasTargetSubjectClaim`（非 `TargetSubjectPresent`——避免与 WorldGraph membership 混淆，且不偷做 binding 判断） | R2-Q1 修正 + R3-Q1 |
| D7 | `ActionAssuranceView = {RevisionId, RevisionNumber, FreshnessBasis, HasConflictOnTarget:bool}`；Judge 只消费冲突存在性不消费数量 → bool 非 int；`no-unresolved-conflict` 判定由 Assurance 从 fact 推出；不带 claims / basis / uncertainty / graph | R2-Q2 修正 |
| D8 | `OutcomeAssuranceView = {RevisionId, ConflictingClaimCount, Claims(scoped by obligation subjects, IReadOnlyDictionary<string, ScopedClaim>), Conflicts(scoped by obligation subjects, IReadOnlyList<Conflict>), BasisEvidenceIds(全量 refs, IReadOnlySet<string>)}`；全量 basis 例外获批：两个真实 buyer（ResolveBackingEvidence membership + OutcomeProof.BasisEvidenceIds 载荷，后者 OUT-003 锁定语义） | R2-Q3 |
| D9 | view 生命周期四条：consumption-scoped / ephemeral projection；每次正常消费前由 Owner 从 current revision 即时派生；consumer 不缓存、不持有 view 跨 operation 重放；consumer 校验 view.RevisionId 与本次 intent / binding correlation anchor 一致。view 自身不是 currentness authority——**不新增 `IsViewValid`**（view 无"世界已推进"信息，自证 current 需第二 truth）；异步持有 buyer 出现再设计独立 currentness validation | R2-Q5 修正 |
| D10 | 结构验收机制 = **public shape allowlist**（反射：每个 view 的 public member 集合 = 白名单，新增成员即失败）+ consumer 实例字段反射断言（三 consumer 类不得直接 / 经 collection 持有 view；方法参数与局部变量允许）+ 签名反射断言（Control 零 RunState；Assurance / EB 零 WorldBeliefRevision）。不粗暴禁 `IReadOnly*`——合法 scoped view 可携带集合，禁的是 owner-internal aggregate 类型 / owner logs / mutable collections | R1-Q6 修正 + R2-Q6 + R3-Q2 |
| D11 | stale 证明 = **correlation mismatch**：旧 view(rev-N) + current intent/binding(rev-N+1) → consumer 既有 revision 检查族 fail-closed（Bind stale-revision / Judge binding-revision-currentness、intent-basis-currentness / Dispatch binding-stale）；不测"consumer 只拿旧 view 自行发现世界已推进"（架构无此信息） | R2-Q6-5 修正 |
| D12 | ADR-0011 锁四原则（consumer-specific immutable projection / Owner-only derivation / projection ≠ second truth / 不承载 consumer-owned judgment）+ 类型复用判据 + P5 deferred 结论表述；立项时落档 | R1-Q7 |
| D13 | Route=Direct（单一语义切片：三 view + 派生 + 签名收缩高内聚，拆散会把表达与消费割裂）；depth=decision-heavy；TDD RED 先行；测试迁移走 CBA-005 式台账 | 与 CBA-005 D8 / ING-006 D8 / FRS-007 D11 同构 |

## Acceptance（10 条）

1. **Control 零 RunState**：`SelectIntent` / `ControlInputs` 移除
   RunState；反射断言 Control 命名空间公开签名无 RunState 参数。
2. **EB 零 WorldBeliefRevision**：`Bind` / `IsBindingValid` /
   `Dispatch` 只收 `BindingView`；反射签名断言。
3. **Assurance 零 WorldBeliefRevision**：`Judge` 只收
   `ActionAssuranceView`；`EvaluateObligations` / `JudgeOutcome` 只收
   `OutcomeAssuranceView`；反射签名断言。
4. **shape allowlist**：三个 view 的 public member 集合 = D6/D7/D8
   白名单（反射精确匹配，新增任何 public member 即失败）；且不含
   owner-internal aggregate 类型（WorldBeliefRevision / RunState /
   WorldGraph 型别 / owner logs / mutable collections）。
5. **ephemeral 证明**：旧 view(rev-N) + current intent/binding(rev-N+1)
   → correlation mismatch fail-closed（Bind / Judge / Dispatch 三面）；
   反射断言 ControlLoop / RuntimeAssurance / EffectBoundary 实例字段
   不得直接或经 collection 持有三个 view（方法参数 / 局部变量允许）。
6. **CBA-005 回归**：Bind→Judge→Gate 序 + 三元组 correlation 断言语义
   不变（直调 seam 用例迁移不删除，台账逐条留痕）。
7. **FRS-007 回归**：freshness 三态 + Scenario 14 语义不变
   （FreshnessBasis 经 ActionAssuranceView 抵达 evaluator，窄输入
   语义不变）。
8. **上层回归**：OUT / ING / GEV / E2B 全 GREEN；Agent 层零改动；E2B
   8 断言零漂移（ING-006 D1 冻结延续）。
9. **全量 GREEN**：≥70 通过、无静默测试删除（迁移台账 CBA-005 式
   留痕）。
10. **文档同步**：基线 P5 → deferred(no-current-buyer，dependency
    暂时消失) / P11 → verified（Reference Realization = 三 view）+
    `Conflict` 升格注记 + §5 第二梯队清单更新；ADR-0011 / CONTEXT.md
    一致性核对（立项已落档）。

## Constraints

- ADR-0011 与协议基线（改写后 P5/P11）为直接权威；ADR-0009（三元组
  correlation）/ ADR-0010（freshness 消费相对）语义不动
- Target v0.1 不变量 8-27、32-34、42 不可违反
- 1:1:1（Run:World:Control 单 run）assumption 保持；不引入 Session /
  multi-run correlation
- Deferred ①-⑫ 不偷解；WorldGraph schema / L4 / 观察控制等 Out of
  Scope 红线不越
- view 派生在单线程同步组合内（Act 内 world 不中途演进——reflux 在
  dispatch 后），ephemeral 语义由消费点即时派生结构性保证

## Verification

```yaml
level: DETERMINISTIC   # 纯内存，无 IO / 真机 / 时钟依赖
method: >
  RED（目标类型 stub + 新增用例先行：shape allowlist / 无持久字段 /
  签名零 aggregate / correlation-mismatch / 迁移用例改指向新签名）→
  GREEN（最小实现：三 view + WorldModel 派生 + 消费者签名收缩 +
  UniKernel 组合点）→ REVIEW（fresh SubAgent）→ VERIFY（验收 10 条
  逐条 + 全量 dotnet test + grep 残留 + 台账同步核对）
expected: >
  验收 10 条全 GREEN；全量测试 GREEN（≥70，迁移不删除用例）；
  Control 命名空间公开签名零 RunState 引用；Assurance / Effects 公开
  签名零 WorldBeliefRevision 引用；E2B 断言零改动
actual: >
  2026-09-07 dotnet test（UniClaw.Kernel.slnx；SDK 10.0.400；net10.0）：
  RED 失败 38 / 通过 22（N5/N6 行为失败 = 派生 stub；N1/N2/N3/N4/N7
  结构项即过；Agent 17 通过零触碰）→ GREEN 失败 0 / 通过 77（Kernel 60
  = 既有 53 + N1-N7；Agent 17）→ 终验复跑 77/77。grep：Control 命名空间
  RunState 零代码引用；Assurance/Effects WorldBeliefRevision 仅 doc 注释
  （签名零引用）；EvidenceToBeliefTests 零 diff；Agent 层零 diff；改动面
  = plan After 表（src 7 文件 + tests 5 文件）+ docs。验收 1-10 逐条证明
  映射见 evidence；迁移台账 27 处 derive 调用点 + 1 处实参移除，grep 实测
  校正（plan 草稿 24 → 实际 C2E 19）
evidence: evidence/2026-09-07-exp-008-deterministic.md
```

## Assumptions

- 单线程内存模型（§22 开放项延续）；view 在单次同步消费内用毕即弃
- 测试 policy（ScriptedPolicy / SequencedPolicy / ObserveOnlyPolicy）
  不读 inputs 成员（ENTRY 实测），移除 RunState 零行为影响
- obligation subjects 在 EvaluateTerminal 派生点可得（Kernel 持
  ProofObligationState）；scope 参数含空 subject（占位 obligation）时
  该 subject 派生为无 claim
- `FreshnessEnforcementTests` 直调 seam（IsBindingValid ×2 等）属机械
  迁移面，行为断言不变

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| 零载荷 P5 协议（保编号 / 空 DTO / 空类型占位） | 没有 buyer 就没有协议载荷；空协议是形态闭环、语义空转（R1-Q1c 亦拒） |
| 基线种子 RunControlView（objective / obligations / progress） | 零读者的字段违反"真实 buyer"原则，为假想 consumer 提前加字段 |
| 单一 WorldView 并集（Assurance / EB 共用） | action-local 消费携带从不读的 claims / basis / uncertainty，Assurance 内复制小 P11 泄漏 |
| consumer 侧自行投影（view factory 在 consumer） | consumer 仍依赖完整 aggregate，泄漏未消除，违反 Owner-only derivation |
| 通用查询接口（world.Query 按需取语义） | 暴露决定权从 Owner 挪到调用方，God DTO 的接口版 |
| view 预计算判定（TargetSubjectResolves / ConflictsOnTarget 计数） | Resolves 偷做 EB binding 判断；计数多暴露无 buyer 的数量信息；收敛为纯 belief fact 命名 + bool |
| IsViewValid 类 view 自证 currentness | view 无"世界已推进"信息，自证需第二 currentness truth（ADR-0011） |
| OutcomeAssuranceView basis 收窄为 scoped | 改 OUT-003 proof 载荷（BasisEvidenceIds）语义，超本 change 边界 |
| 复用 WorldClaim 跨边界 | 无词汇地位（CONTEXT.md 无词条），owner-internal type 不因方便泄漏；ScopedClaim 显式协议表示 |
| TargetSubjectPresent 命名 | 与 WorldGraph membership（graph 含 subject ≠ 有 claim）混淆空间 |

## Owner / Authority Impact

- World Model：**新增** consumer view 派生职责（表达面扩展；无新判定
  权，view 不含 judgment）
- Run Model：零变化（P5 移除是消费侧收缩，canonical 记录面不动）
- Control：输入面收缩（RunState 移除；ContractView / Slice 输入不变）
- Assurance / Effect Boundary：输入面从 aggregate 换 view；**判定权
  不变**（四态拒绝 / no-unresolved-conflict / no-blind-retry 等从
  Owner fact 推出，check 名与语义不变）
- 无 Owner 迁移、无第二 truth、无新 Authority

## ADR Refs

- docs/adr/0011-consumer-views-are-owner-derived-immutable-projections.md
  （本 change 裁决来源）
- docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md
  P5 / P11 + §5 第二梯队 + §6 重开条件
- ADR-0009（三元组 correlation，语义不动）/ ADR-0010（freshness 消费
  相对，语义不动）
- CBA-005 D3/D5（检查集与 RejectionReason 惯例）、ING-006 D1（E2B
  语义冻结基线）

## Residual Risks

- derive 方法签名细节（scope 参数形态、null scope 处理）与测试组织
  属 realization，REVIEW 时可能微调（语义不变）
- view 类型命名已定（BindingView / ActionAssuranceView /
  OutcomeAssuranceView / ScopedClaim），如 REVIEW 发现与既有词汇冲突
  可微调（白名单随调）
- 异步持有 / 跨 cycle view buyer 出现时需回 RESOLVE 设计 currentness
  validation（D9 未永久禁止）
- P5 恢复条件（真实 Control buyer 出现）依赖未来 change 主动援引
  ADR-0011 与基线 deferred 状态

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-07 | →resolved | ENTRY 核验（base 79519a43、70/70 GREEN、P5 零消费实测：ControlLoop 不 dereference + 5 policy 忽略 inputs、P11 消费画像逐方法实测、WorldGraph/ParentRevisionId 零外部读者、测试触碰面 grep 实测）+ grill 三轮（R1 7 问 / R2 6 问 / R3 2 问，共 6 处用户修正）定稿 D1-D13 / acceptance 10；ADR-0011 + CONTEXT.md（Consumer View 词条 + Run Snapshot 改写 deferred + Slice avoid 修正）即时落档；基线 P5/P11 状态改写留 implementation 完成时 |
| 2026-09-07 | resolved→planned | PLAN 落盘 plans/2026-09-07-exp-008-runtime-view-exposure-hardening.md；Route: Direct（D13）；停在 implementation 前等放行 |
| 2026-09-07 | planned→implemented | TDD RED（失败 38 / 通过 22：N5/N6 行为失败 + 36 既有 act 路径 = 派生 stub；N1-N4/N7 结构项即过；Agent 17 零触碰）→ GREEN（77/77：Kernel 60 = 既有 53 + N1-N7；Agent 17）；改动面 = plan After 表（src 7 文件：1 新 + 6 改；tests 5 文件：1 新 + 4 迁移）+ docs（基线 P5/P11/§5 改写 + CONTEXT.md + ADR-0011）；迁移台账按 grep 实测校正（C2E 19 处，草稿估算 24） |
| 2026-09-07 | implemented→reviewed | fresh SubAgent 六轴 APPROVE（A1-A6 全 PASS：白名单逐成员核对 / 判定权位置逐行核对 / ephemeral 字段与派生点核对 / Deferred grep 零命中 / 冻结面零 diff / 台账逐项核对 + 独立 dotnet test 自跑 77/77）；minor：文档改动面（CONTEXT.md + 协议基线）补入台账（属验收 10 应有产物，已列入 evidence 改动面清单）；nit ×2：UniKernel `_ = Current ?? throw` 前置门冗余（保留原错误顺序，接受）；.tmp-hf-intake/ 无关杂物不入提交（延续 FRS-007 F2） |
| 2026-09-07 | reviewed→verified | 验收 1-10 逐条对照 evidence 全 GREEN（N1-N7 ↔ 验收 1-5 真实映射、C2E/FRS/OUT/ING/GEV/E2B 回归、grep 结构残留零命中）；终验复跑 77/77；四元组 actual/evidence 完整；台账 27 处 derive 调用点 + 1 处实参移除与 diff 精确一致 |
| 2026-09-07 | verified→closed | 范围完成 + acceptance 被证明 + 无未授权改动（冻结面零 diff）；文档即时同步（立项：ADR-0011 / CONTEXT.md 词条；实现：基线 P5→deferred / P11→verified / §5 清单）；提交采用显式路径清单排除 .tmp-hf-intake/；无阻塞 Human Decision |
