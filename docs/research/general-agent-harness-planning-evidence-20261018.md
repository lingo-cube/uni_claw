# 通用 Agent Harness 的规划、动作循环与重规划：一手来源证据

状态：Research / Authority: NONE  
研究日期：2026-10-18  
用途：供 UniClaw 设计讨论与后续 grill-with-docs 使用；不冻结接口，不构成实现决定。

## 范围与证据边界

本文按规划表示、执行循环、重规划触发、持久状态与完成验证比较 Anthropic 长时任务 Harness、LangChain Deep Agents、Microsoft Magentic-One、OpenAI Agents SDK/Responses 与 OpenAI Codex 使用建议。来源限厂商官方文档、研究团队论文或官方工程文章；访问日期均为 2026-10-18。本文没有复现实验，也没有审计 UniClaw 产品代码；厂商报告的效果属于其特定模型、任务和评测设置，不能直接推出手机 UI 效果。

这里把几个容易混称的对象拆开：

- **Todo/检查清单**：给人或运行时看的任务进度账本，记录待办、完成状态；它未必规定下一条机器动作。
- **只读 Plan Mode / Ask Mode**：先让模型提出方案供检查，随后由人决定是否进入执行阶段；计划文本本身不执行动作，也不代表授权。
- **任务规划**：面向目标的子任务分解或调度提示，可能在运行中被改写，不一定有严格可执行语义。
- **typed 执行 Plan**：有结构化步骤及明确采纳、结果/错误语义的Runtime载荷，可进一步表达前置条件或预期；只有在系统明确赋予执行语义时才算执行计划。
- **工具调用循环**：模型发出一个或多个调用，运行时执行并返回观察，模型据此继续、修正或结束；没有事先完整 Plan 也可构成闭环。

这些概念不是成熟度等级。清单可作可视化而不控制执行；自然语言计划可指导工具循环但并非机器合同；typed Plan 也不会自行提供状态新鲜度、授权或结果证明。

## 机制比较

