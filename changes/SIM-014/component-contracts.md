# 两个组件切片的测试接口与变更验证

## 当前实际调用链
`RecordedComponentInputs.Load` 校验 SIM-012 原 XML 与公开 Host 冻结导入，投影公开 `PageCaptureObservation`，不读取旧 expected。两种布局供给分别是 typed-import 和冻结 Product JSON；输入内容经公开 codec 对齐。它们只用于测试，不承担产品 Host 的解析或识别职责。

感知链：布局→真实 Ledger.Admit→真实 ISemanticPerception.ObserveSemanticsAsync→受控 SemanticPerceptionExecutor→真实 SemanticObservationProjector→真实 Ledger。Registry 中始终注册并取回同一真实 UniPerceptionCapability，两个 L3 供应分别是脚本和冻结 Product response；不替换 L2。身份、语义响应、候选目录、意图和时钟均受控，原设备录制仅证明布局。独立 oracle 是固定字面值，不由实际输出产生。

World 链：同一布局→真实 Ledger.Admit→真实 World.JudgeRelevance/Reconcile→受控 PageIdentityObservation→真实 World 归属。对比父页、无主子页、错捕获身份、正确身份、重复、矛盾、晚到及旧快照。没有装配 Control、Assurance、Effect 或 Agent；不声称此链路跑过整装 Run。

## 提取依据与边界
两个 buyer 重复读取同源录制、验证来源、施加一次 capture 错配、记录回执与验证构成，因此提取 RecordedComponentInputs、CaptureMismatchFault、ReplayCompositionGuard。组件专属 supply/请求/断言留在各自测试中，没有统一 Execute、反射或产品仿真开关。所有新 helper 都为 internal 测试代码，不声明新 Capability、不复制产品 DTO 或运行时。

原输入保持不变，故障只改传递出的捕获引用。感知在调用 L3 前拒绝错捕获，校正后同一请求可执行；World 拒绝错误归属，正确身份到达后解除 observation need。供给故障、响应错配、意图冲突和取消分别保留各自语义，不把所有失败压成一个布尔值。取消通过供给开始/释放/完成信号验证，不依赖睡眠；两秒仅是测试死锁边界，不是产品性能指标。

## 新场景与协议/组件变更
新增同形布局场景时，先冻结来源与 Product JSON，独立写 oracle，再在 suite.sourceIds 添加数据行；MemberData 自动发现，不改组件或 ScenarioRunner。SIM-014 先以 SIMs 执行，随后仅加 All apps、Default apps 两行验证增长；数据增长前后源码指纹和测试证据保存在 evidence/sim-014/data-only-growth.json、component-green.trx。

composition.json 记录本切片的产品契约、实现、测试 adapter、回放输入、oracle 五类来源。每个组件创建前校验明确列出的文件；同版本号但内容变化也会拒绝，并导出使用的 manifest 与 Kernel 程序集 hash。它是内容一致性门，不是语义兼容证明，也不是全仓依赖解析器。当前覆盖范围由 pins 明示；依赖范围变化时须评审 pins。

组件/协议变化时执行以下流程：定位影响的公开缝与固定正反例；保留旧数据和失败证据；先证明旧构成被拒绝，再按致因 Change 评审输入/独立 oracle 是否仍有效；仅经明确 recertification 步骤更新 composition（evidence/sim-014/pin-composition.py 不在测试中自动调用）；运行组件专项、场景标注、完整验证与公开协议生成检查；核对旧录制/认证未被静默重写。full 的 Simulation 测试会执行此门；quick 的既有首批选择不被宣称覆盖本切片。

如来源有新协议 shape，应在产品 canonical seam 决策后改 typed adapter，不由场景私造协议版本。既有产品协议仍由产品类型、state-machine/generated artifact 与已有架构执法负责；本变更没有建立另一份协议源。

## 仍待后续证据
真实 Host 采集/身份识别及模型/Provider 行为、任意 L2 实现等价、原完整 Run 逐决策重放、真实设备 Stay awake 关闭补采，以及实际大模型生成候选场景均不在本切片证据中。场景生成可沿 SIM-013 的候选→独立 oracle→固定回归流程接入，模型不能自行认定成功或更新 golden。SIM-015 仅授权无 Agent 组件场景的 N/A 标注，不能扩大上述证明范围。
