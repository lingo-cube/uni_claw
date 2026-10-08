# DSH 默认 Memory 机制与按机型召回研究

> Status: RESEARCH_DELIVERED（调研产物，未授权实现）
> Authority: `NONE`（调研记录，不是架构决策；与 ADR / baseline 冲突时以后者为准）

研究范围：本地 `dk-harness` 源码与官方仓库文档（当前 checkout）。结论只覆盖 DSH 自己交付的机制；第三方记忆服务器的存储、检索算法和数据生命周期仍由各提供方负责。

## 结论先行

DSH 没有一个默认开启、由 Harness 自己拥有的 Memory 数据库。默认 profile 不挂载记忆服务器；`dsh-mcp-client` 的文档明确写明“默认不启用任何服务器”，仓库中的三份记忆 overlay 也是默认关闭的参考配置（`/Users/fran/Documents/Code/dk-harness/packages/mcp/mcp-client/README.zh.md:10-12`；`/Users/fran/Documents/Code/dk-harness/docs/user/guide/mcp-memory.zh.md:5-7,23-33`）。

启用 Memory 的实际路径是：配置一个 MCP server → DSH 启动/连接它 → 发现工具 → 将工具注册进当前 Agent scope → 每个后续请求携带工具 schema，模型按提示决定调用写入或检索工具。记忆内容不会自动注入每个请求；只有工具调用结果（或显式读取的资源）才会进入会话上下文（`README.zh.md:160-168,174-186,188-200`）。

因此，按机型提供知识的可行边界是“按请求选择记忆作用域并显式检索”，而不是把所有机型资料放进全局系统提示词。UniAgent 应先生成受控的 `device/model identity` 查询条件，再调用 Memory 的搜索/读取工具，把返回结果当作历史先验或操作提示；当前设备状态仍必须由实时 Perception/Evidence 重新确认。

## DSH 机制（有代码证据的部分）

### 数据模型与存储归属

DSH MCP bridge 只定义连接配置和工具投影，不定义 Memory record schema。每台服务器由唯一 `serverName` 形成命名空间；发现的工具公开为 `mcp__<serverName>__<rawName>`，并携带远端描述与输入 schema（`/Users/fran/Documents/Code/dk-harness/packages/mcp/mcp-client/README.zh.md:30-69,73-81,160-164`）。

三种仓库参考提供方的存储归属不同：MCP Reference Memory 保存本地知识图谱，示例默认 JSONL 路径为 `$HOME/.dsh-mcp-reference-memory.jsonl`；Memorix 默认使用 `~/.memorix/data`，Engram 默认使用 `~/.engram`，路径/项目选择由各自环境变量或项目配置控制（`/Users/fran/Documents/Code/dk-harness/docs/user/guide/mcp-memory.zh.md:35-64`）。这说明 DSH 不负责 memory record 的 schema、embedding、摘要、冲突解决或遗忘策略；Reference Memory 明确只做实体/关系/观察的本地知识图谱和不区分大小写的子串搜索，不做语义检索、embedding、自动摘要、冲突消解或遗忘（同文件 `:46-55`）。

### 检索和注入时机

连接成功后，MCP server 的工具在首轮开始前完成发现并加入模型工具列表；工具列表变化会自动重新同步（`README.zh.md:89-93`）。工具描述和输入 schema 会进入每次请求，但记忆正文不会自动进入系统提示词（`README.zh.md:164-172`）。

服务器返回的非空 instructions 会由 `server-context.ts` 注册为一个带服务器名的 system-prompt section，作为字面文本加入后续组装；MCP prompt templates 不支持（`/Users/fran/Documents/Code/dk-harness/packages/mcp/mcp-client/src/server-context.ts:28-38`；`README.zh.md:10-12,188-200`）。这是一种“服务器使用说明”注入，不是 Memory 记录自动召回。

真正的召回发生在模型调用 MCP 工具后：结果按 MCP block 顺序投影为普通文本/结构化内容，公开工具名和 JSON 参数留在 assistant history；资源文档只有显式资源读取才进入历史（`README.zh.md:83-87,174-196`）。官方记忆指南给出的跨会话验证也要求在会话 A 调用写入工具、在新的会话 B 明确提出“Check memory”并确认模型调用搜索/召回工具；无需重启 Host（`/Users/fran/Documents/Code/dk-harness/docs/user/guide/mcp-memory.zh.md:74-82`）。

### 配置与生命周期

记忆服务器通过 Cordis overlay 的单个 `@deepseek-ai/dsh-mcp-client` 条目接入，至少要声明 `serverName`、`transport` 和 stdio command 或 HTTP URL（`README.zh.md:25-60`）。三份参考 overlay 的源码只包含该桥接配置，并明确要求先安装上游可执行文件；DSH 不运行包管理器（`/Users/fran/Documents/Code/dk-harness/apps/cli/config/examples/mcp-memory/mcp-reference-memory.cordis.yml:1-13`）。

stdio 子进程启动前会清洗通常表示凭据的环境变量以及所有 `DSH_*` 变量，再合并显式 `config.env`；Memory 文件路径等提供方参数必须显式配置（`/Users/fran/Documents/Code/dk-harness/docs/user/guide/mcp-memory.zh.md:9-13`）。连接断开时，桥接器按配置自动重连；连续失败达到预算后注销工具，直到重载或重启（`README.zh.md:61-67,89-93`）。

## 面向 UniAgent 的按机型知识方案

推荐把“设备/机型知识”做成独立的、可查询的 Memory provider 或 MCP server，而不是修改 DSH 默认 system prompt：

