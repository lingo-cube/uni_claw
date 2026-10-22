# World 未知显式化与决策验证体系 — 提案

> Status: `DRAFT / CANDIDATE`（提案，未授权实现；实施须另立 change 并按 UniFlow 验收）
> Authority: `NONE`（分析产物，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> Date: 2026-10-22
> Scope: 承接同日《World 模型胜任性 / 当前-设计 Gap / Kernel 兜底健壮性评估》
> （诊断已完成），本文只回答"剩下怎么做"：①未知显式化的方案路线（S0-S4）；
> ②决策质量验证体系四层（A/B1/B2/C）与回放式决策回归；③场景库补题清单；
> ④明确不做的事。完备性推演（数学/流程/哲学三法）作为方案依据收录附录。
> 输入：评估文档结论、场景库实况（30 条，全部 agent=double）、仿真设施盘点、
> TRAJECTORY-001 事件流/心跳基础设施、RUN-006 录制语料。

---

## TL;DR

1. **两个目标**：让"不知道"可用（Agent 看得见糊涂、补证有靶子、验收不一刀切、
   卡死有警报）；让决策质量验证从"每改一版 prompt 上真机"降为"离线回放回归"。
2. **路线**：S0 修缮两洞（封直读、步账落盘）→ S1 未知清单+计数（主菜）→
   S2 内核看门狗 → S3 场景库补三条题 → S4 回放编排器。
3. **验证四层**：A 替身+仿真（每提交，系统逻辑）/ B1 回放开环（离线，决策
   质量）/ B2 回放+仿真接棒（新决策后果）/ C 真机（世界保真度，定期）。
4. **诚实边界**：纯回放验证"每个状态下的决策质量"，验证不了"全新决策的
   下游后果"；后者归 B2/C。世界保真度只有 C 能背书。

---

## 1. 方案路线（S0-S4，每片可独立验收）

### S0 前置修缮（最小，先行）

| 项 | 内容 | 验收 |
|---|---|---|
| S0-1 封直读 | `KernelRunDriver.cs:2183` 返回判断直读 `ui.container.current` 绕过冲突过滤（正门在 3117-3124），改走正门；冲突时落入既有"目标页未知→Pause"路径（2204 行先例） | 构造带冲突 claim + Return 的场景：改前报 `traversal:return-failed`（RED），改后走 Pause/补证（GREEN） |
| S0-2 步账落盘 | `_stepsDispatched` / `_stepsAtLastGrant` / 续授记录三件一起入 journal（带防重放标识）；重启恢复点与咨询轮次同处折叠 | 跑 N 步 → 同 journal 起新 driver：剩余额度 = Max−N 而非 Max；重放不双记 |

### S1 未知清单 + 计数（主菜，F1+F2 合一）

- **记账**：World 每轮 revision 附"未知清单"，条目 = `{Subject, Kind, SinceRevision, Count, Basis}`；
  `Kind` 闭集四类：`IdentityAmbiguous / AttributionUnknown / RelationUnproven / ExtentUnknown`。
- **计数（M-of-N）**：ObservationNeed 从单槽开关改为"暂定→计数确认→销账/丢失"
  状态机；同槽扩展为清单条目之一，不再独占。
- **四读者接线**（全部现成岗位，零新建）：
  1. Heartbeat/CasePacket 透出清单 → Agent 介入判断从推断变读数；
  2. Control 补证调度按"挡路优先"取条目；
  3. Outcome Assurance View 用类型化未知替代裸 int（目标上未知挡完成，无关处放行）；
  4. S2 看门狗读同一清单。
- **纪律**：分类不分级（不引入概率，守住 confidence ≠ truth）；记账人仍
  World 一个；条目闭集进 schema 校验防方言回潮。
- **验收**：回放 RUN-006 弹窗循环类场景，心跳出现"IdentityAmbiguous 已 N 轮"；
  构造"疑虑在亮度、目标在 Wi-Fi"用例验收放行；未知条目随 revision 落 journal 可回放。

### S2 内核看门狗

- 只读清单、只发信号（"同一 Kind+Subject 持续 ≥N 轮未消"→ 卡死信号进事件流），
  不指挥动作——不违反"Kernel 不越权"。
- **分工**：Kernel 喊卡；换路/跳过归 Agent/Host 决策。上线后 Host 侧 M5 指纹
  降级为后备，禁止双层同时注入提示（TRAJECTORY-001 修过 latch 单发的坑，勿再造）。
