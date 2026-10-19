# 计划树与遍历机制：架构原则评审及持久化结论

日期：2026-10-19。评审对象：[D1-D19](state.md)、[流程契约](flow-contract.md)、[第12轮职责澄清](grill-round-12.md)。范围：逻辑设计与Owner职责；不是实现评审、接口冻结或现场适配证明。

## 1. 结论

**设计满足当前架构原则，可以作为后续任务的持久设计基线。** 两处容易歧义的Owner表述已在本次评审中收紧：

1. Traversal Control和Recovery / Reorientation都归既有Control Loop；Kernel internal run driver只有执行编排/生命周期职责，不新增planner、独立Traversal Owner或Recovery系统。
2. handler提供能力协议定义的领域结果；Kernel协调调用和进度记录，结果进入Run仍经既有Owner写入缝。handler不直接改写Run、World或Control，不因返回流程信号就成为控制权威。

上述是对accepted baseline的对齐，不推翻D1-D19，不增加业务分支。当前无阻碍设计持久化的架构冲突。Owner已明确授权“满足则持久化，后续等我开启任务”，因此保存后停在PERSIST，不进入实现。

## 2. 原则逐项核对

| 原则 | 当前设计及核对结果 | 结论/后续验证边界 |
|---|---|---|
| 架构优先 | 保留六Owner，Agent规划、Control签发意图、Assurance裁决、Effect投递；Recovery重入原闭环 | 符合baseline §8、§14、§19、§24.2；未为实现便利转移权威 |
| 单一职责 | PlanTree表达意图；Traversal Control负责结构探索；能力负责自身专业判断；Recovery负责恢复意图；driver只协调 | 设计满足；语言/语音规则变化不应要求修改driver，恢复算法变化不应要求修改遍历规则解释器 |
| 职责清晰 | 区分调用完成、领域不符合、流程是否继续、Recovery如何处理；原动作独立验证另归Assurance | 已明确，不能把handler不符合一律当执行异常或安全裁决 |
| 高内聚 | 发现、展开、去重、深度和返回内聚于Traversal职责；领域输入/规则/结果有效性内聚于该能力协议；恢复内聚于Recovery | K1/K2是遍历内部算法义务，K4/K5分别属于能力协议和Recovery交接，避免集中到Kernel |
| 低耦合/依赖抽象 | 调用使用已声明typed能力协议、引用和显式Owner缝；driver不读具体语言算法，不跨信任域直接Resolve | 设计满足；具体typed适配/跨域桥接按后续真实买方定义，不开放反射或万能Execute |
| 权威与状态唯一 | World拥有belief/身份，遍历图为可重建引用视图；Run拥有进度真值；Control只保留所需控制状态 | 不建第二World/Run/God Context；记录可派生但不并立canonical store |
| 深模块与小接口 | 调用方提交计划/任务关联，运行中只读查询，结束后取报告；栈、待办、回路等由内部算法处理 | 对调用方隐藏重复控制复杂度，接口具有价值；具体异步/wire形状不在本轮冻结 |
| 可替换与可扩展 | 同能力协议替换realization不改核心控制语义；新检查通过其能力协议/绑定接入，计划引用并排序 | 设计支持，尚无新机制替换测试，不能标为实现已证明；新领域/新协议仍须真实买方验证 |
| 安全与最小权限 | 计划/handler不授额、不授权；每个Effect走原接地/Assurance/Effect链；安全Unknown不能靠规则兜底放行 | 不存在因挂handler而获准点击的设计路径；跨域Registry隔离保持 |
| 验收可判定 | 区分范围发现、操作执行、handler结果、限定覆盖与Goal满足；记录成果/缺口/依据 | 分层标准明确；后续实现逐项method/expected/actual/evidence，不用文件存在或旧passing替代 |
| 不预造能力 | 结果校验Capability仅为后续方向；没有新增Handler Registry、通用BT平台、Recovery执行器或万能hook | 符合买方优先与范围约束；尚未冻结的新能力不冒充已经存在 |

## 3. 最终职责分配

| 角色/Owner | 负责 | 不拥有 |
|---|---|---|
| UniAgent | 任务理解、计划提出/修订、Goal Evaluation | World事实、Effect授权/投递、Run直接写入 |
| Control Loop / Traversal Control | 按已采纳计划与事实推进探索、选择方向、覆盖控制及控制权返回；Control签发意图 | WorldBelief、canonical Run进度、Effect Verification/投递 |
| Control Loop / Recovery | 依据divergence/执行事实形成有界恢复或重定向意图，重入原闭环 | 盲重发、清空消耗、回滚已发生事实、绕过正常验证 |
| Kernel internal run driver | 协调调用时机/顺序/等待/生命周期；通过Owner缝提交执行记录并暴露查询/报告 | 领域检查算法、规划/恢复智能、canonical Owner直写替代 |
| handler对应Capability | 定义并执行本专业检查，给出typed结果、覆盖/完成/有效性含义和限制 | Control Intent、World/Run修改权、Assurance裁决、Goal完成权 |
| World Model | 接纳证据后的belief、结构关系、身份与当前有效性；提供视图来源 | 本次任务visited/handler执行进度真值 |
| Run Model | 合同/进度/执行与终局状态的canonical记录 | World事实、Control意图、Assurance裁决 |
| Evidence / Assurance / Effect | 各自拥有接纳、判断/独立验证、绑定/门禁/投递权威 | 不合并到handlers或driver |

“Kernel执行handler”指编排和通过显式typed缝调用，不意味着所有能力必须搬入Kernel同一信任域，也不意味着driver成为领域能力的实现者。

## 4. 仍需后续任务完成的内容

K1-K8保留为实现/适配证明义务：发现穷尽、去重与深度、操作事件、typed能力适配、Recovery事实交接、预算/超时、任务组合和结果证明。它们不构成当前职责设计的冲突，也不是本轮必须提前实现的框架。

后续用户开启任务后：先读state与本评审，按届时任务范围设计最小typed缝和可判定验收；首条菜单链验证共享子页、未知、不符合仅记录、必需条件暂停、Recovery对账及部分覆盖报告。是否新增结果校验Capability届时由真实买方触发。

## 5. 持久化与停止边界

- D1-D19及Owner归属作为已接受的设计基线保存；流程契约是当前集中阅读入口，历史round/protocol演变稿保留。
- Change处于PERSIST（state中采用canonical token persisted），索引同步。没有产品实现任务在运行，也不将设计完成标成产品CLOSED。
- 等待Owner主动开启后续任务；不自动进入PLAN/IMPLEMENT，不创建新任务、不启动后台监控/自动续行，不提交或推送。
- 这次评审/文档验证见[verification.json](../../evidence/runtime-plan-002/architecture-review-20261019/verification.json)。旧场景/参考模型只保留其原基线结论。

## 6. 依据

- [AGENTS.md](../../AGENTS.md) §1.5：架构优先、单一职责、低耦合、验收明确、买方驱动的可扩展缝。
- [产品架构基线](../../docs/architecture/product-architecture-baseline-l0-l3.md) §8、§14、§19、§24.2/24.3：Capability边界、Traversal/Recovery归Control、六Owner、driver只编排、串行验证。
- [ADR-0053](../../docs/adr/0053-composable-product-plan-with-bounded-local-execution.md)：advisory计划和有界展开不新增控制/执行权威。
- [codebase-design](../../.agents/skills/codebase-design/SKILL.md)：深模块、小接口、显式依赖和可替换缝。
- [capability-component](../../.agents/skills/capability-component/SKILL.md)：每能力typed协议、信任域绑定、职责单一、禁止万能hook、买方验证。
