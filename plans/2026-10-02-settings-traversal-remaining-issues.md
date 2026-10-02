# Settings 遍历战线 — 遗留问题汇总与解决方案（审阅稿）

> 范围：AGT-005（正常覆盖遍历）→ AGT-008（证据持久化）全部关闭后的
> 遗留项。每项含：问题描述、证据、解决/设计思路、验证方式、优先级、
> 待裁决点。审阅后按优先级立项。
> 基线：7b49038d + 本战线已合入改动；全量 1167 测试绿、29 场景认证 PASS。

## 总览

| # | 问题 | 优先级 | 预估 | 状态 |
|---|---|---|---|---|
| 1 | 弹窗/覆盖层：无识别、无处理、协商缺失 | **P0** | 中 | 设计已敲定（本文 §1） |
| 2 | 路由指纹撞名（子页与根页同标题） | P1 | 小 | 思路定（§2） |
| 3 | 滚动到底失败分支未真机触发 | P1 | 小 | 被 #1/#2 堵塞，解锁后重试（§3） |
| 4 | Wi-Fi 闭环（HostRunner）证据持久化未接线 | P2 | 小 | 照搬 AGT-008（§4） |
| 5 | 滚动候选质量：非入口元素仍可成为候选 | P2 | 小 | 思路定（§5） |
| 6 | 多步 Act 提案拒绝分支无显式单测 | P2 | 极小 | 补测（§6） |
| 7 | 场景库 schema 不承载遍历场景 | P2 | 中 | 需 schema 扩展裁决（§7） |
| 8 | 慢智能（Slow）仲裁未接入 | P1 | 中 | 当前闭环必须接入（§8） |
| 9 | 环境变体矩阵未测（locale/旋转/视口/NAF/API<35） | P3 | 中 | 声明性（§9） |
| 10 | 容量重试真机触发 | — | 不可行 | 不可诚实强制，维持确定性背书（§10） |
| 11 | UniAgent 抽象遍历计划与清障分支缺少明确契约 | P1 | 中 | 纳入 AGT-009（§11） |

依赖关系：#3 被 #1、#2 堵塞（弹窗与撞名的连锁正是当初走不到底的实证
原因）；其余独立。

---

## §1 弹窗/覆盖层：无识别、无处理、协商缺失 【P0，建议 AGT-009】

### 问题（三层，均有实证）

e1 现场（`evidence/real-settings-coverage-negative-20261001/run-e1-with-evidence/`）：

1. **感知层无解释层**：弹窗 XML 里 `package=com.android.permissioncontroller`
   与 `android:id/alertTitle/parentPanel/buttonPanel` 白纸黑字，但无代码
   汇总成"这是弹窗"——被当成"标题缺失的未知页"。
2. **指令层无障碍词汇**：指令只有 进/滚/返回/重进；弹窗在场时 back
   指令指挥不存在的 Navigate up。
3. **对话层盲纠正**：模型提议 DISMISS（实为想关弹窗，局部合理）被
   判偏离；纠正重问上下文一字不改（无拒绝原因反馈），重试 1 次后
   fail closed 终局。

### 解决/设计思路（已与 owner 多轮敲定）

**架构原则：XML、Fast、Slow 分工，typed 声明承载——不加坐标通道、
不加新权威。**

