# Plan — CORE-012 产品侧可靠执行源接入

> PlanType: `CONTROLLED_IMPLEMENTATION_PLAN`  
> Status: `PLAN / READY; IMPLEMENTATION_AUTHORIZATION_REQUIRED`  
> Change State: `changes/CORE-012/state.md`（四问裁决已记录）  
> Contract: `docs/design/core-execution-record-contract-v0.1.md`  
> Simulation contract: `docs/design/core-execution-record-simulation-contract-v0.1.md`  
> Review docket: `evidence/2026-09-19-core-012-human-review-docket.md`  
> Simulation proof: CORE-011（S1–S12 全绿）  
> Implementation authorization: `NONE`

## 1. 计划边界（裁决 → 实现约束映射）

| 裁决 | 落地为 |
|---|---|
| Q1=A | 执行源经 `EffectBoundary` 构造函数注入；接入点 = Dispatch 内 gate 通过之后、`_driver.Deliver` 之前。`KernelRunDriver` / `UniKernel` 不持任何执行记录真相 |
| Q2 | `BindingLog` / `ReceiptLog` 用途不变（owner 观察面 + Receipt 关联）；不承担可靠提交/发现 |
| Q3=B+限定 | 可注入接口 + 文件 append-only journal 默认实现；提交边界操作定义见 §2 |
| Q4 | 最小行为面（§3.2）+ 五条冻结边界（只存 correlation 与不可变记录；append-only；不重铸 CanonicalBinding / 不重判 admissibility；与 `IRunTrace` 零依赖零供给；Discovery 只读、重试/补偿/重观察决策归 Control/Agent 流）。规划词不冻结为公共 API 名 |

禁止（延续，不因裁决放开）：Trace 权威化、新增 Core 顶层对象、真实
外部操作、数据库、跨节点、自动重试/补偿编排（v1 只有记录与关联面，
没有自动 buyer）。

## 2. 提交边界操作定义（Q3 限定的落地）

`CommitResult=success` 的判定，在单机、单进程崩溃/重启的声明故障范围内：

1. **写入边界**：记录帧完整写入 journal 文件并 `Flush()`（托管缓冲 →
   OS 文件系统）。进程死亡（含未 `Dispose` 的异常终止）后，同机重新
   打开同一 journal 路径必须能完整读取该记录。
2. **页缓存即达标**：本版故障范围是进程级；OS 页缓存可在进程死亡后
   存活，故不要求 fsync——与「fsync 调优不在本版」一致。OS 崩溃/断电
   （需 fsync）与磁盘损坏恢复（需校验/修复）都是显式 non-goal。
3. **torn frame 语义**：进程在帧写入中途死亡 → 重放时截尾忽略尾部不
   完整帧 = 该提交从未成功（append-only 的正确语义，不是损坏恢复）。
4. **提交路径禁异步**：提交路径上禁止异步写、批量缓冲或后台队列
   （`AsyncFileTraceWriter` 是诊断采集先例，明确不是提交路径）。仅写入
   内存对象、发起异步写或依赖未定义缓冲的实现对 `CommitResult` 只能
   报 failure/unknown，不得报 success。
5. **内存 fixture 的合法用途**：排序语义测试（S1–S3 型断言）可用内存
   double；进程恢复验证（S4–S7 型断言）必须走文件 journal——fixture
   通过不替代 journal 的产品恢复验证（Q3 原文）。

以上每条都要有可证伪测试（§6 边界专项）。

## 3. 现有 seam 与接入形状

### 3.1 接入时序（Dispatch 内部）

```text
gate 级联通过（既有，不改）
  → executionSource 提交固定准备集合        // AttemptId 由 boundary 铸造：
                                           //   "attempt-{BindingId}-{n}"（循 receipt 约定）
  → 提交非 success：零 driver 调用，返回
    (GateDecision(false, "execution-commit-{failed|unknown}"), null)
  → 提交 success：_driver.Deliver（既有）→ receipt 铸造（既有）
  → AppendSubmission + AppendReceipt（追加）
```

- **追加失败不抛过已投递边界**：driver 已调用后 Append 失败/异常不得
  向上传播吞掉已发生的 delivery——记录停留 pending-unknown（S6 安全
  方向），恢复路径可查。仅提交阶段（driver 前）的失败以 fail-closed
  返回。
- **返回面**：沿用 `(GateDecision, EffectReceipt?)`；commit 阻断编码为
  gate reason `execution-commit-*`——先例 `delivery-closed`（同为边界
  机械前置条件，非 authorization 判定，vocabulary 不被污染为第二判定
  权）。拒绝 v1 引入第三元组/新公共记录形状：无 buyer，公共面 churn。
- **构造**：`EffectBoundary(IEffectDriver driver, IReliableExecutionSource?
  executionSource = null)`；null = 既有行为零变化（全部既有测试不改而
  绿是回归判据）。

