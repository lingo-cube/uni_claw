# RUNTIME-PLAN-002：实施会话交接

日期：2026-10-19。Owner本次明确要求开启新会话运行本Change，并要求仿真及协议校验通过。此前“只做文档/等待开启任务”是上一阶段限制，本次实施授权已解除该等待；不重新询问D1-D19或总体机制批准。

## 1. 交付目标及范围

把已持久化的计划树/World派生遍历图/持续Traversal/操作级有序handlers落成可运行的菜单遍历链，补齐所需正式协议、输入/结果/状态查询与Owner交接缝，并通过真实执行链仿真及协议校验。按UniFlow复验入口、明确Acceptance、计划、实施、评审、验证，范围满足后关闭本Change；只有真正需要Owner裁决的新语义分歧才暂停提问。

本轮实现范围是已确认机制及菜单遍历真实买方：动态发现、规则首项匹配、进入/返回、深度/去重、只读处理、每个操作自己的前后handlers、结果记录/显式继续条件、暂停与查询/最终报告、与Recovery的事实交接。复用既有能力/Owner/Plan与Policy语义，不造万能框架。

不扩大为地图/机械臂适配、所有未来UI的完备证明、跨进程恢复、独立Recovery系统、额外结果校验Capability、泛型Execute或第二Handler Registry。结果校验Capability仍是后续方向。不得重标RUNTIME-PLAN-001原验收或失败门槛；修改其共用实现时保留历史和兼容依据。

## 2. 按需阅读顺序

1. [AGENTS.md](../../AGENTS.md)、[UniFlow](../../.agents/skills/uniflow/SKILL.md)、[state.md](state.md)、[流程契约](flow-contract.md)、[架构评审](architecture-review.md)。D1-D19及最新Owner纠正为当前基线。
2. [第12轮](grill-round-12.md)：领域检查归能力、恢复归Control Loop中的Recovery；不符合可仅记录，不默认暂停。Kernel只协调typed调用并经原Owner记录状态/结果。
3. [推导总评及K1-K8](derivation-review.md)、[数学推导](mathematical-derivation.md)、[哲学推导](philosophical-derivation.md)、[场景矩阵](../../evidence/runtime-plan-002/derivation-20261019/scenario-matrix.md)。旧矩阵是D1-D16的历史分析，需按D17-D19解释K4/K5；不是产品PASS。
4. [协议演变草案](protocol-sketch.md)只作需求参考，不能直接当已支持wire。原when/do、traverse、record、positionRef等均须与实际codec对齐，具体字段未冻结。
5. [架构基线](../../docs/architecture/product-architecture-baseline-l0-l3.md) §8/14/19/24.2/24.3、[ADR-0053](../../docs/adr/0053-composable-product-plan-with-bounded-local-execution.md)、[协议基线](../../docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md)、CONTEXT按需核对。
6. 实施/评审按需加载codebase-design、capability-component、tdd、code-review。新能力须过能力买方/细节门；已冻结语言检查规则不重新grill。

## 3. 必须保持的职责

- UniAgent提出/修订计划；Control Loop独占intent，Traversal Control与Recovery均是其既有子职责。
- Kernel internal run driver只编排执行/生命周期，不实现领域检查、规划或Recovery智能。
- World拥有事实/身份，遍历图为其引用视图；Run拥有进度真值。不得直接写Owner存储或建第二真值源。
- 具体操作绑定自己的有序handlers，分操作前/完成并独立验证后；只读操作可挂handler，不为检查制造点击。
- 每个能力自己的typed协议定义输入、覆盖/完成/通过/有效性；Kernel不统一重判领域语义。诊断不符合可记录继续；只有明确必需继续条件不满足才按声明暂停。
- 安全、接地、独立动作验证及Effect投递走原链；Finding不授权，post-commit不等于AfterVerified，Registry不是任意操作分发器，跨信任域须显式typed缝。
- Recovery消费执行事实形成有界恢复意图，重新进入原闭环；不盲重放已发生动作，不重置预算，不把本轮扩大成跨进程恢复。
- 执行中查询只读；受阻需决策暂停同一次执行，最终结果只在结束时返回。限定遍历结束不等于Goal完成。

## 4. 仿真验收：必须实际运行新增路径

