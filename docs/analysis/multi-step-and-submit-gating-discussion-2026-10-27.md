# 多步提案与提交门禁：可讨论结论

> DocumentType: `ANALYSIS`
> Status: `DRAFT / FOR-DISCUSSION`（讨论材料；结论未经裁决，不作为实现依据）
> Authority: `NONE`（分析产物；与 ADR / baseline 冲突以后者为准）
> 来源：2026-10-27 关于「自改进 → 运行中生效 → 多步提案 → 提交门禁」的连续讨论，
> 证据基座是两份已完成扫描：
> `self-improving-ui-automation-ones-worklog-case-2026-10-27.md`（跨运行）与
> `in-flight-agent-intervention-scan-2026-10-27.md`（运行中）。
> **用法**：第 7 节是待裁决问题清单。本文只提供事实与候选方案，不替任何人做决定。

---

## 1. 起点：一个 11 次咨询的案例

ONES 工时登记里，只读日期控件需要「打开面板 → 选时 → 选分 → 选日 → 按回车 →
回读」。按现码走一遍是 **11 次咨询**（7 次有效动作 + 4 次重试 + 1 次终局），
其中 5 次纯浪费。

Kernel 的耗时结构决定了优化方向不是「少点几下」，而是「少问几次模型」：

```text
总耗时 ≈ N_consult × 模型回合延迟        ← 秒级，绝对主导
       + N_step    × (grounding→assurance→gate→dispatch→观察)  ← 毫秒级
       + N_observ  × 感知周期                                  ← 百毫秒级
```

---

## 2. 结论 A：单步限制是 Host 策略，不是接口约束

| 事实 | 证据 |
|---|---|
| 接口接受有界序列 1..16 步 | `AgentActionProposal.Steps`；`MaxProposalSteps = 16`（`KernelRunDriver.cs:110`），V2 只拒空与 >16 |
| 每步独立走全链，串行屏障不变 | `Steps` 逐步 `grounding → binding → assurance → gate → dispatch → post-action` |
| 单步只活在两处 | 注入文本 `Respond with an act proposal containing EXACTLY ONE step`（`SettingsCoverageDirector.cs:542`）+ `ValidateAdherence` 的 `Steps.Count != 1`（`:691`） |
| 「半途越界」已有机检语义 | `KernelRunDriver.cs:1104`：每步开工前比对容器/overlay，不等 → `act-scope-changed` + `StepRejected`，reason 带 `unexecuted-from` + `remaining=[...]` |

**推论**：放开的最小改动是「删一句文本 + 放宽一个判断（`!= 1` → `> 5`）」，
不需要新协议类型。而且最坏结果是多一轮咨询，不是做错动作——序列越界会被
诚实地截断并回传剩余步，不需要我们设计跨 scope 的拼接。

---

## 3. 结论 B：提速与正确性是两条正交的轴

| 改动 | 轴 | 收益 | 不做的后果 |
|---|---|---|---|
| 放开单步 | **提速** | 11 → ≈3 次咨询 | 模型回合数 = 点击数，线性放大 |
| `commit{desiredState}` 原语 | **正确性** | 3 → 2，且不再假成功 | 见下 |
| 投影补 `ReadOnly` | **少试错** | −1 次 `StepRejected` | agent 必须先撞一次墙才知道只读 |
| lesson 上行投递 | **不重学** | 第二次同类失败不再重演 | 每次运行从零开始 |

**关键一条**：只放开单步、不加 `commit`，得到的是「更快的假成功」。面板内
4 步会逐步验证通过（每步 expected effect 是「状态变了」，而它确实变了），
提案结束后失焦回滚，值回到 `15:00`，而 run 认为自己推进了。这正是跨运行
文档第 5 节定义的最贵错误。

---

## 4. 结论 C：提交类操作的门禁分层

「提交」指不可逆、会写数据的动作。按「谁最适合判」分成三档：

### 4.1 已在 Kernel（无需再委托）

| 检查 | 机制 |
|---|---|
| 这个 effect 不在词表里 | 合同 `AllowedEffects` → V2 入口拒绝 |
| 这次提交是否被授权 | Assurance → Effect Gate 只执行既有授权，不重判、不扩权 |
| 结果不明能不能重发 | `DispatchOutcome.UnknownOutcome` 唯一合法后继是 re-observe，**永不盲补发** |
| 未决提交怎么处理 | `ReliableExecutionSource` / `PendingAttempt`：已提交无 receipt 可发现，重启不重复投递 |
| 提交是否真生效 | `Effect Receipt ≠ Verified Effect`：必须 post-action 观察 |

这一档恰好覆盖了「点确定成功 ≠ 数据写进去了」这个真实风险——真正的危险在
提交**之后**的猜，而不是提交之前的想。

### 4.2 可委托 Kernel、但目前缺位

| 缺口 | 为什么适合 Kernel | 现状 |
|---|---|---|
| **per-effect 字段形状** | 纯机械：`commit` 必带 `desiredState`、`tap` 不得带 | 只有 Host 一条粗规则 `directive-step-must-not-carry-desiredState`，散在注入文本里 |
| **跨提案前置条件** | 机械可判：上一步未验证通过不许签发提交 | **无执法点**——`one-step precondition` 仅出现在 `RunObligation.cs:8` 注释，是设计词不是机制 |
| **提案内重复** | 同一 target+effect 在单支提案出现两次 = 双击提交风险 | 无；只能靠跨咨询的停滞计数事后发现 |

### 4.3 不能委托

