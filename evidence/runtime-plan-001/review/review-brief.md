# RUNTIME-PLAN-001 独立评审输入
比较基点为本次实施前的dirty快照，而不是HEAD；HEAD中不包含其他已实现Change的工作区修改，直接git diff会把它们混入本次评审。`implementation.patch`由make_incremental_diff.py逐文件比较归档原字节生成，`inventory.json`记录每个文件前后sha256；原归档位置见entry/snapshot.json。未提交、无新commit。

意图：ADR-0053可组合产品Plan，经既有KernelRunDriver逐动作Control→grounding→Assurance→Effect→verify；单层ForEach从Owner投影动态候选，精确检查点ContinuePlan与预算独立，保留版本/位置/历史/attempt/receipt；Settings是首个现场买方，记录收集是不同结构仿真买方。
规范来源：AGENTS.md §1.5职责/显式缝/真实buyer/可判定验收；docs/architecture/product-architecture-baseline-l0-l3.md；docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md P25；docs/adr/0053-composable-product-plan-with-bounded-local-execution.md；changes/RUNTIME-PLAN-001/implementation-contract.md。实现验收：plans/runtime-plan-001.md §3-7；proposal.md §6-10；state.md已确认D1-D10，不重新设计已接受选择。

评审两轴分别报告，先规格后规范；每条给文件/行/具体输入输出后果，区分阻塞bug、未完成验收、非阻塞建议。不要将尚待运行的ENVIRONMENT验收视为已通过。只审查增量及必要上下文，不把前置ADR-0052未提交实现重开。不得修改代码/Prompt/配置，不运行重型测试或设备，不创建子Agent。Owner限制Leader+1 Worker，按两个轴顺序完成。

规范轴额外气味基线仅作判断，不作硬规则：难懂命名、重复代码、Feature Envy、Data Clumps、Primitive Obsession、Repeated Switches、Shotgun Surgery、Divergent Change、Speculative Generality、Message Chains、Middle Man、Refused Bequest。仓库规则优先；已由工具执法的问题不重复列为人工发现。

已知并已在修复的边界（评审请核对最终实现和测试，不把修复报告当证明）：旧Control E4按顶层游标推进会跳过ForEach body；Begin后grounding/gate失败及聚焦重试的Started attempt对账；普通Act guard拒绝不应进入Plan尝试；ForEach重复节点不能借旧step锚冒充精确迭代。公开反例日志将保留在core目录，未见GREEN前保持未完成。
现场对照预注册见comparison/procedure.md，基线先于数值门槛，门槛先于Plan结果；真实前提/收益尚待实测，不要求review凭静态代码断言性能。
