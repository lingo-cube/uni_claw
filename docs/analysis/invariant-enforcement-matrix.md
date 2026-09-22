# 不变量 × 执法覆盖矩阵（Invariant Enforcement Matrix）

> 派生但有语义映射的工件：不变量清单由 `docs/architecture/product-architecture-baseline-l0-l3.md`
> §20（1–42）+ §24.10（43–47）锁定；执法映射为人工语义判定（ARCH-DOC-016，2026-09-22）。
> 完整性由 `python3 tools/check-invariant-matrix.py` 守护：每条不变量必须有行、判定必须在
> 允许词表内。基线增删不变量而未同步本矩阵 → 检查失败。
>
> 判定词表：
> - `TEST` — 有专项/命中测试执法（载体列给出文件）
> - `STRUCT` — 由类型系统 / 程序集依赖图 / 白名单机械执法
> - `TEST+STRUCT` — 两者兼有
> - `PARTIAL` — 部分执法；余下部分有明确 OPEN GATE 归属
> - `DEFERRED` — 基线显式延后（OPEN GATE），实现时必须携带对应测试

## 汇总（2026-09-22，HEAD 14be825）

```text
47 条不变量：TEST 34 · TEST+STRUCT 4 · STRUCT 3 · PARTIAL 3 · DEFERRED 3
高危组缺口（串行屏障 / UnknownOutcome / 终局关闭 / Freshness Unknown / 激活幂等）：0
新增强化测试：NO_REAL_BUYER（无缺口可拉）
```

## 矩阵

