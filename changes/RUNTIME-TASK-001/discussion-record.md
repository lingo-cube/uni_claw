# RUNTIME-TASK-001 — 设计讨论记录（移交用）

> 本文件是 [state.md](state.md) 的背景与推演过程，供**无上下文的新会话**接手。
> 结论以 state.md 为准；本文件保留"为什么这么定""哪些想法被推翻过"，
> 以免新会话重复走过已否掉的路径。
>
> 记录时点：2026-10-26，base `c3f1833c`。

## 1. 用户的核心主张（按提出顺序，保真）

| # | 原话 / 主张 | 含义 |
|---|---|---|
| U1 | "讨论下 要不要按task分" | 存储是否按 task 组织 |
| U2 | "task 和 run 1:1吗？不是一个task 有很多instance吗？" | 挑战"task 与执行 1:1"的说法；认定定义层是 1:N |
| U3 | "我们要讨论为什么要有task，以及本地怎么管理" | 要求先论证 task 的存在理由，不是先谈布局 |
| U4 | "如果我们针对一个场景修复优化 也是针对一个task" | 场景修复工作本身应被建模为 task |
| U5 | "这样版本就可以随着instance进行额外管理，职责更清晰" | 版本挂在 instance 上，职责分层 |
| U6 | "当前的版本好像不好管理" | 现状缺陷：版本无挂载点 |
| U7 | "我也不知道这个run干嘛的" | 现状缺陷：run 无身份 |
| U8 | "所以run的身份就是task" | 读取视角：看 run 应能直接读出 task |
| U9 | "runs就是一个目录层级而已 其实没业务含义 task才是有业务含义的" | 目录只表达有业务含义的层级 |
| U10 | "应该是{xxx_task}/{taskInstanceId} 这样，然后xxx_task有task的定义" | 具体布局提议 |
| U11 | "这个没定义吗？还是没锁定定义?" | 追问基数是否已裁决 |
| U12 | "为什么要把'预留 1:N'改成'当前 1:1'？从逻辑语义上理解 能这么改吗？" | 指出我方语义错误 |
| U13 | "runId好像都不需要 你觉得呢" | 第三层多余 |
| U14 | "把task 组件提高优先级" | 优先级裁决 |

**用户已裁决、实现方不得推翻**：U9（runs 无业务含义）、U10（按 task 两层）、
U13（runId 不占路径）、U14（提高 task 优先级）、U5（版本随 instance 管理）。

## 2. 已核实的事实（附行级依据）

### 2.1 基数

| 关系 | 事实 | 依据 |
|---|---|---|
| TaskDefinition → TaskInstance | **1:N（已定义）** | `CONTEXT.md:1016`「同一 Task 可被实例化多次」；`repository.js:86` `task.instances[]` |
| TaskInstance → ProductSession | 当前 **1:1**；磁盘**预留** 1:N | `ProductSessionStore.Create` 用 `_byTask` 抑制；重载时 `_byTask[...] = record` last-wins、无唯一约束（`ProductSessionStore.cs:103`） |
| ProductSession → Run | **≤1，硬挡** | `RuntimeHttpServer.cs:198-200` 返回 409 `product-session-already-bound` |
| 同 Contract View 重复激活 | RunId 不变，不新建 run | `RuntimeHttpServer.cs:189` `activationKey=(acceptedContractViewRef, generation)` |
| 多 Goal / 多 Run / parallel / continuation | **明文"暂不锁定"** | `docs/architecture/product-architecture-baseline-l0-l3.md:1060`（§22） |
| 再执行语义 | **造新的** TaskInstance + Session + Run | ADR-0045 Decision 第 6 条 |

> 关键区分：基线 §22 的"多 Run"是**产品模型层**（多 Goal、并行、续跑），
> **不等于** `instance → run` 1:N。混同这两者会导致错误地为 run 层级预置目录。

### 2.2 身份与存储现状

- `TaskInstanceStore` 注释：`1:N（taskInstance → productSession）预留但当前 1:1（ADR-0045 当前范围）`
  —— **这句是对的**，"预留"指磁盘格式允许，不表示路线图已定。
- 存在两套 instance 编号：Workbench `psi-<hex16>`（`repository.js:20`）vs Runtime
  `task-instance-<guid>`。ADR-0045 Consequences 已记为 known deviation。
- `RuntimeRunStore.Create` 缺省会合成 `task-instance-<guid>`（`RuntimeRunStore.cs:58`），
  使"无 task 的 run"在类型上合法。
- **`HostRunner` 文档与实现不一致**：注释写 `./runs/<runid>/`（`:28`），
  实现写 `run-{yyyyMMdd-HHmmss-fff}`（`:120`）。
- **CLI 路径的 run 零身份**：`Host/Program.cs` 不传 `Launch`，因此不写 `metadata.json`；
  只有 HTTP 路径写（`RuntimeHttpServer.cs:718`），其中含 `runDir`/`runId`/
  `productSessionId`/`launchId`/`correlationId`。
- 介质根默认 `runs/`（`Program.cs:30`），`.gitignore` 忽略
  `src/UniClaw.Host.Dsh/runs/`。

### 2.3 当前磁盘布局（讨论起点）

