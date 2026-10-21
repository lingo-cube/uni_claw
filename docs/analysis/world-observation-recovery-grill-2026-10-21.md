# World 身份补证与遍历恢复讨论（2026-10-21）

> Status: `DESIGN-DECISIONS-ACCEPTED / PERSISTED`
> Authority: `NONE`
> Scope: 当前工作树核验及Q1-Q4已接受决定；正式范围与验收见 `changes/WORLD-OBSERVATION-001/state.md`，不构成产品实施授权或具体载荷冻结。
> Base: `bce0ec3a981a4598b889bb0bf37d2b3e389aa41c`；共享工作树有大量并行未提交变更。

## 本次核心闭环（用户2026-10-21明确要求，不得遗漏）

```text
页面身份未确认
→ 暂停依赖该身份的推进
→ 补充识别，按预算等待
→ World采信并确认页面及元素归属
→ 验证导航到达
→ Traversal继续
```

这六步是本次设计、实现和验收的主线。不能用“已接入Slow/Jev”“tap成功”“route字符串变化”或“未新建身份、仍保留旧anchor”替代任何一步。验收须同时证明等待期间依赖该身份的推进被阻止，以及确认后经导航验证才恢复；超时、错帧、迟到或歧义不得越过该顺序。具体接缝与恢复策略继续按后续轮次确定。

## 当前已核验状态

- RUNTIME-PLAN-006 关闭的是有界安全遍历尝试，结论仍为 PARTIAL_NOT_FULL_COVERAGE；不是完整设置遍历通过。原始失败与 gap-analysis.json 保留。
- WORLD-SEMANTICS-002 已关闭身份时序/冲突、重访签名和辅助语义消费的范围。2026-10-21 本次逐项核对其 source-freeze.json，12/12 文件哈希一致。
- 本次从当前源码重新运行 `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~WorldIdentityReconciliationTests|FullyQualifiedName~WorldSemanticEvidenceTests' --verbosity quiet`：17/17 通过。出现 NuGet 漏洞缓存访问警告，不影响这次测试通过；未运行设备或模型。
- 上游联合证据记录 Host 400/400、Simulation 250/250、Kernel 1011/1014，另有三项已记录的范围外失败。这些全量数字来自既有 verification-summary.json，不是本次重跑结果。

## 已定边界，不重新裁决

- UIWorld 基线 §13：Matched/New/Ambiguous/Insufficient；后两者不能为了推进而强建身份，受影响 claim 按证据状态降级。
- UIWorld 基线 §15：World 表达缺失信息，观察控制选择获取策略，Perception→Ledger→World 再判断。World 不命令具体模型。
- ADR-0030：关键 claim 不足时 Control 在预算内等待补证，超时保留未知；Effect Gate 不调用或等待 Slow。
- World 拥有身份和关系，Assurance 拥有效果验证，Traversal 拥有遍历进度；语义分类、预期符合性、实体身份不等同。
- ADR-0010：freshness 是消费相对的 Assurance 判断，不给 World/页面增加全局 IsFresh 真相。
- 单条普通内容证据的 association Insufficient 不能机械解释为整页身份失效；缺失/撤销须与本次观察范围、证据和消费要求相联系。

## 当前剩余断点

1. `ObservationNeed` 仍只有设计描述，没有查到可执行的 World→Control 补证协议；基线外部边仍为 deferred，PER-017 原范围明确没有扩展它。
2. Settings 标题提取仍依赖固定 XML resource-id；正常跨包页面仍可能被视为 popup。006 验收仍为 `Slow=new()`，没有注入 SlowConsult，不能把其结果表述为完整 Slow 链失效。
3. 新 capture 未确认身份时，`UiHierarchyOccurrenceStrategy.Derive` 仍从 previous current anchor 取 owner；较新的迟到保护不能自动证明这次归属正确。本轮为源码核验，未把旧实际代码回放冒充当前再次复现。
4. `WorldModel.Semantics.cs` 当前辅助消费要求已有当前身份观察、其 EvidenceId 和 exact capture/time；有效语义丰富已有容器，不能独立 mint/merge。因此 Jev 已接入不等于未知页面身份恢复已完成。
5. 当前 Traversal 导航推进仍读取 current-container claim；没有把上述缺失信息→补证→当前观察关联→到达验证完整串成可证明的恢复路径。迟到、错帧、预算耗尽等需要同链验证。
6. 子页滚动发现、范围完成证明、通用恢复策略与 XML 采集失败细分仍为相邻问题，不因身份补证闭环成立而自动解决。

## 第一轮决议（用户2026-10-21答“按推荐”）

### Q1 — 本次推进范围

- 已选 A：先完成身份不确定→补证→World 确认身份/元素归属→Assurance 验证到达→Traversal 恢复这一条纵向闭环；带上必要的观察适配和完整感知验收样例。子页发现、整体完成判据、通用恢复和采集诊断保留独立后续范围。
- 未选 B：本次同时纳入上述全部相邻缺口，以完整设置遍历为交付目标。

### Q2 — 是否启用既有设计中待实现的补证需求出口

