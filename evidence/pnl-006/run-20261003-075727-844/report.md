# 全链路测试报告 · Android Settings · Wi‑Fi 导航与状态确认

| 身份 | 值 | 身份 | 值 |
|---|---|---|---|
| RunId | `host:v0-flip-switch` | 设备 | emulator-5556 (API 35, 1080x1920) |
| ProductSession | `settings-traversal-session-6098c7dddc9d40bda955f82d9e72a27e` | 模型 | zai-coding-cn/glm-5.3-flash |
| DSH Session | `session-06d69e93-56f4-4b58-b991-69eabe3301e2` | 测试集 | Android Settings 测试集 |
| TraceId | `trc-d25f7036d4c2` | 输入目录 | `evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844` |

## ① 需求

> 〔run 产物未携带任务需求原文，此处以 session 标题代替；需求落盘属 P2 产品侧改动〕

> Android Settings · Wi‑Fi 导航与状态确认

## ② 效果达成

| 判定项 | 值 |
|---|---|
| 执行终态 | Completed · Completion · terminal=是 |
| 效果投递 / 完成步骤 | 2 / 2 |
| 验证锚点 | 未采集（facts.completionAnchors 为空） |
| Receipts | `DeliveryCompleted` `DeliveryCompleted` |

## ③ 各组件做到什么程度

| 组件 | 关键量化 | 观测状态 |
|---|---|---|
| Runtime Host | 终态 Completed · 落盘产物 5 件 | 降级 |
| UniAgent | 咨询 3 · 模型 zai-coding-cn/glm-5.3-flash | 降级 |
| Capability | 观察 cycle 3 · AdbLiveEffectDriver | 完整 |
| WorldModel | revision 1905（rev-1→rev-1905）· 控制读取点 6 | 完整 |
| Evidence | admission 1910 · Finalized · integrity 已声明 | 完整 |
| 执行操作 | attempt 2 · receipts DeliveryCompleted/DeliveryCompleted | 完整 |

## 异常观察

无。

---

# 细节区（默认折叠；异常或需核查时展开）

<details>
<summary><b>L2 · 全链路时间线</b></summary>


跨轨排序说明：各事件只按其来源原生序（cycle / journalSeq / captureSequence）排列；咨询→效果、效果→PostAction 观察的先后为 documented derived 规则，非时间戳。

| # | 轨道 | 事件 | 关键细节 | 引用 |
|---|---|---|---|---|
| 1 | observation | observation-cycle | context=External; screenIdentity=android.settings\|rk1:Settings\|src=homepage_title\|up=0; proposalCount=585; fastLatencyMs=64400.6 | `capture-18b449d9804b400b8585535c88e0df73` |
| 2 | kernel | world.derive-slice | revisionNumber=584 | `sp-1169` |
| 3 | kernel | world.resolve-current | revisionNumber=584 | `sp-1170` |
| 4 | effect | effect-attempt | effectClass=tap; targetSubject=occ-9f610212f8ba-3; executorId=AdbLiveEffectDriver; revisionNumber=584; receipt=DeliveryCompleted; admissionNote=admissible:True:checks:10:freshness:Sufficient | `attempt-1` `bind-intent-1-occ-9f610212f8ba-3` |
| 5 | observation | observation-cycle | context=PostActionEffectFlow; screenIdentity=android.settings\|rk1:Internet\|src=title\|up=1; proposalCount=635; fastLatencyMs=892.445 | `capture-eeb9a6842e8a48289629ae0ead110017` |
| 6 | kernel | world.derive-slice | revisionNumber=1218 | `sp-2441` |
| 7 | kernel | world.derive-slice | revisionNumber=1218 | `sp-2442` |
| 8 | kernel | world.resolve-current | revisionNumber=1218 | `sp-2443` |
| 9 | effect | effect-attempt | effectClass=tap; targetSubject=occ-a3cc6414152a-3; executorId=AdbLiveEffectDriver; revisionNumber=1218; receipt=DeliveryCompleted; admissionNote=admissible:True:checks:10:freshness:Sufficient | `attempt-4` `bind-intent-2-occ-a3cc6414152a-3` |
| 10 | observation | observation-cycle | context=PostActionEffectFlow; screenIdentity=android.settings\|rk1:T-Mobile\|src=title\|up=1; proposalCount=688; fastLatencyMs=836.084 | `capture-13e008ebc7e0470aada29a5794b1f39c` |
| 11 | kernel | world.derive-slice | revisionNumber=1905 | `sp-3820` |
| 12 | terminal | run-terminal | status=Completed; outcome=Completion; reason=terminal-emitted | `host:v0-flip-switch` |

</details>

<details>
<summary><b>L3 · 分层明细</b>（含覆盖度说明）</summary>

### 覆盖度

| 分区 | 状态 | 说明 |
|---|---|---|
| host | 降级 | Runtime Host lifecycle 事件（.runtime-runs）不在输入内；仅由 run 目录产物重建 |
| agent | 降级 | consultations.json 缺失；仅有 metadata.consultations 计数 |
| capability | 完整 | — |
| worldModel | 完整 | — |
| evidence | 完整 | — |
| operations | 完整 | — |
| timeline | 完整 | — |

