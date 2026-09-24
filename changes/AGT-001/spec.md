# AGT-001 — UniAgent Runtime Architecture（DSH Product Realization）

> Step 1：Explore / Architecture Draft。设计稿：
> `plans/2026-09-24-agt-001-uniagent-runtime-architecture.md`（grill 靶）。

## Intent（WHAT/WHY）

在 Product Architecture、RUN-004 咨询协议、GEV-004 目标评价均已冻结、
ADR-0022 + uniagent-realization-baseline-v0.1 已裁决「DSH = Product
Realization」的前提下，设计 **DSH 如何作为 UniAgent 的 runtime
realization**——填 baseline §9 的 Deferred 槽位（transport / deployment /
session 映射 / DSH profile 形状），产出可进入独立 Grill 的架构稿。

核心原则：**UniAgent 是产品角色，DSH 是实现机制**；DSH 的
session/turn/tool/transcript 全部 realization-private，不取得任何
Product Authority。

## Scope（Step 1）

- Q1 Runtime Boundary（embedded / sidecar / standalone 裁决）
- Q2 Session Mapping（1:1；创建/关闭/resume/ID 归属）
- Q3 Consultation ↔ Turn（严格 1:1；submit_decision 唯一终结）
- Q4 AgentDecision 统一四元 union（Act/Policy/NoAction/Defer）
- Q5 L0/L1/L2 粒度正式定义；L1 不单独进协议
- Q6/Q7 DSH 承担面 + uniclaw-agent 单工具 profile
- Q8 Strategy State 边界（记「怎么做」不记「是什么」）
- Failure Model（12 项：detect→response→owner→fail-closed）
- Simulation/Replay（double 与 real 同缝共存）
- Provider Boundary + Determinism 裁决（含 amendment candidate）
- 四张图（Authority / Runtime Sequence / State Ownership / Failure）

## Out of Scope

实现 DSH bridge · Node sidecar 代码 · production prompt · 调真实 DeepSeek ·
修改 AgentDecision protocol · Policy runtime/Kernel 展开（RUN-005）·
Kernel driver 修改 · Memory · Goal NLP · Contract authoring · multi-agent ·
把 development Leader/Worker 引入产品。

## 关键裁决预览（详见设计稿）

| 议题 | 裁决 |
|---|---|
| Q1 | **B：.NET 监管 local sidecar，stdio 版本化 JSON-RPC** |
| Q2 | 1 Product Session : 1 DSH session；adapter 创建/关闭；crash 后 log-resume，失败则新 session（策略记忆诚实降级） |
| Q3 | 严格 1 consultation : 1 turn；唯一终结 = submit_decision（concludesTurn）；其余终因一律 no-response fail-closed |
| Q4 | 四元封闭 union；Policy 为正式成员；transport tagged JSON 为 realization-private 投影 |
| Q5 | L0/L1 同为 Act（粒度=内容）；L1 假设维度=策略记忆不进协议；L2=Policy |
| Q7 | 工具白名单 = {submit_decision}；无任何现实效应工具（结构性排除，非提示词约束） |
| Determinism | 协议/correlation/validation/状态机/double 确定性要求成立；live LLM 不要求 bit-for-bit（baseline §8.3 已冻结） |

## Acceptance（本 Step）

1. 设计稿覆盖指令全部 8 问 + Failure Model + Replay 策略 + Provider 边界 +
   Determinism 裁决 + 四图；
2. 十条 Grill 预攻击问题逐条预答（设计稿 §11）；
3. 与 FROZEN 上游零冲突（凡涉及冻结面只引用不重定义；RUN-004 协议、
   GEV-004 评价、baseline 不变量 43-47 均继承）；
4. state = ready-for-grill（不 CLOSED）。

## 上游对齐清单

product-architecture-baseline-l0-l3（§24.2/§24.5/§24.8/不变量 43-47）·
uniagent-realization-baseline-v0.1（FROZEN 全文，尤其 §4/§5/§6/§8）·
ADR-0019/0022 · consultation-protocol-v0.1（T1-T6/D1-D7/V1-V6/M1-M3）·
decision-granularity-scenarios-v0.1（L0/L1/L2 + 五要素 + 18 场景）·
RUN-004（冻结协议；MaxConsultations 16/MaxTotalSteps 256；Defer 三态同律）·
GEV-004（确定性评价，零存储）· AgentDecision.cs/ConsultationTypes.cs/
RunDriverInputs.cs（现行缝形状）· DSH checkout 机制核查（ReactLoopAgent/
concludesTurn/session log/tool registry/headless/ACP）。