- 已选 A：以本次真实导航身份故障作为 buyer，启用 World owner 派生的有界缺失信息消费面，让 Control 安排补证；优先复用现有消费面，若表达不足才显式更新最小协议。不得让 Host 从 XML 是否存在等表面条件独自猜测 World 所缺信息。
- 未选 B：继续只靠 Host 的现有触发条件补证，不在本轮实现 World 缺失信息向 Control 的消费路径。

Q1/Q2 已确认；继续展开载荷作用域、未确认元素保存与关联、恢复结果和预算耗尽策略，不提前假定接口形状。讨论尚未收口，不以此宣布新协议已冻结或进入实现；CONTEXT/ADR 仅在具体术语或架构决定明确后同步。

## 第二轮决议（用户2026-10-21答“按推荐”）

### Q3 — 页面身份未确认时，已经看到的元素怎样处理

- 已选 A：保留观察与证据，页面归属保持未知；依赖该身份的操作与Traversal推进不可消费它们。补证后只有World确认本次观察关联，才能在新的revision中提供已归属的元素；旧revision不原地改写。
- 未选 B：只保留原始证据，不保留可复用的元素结构；身份明确后重新采集、识别元素。

事实依据：OccurrenceFact/OccurrenceBelief 支持 nullable owner，Slice 已过滤不在scope内的元素；但 ResolveCurrent 的无container条件及部分匹配规则仍可匹配owner=null，故不能把“owner设null”等同于整个核心暂停机制已成立。必须按本次消费是否依赖页面身份，明确执行屏障；不因修复此UI场景而全局禁止既有合法ownerless用途。选择A也不等于跨capture拼接、迟到结果自动恢复或越过消费时Assurance。

## 沿既有决定推导的约束（无需重复裁决）

- 身份确认与页面类别不同：补证须提供World可判别的accepted evidence，不能以PageKey或expected page直接铸造身份。当前Jev辅助挂接要求已有身份，因此仅复用它的现状不足以证明本次闭环。
- 未确认期间暂停的是依赖该身份的推进；其他不依赖该身份的合法观察/查询可继续。普通内容record的Insufficient不等于整页身份失效。
- 同capture补证保留原capture/cycle/time与证据关联，按World reconciliation和消费时Assurance判断当前适用性；场景已变化、错帧、相关性不足时重新观察，不能跨capture拼成当前事实。
- 恢复接回原导航后的待验证位置，不再次点击已投递目标；保留attempt/遍历位置及已消耗预算。原attempt已过期时不能由迟到结果追溯授权或自动恢复。
- 超时/失败仍为Unknown/Conflicted，由既有Recovery/任务策略决定下一步；不默认成功，不把所有领域finding失败统一升级为Run终止，也不越权点击Back。
- 具体provider/模型选择留在既有感知编排和绑定；World只表达缺失信息。预算不得因补证或恢复而隐式增加。
- 案例中的预期页名称是独立验证意图，不是身份证据；导航到达验证仍由Assurance拥有，不以模型返回成功代替。

## 第三轮决议（用户2026-10-21答“A”）

### Q4 — 无XML的成功补证是否为本次必须验收的范围

- 已选 A：必须覆盖“XML存在但身份证据不足”与“无XML、视觉/文字/布局依据足够”两种成功路径，同时覆盖证据仍不足/超时/错帧/迟到的负向路径。成功案例须证明六步顺序、元素归属及不重复点击；仿真与协议校验必需，并保留有界真实线路/设备案例的证据等级和覆盖限制。
- 未选 B：本次成功路径只验收有XML情形；无XML仅验证安全暂停，成功补证留后续工作。

Q4仅决定本次交付的验收范围。XML可选、World保持身份权威、未知时禁止依赖身份推进是已有约束，不因选B而改成XML硬依赖。没有承诺任意页面都能识别；证据不足时保持未知仍是正确结果。

## 收口

Q1-Q4均已由用户选择；本轮没有新增待裁决的人歧义。具体record/字段、关联算法和最小执行接缝由实施前协议细化与反例验证决定；若发现必须变更既有Owner、把预期作为观察、跨capture拼接当前真值或突破总预算，回到RESOLVE，不默认已获授权。正式范围和验收已整理到WORLD-OBSERVATION-001；当前仅文档持久化，未进入产品实现。

## 可追溯来源

- `docs/architecture/uworld-protocol-baseline-l4.md` §13–16；`docs/adr/0010-freshness-is-consumption-relative-sufficiency-judgment.md`；`docs/adr/0030-slow-perception-first-class-capability-bounded-async-consumption.md`。
- `changes/WORLD-SEMANTICS-002/state.md`、`integration-notes.md`、`evidence/source-freeze.json`。
- `changes/JEV-SEMANTIC-CAP-001/execution-contract.md`；`evidence/jev-semantic-cap-001/verification-summary.json`。
- `changes/RUNTIME-PLAN-006/state.md`；`evidence/runtime-plan-006/event-trace-20261021/gap-analysis.json`。
- `src/UniClaw.Kernel/World/WorldModel.cs`、`WorldModel.Semantics.cs`；`src/UniClaw.Host/UiHierarchyOccurrenceStrategy.cs`、`SettingsTraversalLiveFeed.cs`；`src/UniClaw.Kernel/Runtime/KernelRunDriver.cs`。