1. **记录键**：每条记录至少带 `deviceKey`（稳定设备身份）、`modelKey`（厂商/型号/变体）、`osApi`、`source`、`observedAt`、`validUntil`、`confidence` 和 `kind`（导航先验、已知限制、历史故障、操作注意事项）。稳定身份与临时运行状态分开存储。
2. **隔离策略**：将 `deviceKey`/`modelKey` 作为所有写入和搜索的必需过滤条件；对高风险场景默认 exact-match，禁止仅凭相似文本跨机型回流。可按机型使用不同 `MEMORY_FILE_PATH`/`ENGRAM_PROJECT` 或不同 MCP `serverName`，但这只是存储隔离；真正的授权仍需在 UniAgent 查询层执行。
3. **按需召回**：UniAgent 在开始规划或遇到机型相关不确定性时，调用 `search(modelKey, deviceKey, kind, query, limit)`；将返回的 provenance 和时间戳带入工作上下文。不要在每轮把整个知识库放入 prompt，以免 token 膨胀和 KV 前缀变化。
4. **写入门槛**：只把已完成、可复核的历史经验写入 Memory；实时观察、当前页面和坐标仍走 Evidence/World Model。Memory recall 只能影响 hypothesis/observation strategy，不能单独证明当前世界状态。
5. **失效与冲突**：记录必须可 supersede/expire；检索结果过期、缺少设备过滤或 provenance 不完整时返回“不可用”，促使 UniAgent 重新感知，而不是静默采用旧知识。

这套方案与 DSH 的实际边界一致：DSH 负责 MCP 连接、工具发现、工具调用和结果投影；UniAgent/Memory provider 负责记录 schema、设备过滤、权限、保鲜度和冲突策略。若希望模型稳定触发检索，可在既有模型指令中加入仓库指南给出的通用规则：“用户要求记住某事时调用记忆写入工具；历史信息可能相关时，检索记忆并使用相关结果”（`mcp-memory.zh.md:66-72`），但这只是行为提示，不能替代查询层的强制过滤。

## 与 UniClaw 现有基线的对齐

UniClaw 的产品架构已经把 Memory System 的 owner 限定为 durable records、provenance、retention metadata 和 recall result production；Memory 不拥有 Evidence admission、Current WorldBelief、Run State、Control Intent、Effect Verification 或 Outcome Proof（`docs/architecture/product-architecture-baseline-l0-l3.md:351-385`）。P20 Persist 与 P21 Recall 仍是 placeholder，recall 明确只能作为 prior/context/hypothesis/historical reference，不能直接建立 current-world claim（`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md:617-640`）。

因此当前不应把某个 DSH 工具名、MCP provider 或数据库格式写进 UniAgent 核心。真实 buyer 到来后，落地一个稳定的 Memory consumption seam：UniAgent 产生带 `DeviceProfile` 和 freshness 要求的查询；Memory realization 可以用 DSH MCP adapter，也可以是原生实现；返回带 provenance、适用范围和失效信息的 recall。替换存储、索引或 DSH provider 时，UniAgent 的消费缝和上述 non-evidentiary 约束保持不变。现有 `docs/design/memory-system-component-declaration-v0.1.md` 已将实现门槛定为“出现真实记忆 buyer 后再独立 change 落地”。

## 当前证据边界

- 已证实：DSH 默认不启用 Memory server；MCP bridge 的工具发现、命名、system-prompt instructions、工具调用结果和重连行为。
- 已证实：仓库提供三份可选第三方 Memory overlay，各自的默认存储位置和 Reference Memory 的字符串搜索语义。
- 未在 DSH 源码中发现：内建 Memory record API、统一 embedding/index、自动按设备召回、写入审批或跨 provider 的冲突/遗忘策略。上述能力应由 UniAgent 侧协议和选定 provider 明确定义，不能从 DSH 的 MCP bridge 推断出来。

## 第三方方案选型补充（2026-10-07）

- **MCP Reference Memory**：官方 MCP servers 中的基础本地知识图谱，适合验证 DSH 的 MCP 接入和跨会话 round-trip；官方 README 将其定位为 basic implementation，数据是本地 JSONL，搜索/读取模型简单，不适合作为产品级检索与治理底座。[官方 README](https://github.com/modelcontextprotocol/servers/tree/main/src/memory)
- **Mem0 / OpenMemory**：更适合作为现成的通用 Memory provider。官方 MCP 文档提供语义搜索、结构化过滤、分页、CRUD、实体枚举和事件查询工具；但托管 MCP 需要认证，产品部署还要明确租户、数据驻留、费用和 provider 依赖。[官方 MCP 文档](https://github.com/mem0ai/mem0/blob/main/docs/platform/mem0-mcp.mdx)
- **Graphiti / Zep**：适合存在时间变化、关系和来源追踪的设备知识。Graphiti 提供 episodes、实体/关系、事实有效期、来源链和混合检索；Zep 是其托管的生产平台。Graphiti 自托管需要图数据库、LLM/embedding 和运维，复杂度明显高于第一阶段需要。[Graphiti](https://github.com/getzep/graphiti) · [Zep 图模型](https://help.getzep.com/v2/understanding-the-graph)

选型结论：短期接 DSH 做验证可用 MCP Reference Memory；要快速获得可用的通用长期记忆，可评估 Mem0；UniClaw 的产品级核心不应直接依赖其中任何一个。推荐自有 Memory service：关系型 canonical records + append-only provenance/event log + PostgreSQL 全文/向量混合索引，后续确有复杂时间关系时再增加 Graphiti 式图投影。DSH 只通过一个受控 MCP adapter 暴露 `memory.recall`/`memory.propose`，设备身份和过滤条件由 UniClaw 注入并在服务端强制执行。