使用仓库现有tests/UniClaw.Simulation.Tests的SimulationHost、ScenarioRunner/GoldenScenarioBundles等实际缝，补新机制输入并跑真实Kernel/Control/World/Assurance/Effect/Run链；仿真感知、机械Effect/Agent double等须如实标注。只有helper单测、手写相同算法、6,244个图组合检查或文档链接通过，不满足此验收。

至少证明以下可判定行为：

1. 同一有界计划动态发现未预列子页，按安全规则进入/返回、处理兄弟节点，显式结束并交代覆盖范围。
2. switch只读记录/检查零点击；不同act绑定不同handlers，前后位置及顺序正确，至少复用一个实际已注册能力或其同协议仿真实现走完整绑定路径。
3. 规则多匹配只选首项；影响选择的Unknown不当false；禁止/越权动作零投递。
4. 检查不符合但仅记录时继续；明确必需条件不满足才暂停；调用异常/不可用不能假报调用完成。能力自己的结果语义与执行状态分开。
5. 缺后验、矛盾后验不提前跑成功后置/下个Effect；暂停查询不推进，明确续行/修订保留预算与已发生动作，Recovery交接可观察且不盲重发。
6. 环/共享子页/同页不同处理义务正确；root→A→C和root→C的深度反例不漏允许范围内D；不凭全局visited吞掉较浅路径的展开机会。
7. 发现未穷尽、深度截断、禁入、目标消失或返回失败如实保留缺口；已知待办为空不能单独宣布Goal满足。
8. 预算、取消、超时/能力失效的边界可控，查询和handler不激活第二Run、复活终态或绕开Owner链。

28条库项保留原场景与期望；新增机制需要自身可执行场景及负向分支。execution.kind=none不能计仿真通过。若改Kernel源，按能力纪律与tools/scenario_certify.py重新认证受影响场景；认证/资产更新不能替代执行结果。仿真不冒充真机或真实模型稳定性证明，本次不新增物理设备验收门槛。

## 5. 协议验收：编解码和消费闭包一致

先定义可判定协议契约，再实现：计划/操作handler绑定、声明顺序/时机、输入与typed结果关联、状态查询/续行/最终报告均需真实producer→codec→consumer路径。未知kind、非法绑定、参数缺失/错型、越界/越权及不兼容版本等必须有负向校验，不能只验证JSON语法。

按真实变更范围同步：Kernel AgentDecision/Plan类型、Agent.Dsh codec/ProtocolModels/ProtocolSchema、schemas/agt-002生成物、product Prompt及manifest/hash、dsh/uniclaw-decision-channel镜像/schema/测试、相关文档/CONTEXT/ADR。以实际生成工具与闭包检查为准，不手改hash蒙混，不把状态/结果随意塞成非法AgentDecision类型。

运行相关Kernel/Agent.Dsh/Host/Simulation与DSH插件测试、协议一致性/公开面检查；生成后再检查无漂移。Harness的WorkItem Schema校验不能替代产品协议校验。版本/authority变化只能按既有纪律处理，不静默绕过accepted基线。

## 6. 入口、委派与完成纪律

当前共享checkout基线为2225c642，已有大量未提交/未跟踪的001和其他任务改动，不能reset/clean/覆盖。先记录当前HEAD/status/文件hash及归属，识别已有修改属于哪个Change，再制定写入范围。新会话使用当前本地项目，因为设计资料尚在此工作区；若确需隔离，应先完整携带相关未提交基线并证明可恢复，不能从remote默认分支漏掉依赖。

Leader负责语义/协议设计、拆分、集成、Review与Gate判断；Worker按本次会话指定模型执行有界实现/测试工作。具体模型只在会话/派发配置中设置，不写入model-routing.yaml、共享Skill或WorkItem schema。每个Worker有明确文件所有权与WorkItem，告知共享代码库中还有他人，不回滚他人修改；依赖串行，控制并发。

仿真和协议两类门均通过、所定Acceptance四元组齐全、评审问题处置、文档/索引/生成物同步后才可关闭本Change；失败与外部阻塞如实保留，不能为了关闭降低门槛。当前未获推送/合并的明确要求，不自动对外发布。完成按仓库programmer-style汇报代码/行为变化、验证和限制。

入口证据：[task-handoff-20261019](../../evidence/runtime-plan-002/task-handoff-20261019/entry.json)。新会话标识及启动结果由派发后记录保存。
