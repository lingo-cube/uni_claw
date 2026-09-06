# Plan — E2B-001 Evidence-to-Belief Vertical Slice

Change State: `changes/E2B-001/state.md`（resolved → 本计划为 PLAN 态输出）
Route: **Direct**（语义密度高、边界有界、首份产品代码定约定；委派打包成本 > 收益）
Date: 2026-09-07

## 已决实现入口决策（PLAN 态补齐）

| 决策 | 结论 | 依据 |
|---|---|---|
| 产品代码落点 | **当前分支 `uni-harness`（即重构分支），不建新分支** | 用户裁决（2026-09-07 会话内推翻此前分支方案；参考旧分支 = `uni-agent`） |
| 语言/运行时 | C# / .NET 10 + xUnit 2.6.2 | 用户裁决（2026-09-07）；约定沿用 uni-agent 谱系（`UniClaw.*` 命名、src/tests 布局、global.json 钉 SDK、net10.0、Nullable enable） |

> 注：用户裁决后，AGENTS.md §1「本分支不承载产品代码」与此冲突——以用户
> 直接指令为准；文档同步留待收口时处理。

## 垂直切片（tracer bullet）

```text
行为(8 条验收) → 模型(Evidence/World 最小类型) → 运行时(Uni.Kernel 组合缝)
              → 测试(xUnit 8 用例) → 证据(测试输出落 evidence/)
```

### 1. Scaffold（当前分支 `uni-harness`，约定参考 uni-agent）

```text
UniClaw.Kernel.sln
├── global.json                    # SDK 10.0.100 rollForward latestFeature（同 uni-agent）
├── src/UniClaw.Kernel/UniClaw.Kernel.csproj   # net10.0 / Nullable / 零 ProjectReference（GREENFIELD 隔离）
│   ├── Evidence/                  # Evidence Ledger — sole Admission Authority
│   │   ├── Provenance.cs          #   producer/captureTime/scope/lineage
│   │   ├── ObservationRecord.cs   #   输入（claim + provenance）
│   │   ├── AdmissionRecord.cs     #   accepted|rejected + 逐项检查结果（留痕）
│   │   ├── EvidenceRecord.cs      #   canonical 不可变；EvidenceId=内容确定性哈希
│   │   └── EvidenceLedger.cs      #   Admit()：仅判 integrity/provenance（§11.1）
│   ├── World/                     # World Model — sole Belief/Reconciliation Authority
│   │   ├── RelevanceJudgment.cs   #   per-record 独立留痕（与 Admission 分离）
│   │   ├── WorldBeliefRevision.cs #   Graph+State+Basis+Freshness+Uncertainty+Conflicts
│   │   ├── Slice.cs               #   只读投影：sourceRevisionId+scope+freshness
│   │   └── WorldModel.cs          #   JudgeRelevance()/Reconcile()/DeriveSlice()/IsSliceValid()
│   └── UniKernel.cs               # 仅组合两 L2；无兜底 ownership（§3.5/不变量3）
└── tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj
    └── EvidenceToBeliefTests.cs   # 8 用例 ← 验收 1..8 一一对应
```

### 2. 关键语义映射（验收 → 机制）

| # | 验收 | 机制 |
|---|---|---|
| 1 | 分离可观察 | `AdmissionRecord` 与 `RelevanceJudgment` 两个独立产出，kernel pipeline 顺序执行且各自留痕 |
| 2 | relevant→恰好一新 revision | `Reconcile` 产出 `revision(parent+1)`，basis 引用 evidenceId |
| 3 | irrelevant→零 revision | reconcile 对 irrelevant 返回 current 不变 |
| 4 | fail-closed | provenance 字段缺失 → `rejected` AdmissionRecord + canonical record 为 null + pipeline 短路（无 relevance/reconciliation 调用可断言） |
| 5 | conflict 显式 | 同 subject 异 claim → revision.Conflicts 携带双方 evidence ref；不静默覆盖 |
| 6 | 派生失效为派生判定 | Slice 只存 sourceRevisionId+freshness；有效性 = 比对 current（无 invalidation event 字段） |
| 7 | plan 无路径 | `Reconcile` 签名仅收 Evidence 类型；反射断言参数类型封闭于 evidence 域 |
| 8 | 幂等 | EvidenceId=确定性内容哈希（ledger 去重）+ basis 已含该 id 时 reconcile 不再产 revision |

### 3. TDD 顺序（A6）

RED（8 用例全失败/或类型缺失编译失败）→ 最小实现 → GREEN → 仅结构性
refactor。测试验证行为不验证实现细节（Constraints）。

### 4. 验证与收口

- `dotnet test` 全 GREEN → 输出存 `evidence/2026-09-07-e2b-001-deterministic.md`
- 回填 state.md verification 四元组（level: DETERMINISTIC）+ Status Log
- REVIEW（意图对齐/范围/不变量 8-20 对照）→ VERIFY → commit → CLOSED

## Out-of-plan（不做）

Control/Assurance/Effect/Memory、FSM、持久化、多 Run、Provider 框架化
（scripted provider = 测试内直接构造 ObservationRecord）。
