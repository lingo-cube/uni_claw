# World相关Change清单

按用户2026-10-21指令，将World相关工作集中列出。各Change继续保留独立目录、生命周期、决策和证据；本文只负责导航与分工，不新增状态机、协议或实施授权。实际状态以各自state.md为准。

## 当前World工作组

| Change | 负责的问题 | 与本轮设计的关系 |
|---|---|---|
| [WORLD-SEMANTICS-002](WORLD-SEMANTICS-002/state.md) | World迟到/冲突/重访修复与辅助语义关联 | 已实现并与上游语义能力联合验收；沿用现有World图与关联缝，不另建业务接线工程 |
| [WORLD-SEMANTICS-001](WORLD-SEMANTICS-001/state.md) | 已有图关系、身份、Slice与语义消费侧核验 | 提供检查结论及迟到/冲突/签名问题的探针证据；检查完成不代表缺陷已修复 |
| [WRC-003](WRC-003/state.md) | 实际操作关联的同页滚动关系、空间变换和完整重放 | 现有ViewportTransition与滚动重放路径；避免语义设计重造同页关系 |
| [WRC-002](WRC-002/state.md) | World修订回放、坐标重建和滚动诊断测试 | 提供已有表达能力、信息缺口与回放验收基础 |
| [WRC-001](WRC-001/state.md) | 单revision的ContentOnly语义重建测试参照 | 只作测试/诊断材料，不成为生产World或跨revision真值 |

## 已有World基础变更

| Change | 基础职责 |
|---|---|
| [UIW-001](UIW-001/state.md) | UIWorld可执行观察—证据—世界模型主干 |
| [UIW-002](UIW-002/state.md) | UI实体三层模型、Slice与连续性语义冻结 |
| [UIW-003](UIW-003/state.md) | occurrence、LogicalItem、连续性与demand实现 |
| [UIW-004](UIW-004/state.md) | P23消费缝、GroundingView、Slice投影及当前绑定 |
| [UIW-005](UIW-005/state.md) | 现有产品container association realization及可替换缝 |

## 上游协作，保留独立职责

[JEV-SEMANTIC-CAP-001](JEV-SEMANTIC-CAP-001/state.md)负责语义感知协议载荷和Jev realization。自动遍历与World通过既有能力插件替换使用新的实现，不另拆通用接线Change；World具体关联设计归WORLD-SEMANTICS-002。若现有执行协议/替换缝不足，分别显式记录和评审，不由provider绕过现有Owner弥补。

页面语义、观察/操作关联与符合预期分别表达。语义结果经过既有证据链，World仍拥有身份和关系接纳；操作意图不进入P22或变成观察事实。具体载荷见上游execution-contract.md；联合结果和已知模型误判/冲突限制见上游report.md，非实时设备遍历验收。