```
XML 结构分类器（确定性）：
  窗口根 package ≠ hostPackage（配置）
  ∨ resource-id ∈ {android:id/alertTitle, parentPanel, buttonPanel}
  → ui.overlay.popup = present/absent（类型化声明入证据流；
    provenance 带感知/结构标识）
  几何形态不作独立判据（UI 风格，不稳）

指令层：popup=present 优先于普通遍历指令 → 障碍处理分支
  （点取消类按钮：CANCEL/button2 优先，DISMISS/button1 次之；
   词汇与优先序进配置 obstacle 段）
  单步、当前观察 grounding、回执、有界纠正机制照旧（零新权威）

验证层：obstacle 步骤 = popup 声明 present→absent
  （同一分类器前后测；新 Assurance check post-action-obstacle-cleared；
   弹窗仍在 → fail closed；连锁弹窗自然逐个清）

Fast/YOLO：保留 label、位置、置信度和 provenance；XML 对结构事实更强，
Fast 对视觉候选提供旁证，不单独制造业务语义。

无 XML 分支：允许使用 Fast 文本/布局、截图/OCR 或 Slow 形成候选证据；
只有在没有足够的新鲜证据完成目标 grounding 时，才进入既有 Defer→
  bounded-stop:unknown-page 路径。没有视觉 Slow 配置时，不得把文本结果
 伪装成视觉结果。

错误不对称性保底：观察冲突、目标不唯一或动作前检查失败时，Kernel
  不出手并保留冲突原因；不允许用单一模型置信度绕过 grounding。

慢智能：在无 XML、结构与视觉冲突、弹窗连续处理失败或语义不明确时
  异步咨询；结果进入观察证据和 Agent 决策上下文（见 §8、§11）。
```

### 待裁决点

- 无（设计已与 owner 确认到实现级）。

### 验证方式

① e1 录制的弹窗/正常页 XML 做分类器离线单测 ② 账本/指令/验证单测
③ 模拟世界"弹窗插曲"场景（任务中途弹→自动清→完成）④ 真机终考：
复跑 e1 场景，预期自动点 CANCEL、任务继续（顺带冲击 §3）。

---

## §2 路由指纹撞名 【P1】

### 问题

Security & privacy 页面标题 = "Settings"，与根页指纹相同 → 进入成功
但验证判"没进去"（post-action-target-unique 失败）。实证：
`run-e1-with-evidence` step 20（点击前后截图均在档）。

### 解决/设计思路

页面身份从“仅标题”升级为多信号 RouteKey，并与滚动摘要分开：

```
RouteKey := 稳定组合(标题、布局、上下文、固定锚点)
ViewportDigest := 稳定摘要(当前视口可见元素/文本/结构)
```

- RouteKey 用于页面身份和导航验证；ViewportDigest 用于判断滚动后内容
  是否变化，二者不得互相替代。
- XML、截图/OCR、Fast 或 Slow 都可以提供摘要来源，但必须保留来源和
  观察周期；没有 XML 不能退化成只看标题。
- 用 e1 的 9 个录制页面做离线区分度验证：撞名页与根页必须不同，
  同页相邻观察在稳定条件下必须一致。
- RootRoute 配置按新 RouteKey 语义迁移一次，不把旧标题值继续当作完整身份。

### 实施约束

- RouteKey 与 ViewportDigest 必须保持独立；具体摘要字段以 e1 离线区分度
  结果为准，不在本计划中预造新的全局身份接口。

### 验证方式

录制页面语料离线区分度测试 + 真机复跑（撞名页进入应验证通过）。

---

## §3 滚动到底（内容不变）失败未真机触发 【P1，被 #1/#2 堵塞】

### 问题

swipe 内容不变 → 验证失败 ×3 → bounded stop 的分支只有确定性测试
背书。真机三次冲击（d1/d3/d4，digest 逐字节一致）均被撞名+弹窗连锁
堵在列表底部前。证据：`run-d1-blocked`…`run-e1-with-evidence`。

### 解决/设计思路

1. **主路径**：#1、#2 落地后复跑同配置（settings-coverage-neg-c）——
   撞名可验证进入 + 弹窗自动清障后，列表底部应可达。
2. 滚动完成由新的 ViewportDigest 比较判定；Digest 可以来自 XML，
   也可以来自截图/OCR/Slow 的结构摘要，但必须在重新观察后比较。
3. **备路径**（若真机仍不可达）：自建浅列表测试 app（ScrollView 内
   2-3 项）作为受控夹具，一次实验即可。

### 待裁决点

- 备路径是否立项（若主路径解锁则不需要）。

