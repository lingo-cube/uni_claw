# RUN-004 — 多轮决策协议（P25 契约 v2 + 驱动器返回转移 + 完成证明三层）

lifecycle_state: planning · disposition: none · depth: decision-heavy · base: c4fcd5d9

## Intent

把「每 Run 单次咨询」演进为多轮咨询协议：六个触发点（v1 落五个）、
有界上下文 v2、回答 v2（含 Defer 与完成自证）、预算合同声明制、
完成证明三层（世界状态 / 锚定自证 / 人为终极裁定）。L0/L1 在其上
直接可用；L2 策略形态归 RUN-005。

## 上游规格（本 spec 只引用不复制）

- `docs/design/consultation-protocol-v0.1.md` —— 协议与数据格式推导
  （150 场景反推，逐字段带 SR 出处；三条元原则 M1/M2/M3）
- `docs/design/decision-granularity-scenarios-v0.1.md` —— 粒度分层与预算经济学
- 边界卡两份（会话定稿）：状态机边界 / UniAgent 计划-策略边界
- 裁决⑧（台账 #21）：完成证明 = **核验后信 + 人为终极裁定**

## Decisions

- D1 触发词汇 v1 = InitialPlanning / StepVerified / StepRejected /
  VerificationFailed / DeferRoundsExhausted；PolicyGuardTripped 留 RUN-005。
- D2 状态机零新增状态：改 4 条返回转移（提案耗尽→NeedDecision；两类失败
  →NeedDecision（预算余）；plain-Observe→TerminalEvaluation）+ Defer
  状态内循环（NeedDecision 原地拉观察再问，扣轮次）。
- D3 上下文 v2 与回答 v2 字段集 = consultation-protocol §2 全文
  （CurrentWorldClaims 值类型改 ClaimSummary，库内消费方同步改）。
- D4 预算合同声明制：ExecutionContract 加 `int? MaxConsultations`
  （默认 16）；Defer 上限 4 轮/次。
- D5 **完成证明三层**（裁决⑧）：
  ①世界状态命题折抵（现有 obligation 路径）；
  ②CompletionEvidence（Basis + Checklist 锚点：已派发步/journal/观察帧
  引用）——Assurance 机械核验锚点存在性，全锚住→折抵；
  ③锚不住且 Agent 坚持完成 → 新合法等待态
  `RunDriveStatus.AwaitingCompletionAdjudication`（人为终极裁定；
  awaiting-human-review 先例）——裁决记录入档后按裁决终局。
- D6 机械校验 V3–V5（超预算 / 空洞完成 / 无界 Defer）fail-closed。
- D7 **[评审 S1 修正]** 白名单 +9 公开类型条目（200→209）：
  ElementEpistemic / ElementSummary / ClaimSummary / ConsultationProgress /
  ConsultationBudget / CompletionEvidence / ObserveSpec / ScreenSummary /
  **AgentDecision+Defer（嵌套，按 FullName 计——先例 L139-156）**。
  v0.1 的「+6」为算术错误（漏枚举类型与嵌套条目），评审拦截。

- D8 **[评审 F5/F6 落形]** 预算合同语义：MaxConsultations（默认 16）与
  MaxTotalSteps（默认 256）入 ExecutionContract **并参与 RunId canonical**
  ——同 version 异预算 = 不同合同 → version-conflict fail-closed；View 携带
  已解析值（驱动器可读）。扣减/耗尽谓词/reason 规范见 spec §2.1。

## Acceptance（RED 先行）

1. 弹窗重议：StepRejected → 重咨询 → 关弹窗 → 达标（SR-106）
2. 多屏遍历：StepVerified 连环咨询 ≥3 屏，轮次预算 64（SR-099）
3. 验证失败重规划：VerificationFailed → 换路（SR-103）
4. Defer：低置信帧 → Defer 一轮 → 再问（SR-013/067）
5. 零动作如实终局：期望态已满足 → Completion（SR-007/139）
6. 预算耗尽诚实失败（SR-102/049）
7. 完成证明三层各一例：claim 折抵 / 锚定自证折抵 / 锚不住→
   AwaitingCompletionAdjudication→人工裁决记录（SR-073/074/150）
8. 确定性：同输入两跑决策序列与 digest 一致；断点续跑不重问（SR-107/108）
9. T6 全链：Defer 至 MaxRounds 耗尽 → DeferRoundsExhausted 最后再问 → 仍 Defer → defer-exhausted 终局
10. V3/V4/V5 各一 RED（超预算提案 / 空洞 NoAction / 无界·嵌套 Defer + T6 豁免正例）
11. 层3 双分支：approved→Completion 与 rejected→completion-rejected（含 dossier 呈递）

## Out of Scope

L2 Policy 形态（RUN-005）；传输层 schema（LLM realization 立项时）；
世界突变主动检测（preemption Phase 6/7）；跨 run 迟到反馈（恢复编排）。

## Status log

- 2026-09-22 · spec-review-2·CHANGES_REQUIRED(收口型)→v0.3 · 复评
  12 closed/4 partial/5 新（G1–G5），无回退无 worsened。两个 major 残留
  闭合：F5 幂等分支升级合同签名比较（D8 fail-closed 可达）；G2 折抵落点
  = completion claim 入证（kernel.completion-* producer，ConflictResolver
  写回先例）。F8 归属参数化、F3/G4 批过滤+nbounds、F2 差集登记、
  G1/G3/G5 收口。处置表 spec §10。待放行。

- 2026-09-22 · spec-review-1·CHANGES_REQUIRED→v0.2 · 对抗评审 1B+9maj+6min
  全项处置（处置表 spec §9）：S1 白名单算术修正（+6→+9，200→209）；F5/F6
  预算通道+canonical+记账算法（D8）；F8/F9 完成证明闭环（_completedSteps
  归档/dossier 元组面/rejected 终局防环）；F2/F3/F4 上下文三源补全；
  F10 失败点映射表；F1 涟漪重列；F12 验收补 9–11；S3 边界卡固化 spec §0。
  待复评或放行。

- 2026-09-22 · created · 十余轮设计对话（边界/粒度/协议/核验）+
  mini-grill 裁决⑧（台账 #21）后立项；spec 即上文，评审待用户。