### Runtime Host

- availability: 降级
- 落盘产物: metadata, facts, trace, settingsTrace, journal

### UniAgent

- availability: 降级
- consultation 数: 3
- 模型: zai-coding-cn/glm-5.3-flash（来源 metadata.json）

### Capability（感知 / 执行 / 模型）

- availability: 完整
- executors: `AdbLiveEffectDriver`

| cycle | context | proposals | fast(ms) | hierarchy(ms) | screenIdentity | popup |
|---|---|---|---|---|---|---|
| 1 | External | 585 | 64400.6 | 2079.4 | `android.settings\|rk1:Settings\|src=homepage_title\|up=0` | absent |
| 2 | PostActionEffectFlow | 635 | 892.445 | 1994.85 | `android.settings\|rk1:Internet\|src=title\|up=1` | absent |
| 3 | PostActionEffectFlow | 688 | 836.084 | 2061.77 | `android.settings\|rk1:T-Mobile\|src=title\|up=1` | absent |

### WorldModel

- availability: 完整
- revision 数: 1905（rev-1 → rev-1905）
- 控制读取点: 6 个（derive-slice / resolve-current，见时间线 kernel 轨道）

### Evidence

- availability: 完整
- admission 数: 1910
- recorderTerminal: Finalized
- integrity: 已声明（64 hex）

### 执行操作

- availability: 完整

#### attempt-1

| 项 | 值 |
|---|---|
| effectClass / target | tap / `occ-9f610212f8ba-3` |
| intent / binding | `intent-1` / `bind-intent-1-occ-9f610212f8ba-3` |
| executor | AdbLiveEffectDriver |
| 绑定时 revision | 584 |
| admission | `admissible:True:checks:10:freshness:Sufficient` |
| receipt | DeliveryCompleted |

#### attempt-4

| 项 | 值 |
|---|---|
| effectClass / target | tap / `occ-a3cc6414152a-3` |
| intent / binding | `intent-2` / `bind-intent-2-occ-a3cc6414152a-3` |
| executor | AdbLiveEffectDriver |
| 绑定时 revision | 1218 |
| admission | `admissible:True:checks:10:freshness:Sufficient` |
| receipt | DeliveryCompleted |

completedSteps: decision=1 step=0 receipt=`receipt-bind-intent-1-occ-9f610212f8ba-3-1`; decision=2 step=0 receipt=`receipt-bind-intent-2-occ-a3cc6414152a-3-2`

</details>

<details>
<summary><b>L4 · 引用索引</b></summary>


| id | kind | artifact | locator |
|---|---|---|---|
| `attempt-1` | attempt | exec.journal | attempt attempt-1 |
| `attempt-4` | attempt | exec.journal | attempt attempt-4 |
| `bind-intent-1-occ-9f610212f8ba-3` | binding | exec.journal | attempt attempt-1 prepare.BindingId |
| `bind-intent-2-occ-a3cc6414152a-3` | binding | exec.journal | attempt attempt-4 prepare.BindingId |
| `capture-13e008ebc7e0470aada29a5794b1f39c` | capture | settings-trace.json | cycle 3 |
| `capture-18b449d9804b400b8585535c88e0df73` | capture | settings-trace.json | cycle 1 |
| `capture-eeb9a6842e8a48289629ae0ead110017` | capture | settings-trace.json | cycle 2 |
| `host:v0-flip-switch` | run | report envelope | identities.runId |
| `intent-1` | intent | exec.journal | attempt attempt-1 prepare.IntentId |
| `intent-2` | intent | exec.journal | attempt attempt-4 prepare.IntentId |
| `receipt-bind-intent-1-occ-9f610212f8ba-3-1` | receipt | facts.json | completedSteps[].receipt |
| `receipt-bind-intent-2-occ-a3cc6414152a-3-2` | receipt | facts.json | completedSteps[].receipt |
| `rev-1` | revision | trace.json | first world.reconcile event reference |
| `rev-1905` | revision | trace.json | last world.reconcile event reference |
| `session-06d69e93-56f4-4b58-b991-69eabe3301e2` | session | report envelope | identities.dshSessionId |
| `settings-traversal-session-6098c7dddc9d40bda955f82d9e72a27e` | run | report envelope | identities.productSessionId |
| `sp-1169` | span | trace.json | spans / sp-1169 |
| `sp-1170` | span | trace.json | spans / sp-1170 |
| `sp-2441` | span | trace.json | spans / sp-2441 |
| `sp-2442` | span | trace.json | spans / sp-2442 |
| `sp-2443` | span | trace.json | spans / sp-2443 |
| `sp-3820` | span | trace.json | spans / sp-3820 |
| `trc-d25f7036d4c2` | trace | report envelope | identities.traceId |

</details>

---

生成器：`gen-run-report` v1 · uniclaw.workspace.run-report.v1 · 输入 artifact 的 SHA256 清单见 report.json `input.artifacts`。