### 3.2 最小行为面（规划词 → 实现轮命名评审）

`CommitPrepare`（三值）/ `DiscoverPending`（无 ID）/ `GetAttempt`（只读
投影还原）/ `AppendSubmission` / `AppendReceipt`（允许显式无 Receipt）/
`AppendLateFeedback`（含待关联）/ `LinkRetry` / `LinkCompensation`。
本地提交重试（S9）折叠进 `CommitPrepare` 的重复调用语义，不设独立方法。

### 3.3 记录与序列化

- journal 只存 correlation 原语：ids（intent/binding/attempt/effect-ref）、
  固定请求值、revision 锚、执行端标识、准入摘要、追加条目。
- 不序列化活对象图；`GetAttempt` 返回只读投影记录——不重铸
  `CanonicalBinding`、不重判 `AssuranceJudgment`（Q4 冻结边界）。
- journal 身份 = 文件路径（组合作用域）。v1 记录不携带 RunId
  （`EffectBoundary.Dispatch` 无 RunId 输入；路径即关联键；RunId 提示
  deferred 到有 buyer 时）。Host A/B 恢复 = 新 composition 以同一路径
  重开 journal。

### 3.4 组合根接线

`UniKernel` 不构造 `EffectBoundary`（既有组合方向不变）；接线归
composition root：`SimulationHost.Compose` 增加可选 journal 注入
（默认不注入 = 既有行为），产品 host 组合根同理。零新依赖（BCL
System.IO only；csproj/slnx 预期零改动）。

## 4. 垂直切片（RED → GREEN，每片独立可验）

**Slice 1 — 执行源契约 + 文件 journal（行为 → 模型）**
接口、只读投影记录、append-only journal（长度帧 + 截尾重放）、测试程序集
内存 double。测试：提交边界五条（§2 逐条）、三值提交、无 ID 发现、
append-only 历史、torn frame、重试/补偿关联语义。
（level: DETERMINISTIC）

**Slice 2 — EffectBoundary 接入（S1–S3 产品版）**
提交 success → 恰好一次 driver + receipt + 两条追加；failure/unknown →
零 driver + `execution-commit-*` reason；null source → 既有行为回归。
（level: DETERMINISTIC）

**Slice 3 — Act / KernelRunDriver 传递（最小面核对）**
Receipt-null + `execution-commit-*` reason 沿既有 fail-closed 路径
（GateRejected 类）透传；预期 KernelRunDriver 零改动或仅 reason 透传
核对——不新增 DriveStatus（无 buyer）。（level: DETERMINISTIC）

**Slice 4 — 进程重启恢复场景（S4–S12 产品版）**
Host A（真实 journal，临时目录）驱动到各提交/追加点 → 丢弃 host 与
writer（不 Dispose，模拟进程死亡）→ Host B 同路径重开 → 无 ID
DiscoverPending / GetAttempt。含 S8 迟到反馈、S9–S11 关联、S12 Trace
两臂对照。CORE-011 测试侧 fixture 与场景保留为契约回归，不删。
（level: SCENARIO）

**Slice 5 — 证据与文档**
evidence：提交边界证明 + S1–S12 产品版映射 + RED→GREEN 留痕；设计契约
状态行更新（authorization ledger）。

## 5. 文件影响清单（实现授权必须点名）

| 动作 | 路径 |
|---|---|
| 修改 | `src/UniClaw.Kernel/Effects/EffectBoundary.cs`（构造注入 + Dispatch 接入 + AttemptId 铸造 + 追加失败语义） |
| 新增（产品） | `src/UniClaw.Kernel/Effects/ExecutionSource/`（接口 + 只读投影 + FileJournal；BCL-only） |
| 可能修改（预期零或最小） | `src/UniClaw.Kernel/UniKernel.cs`（reason 透传核对）、`src/UniClaw.Kernel/Runtime/KernelRunDriver.cs`（同） |
| 新增（测试） | `tests/UniClaw.Kernel.Tests/Effects/`（Slice 1–3）、`tests/UniClaw.Simulation.Tests/`（Slice 4 + SimulationHost 注入） |
| 不动 | Trace 全家、Core 模型、`BindingLog`/`ReceiptLog` 语义、存储其他一切 |

## 6. 验收

S1–S12 逐条改写为产品 seam 断言（断言点 = CORE-011 场景 + 真实 journal
重开），外加提交边界专项：flush-后才-success、无 Dispose 终止可重读、
torn frame 截尾 = 未提交、异步缓冲实现不得报 success（内存 double 的
契约测试）。验证声明随 Slice 等级（§4）落 evidence。

## 7. 实现前最后一道 Gate

本计划 `READY` 但实现授权仍为 `NONE`。授权必须显式点名 §5 清单与本
计划；未授权前不修改 `src/` 任何文件。授权后的实现 Change 另建
（CORE-013 或续用本 Change 实施——授权时一并裁定）。
