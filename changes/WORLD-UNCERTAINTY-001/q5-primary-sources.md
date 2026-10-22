# Q5 业界一手资料：四类不确定是否同期开

状态：COMPLETE / EVIDENCE-BOUNDED
Authority：NONE（研究建议，不冻结协议；结论与验收由 Leader 决定）

## 结论

业界资料支持把“不知道”与“已确认不存在”分开，也支持让每个状态带有其判定范围、原因和依据；但没有来源把 IdentityAmbiguous、AttributionUnknown、RelationUnproven、ExtentUnknown 规定为标准分类，更不能据此断言四类互斥或完备。建议可把四类作为同一版本的候选标签同时讨论，但先视为可组合的分析维度，而非已验证的封闭枚举。若“kind”描述缺口对象/命题，“reason”描述为何未知，IdentityAmbiguous 很可能把两轴混在一个名称中；建议 Leader 裁决是否将原因独立表达。

## 可迁移模式

1. **命题状态与原因分轴，且标明新旧依据。** Kubernetes Pod Condition 将 `type`、`status`（True/False/Unknown）、`reason`、`message`、`observedGeneration` 分列；官方说明 generation 是记录该 condition 时观察到的对象 generation。[Pod Conditions](https://kubernetes.io/docs/concepts/workloads/pods/pod-condition/) 迁移推论：Unknown 应描述某个具体命题在当前证据下不可判定，reason 解释未知原因；记录适用对象、证据代次/时间，避免把“身份对象不清”与“证据不足/过期”压成同一种 kind。边界：Kubernetes 条件是控制器对 Pod 状态的 API 设计，不能证明本项目必须采用三值逻辑、字段结构或四类标签；也不能据 status/count 推出全局事实完备。

2. **关系未观测不等于关系不存在；完整性只在明确查询范围内成立。** SHACL 验证以 data graph、shapes graph 和 focus nodes 为输入，目标声明决定哪些节点参与；`conforms` 只表示这些输入节点针对给定 shape 的约束检查结果为空。[W3C SHACL](https://www.w3.org/TR/shacl/) 迁移推论：对某范围内未观察到导航边，只能支持“本次范围/观测中未见边”，不能直接推出边不存在；“无关系”需要明确封闭范围及能支撑否定结论的完备性依据。报告应保留被检查对象/范围与约束依据。边界：SHACL 是图数据约束验证，不是开放世界事实推理规范；验证通过不证明底层图已经穷尽真实关系，也不提供本项目的缺口生命周期规则。

3. **结构层级与发现覆盖分开记录。** Android `AccessibilityWindowInfo` 分别提供 parent/child window、child count、root hierarchy 等接口；返回的层级结构只表述 API 暴露的窗口/节点关系。[AccessibilityWindowInfo API](https://developer.android.com/reference/android/view/accessibility/AccessibilityWindowInfo) 迁移推论：可记录已见对象之间的关系及捕获范围；Task/Control 的“探索结束/发现完整”是执行侧对某次任务范围的穷尽证明，不应直接升级为 World 的客观事实。World 覆盖可与控制侧完成度各自陈述，不能用节点数或发现计数替代真值或完整性证明。边界：Android API 不保证任意 App 的可访问性树覆盖全部视觉/虚拟化内容，也不规定跨捕获持久化或安全决策复核语义。

## 对四类方案的压力点

- **互斥性未证。** 同一观察可能同时有对象身份不确定、归属不确定、关系证据不足和范围不足；除非先定义唯一主分类规则，否则不宜声称互斥。
- **完备性未证。** 上述规范均无这四种本体分类；应允许“其他/待归类”或组合表达，避免封闭枚举造成漏记。
- **Extent 应有请求范围与覆盖依据。** SHACL 的 focus nodes/shape 给出可界定的检查输入；因此 ExtentUnknown 最好关联具体 requested scope 与实际 coverage/停止原因。未知范围不等于已证实范围内不存在对象。任务侧穷尽与世界侧观察覆盖是不同事实。
- **关系未知与关系不存在分开。** 负结论必须有范围内闭合/完整性依据；缺少边、边未见、API 未暴露都不能单独证明关系不存在。
- **计数不是 truth/complete。** count 是特定接口/查询范围中的结果；没有覆盖边界、过滤条件和完备性论证，计数不能证明集合穷尽。

本轮仅支持“把四类同期开作待裁决方案可行，但须允许重叠并分离分类轴”的讨论建议；不主张协议已冻结。
