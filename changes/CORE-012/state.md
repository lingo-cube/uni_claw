# CORE-012 — 产品侧可靠执行源接入（规划：实现前 Human Gate 复审）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree · Human closure 2026-09-20（首批批量 closure，GATE-001 台账事件 #1）
triage_label: plan-ready · implementation_authorization_required
parent_change: CORE-010
prior_slice: CORE-011（closed：测试侧 fixture 仿真证明）

## Intent

把 CORE-009 契约 + CORE-010 计划 + CORE-011 仿真证明推进到产品侧：在
`EffectBoundary.Dispatch` 的 gate 通过、driver 调用之前接入可靠执行源。
本 Change 只承载实现前 Human Gate 复审材料与裁决记录；实现授权仍为
NONE，未获显式授权前不修改 `src/`。

## Scope

- 产出四问复审 docket（Owner / 复用判断 / 持久性范围 / 最小接口），
  逐问给出仓库事实、选项与建议裁决。
- 记录用户对 `DECIDE-Q1..Q4` 的裁决；裁决齐备后形成实现计划供下一次
  单独实现授权。
- 实现计划：`plans/2026-09-19-core-012-reliable-execution-source-integration.md`
  （READY；含 Q3 提交边界操作定义、Dispatch 接入形状、五垂直切片、
  文件影响清单）。

## Out of Scope（授权前禁止）

- 修改 `src/`（含 `EffectBoundary` 构造函数——产品轮必须显式点名放开）。
- 新增生产存储、Core 顶层类型；把 fixture/接口名冻结为公共 API。
- Trace 权威化、真实外部操作、数据库、跨节点。

## Decisions

### DECIDE-Q1 — 执行源 Owner：A，批准

可靠执行源由 `EffectBoundary` / Runtime 承担；后续产品实现可在
`EffectBoundary` 构造函数注入执行源，并在 Dispatch 的 gate 通过、driver 调用之前
接入。`KernelRunDriver` 和 `UniKernel` 不成为第二个 delivery truth owner。

这项裁决仅放开后续实现计划中的构造注入 seam；当前 Change 仍禁止修改
`EffectBoundary` 或任何产品代码。

### DECIDE-Q2 — 现有 Log：不复用，批准

不把现有 `BindingLog` / `ReceiptLog` 作为可靠执行源。它们继续保留原有 owner 观察面
和 Receipt 关联用途；产品侧另行设计满足契约的最小执行源。理由是现有 Log 缺少
driver 前可靠提交点、提交三值结果和无 Attempt ID 的未决发现能力。

### DECIDE-Q3 — v1 持久性：B，带提交边界限定，批准

第一版采用可注入执行源 + 文件 append-only journal 默认实现，范围限定为单机、单进程
崩溃/重启；磁盘损坏恢复、跨节点接管和 fsync 调优不在本版承诺内。

但 `CommitResult=success` 必须表示记录已到达本契约声明故障范围内可恢复读取的提交
边界。仅写入内存、发起异步写入或依赖未定义的进程缓冲，不得报告提交成功。测试仍可
使用内存 fixture；fixture 通过不替代文件 journal 的产品恢复验证。

### DECIDE-Q4 — 最小接口与五条约束：批准

批准 `Prepare` / 三值 `CommitResult` / `DiscoverPending` / `GetAttempt` /
`AppendSubmission` / `AppendReceipt` / `AppendLateFeedback` 以及重试、补偿关联所需
的最小行为面，作为实现轮的验收契约；不冻结这些规划词为公共 API 名称。

同时冻结以下边界：只存 correlation 与不可变执行记录；append-only；不重生成
CanonicalBinding 或重判 admissibility；与 `IRunTrace` 零依赖、零供给；Discovery
只读，重试/补偿/重观察决策仍归 Control/Agent 流。

## Verification

```yaml
docket_review:
  level: CONTRACT
  method: docket 复审（仓库事实核对 + CORE-011 S1–S12 映射）
  expected: 四问各有显式裁决；授权边界清单点名必改文件
  actual: 四问裁决已记录（Q1=A / Q2=不复用 / Q3=B+提交边界限定 / Q4=最小面+五约束）
  evidence: evidence/2026-09-19-core-012-human-review-docket.md
plan:
  level: CONTRACT
  method: 裁决映射 + 提交边界操作化 + seam 形状 + 垂直切片 + 文件影响清单
  expected: Q3 边界可证伪；接入形状零公共面 churn；授权点名清单明确
  actual: READY——plans/2026-09-19-core-012-reliable-execution-source-integration.md
  evidence: 同上
```

## Status log

2026-09-19 · created · commit 检查点后起草四问复审 docket；CORE-012
保持 planned + implementation_forbidden，等待用户逐问裁决。
2026-09-19 · decisions-recorded · Q1=A EffectBoundary 注入；Q2=不复用现有
BindingLog/ReceiptLog；Q3=B 文件 append-only journal（附可恢复提交边界限定）；
Q4=批准最小行为面与五条不成为第二事实权威约束。实现授权仍为 NONE，下一步为
形成实现计划并等待单独实现 Gate。
2026-09-19 · plan-ready · 受控实现计划成形（五垂直切片 + 提交边界操作定义 +
文件影响清单）；Q3 边界落地为 flush-to-OS 提交判定、torn frame = 未提交、
提交路径禁异步、恢复验证必须走文件 journal。实现授权仍为 NONE，等待单独
授权 Gate（授权须点名计划 §5 清单）。
2026-09-19 · implemented via CORE-013 · 用户授权按计划 §5 实施（落
CORE-013）；CORE-013 已 closed：567/567 全绿、544 基线零回归、
Q3 提交边界五条逐条可证伪、授权边界零越界。本 Change 的规划与裁决记录
保持不变。
2026-09-20 · closed·human-closure · 首批批量 closure（GATE-001 台账事件 #1）：人批准关闭。docket 裁决与受控计划职责已交付（实现经 CORE-013 closed 交付），无残余。