- **验收**：同场景 Kernel 层发出卡死信号（不依赖 Host 指纹）；信号进事件流
  可回放；与 M5 双注入的负向测试。

### S3 场景库补题（考卷缺口，仿真器零新增）

完备性推演发现的三个洞（附录 A.2）逐条补：

| 新 golden | 考什么 | 现状 |
|---|---|---|
| 六步补证闭环 | 暂停→补证→验证→恢复主链（WORLD-OBSERVATION-001 只有单测无场景） | 仿真缝现成，只缺场景条目 |
| 多容器期望 | 双容器/分屏下当前容器唯一性的期望定义 | 缺的是期望，不是仿真器 |
| 清单换路（出站断言版） | 心跳携带清单字段 + 剧本 Agent 按清单换路的链路 | TRAJECTORY-001 有出站断言先例 |

- schema 扩展：为 B1 增加**性质式期望**（不变量/边界/统计型），与现有精确
  期望并存、分 realization 标注。
- 随动：S1 落地后 PERC-005 的 uncertainty 期望形态、POLICY P6 的看门狗出口
  需重认证（certifiedByChange 机制现成）。

### S4 B1 回放编排器（决策回归）

- **定点回放开环**：录制 run（RUN-006 有 194 组 capture + 事件流）按
  DecisionId/seq 切决策点 → 重建 Agent 可见上下文（心跳/CasePacket/参考索引，
  均为投影可复现）→ 喂真实 Agent（真模型+当前 prompt）→ 双尺评分：
  ①性质判据（同目标≤3 次、危险点击=0、收到清单 N 轮内换路）；②与录制决策
  的一致率。
- **用途**：prompt/插件/模型升级的决策回归——TRAJECTORY-001 链改了 9 版
  prompt 每版上真机试错；有 B1 后大部分降级为离线回放。
- **B2（可选接棒）**：回放到与录制分叉处接仿真世界续跑，验新决策下游后果。
- **语料治理**：代表性 run 策展、防泄漏（不得拿测试集调 prompt）。
- **验收**：对同一录制语料，旧版 prompt 与新版 prompt 的性质通过率/一致率
  可对比输出；编排器产物落 evidence 可复现。

---

## 2. 验证体系四层

| 层 | 配置 | 验证对象 | 频率/成本 | 现状 |
|---|---|---|---|---|
| A | 仿真世界 + 替身 Agent | 系统逻辑（确定性回归） | 每提交，零成本 | ✅ 30 条场景 + 仿真全绿 |
| B1 | 仿真世界（录制回放）+ 真模型 | 决策质量：读不读清单、换不换路 | 离线、便宜、可反复 | ❌ 缺编排器+判据库（S4） |
| B2 | 回放前缀 + 仿真接棒 | 新决策的下游后果 | 中 | ❌ 可选 |
| C | 真机 + 真模型 | 世界保真度 + 最终背书 | 贵，定期 | 部分（RUN-006）；H7 blocked |

要点：仿真能验证"系统对决策的反应"（A）与"决策本身质量"（B1/B2），
**验证不了"仿真世界 ≈ 真机"**——后者只有 C。TRAJECTORY-001 验证轮已证明
B 配置可行（真 Agent 跑 SIM 遍历环），schema 的 `agentDecisionRealization`
字段本就支持标注；堵点是桥运维（3081 专线）与性质式期望，非架构。

---

## 3. 不做清单（与理由）

| 不做 | 理由 |
|---|---|
| 概率/贝叶斯化未知 | 感知质量撑不起校准概率；违反 confidence ≠ truth 纪律 |
| 机器人式连续不确定（协方差） | UI 是离散世界，分类闭集够用 |
| 关联闸门（差距超阈开新页） | 质量依赖感知侧"候选/概率/弃权"三件套（见 Jev 调研 P1），先备料再立项 |
| ODD 式分级降级 | 无"部分成功也算完成"的真实 buyer 前保持一刀切 fail-closed |
| Contains/Overlays 预造 | ADR-0026：不为想象需求预造，等真实 buyer |
| 开放世界辨识 | 数学不可实现（附录 A.1 定理 1），由"显式未知+屏障"承担 |

---

## 4. 风险与开放问题

