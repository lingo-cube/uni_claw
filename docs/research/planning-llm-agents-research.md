# LLM Agent Planning 方法研究

状态：Research / Authority: NONE  
范围：根据知乎文章《Agent技术解读：Planning（规划）模块》及其引用的一手论文，提炼 LLM Agent 如何做 Planning；本文是研究输入，不冻结 UniClaw 产品接口，也不把论文结果直接当作产品验收证据。

## 1. 研究对象与证据边界

知乎原文将 Planning 概括为两件事：把复杂任务拆成子任务，以及根据动作结果反思并改进后续动作；并区分无反馈规划（一次推理、多路候选或外部规划器）与有反馈规划（环境、人类或模型提供反馈）。原文随后引用 Huang 等人的综述，将方法归纳为任务分解、多计划选择、外部模块辅助、反思与细化、记忆增强五类。[知乎原文](https://zhuanlan.zhihu.com/p/718959013) · [Understanding the planning of LLM agents: A survey](https://arxiv.org/abs/2402.02716)

论文原文支持这五类分类，但不支持把所有任务都交给 LLM 自由生成动作：综述明确把规划看成从目标、状态和动作约束出发的决策问题，并指出现有方法在可靠性、长时域、反馈利用和评估方面仍有挑战。因此下文把“Planning”理解为一个可验证的闭环：目标/约束建模 → 生成候选计划 → 选择或搜索 → 执行 → 获取反馈 → 修正/记忆 → 评估。

## 2. 五类方法及其适用条件

| 方法 | 做法 | 适合场景 | 主要代价/边界 | 代表一手来源 |
|---|---|---|---|---|
| 任务分解 | 先把目标拆成子目标，再为子目标生成步骤；或在执行中交错揭示子目标与计划 | 多步、层级清晰、子任务依赖较稳定的问题 | 分解错误会级联；交错轨迹过长会遗忘目标并增加幻觉；调用和上下文成本增加 | [Plan-and-Solve](https://arxiv.org/abs/2305.04091)、[HuggingGPT](https://arxiv.org/abs/2303.17580)、[CoT](https://arxiv.org/abs/2201.11903)、[ReAct](https://arxiv.org/abs/2210.03629) |
| 多计划选择 | 采样/提议多个候选计划，再用投票、评分或树搜索挑选 | 存在多条可行路径、单次生成不稳定、允许增加推理预算 | 候选生成和评估成倍增加；LLM 作为评估器可能偏置；随机采样影响一致性 | [Self-Consistency](https://arxiv.org/abs/2203.11171)、[Tree of Thoughts](https://arxiv.org/abs/2305.10601)、[Graph of Thoughts](https://arxiv.org/abs/2308.09687) |
| 外部模块辅助 | LLM 负责自然语言理解/形式化，符号或神经规划器负责约束搜索、验证或动作排序 | 规则、前置条件、资源约束和最优性要求明确；长时域或安全敏感任务 | 需要维护领域模型、状态接口和验证器；自然语言到形式模型可能出错；神经规划器受训练分布限制 | [LLM+P](https://arxiv.org/abs/2304.11477)、[LLM-DM](https://arxiv.org/abs/2305.14909)、[Generalized Planning in PDDL](https://arxiv.org/abs/2305.11014) |
| 反思与细化 | 生成初稿，取得自评/环境/工具反馈，批评并重写，直到通过停止条件 | 可试错、反馈及时、错误可观测的任务 | 自评不能替代外部事实验证；可能反复循环；文本反馈未必真正改变目标达成率 | [Self-Refine](https://arxiv.org/abs/2303.17651)、[Reflexion](https://arxiv.org/abs/2303.11366)、[CRITIC](https://arxiv.org/abs/2305.11738) |
| 记忆增强 | 将成功/失败经验以可检索文本或参数形式保存，在新计划前检索或调用 | 重复任务、长期交互、经验可迁移的场景 | RAG 依赖检索相关性和新鲜度；微调更新成本高且可能遗忘；错误经验会污染后续计划 | [Generative Agents](https://arxiv.org/abs/2304.03442)、[MemGPT](https://arxiv.org/abs/2310.08560)、[MemoryBank](https://arxiv.org/abs/2305.10250) |

这些类别可以组合：例如先用任务分解形成候选，再用 ToT 搜索选择，执行中用 ReAct 接收环境反馈，失败后用 Reflexion 写入短期记忆，跨任务再用 RAG 检索经验。综述的分类是分析维度，不是互斥的流水线。[综述原文](https://arxiv.org/abs/2402.02716)

## 3. 关键论文给出的可复用机制

### 3.1 先计划、再执行：降低漏步骤

Plan-and-Solve 将零样本 CoT 拆为“制定计划”和“按计划执行”两个阶段，针对 Zero-shot-CoT 常见的漏步骤、计算错误和语义误解提出 PS/PS+ 提示。可复用的最小结构是：先列出可检查的子任务及依赖，再逐项求解并汇总；不要把一段不可验证的长思维链直接当作动作序列。[Plan-and-Solve](https://arxiv.org/abs/2305.04091)

CoT 的论文证明，在足够大的模型上，显式生成中间推理步骤能改善多步算术、常识和符号推理；这说明“分解”是提高测试时计算的手段，但并不等于计划已经可执行或事实正确。[Chain-of-Thought](https://arxiv.org/abs/2201.11903)

### 3.2 从单一路径到搜索：处理不确定性

Self-Consistency 用温度采样产生多条推理路径，再按答案一致性聚合，而不是采用贪心单路径；它适用于答案可比较、可投票的任务，不适合每条路径都有副作用的真实动作执行。[Self-Consistency](https://arxiv.org/abs/2203.11171)

ToT 把若干“思维单元”作为树节点，用生成、评估、BFS/DFS 等搜索来前瞻、回溯和剪枝；GoT 将结构推广为任意图，允许聚合和反馈环。它们的共同要求是：必须定义候选状态、评估标准和预算，否则只是重复调用 LLM。[ToT](https://arxiv.org/abs/2305.10601) · [GoT](https://arxiv.org/abs/2308.09687)

### 3.3 让形式化规划器负责硬约束

LLM+P 的三段式管线是“自然语言 → PDDL → 经典规划器 → 自然语言计划”。论文报告：在长时域、需要可行性/最优性的基准上，LLM 直接规划往往不可靠，而经典规划器在获得正确形式化输入后能稳定搜索；因此 LLM 更适合做语义解析和结果解释，不能独自充当约束验证器。[LLM+P](https://arxiv.org/abs/2304.11477)

LLM-DM 进一步把 LLM 用于构建显式世界模型，并通过 PDDL 验证器或人类反馈修正模型，再交给可靠的领域无关规划器。由此得到的工程原则是：把“模型生成的假设”和“规划器确认的状态转移”分开存储；形式化模型变更要可验证、可回滚。[LLM-DM](https://arxiv.org/abs/2305.14909)

### 3.4 反馈闭环：反思必须有可观测依据

ReAct 将推理轨迹和动作交错：推理帮助形成、跟踪和更新计划，动作从外部知识库或环境取得新信息；它适用于状态会变化、需要工具观察的任务。[ReAct](https://arxiv.org/abs/2210.03629)

Self-Refine 使用同一 LLM 依次完成生成、反馈、细化，不需要额外训练；Reflexion 把环境得分或失败转成文字经验，放入 episodic memory 供下一轮使用；CRITIC 则要求调用搜索、知识库或解释器等外部工具验证输出后再修正。共同的停止条件应是“外部验证通过/目标状态满足/预算耗尽”，而不是模型自己说“看起来正确”。[Self-Refine](https://arxiv.org/abs/2303.17651) · [Reflexion](https://arxiv.org/abs/2303.11366) · [CRITIC](https://arxiv.org/abs/2305.11738)

### 3.5 记忆：保存可迁移经验，而非完整噪声轨迹

Generative Agents 将经历保存为自然语言，周期性合成高层反思，并按当前情境检索来帮助计划；这提示记忆至少应有“原始经历、抽象经验、检索条件”三层。RAG 记忆易更新但依赖检索质量；参数记忆容量大却需要训练和发布流程，不能当作即时事实库。[Generative Agents](https://arxiv.org/abs/2304.03442) · [MemoryBank](https://arxiv.org/abs/2305.10250)

## 4. 一套可落地的 Planning 流程

1. **定义目标和边界**：把用户目标改写成可判定的目标状态，列出不可违反的约束、可用工具、资源/时间预算和停止条件。若目标或状态不清楚，先澄清或安全停下。
2. **选择规划模式**：简单、确定、低风险任务用单路分解；有多解或模型不稳定时使用候选生成+选择；有硬约束时引入 PDDL/规则/搜索器；可观察且可试错时使用 ReAct+反馈。
3. **生成结构化计划**：每个步骤至少包含 `id`、前置条件、动作、预期结果、失败处理、验证方式和依赖。计划应能逐步执行和回滚，避免只输出自然语言长段落。
4. **检查计划**：先做静态检查（目标覆盖、依赖闭合、权限/资源、参数格式），再做外部模拟或规划器验证；对高风险动作要求人工确认或更强验证。
5. **小步执行并观察**：执行一个动作或一个短批次，记录实际状态与预期差异；不要在反馈缺失时假定动作成功。
6. **反馈驱动重规划**：出现失败、环境变化或新信息时，定位受影响的步骤，局部重规划；只有当全局假设失效时才重建整棵计划树。
7. **反思和记忆**：把失败原因、有效修复、适用条件和证据写成短经验；通过相关性、时效性和可信度检索，避免把未经验证的模型自评写成事实。
8. **评估与收敛**：至少报告任务成功率/目标达成、计划可行率、动作失败率、重规划次数、token/时延成本和人工介入率，并按交互环境、文本环境、检索环境、编程环境分别测试。[综述](https://arxiv.org/abs/2402.02716)

可将执行控制流写成：

```text
目标/约束 → 状态建模 → 选择规划器 → 候选计划 → 验证/排序
     ↑                                  ↓
记忆 ← 反思/归因 ← 反馈/观察 ← 执行动作
```

## 5. 适用边界与对 UniClaw 的研究启发

- CoT、ToT、GoT 主要改变推理时的搜索组织，不能自动提供真实世界状态、权限正确性或动作安全性。
- 反思若没有环境、人类或工具反馈，容易成为“模型评价模型”；CRITIC 的结果支持把外部验证放进闭环，而不是把语言自评当证据。
- PDDL/符号规划器能提供可行性和约束搜索优势，但前提是自然语言到形式模型的转换正确；模型、验证器和执行环境必须分开记录。
- 多计划和树搜索以更多调用、token 和延迟换取探索；应按任务风险和资源预算启用，并设置最大深度/分支/回合数。
- 记忆是辅助输入，不是当前世界事实的替代品。过期、错误或来源不明的经验应降权、隔离或淘汰。
- 论文基准多为游戏、文本环境、检索或编程任务；其成功率不能直接证明移动设备、真实 UI 或高风险操作的安全性。真实系统仍需独立的状态观测、权限检查、动作边界和人工门控。

对 UniClaw 的直接设计输入是：Planning 只提出带前置条件和预期结果的候选动作；Runtime/World/Capability 负责提供当前状态和可用能力；Effect 层负责授权与执行；Observation/Verification 负责确认结果；Memory 只提供可追溯的历史经验。任何“计划生成成功”都不能直接升级为“动作已执行”或“目标已达成”。

## 6. 论文与官方来源索引

- 综述：[Understanding the planning of LLM agents: A survey](https://arxiv.org/abs/2402.02716)
- 分解与推理：[Chain-of-Thought](https://arxiv.org/abs/2201.11903)、[Plan-and-Solve](https://arxiv.org/abs/2305.04091)、[HuggingGPT](https://arxiv.org/abs/2303.17580)、[ReAct](https://arxiv.org/abs/2210.03629)
- 多计划搜索：[Self-Consistency](https://arxiv.org/abs/2203.11171)、[Tree of Thoughts](https://arxiv.org/abs/2305.10601)、[Graph of Thoughts](https://arxiv.org/abs/2308.09687)
- 外部规划器：[LLM+P](https://arxiv.org/abs/2304.11477)、[LLM-DP](https://arxiv.org/abs/2308.06391)、[LLM-DM](https://arxiv.org/abs/2305.14909)、[Generalized Planning in PDDL](https://arxiv.org/abs/2305.11014)、[RAP](https://arxiv.org/abs/2305.14992)
- 反思与修正：[Self-Refine](https://arxiv.org/abs/2303.17651)、[Reflexion](https://arxiv.org/abs/2303.11366)、[CRITIC](https://arxiv.org/abs/2305.11738)
- 记忆增强：[Generative Agents](https://arxiv.org/abs/2304.03442)、[MemoryBank](https://arxiv.org/abs/2305.10250)、[MemGPT](https://arxiv.org/abs/2310.08560)

知乎文章还点名但未在本文展开的工作：

- [Program-aided Language Models (PAL)](https://arxiv.org/abs/2211.10435)、[Program of Thoughts (PoT)](https://arxiv.org/abs/2211.12588)
- [Reasoning with Language Model is Planning with World Model (RAP)](https://arxiv.org/abs/2305.14992)、[LLM-MCTS](https://arxiv.org/abs/2305.14078)
- [LLM-DP](https://arxiv.org/abs/2308.06391)、[LLM+ASP](https://arxiv.org/abs/2307.07696)、[SwiftSage](https://arxiv.org/abs/2305.17390)
- [Deep Reinforcement Relevance Network (DRRN)](https://arxiv.org/abs/1511.04636)、[Decision Transformer](https://arxiv.org/abs/2106.01345)、[LEMA](https://arxiv.org/abs/2310.20689)
- [TiM](https://arxiv.org/abs/2311.08719)、[RecMind](https://arxiv.org/abs/2308.14296)、[REMEMBER](https://arxiv.org/abs/2306.07929)、[AgentTuning](https://arxiv.org/abs/2310.12823)
