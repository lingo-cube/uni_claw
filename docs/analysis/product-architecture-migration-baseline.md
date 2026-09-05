# UniClaw Product Architecture Migration Baseline

> DocumentType: `PRODUCT_ARCHITECTURE_MIGRATION_BASELINE_CANDIDATE`
>
> Status: `CANDIDATE / NOT_ADOPTED`
>
> Authority: `NONE`
>
> Date: `2026-09-05`
>
> Scope: `Legacy / Current → Target Product Architecture Migration Procedure`
>
> Target Architecture: [Target Product Architecture — L0–L3](product-architecture-baseline-l0-l3.md)
>
> Semantic Mapping: [Legacy → Target Architecture Mapping](legacy-to-target-architecture-mapping.md)
>
> Forbidden Boundary: 本文不定义 Product Architecture，不授权实现，不改变现行 Architecture、Protocol、Runtime Contract、active change、代码、测试、Owner 或生命周期。

---

## 0. 文档目的

本文只回答：如何把现行或历史结构安全迁移到已经定义的 Target Product Architecture。

它不重新定义 Target 语义。任何迁移单元必须先引用 Target Owner/Authority，再处理 source-side state、writer、reader、projection、cutover 和历史处置。

## 1. Migration outcome

迁移完成的含义不是“新类存在”或“旧文件已删除”，而是：

```text
Approved target semantic is preserved
  + exactly one canonical owner exists
  + exactly one authority exists per judgment class
  + all writers use the target path
  + all readers consume canonical state or read-only projection
  + legacy authority is retired
  + behavior is proven by evidence
  + rollback does not restore dual truth
```

## 2. Authority-first

任何迁移工作开始前，必须按当前项目 Authority Order 加载并记录：

1. governing architecture / constitution；
2. approved change-local requirements；
3. applicable decisions；
4. target architecture candidate and adoption status；
5. current-state projection；
6. active work and remaining gates；
7. source implementation evidence。

低优先级来源不得覆盖高优先级来源：

```text
Target intent
  != current authorization

Code existence
  != architecture authority

Historical design
  != current lifecycle truth
```

若 Owner、Authority、lifecycle 或 source/target semantics 发生冲突，迁移停止在 architecture decision gate；不得通过 adapter、fallback、双写或术语改名自动裁决。

## 3. Owner / Writer Census

### 3.1 必查对象

每个迁移单元必须识别：

- canonical mutable state；
- judgment / decision class；
- identity 与 scope；
- lifecycle 创建、修改、终止路径；
- all writers；
- all readers；
- reducers / transition functions；
- caches、indexes、snapshots、projections；
- event / trace / evidence producers；
- external side-effect paths；
- test fixtures、serialization 与 compatibility consumers。

### 3.2 BEFORE census

```text
State / Judgment:
Semantic Meaning:
Current Canonical Owner:
Current Authority:
Current Writers:
Current Readers:
Lifecycle Start:
Lifecycle Mutation:
Lifecycle Terminal:
Identity / Scope:
Persistence:
Derived Projections:
External Effect Paths:
Known Active Work:
Known Conflicts:
```

未完成 BEFORE census，不得新增 target state 或 writer。

### 3.3 AFTER census

```text
Target Canonical Owner:
Target Authority:
Target Writers:
Target Readers:
Retired Writers:
Read-only Compatibility Paths:
Canonical Source:
Exact-prior Guard:
Terminal Guard:
Verification Evidence:
Rollback Owner:
Remaining Gate:
```

AFTER census 必须证明 source 与 target 不会长期并持正式 authority。

## 4. State disposition vocabulary

| Disposition | Definition | Required constraint |
|---|---|---|
| `KEEP` | 保留语义、Owner 与 authority | 可替换内部实现，不改变 contract |
| `MOVE` | 将同一 canonical state 移到 target boundary | cutover 时同步关闭旧 writer |
| `ADAPT` | 通过 typed adapter 连接 source 与 target contract | adapter 不形成 state、judgment 或 lifecycle authority |
| `DERIVE` | 从 canonical source 生成 read-only projection | projection 不可回写 canonical source |
| `SHADOW` | target path 只读计算并与 official path 比对 | 不影响动作、Outcome、外部输出或 official state |
| `CUTOVER` | 把 sole ownership / authority 一次性切到 target | 必须有 exact-prior、atomicity、old-writer guard 与 rollback receipt |
| `RETIRE` | 取消 source path 的生产、写入或判断职责 | 可以保留只读兼容，不得恢复正式 authority |
| `SUPERSEDE` | 声明 source semantic/artifact 的明确 successor | 必须保留 predecessor/successor mapping 与日期 |
| `ARCHIVE` | 冻结历史内容供审计与检索 | Archive != Delete；不得改变历史事实 |