---

## §4 Wi-Fi 闭环证据持久化未接线 【P2】

### 问题

AGT-008 只给 Settings 覆盖遍历 feed 接了 evidence 落盘；HostRunner
（Wi-Fi 两步闭环）用同一个 `SettingsTraversalLiveFeed` 但未传证据目录。

### 解决/设计思路

照搬：HostOptions 加证据目录参数（或直接复用配置），构造 feed 时传入。
~10 行 + 1 个测试。

---

## §5 滚动候选质量：非入口元素可成为候选 【P2】

### 问题

已修两处（BackDescriptor 排除、失败候选跳过），但通用问题仍在：
任何可见 ui.element（如通知卡按钮、搜索栏）都可能成为"滚动发现
入口"。e1 的 Dismiss 即此类。

### 解决/设计思路

候选准入过滤（分层）：

1. **结构层**（推荐先做）：候选必须位于滚动容器 bounds 内 **且**
   具有"设置行"结构特征（有副标题文本的容器 / resource-id 匹配
   `*:id/recycler_view` 子项模式）——从 XML 结构判，确定性。
2. **词汇层**（兜底）：配置候选排除词汇表（Dismiss/Clear/Search 等
   动词性短文本不作为入口）。
3. #1 落地后弹窗本身已走障碍模式，候选污染面自然缩小——本项降级为
   防御纵深。

候选过滤不代替 UniAgent 的抽象计划。UniAgent 需要把候选分为“进入、
记录、操作、禁止、未知”；未知候选只能触发重新观察或重新规划，不能
直接变成点击动作。

### 待裁决点

- 结构特征的具体判据（需对录制语料做一次离线分析后定）。

---

## §6 多步 Act 提案拒绝分支无显式单测 【P2，极小】

`ValidateAdherence` 的 `directive-requires-single-step` 分支只有代码
无测试。补一个 director 单测（模型返回两步提案 → 偏离记录 + fail
closed）。

## §7 场景库 schema 不承载遍历场景 【P2，需裁决】

scenarios/schema.json 的 expectations 是 Kernel 语义专用 6 键；覆盖
遍历（覆盖率/未覆盖项/分歧点）无承载字段。若要把遍历做成认证场景
（纳入 29 个场景的 digest 链），需扩 schema + 双语言认证器同步改。
**裁决点**：是否值得把遍历场景入库（当前以 Host 测试 + evidence
承载，亦可接受不扩）。

## §8 慢智能（Slow）仲裁接入 【P1，纳入当前闭环】

PER-017/018 基础设施在（/slow 端点、视觉深究 provider、专用 slow
session），但 Settings 业务链路尚未接入。当前最小范围是：

1. 复用 Fast 已有的文本、布局和截图上下文发起异步 Slow 请求；
2. Slow 结果带 request、capture、observation cycle 和 provenance，进入
   观察证据和 `ConsultAgent` 决策上下文；
3. 触发条件先限定为：无 XML、结构/视觉冲突、弹窗连续处理失败、页面
   语义不明确；
4. Slow 只提供页面/元素语义和策略建议，不直接提供执行授权；
5. 超时、能力未配置或结果冲突时，回到 Defer/Unknown，不静默降级。

视觉 Slow 必须由 profile 显式配置；暂时使用文本模型时，只能处理它实际
拿到的文本和布局，不能宣称完成视觉判断。具体重试次数和容量策略复用
PER-017/018，不在 Settings 另造一套。

### 实施约束

- 触发条件和 N 先用现有有界预算落地；不能以“模型置信度足够”直接授权动作。

## §9 环境变体矩阵未测 【P3，声明性】

其他 locale（标题本地化会全链变）、旋转、视口、NAF 节点、API<35。
均未测但均为配置/夹具层变量，当前 AOSP API35 英文单夹具是显式边界。
建议保持声明，等真实多夹具需求出现再立项。

## §10 容量重试真机触发 【不可行，显式关闭】