| # | 不变量（缩写） | 判定 | 执法载体 | 备注 |
|---|---|---|---|---|
| 1 | canonical state 唯一 Owner | TEST+STRUCT | `KernelRuntimeSurfaceWhitelistTests` · `UiRealizationBoundaryTests` · `ProductHostClosureTests` | 白名单实测拦截过违规（UIW-005 D3） |
| 2 | authority class 唯一 Authority | TEST+STRUCT | 同上 + `TerminalOutcomeTests` Outcome16 | |
| 3 | Kernel 是边界不兜底 | STRUCT | 白名单 + 程序集依赖图（Agent/Host→Kernel→Core） | |
| 4 | Effect Boundary 独占 delivery | TEST | `EffectBoundaryExecutionSourceTests` · `DispatchSeamSpecificationTests` | |
| 5 | 跨 L2 只传 immutable | TEST+STRUCT | C# 不可变类型 + `RuntimeViewExposureTests`（Consumer View） | |
| 6 | 能力强弱不改 Authority | TEST | `DispatchSeamSpecificationTests`（provider 对授权零感知） | |
| 7 | Memory 不建立 current claim | DEFERRED | Memory System 未实现（CONTEXT 占位词条） | 实现时必须携带 recall-is-prior 测试 |
| 8 | Provider raw / Ledger canonical | TEST | `ObservationIngressTests` · `EvidenceToBeliefTests` | |
| 9 | Admission 唯一且只判 eligibility | TEST | `ObservationIngressTests` | |
| 10 | Admission≠Truth/Relevance/Sufficiency | TEST | `EvidenceToBeliefTests`（relevance 分离）· `FreshnessEnforcementTests` | |
| 11 | Provider 无独立 evidence authority | TEST | `ObservationIngressTests` · Outcome14（provider claim 不写 Outcome） | |
| 12 | Evidence≠Belief | TEST | `EvidenceToBeliefTests` · `WorldModelCanonicalOracleTests` | |
| 13 | Belief≠Reality | STRUCT | 表达层：由 #18 的 basis/freshness/uncertainty 执法其可修正性 | 真值本身不可测（按设计） |
| 14 | World Model 拥有 belief 非 intent | TEST | `ControlReferencePolicyTests` | |
| 15 | Reconciliation 只吃 accepted Evidence | TEST | `EvidenceToBeliefTests` | |
| 16 | Plan/expectation 不改观察解释 | TEST | `WorldModelCanonicalOracleTests` · `FailClosedScenarioTests` | |
| 17 | Belief/State 不并列 current truth | TEST | `WorldModelIndexInvariantTests` | |
| 18 | belief 表达 basis/freshness/uncertainty/conflicts | TEST | `ClaimEvolutionTests` · `FreshnessEnforcementTests` | |
| 19 | projection 派生 immutable | TEST | `RuntimeViewExposureTests` · `WorldModelIndexInvariantTests` | |
| 20 | Slice 语义与命名 | TEST | `UIWorldReplayabilityTests` · `RuntimeViewExposureTests` | |
| 21 | Control Intent 唯一权威 | TEST | `ControlReferencePolicyTests` · `ControlToEffectTests` | |
| 22 | Assurance 唯一判断权威 | TEST | `FreshnessEnforcementTests` · Outcome16 | |
| 23 | Binding+Delivery 唯一权威 | TEST | `EffectBoundaryExecutionSourceTests` | |
| 24 | Candidate≠Canonical Binding | TEST | `UIWorldGroundingSeamTests` | |
| 25 | Candidate≠Admissible≠Authorized | TEST | `ControlToEffectTests` · `DesiredStateSatisfactionTests` | |
| 26 | Gate 只执法不重判 | TEST | `EffectBoundaryExecutionSourceTests` | |
| 27 | Driver 不 retry/replan | TEST | `AdbLiveDriverTests` · `EgoBrowserDeliveryTests` · `DeterministicEffectDriver` | |
| 28 | Control State 不成 God Context | STRUCT | 白名单 + `KernelRunDriverTests`（最小状态侧面执法） | |
| 29 | Traversal 不拥有 Goal/Belief/Completion | PARTIAL | Outcome15（Control 宣称≠completion） | Traversal 专项待 Phase 扩展 |
| 30 | Recovery 重入闭环 | PARTIAL | `UnknownOutcome` 屏障已执法（见 #43/#31 面）；恢复编排 CORE-014 悬置 | Phase 5 |
| 31 | Recovery 不盲试 | PARTIAL | `DispatchSeamSpecificationTests`（UnknownOutcome 唯一后继 re-observe）+ `ReliableExecutionJournalRestartTests` | Phase 5 补全 |
| 32 | Plan 是 disposable hypothesis | TEST | `ControlReferencePolicyTests`（不进 assurance/binding 签名） | |
| 33 | Attempt≠Effect | TEST | Outcome2 · Outcome3 | |
| 34 | Receipt≠Verified Effect | TEST | Outcome2 · `DispatchSeamSpecificationTests` | |
| 35 | Effect≠Completion | TEST | Outcome3 · Outcome4 · Outcome18 | |
| 36 | Obligations 只含 run 级 | TEST | `EntityScopedObligationTests`（action-local 不入 Run State） | |
| 37 | Assurance 判终局四分类 | TEST | Outcome1/5/6/7/16/17 | |
| 38 | Run Model 只记录不判断 | TEST | Outcome16 | |
| 39 | Outcome immutable exactly-once | TEST | Outcome10 | |
| 40 | UniAgent 判 Goal satisfaction | TEST | `GoalEvaluationTests` | |
| 41 | Evaluation 不回写执行事实 | TEST | `GoalEvaluationTests` | |
| 42 | terminal 后关闭 delivery | TEST | Outcome11 · Outcome12（late receipt 不复活） | |
| 43 | 现实 Effect 串行验证屏障 | TEST | `TwoStepBarrierTests`（3 例）· `EntityObligationFulfillmentTests` · `FailClosedScenarioTests` | 高危组 |
| 44 | 激活幂等 + at-most-one Run | TEST | `KernelRunDriverTests` · `RunIdentityTests` · `DeterministicScenarioTests`/`SimContract` | 高危组 |
| 45 | Agent/Control/Kernel 职责不漂移 | TEST+STRUCT | 白名单 + `KernelRunDriverTests`（driver 只编排） | |
| 46 | Grant 授权链 | DEFERRED | OPEN GATE Phase 6（ADR 载荷未裁决） | 实现时必须携带链路测试 |
| 47 | untrusted content 隔离 | DEFERRED | 检测机制 OPEN GATE Phase 6/7 | 同上 |

## 复核方法（可复现）

```bash
# 命中扫描（本矩阵 TEST 判定的证据来源，2026-09-22）：
grep -rlE 'PostActionEffectFlow|UnknownOutcome|TerminalOutcome|AttemptReport|MaterialEffect|Admission|Reconcil|Freshness' tests --include='*.cs'
# 完整性守护：
python3 tools/check-invariant-matrix.py
```

## 维护规则

- 基线不变量增删/改号 → 必须同步本矩阵（守护脚本会拦）。
- 测试文件改名/迁移 → 更新载体列；判定词不得凭记忆改，须附 grep 证据。
- DEFERRED 行实现时：先补测试再摘 DEFERRED 标记（Memory / Grant / untrusted / Recovery 编排四组）。
