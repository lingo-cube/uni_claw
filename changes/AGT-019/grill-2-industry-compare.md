# AGT-019 Grill 记录 2 — 业界通行做法对照（research）

> Provenance：fresh subagent（无本会话上下文），zai-coding-cn / glm-5.3-flash（owner 指令），
> 2026-10-17 派发，原文未改动。方法要求 = plan 步骤 1 之「对照业界通行做法」。
> 工具说明：modelcontextprotocol.io / agentclientprotocol.com 在派发环境连接被拒，
> 已用 GitHub raw 同源文档替代并如实标注；ego-browser 同样被拒不可用，回落内置工具。

研究完成。说明两点：任务要求的六机制对比，我以一手来源（官方 spec/docs/GitHub）逐一取证；无法核实的一律标「未验证」。

# 六机制行业对照（design grill 用）

## M1 异常边界真实咨询（bounded option set）

| 机制 | 通行做法（harness × 具体机制） | 我们的差异 | 初步分类建议 | 不确定/需深挖点 |
|---|---|---|---|---|
| 卡死/不可恢复态的处置 | **MCP elicitation**：定义了严格的三值动作集 `accept / decline / cancel`（cancel 即"未做明确选择地关掉"），由协议层封死可选动作空间，不靠模型自由发挥 — [spec 原文](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-11-25/client/elicitation.mdx) | 我们给出 {re-observe, go back, mark blocked+reason, scroll} 四值集合 | **通行做法一致（同构）**：MCP 的先例正是"host 定义有界动作集、语义判断交给使用方" | ACP v1 `session/request_permission` 也有必填 `options` 列表（见下），可再深挖其 option 枚举形态 |
| 工具失败后的阶梯 | **MCP tools**：失败分两级——协议错误（JSON-RPC error）与执行错误（`isError:true` 把错误文本交回模型），由模型自行决定下一步；仅 "SHOULD" 有人类确认环节，未定义失败动作菜单 — [spec](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-06-18/server/tools.mdx) | 我们不靠错误文本自由发挥，而是收窄为固定选项 | **有意取舍-需补叙事**：主流是"错误回灌、模型自由续"，我们的收缩是增强而非偏离，但需说明为何固定集合优于自由文本 | 各家 client 对 `isError` 后的自动重试策略未公开文档 |
| 人工升级 | **ACP `session/request_permission`**：agent 遇到需授权的点，把 `options`（必填的用户可选动作列表）+ 标题/描述交给 client，由人选择——这就是"结构化选项菜单交给人"的协议级先例 — [v2 RFD](https://github.com/agentclientprotocol/agent-client-protocol/blob/main/docs/rfds/v2/permission-requests.mdx) | 我们的选项集交给模型而非人 | **有意取舍-需补叙事**：ACP 的菜单是给人看的，我们是给模型+host 的；差异点在于"咨询对象"，需叙事支撑 | **Codex on-failure approval**（沙箱内自动、失败才升级人工，[approvals 文档](https://mintlify.wiki/openai/codex/concepts/approvals)）是标准三级阶梯（自愈→工具自查→人工/中止）的最好对照，可深挖其决策树 |
| 硬失败兜底 | **Anthropic 官方 agent 指南**：agent 在阻塞点 "pause for human feedback at checkpoints or when encountering blockers"，并普遍加最大迭代数等停止条件 — [Building effective agents](https://www.anthropic.com/engineering/building-effective-agents) | 我们用 mark blocked+reason 而非直接 pause | **通行做法一致** | Claude Code 是否有公开的"卡死检测+选项菜单"机制：**未验证**（官方文档未检索到对应页面） |

**小结**：有界动作集有直接协议先例（MCP elicitation 三值、ACP options），M1 属"同构移植+扩展"，建议归 (a)。

## M2 观察阶梯（summary → 结构化投影 → 受控截图）

| 机制 | 通行做法 | 我们的差异 | 初步分类建议 | 不确定/需深挖点 |
|---|---|---|---|---|
| 分层暴露环境状态 | **MCP 内容注解**：工具结果/资源可带 `audience: ["assistant"\|"user"]` 与 `priority` 元数据，明确区分"这段内容是给模型还是给人看的"——即内容按受众分级是协议内建概念 — [tools spec](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-06-18/server/tools.mdx)（另见 [resources annotations](https://modelcontextprotocol.io/specification/2025-06-18/server/resources)） | 我们的 judgment-level 标记 + 排除出 ledger | **通行做法一致（方向）**：受众分级有先例；但"排除出规范状态"部分见下一行 | MCP 注解只影响呈现，不定义"是否入 ledger"——我们的更强约束需叙事 |
| 截图只做判断、不进规范状态 | **Anthropic computer use**：每步动作后取截图作为观察反馈，官方并提示控制截图数量/成本 — [docs](https://platform.claude.com/docs/en/agents-and-tools/tool-use/computer-use-tool)（页面正文未在本次抓取中完整返回，具体频控数值**未验证**）；**OpenAI CUA** 有"每步至多一张截图"的频控设计（本次未能打开官方页面，**未验证**） | 截图仅视觉模型可用、且永不进 ledger | **有意取舍-需补叙事**：截图"用后即弃、不作为真值"在主流框架中未检索到明文先例，需自行论证（可用"截图是感知缓存而非状态"的叙事） | 逐字核对 Anthropic/OpenAI 官方频控措辞（本环境页面被截断/不可达） |
| 结构化只读投影 vs 原始视图 | **Playwright MCP**：默认给模型的是 accessibility snapshot（结构化 DOM 投影）而非截图，截图是显式升级的替代观察 — [snapshot 概念页](https://mintlify.wiki/microsoft/playwright-mcp/concepts/accessibility-snapshots)、[Playwright MCP](https://playwright.dev/mcp) | owner-derived local view 同为结构化只读投影 | **通行做法一致**：结构化投影优先、原始视图受控升级，与 Playwright MCP 同构 | browser-use / AppAgent 是否有同等的观察分级：未逐一核实 |

**小结**：M2 的"分层"是通行做法；"观察标为 judgment-level 并排除出规范状态"未见明文先例，是需补叙事的自有增强，建议归 (a)。

## M3 同步慢工具（bounded sync wait）

| 机制 | 通行做法 | 我们的差异 | 初步分类建议 | 不确定/需深挖点 |
|---|---|---|---|---|
| 长任务处理 | **MCP progress notifications**：请求带 `progressToken`，接收方可发多次 `notifications/progress`（0.2/1.0…），**响应本体仍异步返回**——即主流是"通知流+最终响应"，不是"阻塞一轮等待" — [spec](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-06-18/basic/utilities/progress.mdx) | 结果在 fetch bound 内则同步阻塞一轮，不丢弃不延后 | **有意取舍-需补叙事**：主流默认把慢工具当异步（错误返回/通知流/后台轮询），同步阻塞是不同选择 | FastMCP / 各 client 的默认 tool timeout 数值：未在本轮取证（**未验证**），需补 |
| 超时语义 | **MCP tools**：明确要求 "Clients … SHOULD implement timeouts for tool calls" — [spec](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-06-18/server/tools.mdx) | fetch bound 本质就是一个带上限的 timeout-and-collect | **通行做法一致**：有界等待与 timeout 要求同向 | OpenAI function-calling 是否纯同步往返（一请求一结果）：官方页本次未打开，**未验证**；Claude Code 的 `run_in_background`/BashOutput 后台任务模式也需补引 |

**小结**：M3 与"通知流+最终响应"主流形不同但兼容（可解释为 bound 内退化为同步、bound 外回落 MCP 模式），属可辩护取舍，建议 (a)。

## M4 popup dismiss-only 策略

| 机制 | 通行做法 | 我们的差异 | 初步分类建议 | 不确定/需深挖点 |
|---|---|---|---|---|
| 意外弹窗默认处置 | **Playwright**：官方明文 "By default, dialogs are auto-dismissed by Playwright"——框架层（host 层）自动 dismiss，不经任何模型判断；注册 handler 才可 accept — [docs](https://playwright.dev/docs/dialogs) | dismiss-only 白名单策略，host 执法，语义由 agent 判断 | **通行做法一致**：dismiss-only + host 强制与 Playwright 默认行为同构，且我们更细（允许 cancel/back，禁 confirm） | — |
| 弹窗处理原语 | **Selenium/WebDriver**：提供 `accept()`/`dismiss()` 双原语（alert 的 OK 与关闭等价）— [docs](https://www.selenium.dev/documentation/webdriver/interactions/alerts/)；W3C WebDriver 有 `unhandledPromptBehavior` 能力（默认值本轮未逐字核实，**未验证**） | 我们把"哪些按钮可按"语义判定留给 agent，动作空间由 host 收窄 | **有意取舍-需补叙事**：把 dismiss-only 下沉到 host 在 agent 框架中未见明文先例，但测试框架先例充分，叙事容易搭 | AppAgent / AutoGLM 类 GUI agent 是否有系统弹窗策略：本轮未取证，**未验证** |
| 危险动作下沉执法 | **Codex approvals**：对"逃出沙箱"的操作默认拦截、人批准 — [approvals](https://mintlify.wiki/openai/codex/concepts/approvals)；**Claude Code** 有 permission modes / Bash sandbox 分层执法（[permissions](https://code.claude.com/docs/en/permissions)、[sandboxing](https://code.claude.com/docs/en/sandboxing)） | "confirm 类按钮=危险动作"的执法线 | **通行做法一致（执法位置）**："host 层拦危险动作"是主流共识，我们只是把危险面从 shell 扩到 UI 语义 | — |

**小结**：M4 是六项中证据最强的一项：host 层默认 dismiss 是 Playwright 明文行为；唯一新东西是"confirm 禁按白名单"，属可控增强，建议 (a)。

## M5 no-progress 检测注入上下文

| 机制 | 通行做法 | 我们的差异 | 初步分类建议 | 不确定/需深挖点 |
|---|---|---|---|---|
| 循环/步数上限 | **LangGraph**：`recursion_limit` 硬上限（文档默认 25 步），超限抛 `GraphRecursionError` **直接中止**，不回灌模型 — [graph-api #recursion-limit](https://docs.langchain.com/oss/python/langgraph/graph-api#recursion-limit)、[errors](https://mintlify.wiki/langchain-ai/langgraph/api/errors) | 停滞/重复计数注入咨询上下文，而非只做中止阈值 | **有意取舍-需补叙事**：主流是"阈值→硬停"，我们是"信号→模型咨询"，形态不同 | Anthropic 指南确认 "stopping conditions (maximum number of iterations)" 是通行兜底 — [building effective agents](https://www.anthropic.com/engineering/building-effective-agents) |
| 停滞信号计算 | Claude Code 官方是否有 loop 检测及阈值：本轮仅找到社区/第三方讨论，无官方文档，**未验证**；Codex steering 的 stall 检测：**未验证** | frontier-growth 停滞 + 同路线重复计数，作为一等信号 | **有意取舍-需补叙事** | 需要人肉翻 Claude Code changelog 与 codex 源码确认是否内建；若确无，这正是可写进 ADR 的差异点 |
| agent 自评进度 | **Anthropic 指南**：agent 每步从环境拿 "ground truth"（工具结果/代码执行）评估进度 — [building effective agents](https://www.anthropic.com/engineering/building-effective-agents) | 我们把"无进展"从模型自评升级为 host 可测信号 | **通行做法一致（方向）**：环境真值反馈是共识；host 计算信号是增强 | — |

**小结**：M5 的"检测并中止"是通行（LangGraph 硬上限），"把信号注入咨询上下文而非只硬停"未见先例——这是六项中最接近"无据偏离"的一项，但偏离方向是"更温和"，建议 (a) 但必须补一节"为何注入优于硬停/混合策略"。

## M6 编排者/执法者角色切分

| 机制 | 通行做法 | 我们的差异 | 初步分类建议 | 不确定/需深挖点 |
|---|---|---|---|---|
| 编排形态 | **Anthropic 官方模式**：orchestrator-workers（编排 LLM 动态拆任务给 worker），并明确 agents=模型自主驱动、workflows=预定义代码路径，主张"简单、组合、ACI 打磨" — [building effective agents](https://www.anthropic.com/engineering/building-effective-agents) | 高维声明式 Agent + 低维机械 Kernel，两态切分 | **通行做法一致（上层）**：声明式编排=模型驱动，与官方 agents 模式同构 | AutoGPT 式 planner-executor 的失败复盘文献本轮未取证，**未验证**（可作为"为何不让 Kernel 做高维决策"的叙事素材） |
| 执法线位置 | **Codex**：OS 沙箱（read-only/workspace-write）+ approvals（on-request/on-failure）双线，host 机械执法、模型无感 — [approvals](https://mintlify.wiki/openai/codex/concepts/approvals)；**Claude Code**：permission modes + Bash sandbox + hooks 分层 — [permissions](https://code.claude.com/docs/en/permissions) | Kernel 在协议下做确定性执法，异常时刻把编排权交还 Agent | **通行做法一致（位置）**：host 层机械执法 + 模型自由度收窄是主流；"执法与编排两态可切换"是差异点 | MCP 工具注解"MUST NOT 仅凭注解做安全决策"的逐字原文（本轮抓到的版本是 "clients MUST consider tool annotations to be untrusted"，[tools spec](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2025-06-18/server/tools.mdx)）——注解只是提示、执法在 client/host，这条已可引 |
| MCP host 中介 | **MCP tools**：工具是 "model-controlled"，但 SHOULD 有人类确认环节；注解为不可信提示 — 同上 | 我们把"确认/禁做"从人下放到 Kernel 规则 | **有意取舍-需补叙事**：主流执法线=host 权限门控，我们=确定性 Kernel，需论证为何规则化优于逐次人批 | Claude Code hooks（PreToolUse deny）能否逐字引用 deny 语义：页面结构已确认存在（[hooks 文档区](https://code.claude.com/docs/en/permissions)导航含 hooks），具体决策字段**未验证** |

**小结**：M6 的"模型驱动 + host 机械执法"正是 Codex/Claude Code/MCP 的共识形态；独有的是"Kernel/Agent 双层、异常时编排权回交 Agent"——建议 (a)，叙事锚点是 Anthropic 的 workflows-vs-agents 区分（Kernel=确定性层，Agent=自由度层）。

## 总体结论

| 机制 | 分类建议 | 一句话 |
|---|---|---|
| M1 | 通行做法一致 / 有意取舍 | 有界动作集有 MCP elicitation、ACP options 直接先例 |
| M2 | 有意取舍-需补叙事 | 分层观察通行；"judgment-level 不入 ledger"无明文先例 |
| M3 | 有意取舍-需补叙事 | 与 MCP"通知流+响应"可统一解释为 bound 内退化同步 |
| M4 | 通行做法一致 | Playwright 默认 host 自动 dismiss 是明文先例，证据最强 |
| M5 | 有意取舍-需补叙事（最需加强） | LangGraph 等只做硬停，"信号注入咨询"无先例 |
| M6 | 通行做法一致 / 有意取舍 | 执法线位置与主流一致，"双态切分"是自有叙事点 |

未解决的不确定点（需下一轮深挖）：① Anthropic/OpenAI computer-use 截图频控逐字原文；② Claude Code 是否内建 loop 检测；③ FastMCP/各 client 默认 tool timeout 数值；④ W3C `unhandledPromptBehavior` 默认值；⑤ AutoGPT planner-executor 失败复盘文献。这些均标为**未验证**，未影响上表分类结论。