容量错误来自 provider 侧，不可诚实强制触发；确定性背书
（`Consult_RetriesSelectedModelCapacity_OnSameRequest`：同 requestId
重试、同 session）已足够。若未来真实运行自然遇到，按既有策略自动
处理并对照 AGT-006 证据结构归档即可。**建议：不立项。**

---

## §11 UniAgent 抽象遍历计划与职责边界 【P1，纳入 AGT-009】

UniAgent 通过现有 `ConsultAgent` 决策缝为 Kernel 提供抽象计划和局部
策略，不输出坐标、一次性 selector 或直接点击命令。计划至少覆盖：

- 元素处理：进入、记录、操作、禁止、未知；
- 流程控制：重新观察、重新规划、停止；
- 障碍处理：清障、验证清障结果、回到原遍历或停止；
- 计划依据：当前目标、已接受观察、人工配置、记忆和必要的外部资料。

### 已定的计划形状

计划作为新的 advisory `AgentDecision.Plan` 进入现有 P25 决策缝，不修改
`AgentDecision.Policy` 的 L2 规则语义，也不另造执行循环。计划是有界的有序
列表，每项只有语义目标和意图：

```text
PlanItem = Act | Observe | Control
Act      = 目标角色/描述 + effect class + 可选 desired state
Observe  = 记录或补证，不产生 Effect
Control  = Reobserve | Replan | Stop
```

Settings 遍历把它投影为：

```text
进入     → Act(navigate)
操作     → Act(effect)
记录     → Observe(record-only)
未知     → Control(Reobserve 或 Replan)
禁止     → Observe/Skip；只有带人工策略引用时才是绑定约束
```

Kernel 每次最多采纳一个 Act 项，重新 grounding、Assurance、Effect Gate、
dispatch 和 post-action verification；计划中的后续项不能跳过这些步骤。
新观察、路线变化、合约变化、冲突或验证失败会使尚未执行的计划失效，进入
重新观察或重新规划。计划不能包含坐标、occurrence、过期 selector 或
直接 Effect 授权。

`AgentDecision.Act` 继续保留，作为已经形成单步建议的快速路径；`Defer`
继续表示有界重新观察；`NoAction` 继续表示完成或安全终止。新增的 Plan
只负责把高层策略送入既有执行链，不成为第二个控制器。

人工配置对不可逆动作、禁止项和审批要求拥有最终约束。没有显式配置时，
可以咨询 UniAgent 提供策略建议，但仍须经过 Kernel 的 grounding、
Assurance、Effect Gate 和动作后验证；记忆或外部资料不能单独证明当前
按钮存在，也不能单独授权当前动作。

`SettingsCoverageDirector` 继续负责覆盖范围、配额和终止条件，但应允许
受约束的重新观察、重新规划和障碍分支；不能把所有偏离当前 directive 的
Agent 决策都直接判为非法。

---

## 建议的立项顺序

1. **AGT-009** = §1 + §8 + §11（弹窗、Slow 接入、抽象计划与清障分支）
2. **AGT-010** = §2 + §3（去撞名 → 复跑滚动到底，一个 change 内闭环）
3. **AGT-011** = §4 + §5 + §6（小项打包：Wi-Fi 证据接线 + 候选质量 +
   补测）
4. §7 待后续买方裁决；§9 声明；§10 关闭。

## 证据索引

- 弹窗现场（截图+XML+trace）：
  `evidence/real-settings-coverage-negative-20261001/run-e1-with-evidence/`
- 撞名现场：同上 step 20（mystery1-pre/post-tap-security.png）
- 滚动到底阻塞链：同目录 `run-d1-blocked`/`run-d3-deterministic`/
  `run-d4-deterministic`（digest 一致 55291AC…）
- 证据持久化验证：`evidence/real-settings-coverage-evidence-20261002/`
- 各 change 状态：`changes/AGT-00{5,6,7,8}/state.md`
