# ADR-0050：预算续授（Budget Grant）——P25 咨询通道 + 两层顶 + 轻记账

状态：accepted（2026-10-12，协议修复 grill——记录见 `docs/design/protocol-completeness-formal-audit-v0.1.md` 附录 C；实现经 PROTOCOL-FIX）

形式审计 D1/D7：`ExecutionContract` 预算可空 → View 静默缺省（16/256），终止性由
缺省承重且无留痕。Owner 裁决（grill Q1–Q11）：留痕宽松 + 预算可续授，决策者 =
UniAgent 与规则共同决定。

> 2026-10-18：§1中必需有限累计HardCap及其全局停机保证，由accepted ADR-0052部分替代；§2可选窗口在新策略下改为Runtime主动明确复核。保留P25、独立资源维度、消耗记账、非Contract mutation与明确授权上限。下文原决定保留为历史，不能据其给目标推进模式添加任意累计顶。

## Decision

### 1. 两层顶（终止测度的家）

```text
契约预算（MaxConsultations / MaxTotalSteps，可空）= 初始额度（起跑分配）
policy 声明的绝对硬顶（HardCap，有限）        = 续授不可突破的天花板
admission 记录预算来源 valueOrigin ∈ { configured, default }   （Q1:B）
```

终止性落在 policy 硬顶：硬顶有限 ⇒ Run 必然停机（预算耗尽 → 既有诚实终局路径，
T6）。契约值退化为初始承诺，作者意图不被篡改。

### 2. 续授通道 = P25 咨询（不新增协议边）

- **触发**（规则侧）：driver 进入决策边界时检查 `StepsRemaining ≤ 阈值` 或
  `RoundsRemaining ≤ 阈值`（阈值与硬顶同源于 policy，如"剩 ≤20% 或 ≤32 步"），
  为真则本次咨询标记**续授窗口开启**；决策上下文携带剩余额度（既有
  BudgetRemaining 通道）。
- **决定**（大脑侧）：UniAgent 按实际情况提案续授量（正常推进未走完 → 该续就续；
  也可明确不续）。
- **校验**（规则侧）：`已授 + 提案 ≤ HardCap`，且续授是**决策边界决定**，
  **不是 contract mutation**（契约不可变原则不破）。
- **死锁角闭合**：问答轮次耗尽而步数仍剩时，既有 DeferRoundsExhausted **终问**
  允许携带续授提案——不会出现"有步数、想续、没机会开口"。
- **额度范围**：步数与轮次机制统一，硬顶各自独立。

### 3. 轻记账（Q9:A）

driver 内部消耗态 + 每笔续授留在 P25 decision 留痕（决策 id、续授量、依据）+
trace；**不进 canonical Run State**（action-local/run 进度语义不扩入，不变量 36
精神）。拒续或触顶 → 既有 budget-exhausted 诚实终局。崩溃后的额度恢复语义随
Deferred ⑰（激活/恢复收口）一并裁决，不提前单独设计。

## Consequences

- D1/D7 闭合：终止测度从"缺省承重"变为"policy 硬顶的结构保证"，且全程可审计
  （初始来源 + 每笔续授）。
- 运行期适应（续授）与作者意图（初始额度）分离；"UniAgent 与规则共同决定"由
  通道结构保证（大脑提议、规则封顶）。
- P25 语义微扩（续授窗口标记 + 终问携带），不新增边、不新增 Authority。

## Rejected alternatives

- **专用 lifecycle 命令边**：⑯ 先例——命令词汇等真实 buyer；续授是决策边界
  决定，借 P25 足够。
- **policy 自续（大脑不参与）**：违反共同决定裁决。
- **契约内回补**：未耗尽无需补、耗尽即超约——形同虚设。
- **无顶续授**：重新打碎终止性，D1 白修。
- **重记账进 Run State**：每次续授一次 canonical transition，成本高于审计收益。

## Known gaps

- policy 的 `HardCap` / `RenewalThreshold` 参数面、P25 续授窗口与终问携带的字段
  形状，随 PROTOCOL-FIX 切片定义；⑰ 收口前不承诺崩溃恢复的额度语义。
