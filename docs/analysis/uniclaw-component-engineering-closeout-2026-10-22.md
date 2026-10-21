# UniClaw 组件分级与工程化审查收口（2026-10-22）

> Status: COMPLETE / EVIDENCE-BOUNDED
> Authority: NONE

## 做了什么与原来的问题

ARCH-DOC-024原题是组件分级、职责边界和工程化管理，但其证据链接后来指向了生命周期审查。这里恢复独立的工程化入口，复用已完成的[模块审查](uniclaw-module-boundary-design-review-2026-10-21.md)、[目录审查](uniclaw-directory-organization-review-2026-10-21.md)及[实施结果](../../changes/ENG-OPT-001/report.md)，不把生命周期论文当成组件管理交付，也不重复定义架构。

## 自顶向下的分级与管理边界

| 层/管理面 | 职责与Owner | 实现/管理入口 | 边界与可替换性 |
|---|---|---|---|
| Product T0 | UniClaw与User/Host/Device环境的系统边界 | [产品基线](../architecture/product-architecture-baseline-l0-l3.md) | Host/Device接入不成为产品事实总Owner |
| Product T1：UniAgent | Goal、Contract authoring、Goal Evaluation | [Agent归属](../../src/UniClaw.Agent/README.md) | Host仍代造部分Contract，是已登记偏差；模型绑定留Adapter |
| Product T1：Uni Kernel | 生命周期协调及六责任域组合 | [Kernel归属](../../src/UniClaw.Kernel/README.md) | 编排只提供事实/期望/顺序，不接管所有canonical状态 |
| Product T1：Capability Plane / Memory | 能力协议与注册、记忆的声明边界 | Kernel README及产品基线 | 逻辑peer不强制独立程序集；Memory无真实buyer时保持占位 |
| Product T2：Ledger / World / Run | Evidence接纳、Belief演化、Run及终态记录 | Kernel README内组件表及对应目录 | 接纳拥有快照、历史不可外部写；producer可替换而不取得canonical写权 |
| Product T2：Control / Assurance / Effect | Intent、Judgment/Proof、Binding及现实投递 | Kernel README及组件协议 | Control提案、Assurance判断、Effect执法；driver注入不改核心Authority |
| Product T3 / Host realization | 协议实现、观察接线、驱动和DSH传输 | [Host](../../src/UniClaw.Host/README.md)、[Host.Dsh](../../src/UniClaw.Host.Dsh/README.md)、[程序集地图](../../src/README.md) | 具体实现可替换；HTTP接入与任务启动仍有局部设计债 |
| Workspace / Provider / Harness / 验证面 | 只读投影、外部供货、开发流程、测试及证据 | [仓库导航](../../README.md)、[测试](../../tests/README.md)、[工具](../../tools/README.md)、[schema](../../schemas/README.md) | Harness不进入产品运行时；Receipt/模型输出/投影不冒充现实已验证效果 |

六个产品程序集的依赖方向、每个组件的逐项判断及定位证据保留在10月21日模块审查，不因本文件收口而把“基本清晰”升级为“完整实现”。

## P0/P1/P2执行清单

这里的优先级是工程推进次序；原缺陷严重度仍以模块审查中的3个P1、4个P2为准，没有新增已证实的P0产品缺陷。

| 次序 | 原问题→本次处置 | 可判定验收 | 当前结论 |
|---|---|---|---|
| P0：保护现场与稳定基线 | 大量并行dirty及过期断言→等待World停止写入、保存快照、仅整合局部patch | 指纹/允许重叠清单可回指，测试失败逐项归因，旧资料不丢失 | ENG-OPT-001完成；不代表当前其他活动会话已停止 |
| P1：canonical所有权 | Run/Ledger输入别名、World读取逃逸→admission快照和只读live view | 修改原输入不改canonical，公开读取不可回写，比较器保持；RED→GREEN | 完成8条公开回归 |
| P1：判断职责 | Kernel直接定案typed效果→同一Assurance Owner按最小事实定案 | checked/Partial/Unknown、时序和缺dispatch原路径保持，Kernel不构造成功结论 | 完成，相关66项通过 |
| P2：Coverage内聚 | 提示文字反推动作→Kind/Target/Text显式传递，同一Ledger partial归拢 | 两个特殊合法标签不误判，步骤JSON保留kind/target，旧入口集中兼容 | 完成2条反例与77项相关验证；旧text-only兼容不升级为强语义校验 |
| P2：导航与测试归属 | 缺总入口、DSH消费测试错放Simulation→导航与单向fixture复用 | 本轮导航97个局部链接0失效、测试发现总量不减 | 完成 |
| P2：大状态簇及目录 | Driver/HTTP职责集中、历史路径/命名与文档索引债 | 单独范围，保持行为/测试发现/旧证据可解析，不以拆类数量验收 | 后续候选；历史164份资料原路径/字节受保护 |

## 验证结果与限制

本轮文档收口只运行文档元数据、链接/引用及差异格式核验，四元组落在[evidence](../../evidence/ordered-closeout-20261022/)。最近一次完整.NET整合是ENG-OPT-001：8项目2024/2024、场景28/28（20认证+8描述），见[原始结果](../../evidence/eng-opt-001/verification.json)。3个真实模型opt-in未执行内部调用，不计真实模型验收；此处不重命名为今天重新运行全套。场景/单测不能证明完整产品生命周期。

## 新发现与下一步

当前未关闭的功能、环境和布局任务仍按[Change索引](../../changes/INDEX.md)推进。先补真实传输、停滞干预、完整覆盖和工作台集成证据；Driver/HTTP大调整、目录物理迁移与Host authoring跟真实需求单独实施。本报告完成的是ARCH-DOC-024的只读审查交付与工程入口纠偏，未把建议全部当作已实现。