禁止使用含义不明确的 `REUSE`、`REFACTOR`、`MIGRATE_LATER` 或“暂时双轨”代替正式 disposition。

## 5. No dual mutable truth

### 5.1 Rule

任一时刻，每个 canonical mutable state 只能有一个 official owner，每类 judgment 只能有一个 official authority。

不允许：

- source 和 target 同时接受正式写入；
- 两个 reducer 分别生成可被生产 consumer 使用的 current state；
- target shadow result 影响 action、completion 或 public outcome；
- compatibility projection 回写 source 或 target；
- fallback 在失败时静默恢复旧 authority；
- 把 cache、snapshot、event stream 或 trace 当成第二个 state store。

### 5.2 Allowed shadow

Shadow 必须同时满足：

- read-only；
- deterministic or replayable input；
- 无 external effect；
- 无 command / completion / outcome authority；
- divergence 可观测；
- 有关闭开关和 kill criteria；
- 生命周期在 cutover 前结束或转为 target official path。

## 6. `NET_NEW_MUTABLE_TRUTH = 0`

### 6.1 Mandatory budget

每个完成的 migration unit 和每个 owner cutover stage 必须满足：

```text
BEFORE_MUTABLE_TRUTH_COUNT
+ ADDED_CANONICAL_TRUTH
- RETIRED_CANONICAL_TRUTH
= AFTER_MUTABLE_TRUTH_COUNT

NET_NEW_MUTABLE_TRUTH
= AFTER_MUTABLE_TRUTH_COUNT - BEFORE_MUTABLE_TRUTH_COUNT
= 0
```

Read-only projection、immutable event、EvidenceRef 和 offline shadow result 不计为 mutable canonical truth，但必须证明其不可回写。

### 6.2 Rejection conditions

出现以下任一情况即拒绝进入下一 Gate：

- 新增 canonical state，却没有对应 retired source owner；
- 新增 writer，却没有 writer isolation；
- 无法说明 cache 是否可变、是否被 consumer 当 truth；
- target state 可以被 source callback 或 compatibility layer 回写；
- transient dual truth 没有明确截止 Gate；
- mutable-state budget 依赖未验证的“旧路径应该不会再调用”。

## 7. Exact-prior transition

所有 canonical state mutation 与 owner cutover 必须拒绝 stale prior。

可接受的 guard 包括：

- exact revision；
- compare-and-swap token；
- immutable accepted-evidence set identity；
- sequence / epoch；
- state-machine legal prior state；
- equivalent deterministic concurrency guard。

最小语义：

```text
Prepare(expectedPrior, input)
  → RejectedStalePrior
  | Prepared(nextImmutableState, receipt)

Commit(expectedPrior, preparedState)
  → RejectedStalePrior
  | Committed(newRevision)
```

禁止 last-write-wins、blind overwrite、hidden mutable reference 或通过重试掩盖 stale prior。

## 8. Evidence-backed cutover

### 8.1 Required evidence

Cutover 之前至少需要：

- approved target Owner / Authority mapping；
- complete writer and reader census；
- BEFORE / AFTER mutable-truth budget；
- exact-prior rejection tests；
- typed contract tests；
- deterministic scenario evidence；
- shadow divergence report（适用时）；
- stale writer / forbidden dependency guard；
- terminal and fail-closed behavior evidence；
- rollback plan and receipt format；
- 对真实环境敏感能力的 environment/device evidence。

### 8.2 Evidence levels

| Level | Evidence | Can prove | Cannot prove alone |
|---|---|---|---|
| `E1 Contract` | schema、type、owner/authority guard | boundary exists | behavior correct |
| `E2 Deterministic` | unit/property/reducer/transition tests | local semantics | integration or device behavior |
| `E3 Scenario` | end-to-end deterministic scenario | cross-owner workflow | real environment compatibility |
| `E4 Environment` | device/GUI/provider evidence | environment-sensitive behavior | every unsupported scenario |

Focused green 不等于 migration graduation。任何非绿色 suite 必须分类为 introduced、baseline、environment、fixture、process 或 unknown；不能用“与本变更无关”而不提供 evidence。