| 来源/机制 | 规划表示 | 执行循环与反馈 | 重规划/状态 | 验证与公开限制 |
|---|---|---|---|---|
| Anthropic，长任务跨上下文 Harness（2025-11-26） | 首轮 initializer 从目标写结构化功能清单，示例 JSON 条目含类别、描述、验收步骤、`passes:false`；后续 agent 每轮挑一项做。清单是目标覆盖/进度账本，不是全部动作的静态计划。 | 每个新上下文先读进度文件、Git 历史、功能清单，启动服务并跑基本端到端检查，再做单一功能；会话结束提交 Git 并更新进度。 | 跨上下文用进度文件与 Git 留痕续接；失败/已有工作通过启动和端到端检查暴露后再修。 | 作者报告显式浏览器端到端验证改善了发现代码中看不出的缺陷，但也记录 Puppeteer 看不到浏览器原生 alert 等局限。示例是全栈 Web 应用；作者说明跨领域推广是未来工作。[来源](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) |
| LangChain Deep Agents（当前文档；v0.7 起 Todo 规划为 opt-in） | 可选 `write_todos` 提供结构化任务列表，以便追踪进度；官方称复杂/长任务、较弱模型 accountability、以及 UI 展示进度时可能有用。 | 仍是通用 tool-calling loop；Todo 是 harness 可选能力，既可在执行时维护，也能支撑进度 UI。 | agent 可维护结构化清单；框架另有文件上下文、记忆与子 agent 能力。 | 文档将 task planning 明确标为 opt-in，不能据此声称每个任务必须预先 Plan，或 todo 会控制工具动作。[来源](https://docs.langchain.com/oss/javascript/deepagents/overview) |
| LangGraph Plan-and-Execute（作者工程文章，2024-02-13；Leader补充核验） | Planner生成多步任务方案，Executor接收单个任务步骤并调用工具。 | 执行后交给re-planning判断结束或追加方案；把大模型规划调用与局部执行分开。 | 该实现仍串行、各子任务仍可能调用模型；同文另述ReWOO变量依赖和LLMCompiler DAG，不是同一种计划形状。 | 作者提出减少大Planner调用的收益，但没有证明手机UI收益；不能把“减少大模型调用”写成“取消每步观察/验证”或“一切执行都无模型”。[来源](https://www.langchain.com/blog/planning-agents) |
| Microsoft Magentic-One（论文，2024-11-07） | Orchestrator 保存 task ledger：已知/已核实事实、待查事实、待推导事实、估计；同时自然语言生成分派给专门 agent 的步骤计划。论文明确说计划类似 CoT 提示，是行动指南，不要求严格照做。 | 两层循环：外层维护总任务和 task ledger；内层 progress ledger 判断任务是否完成、是否循环、是否前进、下一位 agent 与下一条指令。 | 检测重复或停滞后计数；实验阈值触发回外层反思，更新账本并修订原计划，再开内层循环。可设尝试次数/时间上限。 | 论文给出消融与错误分析，但结果来自特定基准和模型组合，并非手机场景保证；本文不以其成绩推断UniClaw效果。[论文](https://arxiv.org/abs/2411.04468) |
| OpenAI Agents SDK（当前官方指南） | 核心文档定义的是 agent 配置、工具、handoff、approval 与 loop；并未要求每个任务先产出完整的执行 Plan。 | 一次 SDK run 持续循环：调用当前 agent、检查输出、有 tool call 则运行并继续、有 handoff 则切换 agent、无后续工具工作时才返回。 | 可将历史放在应用内、SDK session、Conversations API 或 Responses chaining；文档建议每个对话选一种状态延续策略。SDK session 面向持久记忆、可恢复运行或 approval 流程。 | 这是运行时机制说明，不是各策略的比较实验；状态延续不等于当前 UI/世界状态仍新鲜。[运行循环](https://developers.openai.com/api/docs/guides/agents/running-agents) · [运行时选择](https://developers.openai.com/api/docs/guides/agents) |
| OpenAI Computer Use（官方 API 指南） | 模型可提出截图请求及结构化 click/type/scroll 等动作；指南也说明可用自定义 UI tools 取代原生 computer tool。工具 action schema 并非任务级计划。 | 应用提供环境和驱动；执行动作后返回截图/工具结果，继续模型循环；每批动作后检查状态。 | 环境状态由集成方维持，结果应作为新观察传回；模型请求完成不等于应用中的动作已成功。 | 官方示例在动作后检查真实页面状态；安全建议包括隔离环境、允许列表、将屏幕内容视作不可信，并对购买、发送数据、不可逆变更确认。可作为“计划/动作序列≠授权/验证”的直接例子。[Computer use](https://developers.openai.com/api/docs/guides/tools-computer-use) |
| OpenAI Codex 使用建议（官方 PDF，检索时未见发布日期） | 对大型代码变更建议先在 Ask Mode 请求实施计划，再将计划作为切到 Code Mode 后续请求的输入。此处是先审阅、再执行的交互流程。 | 人可在 Ask 与 Code 两阶段之间检查并重新给提示；文档未声称 Ask 模式计划会作为 Runtime typed Plan 自动运行。 | 跨阶段由人/后续 prompt 携带方案；PDF 未提供机器计划状态、重规划触发器或自动验证协议。 | 这是产品使用建议，不是通用执行架构或对手机动作的安全论证。只能证明一种人控的计划审阅 UX。[How OpenAI uses Codex](https://cdn.openai.com/pdf/6a2631dc-783e-479b-b1a4-af0cfbd38630/how-openai-uses-codex.pdf) |

## 可供讨论的观点与取舍

1. **“计划”至少有三个不同责任。** 清单（Anthropic/LangChain）解决目标覆盖和进度可见；Magentic-One 的自然语言方案解决粗粒度分工与任务导航；OpenAI Agents/Computer Use 的核心执行闭环靠每轮工具结果继续，并不要求先锁定整条步骤链。讨论 UniClaw 时应先问要解决哪种失败：漏目标、无法恢复、观察不够新、权限不清，还是动作序列需要强约束。
2. **完整的事前计划不是闭环可靠性的同义词。** Magentic-One 的计划可被当作 hint 并在外层循环修订；其内层通过当前 progress ledger 判断停滞。Computer Use 则要求动作后回传现实界面状态。对动态环境，闭环中的“看见结果再决定”可能比一次生成很长的静态动作序列更关键。
3. **Todo 有低成本的信息价值，但不天然有执行权。** LangChain 将 TodoList 设为 opt-in，列出的用途包括任务复杂度和 UI 进度流。Anthropic 的功能清单承担的是完成定义与跨上下文工作队列。若把两者直接升级为机器执行 Plan，必须另行定义状态、动作映射、变更、校验和权限；上述来源没有把清单本身定义为授权合同。
4. **持久状态应把“目标/计划/已观察事实/猜测/执行日志”分清。** Magentic-One 明确区分 verified/known facts 与 educated guesses；Anthropic 用进度文件和 Git history；OpenAI SDK 区分应用 history、session、conversation 与 response continuation。可推断：让计划恢复更容易，但必须避免把旧屏幕、过时计划或估计重新读成当下事实。
5. **重规划需要具体触发和预算。** Magentic-One 给出停滞/循环计数与最大尝试/时间的例子；OpenAI SDK 的普通 loop 以“无工具工作、返回最终结果”为停止点；Anthropic 每轮小步后留恢复材料。方案可以比较局部修补、回到高层计划、向人请求输入和预算耗尽时停机；来源并未证明固定阈值普适。
6. **验证和授权必须独立于规划表示。** Anthropic 记录自称完成却未端到端测试的问题；Computer Use 指南要求集成侧执行动作后回传并核对应用真实状态，且 consequential action 由用户确认。typed Plan 可以让预期结果更明确，却不能单凭结构化字段证明点击发生、效果达成或动作获准。

## 对动态手机 UI 的反例

假设任务是“打开设置，把省电模式关闭”。生成式长计划可能列出：进入设置 → 电池 → 省电模式 → 点击开关 → 返回并报告。实际手机中，设置首页可能因搜索建议/系统提示先加载后重新布局；“电池”入口名称可能随 OEM、Android 版本或语言变化；用户通知弹窗可遮住内容；转入二级页时还可能出现权限或确认对话框。此时预先生成的坐标序列或“照计划继续”的 typed 步骤即使结构完全有效，也可能点错控件或沿着过时状态执行。

这不是来源实验验证过的具体失败率，而是将官方 Computer Use 的截图-动作-回传循环、动作权限控制建议，以及 Magentic-One 计划可修订的事实，迁移到动态手机界面的**设计反例**。更稳妥的设计讨论应问：动作前需要什么新鲜观察？哪类界面变化使当前动作失效？动作后以什么可观测状态判定成功？观察与预期不一致时是局部重试、重规划还是停止/询问？来源不足以直接回答 UniClaw 中这些判定应由谁拥有。

## A/B/C 讨论时可检验的假设（非结论）

按当前任务讨论方向，可把候选方案写成以下假设，而不是预先选定：

- **A：加强阶段意图 + Act。** 可能让模型在现有工具循环中声明近期意图，并用反馈继续；优势假设是少引入新的可执行计划状态，风险是假设/Act 的边界和进度可视性仍不够清楚。
- **B：方向计划 + 短执行 Plan。** 可能用高层方向保持目标覆盖，再以短批次动作降低状态陈旧风险；代价是阶段状态、重规划与短计划之间需要明确同步规则。
- **C：强制完整 typed Plan。** 可能有利于预检依赖、结构校验和执行追踪；代价是假设它能减少模型调用或更安全没有来源支持。若界面变化频繁，整计划校验、续行/中断语义和重新授权成本可能增加；需单独验证，并确保计划结构不绕过逐步权限与当前状态核验。

三个方案都需要分别讨论：是展示进度，还是约束下一动作；计划允许谁修改；环境变化时什么状态失效；Act/Plan 怎样通过同一权限守卫；完成由哪个证据源确认。本文引用的业界机制只能提供比较维度，不能代替 UniClaw 的协议与责任边界裁决。

## 来源、日期与不能确认之处

1. Anthropic, “Effective harnesses for long-running agents”, 2025-11-26：[官方工程文章](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents)。文章描述一个全栈 Web demo 的实践及其局限；不证明通用任务或移动设备适用。
2. LangChain, “Deep Agents overview / Task planning”：[官方文档](https://docs.langchain.com/oss/javascript/deepagents/overview)，访问 2026-10-18。页面称 task planning 从 v0.7 起 opt-in；文档会更新，本文没有固定某次 release 的源码快照或独立实测。
3. Microsoft Research, “Magentic-One: A Generalist Multi-Agent System for Solving Complex Tasks”, 2024-11-07：[作者论文](https://arxiv.org/abs/2411.04468)。任务 ledger、progress ledger 与循环机制可直接核对；基准结果受论文评测集/模型版本范围约束。
4. OpenAI, “Running agents”：[官方 Agents SDK 指南](https://developers.openai.com/api/docs/guides/agents/running-agents)，访问 2026-10-18。
5. OpenAI, “Agents”：[官方运行时总览](https://developers.openai.com/api/docs/guides/agents)，访问 2026-10-18。该页面区分 Agents API、Agents SDK 与 Responses API 的执行/状态责任。
6. OpenAI, “Computer use”：[官方 API 指南](https://developers.openai.com/api/docs/guides/tools-computer-use)，访问 2026-10-18。示例和建议说明动作派发、截图反馈及集成侧安全控制；未对所有移动 OS/OEM 作保证。
7. OpenAI, “How OpenAI uses Codex”：[官方 PDF](https://cdn.openai.com/pdf/6a2631dc-783e-479b-b1a4-af0cfbd38630/how-openai-uses-codex.pdf)，访问 2026-10-18。PDF可核验 Ask Mode 先形成代码实施计划的建议；网页索引未显示出版日期，也未检索到可确认发布日期的 PDF 元数据，因此不标注发布日期。
8. LangChain Team, “Plan-and-Execute Agents”, 2024-02-13：[作者工程文章](https://www.langchain.com/blog/planning-agents)，访问2026-10-18。Leader补充这一明确分离Planner/Executor/Replanner的正向样例，避免只研究Todo及反应式循环；引用的是机制，不将文章的成本/速度宣传当作本项目实测。

本轮没有核实 Codex 桌面产品各版本 Plan Mode 的具体内部实现，也没有证据表明 Ask/Plan Mode 是可被 Runtime 直接执行的 typed Plan。其他来源也没有给出以上机制在动态 Android 手机 UI 上的同条件对照、精确调用成本，或“强制 typed Plan”与“反应式闭环”的普遍胜负结论；这些保持为后续设计问题。
