# WRC-003：产品滚动关系与操作重放

## 做了什么
产品 World 保存已验证滚动前后的观察关联和有依据的二维位移；重放通过真实 Kernel 操作链产生并消费这些结果。代码已实现，独立评审待委派授权，Change 尚未关闭。

## 原来的问题
跨页 NavigatesTo 已存在；同页滚动缺少持久化的前后观察关系。旧重建直接比较 Slice，没有经过操作回执和事后验证，测试侧计算不能证明产品自己知道页面如何滚动。

另外，同一批可见元素只移动位置时，Kernel 因标签集合相同而误判滚动未生效；无 resource-id 的滚动容器被首个可见子项命名，滚动后目标描述变化；迟到旧 capture 字段可能重建到当前页；NaN 可绕过坐标范围检查。

## 这次修改
调用链：`KernelRunDriver → EffectBoundary receipt/canonical binding → VerifyPostActionEffect → World.RecordObservedViewportTransition → WorldGraphProjection → replay report`。

- `src/UniClaw.Kernel/World/WorldModel.Viewports.cs`：World 核对前后版本、容器、目标、操作时间和新 accepted 观察；配准只使用目标区域内可判别共同元素，至少两个一致移动锚。关系保留来源、操作、验证引用、对应锚和偏移；不足或歧义明确未知。
- `ViewportTransition.cs`、`WorldBeliefRevision.cs`：不可变关系随 World 历史保留，图投影按 scope 查询。配准对应锚不升级为 LogicalItem。
- `src/UniClaw.Kernel/UniKernel.cs`：小滚动判定加入观察坐标；只有事后验证成功且匹配实际 receipt/binding 的 swipe 才登记产品关系。
- `src/UniClaw.Host/UiHierarchyOccurrenceStrategy.cs`：拒绝旧 capture 重挂页面，沿用元素保留 accepted 证据引用；无资源 ID 的滚动容器改用有限 hierarchy path 描述。
- `SpatialLocator.cs` 拒绝非有限数值；同步 World 文档、术语、协议说明、公开边界白名单及生成 schema。
- `tests/UniClaw.Host.Tests/WorldOperationReplayTests.cs`：真实 KernelRunDriver，外部页面/驱动为确定性替身；经过进入页面、上滚、下滚、返回，生成导航边与滚动关系。重建只累计产品保存的偏移，没有第二套测试配准算法。

## 验证结果
| 方法 | 预期 | 实际 | 证据 |
|---|---|---|---|
| 产品 World 专项与 Kernel 全量 | 有依据平移正确，非法关联拒绝，历史不变 | 981/981，通过双轴、正反向、多区域、重复/裁剪/重排/空间/时序等反例 | kernel-reviewed-final.trx；WorldViewportTransitionTests |
| 完整操作重放与 Host 全量 | 产品图/回执贯通；迟到 capture 不污染当前页 | 370/370；2 个页面、2 条导航边、2 条滚动关系、4 回执，逐操作验证通过 | host-reviewed.trx；output/operation-replay.json |
| 正反滚动往返 | 产品偏移累计后同一元素内容坐标一致 | +0.2 / -0.2；Alpha Y=.4、Beta Y=.6，往返一致；两次运行 JSON 相同 | WorldOperationReplayTests；output/world-operation-replay.html |
| 既有 Apps / Devopts 录制帧经操作链 | 不因有操作而捏造位移 | 分别 AmbiguousAnchors / InsufficientOverlap；关系/回执存在，偏移 null | output/seq-scroll-apps.json；output/seq-scroll-devopts.json |
| Agent/DSH 全量 | 生成协议与消费契约兼容 | 201/201 | agent-dsh-full.trx |
| Simulation 全量与场景认证/覆盖 | 原有场景行为保持 | 244/244；28/28 场景覆盖，20 certified + 8 descriptive；期望/执行内容未改 | simulation-reviewed.trx；scenario-check-reviewed.log；scenario-coverage-reviewed.log；scenario-inputs.json |
| schema 与页面核验 | 生成契约可用，诊断可读且来源明确 | 13 schema/examples 通过；浏览器观察导航端点、关系、偏移、内容表并检查截图 | workspace-schemas.log；operation-replay-reviewed.png |

首轮失败未删除：`operation-replay-red.trx`（小滚动误拒）、`operation-replay-recorded.trx`（容器标签漂移）、`capture-red.trx`、`viewport-boundaries-red.trx`。Leader 审查又以 `viewport-review-red.trx` 证明嵌套区域会误判 Aligned，修复后明确 OverlappingScrollRegions。

## 新发现与限制
嵌套/相交滚动区缺少父子归属，现阶段保守返回未知；需要后续真实场景证据才能购买更精确的区域归属。录制页面中的重复标签/重叠不足也仍无法唯一配准，这与“没有保存滚动关系”不同。

本报告证明有界场景内的产品关系与坐标行为，不证明任意长页均能重建、不证明像素视觉相似、不证明慢感知模型质量。外部输入/投递是仿真，末尾 TerminalNotProven 明确保留，不能当成用户任务完成证明。沿用元素保留证据引用，不等于完整字段派生 provenance 已补全。

## 下一步
收到本 Change 当前模型委派授权或配置合法 Codex 绑定后，派发独立 Standards/Spec 评审；处理发现并对相应行为复验，再关闭 Change。无需再准备代码才能评审，当前模型/接口设计、实现清单、对照页和 TRX 已齐备。