### 8.3 Cutover receipt

```text
Migration Unit:
Cutover Timestamp:
Prior Official Owner:
New Official Owner:
Prior Revision / Epoch:
New Revision / Epoch:
Old Writers Disabled:
New Writers Enabled:
Compatibility Readers:
Mutable Truth Before:
Mutable Truth After:
NET_NEW_MUTABLE_TRUTH:
Verification EvidenceRefs:
Rollback Boundary:
Remaining Gates:
```

## 9. G0–G7 migration gates

所有 Gate 必须串行；禁止以局部验证自动批准后续阶段。

### G0 — Semantic Reconciliation

**Purpose**

对齐 source semantics、Target Architecture 与 Legacy Mapping，识别术语相同但 Owner/Authority 不同的冲突。

**Exit evidence**

- source → target semantic table；
- unresolved conflict list；
- in-scope / out-of-scope；
- target adoption status；
- explicit human decisions still required。

**Forbidden**

不修改 Runtime state、writer 或 behavior。

### G1 — Owner / Writer Census

**Purpose**

建立 BEFORE owner、authority、writer、reader、lifecycle 与 mutable-truth inventory。

**Exit evidence**

- completed census；
- all hidden writers and callbacks identified；
- initial disposition table；
- `NET_NEW_MUTABLE_TRUTH` budget；
- owner/authority conflicts resolved or gated。

### G2 — Contract and Transition Seams

**Purpose**

在不切换 official owner 的情况下建立 typed input/output、immutable state、reducer、exact-prior guard 与 read-only projection seam。

**Exit evidence**

- contract tests；
- stale-prior rejection；
- no authority leak；
- old official path unchanged；
- seam removal path。

**Rollback**

移除 additive seam；source owner 保持不变。

### G3 — Shadow / Divergence

**Purpose**

以相同 Evidence 输入运行 target read-only computation，并度量与 official path 的 divergence。

**Exit evidence**

- divergence dimensions；
- threshold；
- known acceptable divergence；
- unknown divergence count；
- kill criteria；
- proof that shadow has no external authority。

**Rollback**

关闭 shadow；official path 不变。

### G4 — Sole-owner Cutover

**Purpose**

将 canonical owner / authority 一次切换到 target path。

**Entry conditions**

- G0–G3 passed；
- cutover and rollback receipts ready；
- old writer shutdown is atomic or fail-closed；
- `NET_NEW_MUTABLE_TRUTH = 0` after commit。

**Exit evidence**

- new owner is sole writer；
- old writers rejected mechanically；
- current consumers read target state or read-only projection；
- exact-prior and terminal guards pass。

**Rollback**

只能回到明确记录的 prior official owner；不得让 source 与 target 同时恢复写入。

### G5 — Consumer Migration

**Purpose**

将 decision、projection、verification、diagnostics 与 protocol consumers 迁到 target canonical state 或 compatibility projection。

**Exit evidence**

- consumer matrix complete；
- stale read and fallback tests；
- no reverse write through compatibility path；
- source reader retirement status。

**Rollback**

按 consumer 回退到只读 compatibility surface，不恢复旧 canonical writer。

### G6 — Scenario Graduation

**Purpose**

证明迁移后的 Product behavior 满足 success、failure、uncertainty、recovery、effect verification 与 completion semantics。

**Exit evidence**

- contract + deterministic + scenario evidence；
- environment evidence when required；
- failure classification；
- unsupported scenario list；
- no unresolved authority leak；
- graduation decision。

### G7 — Supersede / Retire / Archive

**Purpose**

移除 source-side parallel authority，建立 successor mapping，并冻结历史材料。

**Exit evidence**

- retired writers/readers list；
- predecessor/successor mapping；
- archive manifest；
- retention policy；
- separate delete decision if deletion is requested；
- final rollback boundary。

**Forbidden**

不得根据 path、name、age、file count 或“看起来没用”直接删除历史。

## 10. Migration Unit Contract Card

