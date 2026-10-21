# WRC-003：World 滚动关系与重放消费设计

## 真实买方与调用链
产品 `UniKernel.VerifyPostActionEffect` 已有真实操作回执、事前 World、事后 accepted observations 和独立 Assurance 判断；回放/重建需要查询这些操作把哪些观察连起来、位移有无依据。沿既有 `ProjectWorldGraph` 查询，不新建第二个 World 或公共服务接口。

## 模型与接口（本 Change 单独审阅面）
- 导航边继续使用 ContainerRelation.NavigatesTo，端点为不同容器。
- 增加 ViewportTransition：同一个容器内 source/target revision 之间的已验证操作关联。携带回执/绑定/目标 occurrence 引用、effect class、scope、验证引用、前后观察证据、滚动区域、坐标空间、配准状态、可选偏移及对应锚。它不是 ContainerRelation 自环，不表示创建了新页面。
- ViewportCorrespondence 只记录某次配准所用的前后 occurrence 引用；不是 LogicalItem identity，不购买全局连续性。
- WorldBeliefRevision 保存不可变 ViewportTransitions；WorldGraphProjection 按 scope 派生对应关系，消费者获得同一 owner 的图事实。
- `RecordObservedViewportTransition` 为 owner 内部具体入口；唯一产品调用者是 Kernel 的事后验证组合点，不增加跨组件 Ixxx，也不允许 Host/Replay 铸造关系。

## 位移与有效性
- Kernel 必须找到实际 receipt 和匹配 canonical binding；只有通过事后验证的 swipe 才提交关联。
- World 核对 source/target revision、同一当前容器、source 中实际 scrollable target、操作时间、事后新观察、scope 和幂等 operation receipt。
- 以该 scrollable 的 source/target bounds 限定区域；尺寸/方向/frame 必须兼容；非交互区域与嵌套区域的独立运动不得混成一个位移。
- occurrence 目前没有 scroll-parent 归属；检测到另一个滚动区与目标区域相交时保留关系、配准为 OverlappingScrollRegions，不用几何包含猜测父子归属。
- 唯一同角色/标签/状态/原生锚的共同元素提供矩形端点差值。至少两个可判别移动锚一致才建立变换；区域内观测为静止的锚单独标记，不能污染滚动位移。相同标签的重复控件不能自行变成身份。
- 手势方向只作约束，手势距离不作实际内容位移。正向滚动的 `content = viewport + accumulated offset`，反向为负增量；历史拼图不生成执行 locator。
- 无足够锚或存在不相容运动则关系仍记录为已验证滚动，但变换为 null 并给出原因。关系不等于位移已知。

## 实现顺序与替代方案
1. 产品 owner 的关系/空间计算 RED→GREEN；导航不退化。
2. Kernel 真实回执/验证接线，模型只收 owner 明确投影的事实/引用。
3. 产品 hierarchy 迟到字段拒绝、NaN 拒绝，防止错误现场参与配准。
4. 回放经产品操作链建立关系，重建只消费图关系与原始 Slice，不再运行测试侧的另一套配准。
5. 同步新增 consumer 字段的 schema/文档、边界白名单；独立评审后执行回归与可视化核验。

拒绝：以操作历史代替观察、在 Replay 补边、把滚动作为跨页导航、为配准铸造 LogicalItem、为未来非平移/多设备需求预造通用变换框架。

## 验证限制
慢感知本次不改配置、不作隐式依赖。语义/元素提案可以作为已有外部输入回放，但视觉模型质量与今天的设备运行必须另有环境证据。录制帧不足以支持唯一配准时保留 Unknown，不修改录制资产制造 PASS。