| 检查 | 归属 | 理由 |
|---|---|---|
| 该不该提交（意图/完成判定） | Agent | Kernel 无此 authority，托管会成为第二决策者 |
| 「这个按钮是不是确认侧」 | Host | 需要业务语义；现由 `SettingsActionPolicy.DenyTargets` + 禁手清单承担（确认侧永不中选） |

### 4.4 一个易错点：提交的期望终态恒为 Unknown

防重复提交靠 `Desired-State Satisfaction`（ADR-0017）。但：

- 它现在只认 `checked / unchecked` 两个值（`SettingsActionPolicy.cs:313` 明确拒绝其他
  `desiredState`），`commit{desiredState:"2026-10-19 09:00"}` 会被当成
  `unsupported desiredState` 拒掉；
- 更根本的是：**「已经提交过」不是一个 revision-local state**，面板里观察不到。

按 `CONTEXT.md` 的纪律（`null = Unknown ≠ false`），提交类操作的期望态只能判
Unknown，必须走 observe / safe-stop，**不能默认「没提交过」再补一刀**。

---

## 5. 结论 D：什么检查值得委托（三条标准）

不是「能塞进 Kernel 就塞」，而是同时满足：

1. **纯机械可判定**——不需要业务语义；
2. **违反率高**——检查点本身也消耗咨询轮次，烂检查比没检查更慢；
3. **拒绝理由能让 agent 一次改对**——typed reason 要带精度（如
   `unexecuted-from` + `remaining`），否则等于制造噪声。

缺任一条 → 留在 Host 或 Agent，或不设。

配套的硬边界：**Kernel 只能「拒」，不能「改」**。一旦 Kernel 替 agent 修提案，
它就有了决策权，违反「Host 不判语 / Kernel 非第二决策者」的既有纪律。

---

## 6. 与既有纪律的对齐检查

| 既有约束 | 本文方案是否触碰 | 结论 |
|---|---|---|
| Kernel 非第二决策者 | 只做拒绝，不做改写/补全 | 不触碰 |
| Case Packet 只提供证据，不替 Agent 下结论 | lesson 只带事实 + 可执行集合 | 不触碰 |
| 串行屏障（不变量 43） | 多步仍逐步串行 | 不触碰 |
| 不可逆动作不在试错范围内 | 提交类要求前置检查 + Unknown 走 safe-stop | 加强 |
| 测量只能用可回放证据 | 指标取自 `IConsultationJournal` + receipt + 验证 | 满足 |

---

## 7. 待裁决问题（讨论清单）

| # | 问题 | 候选 | 影响 |
|---|---|---|---|
| Q1 | 多步提案的上界取几 | 5（保守）/ 合同可配 / 16（用满） | 提案越大，单次失败浪费越多 |
| Q2 | 失败后是否允许「续接未执行尾段」 | 现在只回传 `remaining`，下一轮重新决策 / 允许显式 resume | 影响咨询次数与 D5「整支作废」的解释 |
| Q3 | per-effect 形状表放 Kernel 入口还是 Host 策略 | Kernel 入口（统一）/ Host（可配） | 放的层级不同，拒绝理由的精度不同 |
| Q4 | 跨提案前置条件用什么载体 | Proof Obligation / step guard / 提案内断言 | 决定是否需要新协议面 |
| Q5 | `commit` 的 `desiredState` 用什么形状 | 字符串 / 结构化（字段+值）/ 复用 typed state | 直接决定 §4.4 那个拒付问题 |
| Q6 | 提交类是否要独立 effect class | 独立（`submit`/`commit`）/ 复用 `tap` + desiredState | 独立后可做合同级禁用与审计分账 |
| Q7 | 是否允许 Agent 主动声明「这是不可逆动作」 | 允许（风险自报）/ 由 Host 配置判定 | 自报会引入「自报即事实」的既有反模式风险 |
| Q8 | 放开单步后要不要保留单步模式 | 全放开 / 按任务配置 | 既有 settings 合同的稳定性 |

---

## 8. 若要推进（非本文范围）

本文不构成实现授权。真要落地需按 UniFlow 走 change，最小切分建议：

```text
C1 删单步注入文本 + adherence 放宽（≤N）+ 预算联调
   —— 纯 Host 改动，可用同一案例前后对比咨询数
C2 元素投影补 affordance（ReadOnly / Selected）
   —— Kernel 投影层，判据是「B1 即可见」
C3 commit 原语（词表 + driver + per-effect 形状检查）
   —— 需先冻结 Q5/Q6
C4 跨提案前置条件
   —— 需先冻结 Q4，且要证明有真实 buyer（无 buyer 则 deferred）
```

每条都要求「method / expected / actual / evidence」四元组，不能以「改完了」收口。

---

## 附：证据索引

- `src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:110`（`MaxProposalSteps`）、
  `:1104`（`act-scope-changed`）、`:3398`（多步预算校验）
- `src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs:542`（`EXACTLY ONE step`
  注入文本）、`:691`（`directive-requires-single-step`）
- `src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs:313`（`unsupported desiredState`）、
  `:375`（`AlreadySatisfied`）
- `src/UniClaw.Kernel/Effects/EgoBrowserEffectDriver.cs:40`（支持集 `{click, tap, set-switch}`）
- `src/UniClaw.Kernel/Run/RunObligation.cs:8`（`one-step precondition` 仅注释）
- `src/UniClaw.Kernel/Effects/EffectDispatch.cs`（`DispatchOutcome` 三态、`UnknownOutcome` 不盲补发）
- 前序文档：`self-improving-ui-automation-ones-worklog-case-2026-10-27.md`、
  `in-flight-agent-intervention-scan-2026-10-27.md`