```text
Migration Unit:
Buyer / Scenario:

Source Meaning:
Source Owner:
Source Authority:
Source Lifecycle:
Source Identity / Scope:
Source Writers:
Source Readers:

Target Meaning:
Target Owner:
Target Authority:
Target Lifecycle:
Target Identity / Scope:

KEEP:
MOVE:
ADAPT:
DERIVE:
SHADOW:
CUTOVER:
RETIRE:
SUPERSEDE:
ARCHIVE:

BEFORE Mutable Truths:
Added Mutable Truths:
Retired Mutable Truths:
AFTER Mutable Truths:
NET_NEW_MUTABLE_TRUTH:

Exact-prior Guard:
Terminal Guard:
Authority Isolation:
Shadow Divergence Policy:
Kill Criteria:

Verification E1:
Verification E2:
Verification E3:
Verification E4:
Failure Classification:

Cutover Gate:
Cutover Receipt:
Rollback Owner:
Rollback Boundary:
Successor Mapping:
Remaining Human Decision:
```

任何字段为 unknown 时必须显式填写 `UNKNOWN / GATED`，不能留空后默认授权。

## 11. Rollback baseline

### 11.1 Rollback is not dual-run

Rollback 是回到一个明确、可验证的 prior official state，不是让两个路径同时继续。

必须声明：

- rollback owner；
- exact prior revision / epoch；
- target writer shutdown sequence；
- source writer restore sequence；
- in-flight command handling；
- external effect reconciliation；
- evidence and receipt retention；
- forward retry conditions。

### 11.2 External effects are not reverted by state rollback

代码或 state 回退不能假设现实环境也回退。发生过外部 effect 时，rollback 后必须重新 observe → verify → reconcile，再决定继续、补偿、safe-stop 或升级。

### 11.3 Fail closed

当 prior state 不确定、external effect 未知或 authority 无法唯一恢复时，停止 effect delivery并进入人工裁决；不得选择“看起来最新”的 state。

## 12. Supersede / Retire / Archive rules

### 12.1 Supersede

Supersede 必须记录：

- predecessor semantic / artifact；
- successor semantic / artifact；
- effective date / revision；
- preserved facts；
- intentionally changed behavior；
- remaining compatibility surface；
- final authority source。

### 12.2 Retire

Retire 的对象可以是 writer、reader、adapter、projection 或 full component。必须说明它失去的职责和仍保留的只读用途。

### 12.3 Archive

Archive 保留历史 bytes、provenance、decision、gate conclusion、test evidence 与 successor mapping。Archive 不改变历史事实，也不表示内容曾被正式采用。

### 12.4 Delete

Delete 是独立、显式、不可由 archive 自动推导的决策。删除前必须检查：

- terminal lifecycle；
- accepted result / receipt；
- inbound references；
- audit and traceability needs；
- legal / retention requirements；
- recoverability；
- authoritative replacement。

## 13. Cross-stage invariants

1. Authority-first；未知 authority 时停止。
2. 一个 canonical mutable state 只有一个 Owner。
3. 一类 judgment 只有一个 Authority。
4. No dual mutable truth。
5. `NET_NEW_MUTABLE_TRUTH = 0`。
6. exact-prior transition；stale prior 必须拒绝。
7. Shadow 永远 read-only、no-effect、no-authority。
8. Compatibility projection 不得回写。
9. Evidence-backed cutover；代码存在或 focused green 不等于毕业。
10. Owner cutover 与 consumer migration 分阶段。
11. Rollback 不得恢复双写。
12. External effect 必须重新 observe → verify → reconcile。
13. Supersede、Retire、Archive、Delete 是不同 lifecycle actions。
14. 历史 identity 不能由 path、name、age 或 storage location 决定。
15. Active work 与未通过 Gate 不得被 Target 文档静默覆盖。

## 14. Final migration review

每个 migration unit 在结束前必须回答：

1. Target semantic 是否来自独立的 Product Architecture，而不是旧实现？
2. source → target mapping 是否明确且无隐藏职责？
3. 是否仍存在同一 canonical mutable state 的两个 Owner？
4. 是否仍存在同一 judgment class 的两个 Authority？
5. 所有旧 writers 是否被机械拒绝或明确保留？
6. 所有 projections 是否只读且可追溯到 canonical source？
7. `NET_NEW_MUTABLE_TRUTH` 是否等于 0？
8. stale prior 是否 fail closed？
9. cutover 是否有独立 evidence 和 receipt？
10. rollback 是否恢复唯一 owner，而非双轨？
11. external effects 是否经过重新观察和 reconciliation？
12. source lifecycle 是否明确为 active、retired、superseded 或 archived？
13. 删除是否经过独立决定？
14. remaining gates 和 unknowns 是否显式？

任一答案为否，migration unit 不得声明完成。