1. **golden 随动重认证**：S1/S2 触碰 PERC-005、POLICY P6 期望形态与
   runtimeSourceHash——重认证走既有 certifiedByChange 协议，须在 change 中
   显式列出全部随动条目。
2. **双报警**：看门狗与 Host M5 的注入去重（S2 验收含负向测试）。
3. **清单闭集执法**：Kind 四类进 schema 校验；防实现中长成自由文本。
4. **B1 非确定性验收语义**：性质式期望的通过标准（N 轮通过率阈值）需要
   标定，首轮先出报表不定门槛。
5. **语料代表性**：现有录制集中于 Settings 遍历；换域前须补语料。

---

## 5. 与既有工作关系

- 诊断依据：`world-model-capability-gap-and-kernel-fallback-assessment-2026-10-22.md`
  （W1-W8、设计/实现缺陷归类、Kernel 三处可击穿点）。
- S0 直接对应评估 §4.3-1/4.3-2（实现缺陷 1、2）与 §5 P1/P2。
- S1 对应 §5 P3（Uncertainty 结构化），并落实其"四买方已就位"判断。
- S3 洞 A/B/C 来自本文附录 A.2 流程推导。
- S4 复用 TRAJECTORY-001 事件流 seq / 心跳投影 / 录制语料；与 Jev 调研
  P3（shadow consult）互补：P3 评 provider，B1 评决策链。
- H7（真机一轮）保持独立 blocked 项，归层 C。

---

## 附录 A. 完备性推演（方案依据，浓缩版）

### A.1 数学

```
Complete(W,Σ) ⟺ ∀σ∈Σ: Run(W,σ) ⊢ E_σ           （对库完备）
定理1（可辨识性）: ω(s₁)=ω(s₂) ⟹ W 不可分——同 route 同指纹不同语义页
  在观察通道内数学不可辨识；闸门/清单的作用是显式化，不是消除。
定理2（安全可完备）: Safe ⟺ 未知×消费路径全表 fail-closed——只依赖
  "知道自己不知道"，S1 闭集+S0 封绕过后表格可满秩。
定理3（活性有界）: 计数上限+看门狗+HardCap ⟹ 不悬停；不承诺必胜。
```

### A.2 流程推导三洞（S3 依据）

现有 30 条场景逐一走链路（观察→接纳→Reconcile→身份/关系→Slice→消费→
Effect→验证→推进）：30/30 保持，但——

- 洞 A：六步补证闭环无场景（主链裸奔）；
- 洞 B：全部 agent=double，W* 头号新价值（Agent 读清单换路）零覆盖；
- 洞 C：多容器下 World 行为无期望定义。

### A.3 哲学（五条，各一句）

地图非疆域（问边界标注而非画得全）；忒修斯之船（同一性是约定，阈值可显式
化不可消灭争论）；休谟归纳（重访是归纳非演绎，置信永不到 1）；哥德尔式
自指（模型不能自证无盲区→看门狗/场景库/真机三外部担保是结构必需）；
开放世界假设（Contains/Overlays 缺位是"不知道"非"不存在"，按需闭包）。

---

## 附录 B. 关键锚点

- 直读绕过：`src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:2183`（正门 3117-3124）
- 步账易失：`KernelRunDriver.cs:173`（`_stepsDispatched`）
- Uncertainty 标量：`src/UniClaw.Kernel/World/WorldModel.cs:667`、
  定义 `World/WorldBeliefRevision.cs:27`、唯一消费 `Assurance/RuntimeAssurance.cs:217`
- ObservationNeed 单槽：`WorldModel.cs:681-685`、`WorldBeliefRevision.cs:99`
- 续授挡板（Kernel 唯一进度判据）：`KernelRunDriver.cs:611-619`
- Host 侧防循环：`src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs:55`
  （MaxTargetAttempts）、`SettingsCoverageRunner.cs:41`（ShouldIncrementStagnation）
- Contains/Overlays 枚举无生产：`World/UiRealization/ContainerAssociation.cs:28-31`、
  投影只选 NavigatesTo `WorldModel.cs:243-244`
- 场景库实况：`scenarios/`（30 条；schema v2 认证链；全部 agent=double）
- 回放先例：WRC-003（Replay 非权威）、WORLD-OBSERVATION-001（Jev 12 例回放）、
  TRAJECTORY-001（SIM 循环 + 真咨询链验证轮）