```text
runs/
├── run-<时间戳>/            ← 一次 Run
│   ├── evidence.journal.jsonl(+.head)
│   ├── run-state.journal.jsonl(+.head)
│   ├── control-traversal.journal.jsonl(.head)(.versions/)
│   ├── runtime-plan.journal.jsonl(.head)(.versions/)
│   ├── world.journal.jsonl(+.head)      ← v3，周期基线内嵌
│   ├── resource.journal.jsonl(.head)(.snapshots/)   ← v4
│   ├── p25-consultation.journal.ndjson（Settings 跑法为 consultations.journal.jsonl）
│   ├── exec.journal · run-identity.json
│   └── facts.json / failure.json / trace.json / coverage-* / report.* ...
├── <productSessionId>/initialization.json   ← 与 run-* 同层，命名空间混用
├── .identity/{task-instances,product-sessions,activation-records}.jsonl
├── .runtime-runs/<runId>.{snapshot.json,events.ndjson} + <runId>.revisions/
└── .required-reports/<deliveryId>/{head.json,versions/,generator.py,inputs/,published/}
```

提交语义（不因布局改变）：journal 为帧链 + 旁路 `.head`，head 写成功才算提交；
报告交付先写不可变版本、再原子替换 `head.json`；产物目录靠 `Directory.Move` 出现。
所有存储默认不删（无跨 Run 配额 / 期限 / GC 配置）。

## 3. 推演路径与被推翻的想法

### 3.1 被推翻：三层 `<task>/<instance>/<runId>/`

**当时的理由**：给"一个 instance 跑多次"预留，将来零迁移。

**推翻依据**：
1. 再执行按 ADR-0045 造**新 instance**，天然不会出现"同 instance 第二个 run"；
2. 同 session 绑第二个 run 直接 409；
3. 基线 §22 的"多 Run"不是这条边；
4. "将来补一层"在本仓库是**既有模式**而非灾难——Evidence v1→v2、World v1→v3、
   Resource v2→v4、run-state v1→v2 全是"新写新格式 + 旧读旧格式并存"。
   用"迁移不可逆"当理由是夸大了一个已解决过四次的工程问题。

→ 结论改为两层，runId 落 `metadata.json`（state.md D3、D5）。

### 3.2 被推翻：把 `TaskInstanceStore` 的"预留 1:N"改成"当前 1:1"

这是**我方提出的错误建议**，由用户以语义问题当场拦下（U12）。三处错：

1. **换边**：原句讲 `taskInstance → productSession`；改后的句子讲 Run 数量。
   两条不同的边，用一条的"不锁定"改写另一条的"预留"是偷换。
2. **把互补当矛盾**："预留"（结构允许）与"当前 1:1"（当前用法）是**两层信息**，
   不是二选一。删掉预设 = 净减信息。
3. **误读"预留"**：它只表示"结构上留着口子"，**不表示**"已批准将来 1:N"。
   当时把它读成"强度写高了"，是自己给词加了强度。

**给新会话的提醒**：不要再去"修正"这句注释；也不要因为看到"预留"就推断
路线图已定。若要补充 §22 的事实，应落在讲 **Run 基数**的位置
（`RuntimeRunStore` / `LaunchContext` / ADR），不是 `TaskInstanceStore`。

### 3.3 保留但未被采纳：只盖身份章、不动布局

可行且代价最小。放弃它是因为达不到 U9+U10 的目标（"一个 task 的材料在一处"）。
若布局改动代价超预期，可回退到这个方案。

## 4. 为什么 task 需要存在（正面论证）

若只是"跑一次"，不需要 task。TaskDefinition 存在的三个必要条件（缺一即不必存在）：

1. **幂等**——同一 `idempotencyKey` 重入不得变成两次执行（`TaskInstanceStore` 的立身之本）。
   但这条只需 TaskInstance，不需要 TaskDefinition。
2. **意图复用**——同一"开启 WiFi"要在不同日期、版本、设备上反复跑。TaskDefinition 是
   那个"多次"的容器（U2 的 1:N 正是它的正当性）。
3. **版本归属**——只有一个稳定的"要达成什么"，才能问"v3 修好了吗"。

对应到用户诉求：U4（场景修复即 task）落在第 2 条；U5（版本随 instance）落在第 3 条。
因此**版本必须挂在"尝试"上而不是"意图"上**（引用式），否则回退会丢历史。

## 5. 移交清单

新会话接手顺序建议：

1. 读本文件 §1–§3，确认不重走已否掉的路径（尤其 §3.1、§3.2）。
2. 读 [state.md](state.md) 的 Decisions 与 Open questions。
3. **先答 O1**（TaskDefinition 是否带版本）——它是 O2/O5 的前置；
   O4 决定整体推进顺序，建议在 O1 之后。
4. O1–O5 定案后回填 Acceptance 的四元组，再进 PLAN（`plans/RUNTIME-TASK-001.md`）。
5. 涉及布局的裁决按 ADR 三条判据评估是否单独成 ADR：
   难以逆转（交付记录存绝对 `SourceDirectory`）/ 缺上下文会困惑 / 真实取舍。

不要在本 Change 里顺手做：多 Run 语义、通用 Task Catalog 实现、存量数据迁移
（除 O3 明确选择）、任何真机或模型调用。
