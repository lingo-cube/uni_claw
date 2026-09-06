# 0011 — Consumer View：Owner 派生的不可变最小投影，不是第二 truth

协议基线 P5（known leak：Control 整传 `RunState` 聚合）与 P11
（potential overexposure：Assurance / Effect Boundary 整传
`WorldBeliefRevision` 聚合）记录了两处「Owner 的内部 canonical
aggregate 因调用方便成为跨组件协议」。EXP-008 立项 grill
（2026-09-07，三轮）裁决如下。

## 原则

1. **Projection 的存在必须由真实 buyer 证明**。view 的每个字段都必须
   有真实读取证据（实测消费，不以架构文档倒推）；不因 aggregate 里
   存在某字段而自动加入 view；不为未来假想 consumer 提前加字段；
   不造万能 View / God DTO / shared mutable context；consumer-specific
   view 可以不同，不强求统一。
2. **Owner-only derivation**。canonical Owner 唯一有权从 canonical
   state 派生 consumer projection；consumer 只能消费 projection，不能
   从 aggregate 自行投影（否则泄漏未消除）。具名派生方法
   （`DeriveXxx`）是当前 realization，不是协议 invariant。
3. **Projection ≠ second truth**。view 是 consumption-scoped /
   ephemeral 的不可变投影：每次消费前由 Owner 从 current revision
   即时派生；consumer 不缓存、不持有 view 跨 operation 重放；view
   自身不是 currentness / freshness authority——view 手里没有「世界
   已推进」的信息，无法自证仍 current（检测需要另一 current
   authority 输入，等于把 Owner 拉回来）；consumer 以 correlation
   anchor（view.RevisionId 与本次 intent / binding 的 revision
   identity）校验一致性。异步持有 / 跨 cycle 使用 view 的真实 buyer
   出现前，不设 view validity API。
4. **view 只携带 Owner-owned fact，不携带 consumer-owned judgment**。
   Owner 拥有的 belief facts（含 Owner 派生的世界事实，如
   `HasTargetSubjectClaim` / `HasConflictOnTarget`）可以携带；consumer
   拥有裁决权的判定（`BindingAllowed` / `ActionAdmissible` /
   `ShouldRetry` 之类）永不进 view。判据是语义裁决权的归属，不是
   bool vs record 的形态。
5. **类型复用判据**：immutable + 单一语义 + 无 Authority 行为 + 已是
   protocol / domain vocabulary。owner-internal type 即便 immutable
   也不因方便直接泄漏，跨边界需显式 promote（本次：`Conflict` 显式
   升格为 protocol vocabulary——概念已在 CONTEXT.md / E2B 词汇化且
   五字段全消费；`WorldClaim` 无词汇地位，不外泄，claim 语义经
   `ScopedClaim` 协议表示）。

## P5 裁决（Run Model → Control Loop）

实测：Control 对 `RunState` 的字段消费为**零**（ControlLoop 不
dereference 任何字段；全部 `IControlPolicy` 实现忽略 inputs 全部
成员）。因此：

```text
Run Model → Control 的 runtime data dependency = 0
→ 移除 SelectIntent / ControlInputs 中的 RunState
→ P5 = no-current-buyer / deferred
```

P5 不是 CLOSED 成一条零载荷协议，也不为保协议编号制造空 DTO；
结论表述为「当前无 buyer，因此 dependency 暂时消失」。未来出现
progress / obligations 的真实 Control buyer 时，恢复 P5 载荷并按
上述原则立真正的 consumer view。

## 结构验收方式

- **public shape allowlist**：每个 view 类型的 public member 集合 =
  白名单，新增任何 public member 即测试失败（反射）。不使用「禁
  某些集合接口」的宽规则——合法的 scoped view 可以携带
  `IReadOnlyList` / `IReadOnlySet`；真正禁止的是 owner-internal
  aggregate 类型（`WorldBeliefRevision` / `RunState` / WorldGraph /
  owner logs / mutable collections）。
- **消费者无持久持有**：Control / Assurance / Effect Boundary 的
  实例字段不得直接或经 collection / wrapper 持有 consumer view
  （方法参数与局部变量允许——它们是一次 operation 的即时消费）。
- **签名零 aggregate**：consumer 公开签名不得出现 canonical
  aggregate 类型参数（反射断言）。

## Considered Options

- **零载荷 P5 协议（保留边与编号，造空 DTO）**：被拒——没有 buyer
  就没有协议载荷；空协议是形态闭环、语义空转。
- **基线种子 RunControlView（按 P5 minimal payload 携带 objective
  status / obligation statuses / progress）**：被拒——零读者的字段
  违反「真实 buyer」原则，等于为假想 consumer 提前加字段。
- **单一 WorldView（Assurance / EB 共用并集）**：被拒——action-local
  消费将携带它从不读的 claims / basis / uncertainty，在 Assurance
  内部复制一个小型 P11 泄漏。
- **consumer 侧自行投影（view factory 在 consumer）**：被拒——
  consumer 仍依赖完整 aggregate，泄漏未消除，违反 Owner-only
  derivation。
- **通用查询接口（`world.Query(...)` 按需取语义）**：被拒——把
  「哪些语义该暴露」的决定权从 Owner 挪到调用方，是 God DTO 的
  接口版。
- **view 自证 currentness（`IsViewValid` 类 API）**：被拒——view
  无法凭自身信息发现世界已推进；引入该 API 等于制造第二套
  currentness truth。
- **view 携带预计算判定布尔（如 `TargetResolves` /
  `ConflictsOnTarget` 计数）**：被拒 / 收窄——`Resolves` 措辞偷做
  EB 的 binding 判断；冲突计数多暴露一层无 buyer 的数量信息。收敛
  为纯 belief fact 命名与 bool 形态（`HasTargetSubjectClaim` /
  `HasConflictOnTarget`）。

## Consequences

- `Conflict` 类型显式升格为跨边界 protocol vocabulary（协议基线
  P11 改写时注记）；`WorldClaim` 保持 owner-internal。
- 协议基线 P5 状态改写为 deferred（no-current-buyer），P11
  Reference Realization 改写为三个 consumer view
  （BindingView / ActionAssuranceView / OutcomeAssuranceView），
  随 EXP-008 实现闭合时同步。
- view 的 currentness 校验语义 = consumer 侧 correlation anchor
  比较（既有 currentness 检查族），不是 view 自身能力；stale view
  的负向证明用 correlation mismatch（旧 view + current
  intent/binding → fail-closed），不测「consumer 只拿旧 view 自行
  发现世界推进」。
- 未来出现异步持有 / 跨 cycle view buyer 时，须回 RESOLVE 设计
  独立 currentness validation（本 ADR 不预先设计）。
