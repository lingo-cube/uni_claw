# RUNTIME-PLAN-004 场景覆盖边界

本矩阵只论证有限、有界输入下的生成与执行表达，不外推成对所有设备、页面或 UI 测试的完整覆盖。单次目标可拆成观察、选择、动作、检查、等待和反馈组合；PlanTree 组织动态重复与阶段，Act 表达一个原子操作。表达可用不证明物理能力、独立判据、时序和恢复适配已经成功。

| 场景 | 本次接入 / 必须验证 | 可表达但缺 adapter 或证据 | 不据此宣称覆盖 |
|---|---|---|---|
| 动态菜单 | `TraversalScenarioFixture.Source == sim.menu` 是当前正式模拟来源；输入提供 root 的 `menuItem` 网络/蓝牙、只读飞行模式、tap allowlist、最大深度 2 与 Back 返回契约。Kernel tracer 要完整遍历网络、初始未见的嵌套子入口、逐层回返、蓝牙、再回 root，且 switch 只有只读检查、零 Effect。未来子页名不进初始模型输入。 | 真实 Android 层的菜单发现、导航和返回必须由选定 Perception / Effect adapters 实证；`sim.menu` 是 evaluation fixture projection，不是新增产品能力。 | 任意设备的动态页面、未声明目录、视觉歧义、不同导航习惯。 |
| 开关只读 | `sim.menu` 当前候选或结构化当前 Element 提供“关闭”；独立判据要求准确报告该值，且无 Act/Effect。若决定包含 traversal，必须只含已声明来源上的 ReadOnly operation，无未绑定 handler；Kernel fixture 记录的 `ObservedState` 与输入值交叉核对。 | Live/vehicle UI 状态采集及读值能力须按真实绑定提供；当前 case 值是明示 fixture。 | 仅凭 NoAction 文本证明现实状态；任何需要切换才能验证的情形。 |
| 已满足 | CurrentWorldClaims 显式提供当前满足事实；要求 NoAction 和零 Effect。 | 事实新鲜度与具体产品判据由场景所接入的独立 source/assurance adapter 证明。 | 旧事实、冲突事实或未给出证据时的完成判断。 |
| 缺能力 | 输入能力目录为空；期望诚实 Blocked，禁止编造能力或读值。 | 真实能力注册、能力运行健康与替代路径必须由对应产品组合证明。 | 目录未列举能力是否“世界上不存在”。 |
| 未知 / 危险节点 | 节点分类为 unknown 且无授权；期望拒绝并核对零 Effect receipt。 | 安全节点分类、来源和当前策略由真实 Policy / Perception adapters 提供。 | 所有风险分类均已完备，或拒绝行为等同于任务覆盖成功。 |
| 简单 Act | 同一个已解码决定进入 Kernel simulation；使用唯一可见 `网络` 候选和 `tap`，核对一次 Effect receipt 与页面进入事实。 | 真实控件接地、动作绑定、时序和独立页面确认依赖 Host adapter。 | 任意 `set-value`/toggle 均可执行；单次模拟动作代表真实设备效果。 |
| 拒收后修订 | 固定 Task 与独立期望摘要；只允许真实 codec/consumer 拒收反馈触发最多一次修订；保留坏提交、反馈和修订。 | 当前 DSH opened-channel 对外响应只返回最终捕获载荷；首稿和拒收反馈需从正式 DSH session trajectory/validation 证据获取。若 Leader 预检无法读取该证据，不得启动正式调用，也不得把最终 codec 成功记作首稿通过。 | 不能把模型自述、合成拒收或修改期望后通过算作修订证据。 |

## 当前验收入口

`evidence/runtime-plan-004/generation/cases.json` 固定七类输入与独立必需/禁止行为；`semantic-assertions.json` 固定按语义、Kernel 结果与 Effect 日志判定，不比较树形文本。真实模型入口须在 Leader 冻结服务、有效 Profile/Prompt/Skill、schema、输入与实现 hash 后显式启用。模型-only 用例不要求 Android 设备。动态遍历与简单 Act 的最终模型产物须通过正式 `ProductProtocolJson` 解码后交给 Simulation Host；仿真测试只证明这些已声明 fixture 下的 Kernel 消费行为。

场景组合论证限于以上有限输入：观察事实和能力目录先界定可知范围；Traverse 根据当前候选动态重复；Act 每次只表达单个原子动作；ReadOnly/Observe/Checkpoint 可表达检查与阶段；Kernel simulation 提供消费结果与 Effect receipt；拒收反馈作为下一次有界生成输入。等待、真实视觉质量、车型差异、复杂多屏、多输入协调以及实际设备 Recovery 都需要对应适配和独立证据，目前不列为已覆盖项。不为数学上的完整性预造产品节点或协议字段。
