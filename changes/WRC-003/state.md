# WRC-003 — 操作关联的 World 滚动关系与完整重放
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
把已存在的跨页导航边、真实操作回执和滚动前后观察贯通到 World 重放。补齐产品缺少的同页滚动观察关联与实测空间变换，并让重建消费产品结果，避免测试自己重造一套计算后据此评价产品。

## Scope / Out of Scope
- 产品 World：由已验证操作与 accepted 前后观察建立滚动关系，保留版本、证据、操作引用、空间变换或明确无法配准原因。
- Kernel：沿现有事后验证链路登记；导航边保留；操作回执不是位移真值。
- Replay：真实 Runtime 或 Kernel 操作/验证链路，导出并消费导航边、滚动关系、操作记录；重建只投影 owner 结果。
- 与本链路直接相关的缺陷：迟到 capture 不能改挂当前页；无效数值坐标拒绝。
- 不扩展完整页面语义、不改慢感知开关/模型、不生产新逻辑对象身份、不进行全局 UI 重建或像素仿真、不修改其他 Runtime Plan。

## Decisions / Owner-Authority impact
- World 唯一拥有关系/空间关联 belief；Assurance 保留验证权；Effect 保留操作回执权；Replay 非权威。
- 跨页导航关系与同页前后观察关系分开，滚动不得伪装 NavigatesTo 或新容器。
- 真实操作给出关联、区域与方向约束；位移须由 accepted 前后观察中的重叠元素证据计算，手势距离不能替代内容位移。
- 无重叠、歧义、跨空间、重排或证据时序不充分时明确未知，仍可保留“发生了已验证滚动”的有依据关联；不捏造变换。
- 历史拼图具有来源版本标记，不能变成当前 Slice、可执行坐标或新 World。
- 使用既有公开消费缝；新增具体数据形状先在 design.md 说明真实 buyer、约束和替代方案，不新增空 Ixxx。

## Acceptance
1. 原有导航边与操作回执经完整链路产生，重放确实读取并展示，不再只比较两个 Slice。
2. 已验证同页滚动在 World 内留下前后版本/观察、操作和验证引用；无操作、未验证、错目标/容器/时序/空间不能建立有效位移。
3. 对充分重叠的录制/独立 worked examples，正反向/双轴位移正确；区域内锚与固定/区域外元素分开；累积内容坐标 round-trip 正确。
4. 无重叠、重复不可判别、布局重排、尺寸/方向变化、裁剪冲突等明确未知，未知关系不被当成已定位长页。
5. 同语义输入双跑，关系、来源和变换一致；历史与分支不变；导航、grounding、Assurance/Effect 原有回归通过。
6. 旧 capture 迟到字段不能替换当前页面元素；NaN 坐标被拒绝。
7. 产出可查看的图/操作/滚动重建报告与可复现测试；独立评审和验证分别记录；保护并行文件。

## Assumptions / Alternatives / ADR refs
- 沿用 ADR-0012：P22 仅 prior，不把操作意图升级为世界证据。
- 拒绝：只修测试配准算法；只把 swipe 放进 NavigatesTo；以手势长度建立位移；Replay 铸造 canonical 边。
- 选择：产品由 accepted 前后观察计算关系，Replay 只消费并核对产品事实。
- 新的跨组件载荷若进入已有 consumer view，需同改其生成 schema/文档，不能由 JSON 附加隐藏字段。

## Constraints
并行恢复快照：`evidence/wrc-003/entry/`（tracked.patch、status、7354 个文件 hash、untracked-source.tar.gz）。禁止回退其他 Change。Runtime/协议/生成物存在并行修改，必须精确增量合入，不能扫回 HEAD。

## Verification
```yaml
level: SCENARIO
method: RED/GREEN 专项；Kernel/Host/Simulation 回归；schema/协议检查；图与操作关联报告；dirty snapshot attribution
expected: A1-A7；产品计算、重放消费；不能计算的输入保留未知
actual: Kernel 981/981、Host 370/370、Agent.Dsh 201/201、Simulation 244/244；有依据的正反向/双轴配准通过，无依据保留未知；原轮Leader审查完成；2026-10-21独立Standards/Spec均无阻塞，追加滚动关系19/19、Host重放/迟到隔离5/5通过；历史完整回归不冒充本轮执行
evidence: evidence/wrc-003/verification.json、report.md、review.md、output/world-operation-replay.html
```

## Residual risks / Remaining work
- 2026-10-21独立评审已补齐（当前用户授权Leader顺序收口），两轴均0阻塞；旧路由等待记录保留为历史。原验证源码已有后续World/Runtime叠加，本任务收口不代表其新增功能已验收。
- 目前支持有足够可判别重叠元素的平移；嵌套/相交滚动区尚无 scroll-parent 归属证据，保守标为 OverlappingScrollRegions；不声称已支持任意页面长图拼接。
- 两组既有录制帧分别为 AmbiguousAnchors / InsufficientOverlap，关系保留而位移未知。确定性重放替换外部页面与投递驱动，没有运行真实设备或慢感知模型质量评估。
- 本次迟到 capture 修复保护页面归属及沿用元素的证据引用，不声称修复所有聚合字段的完整派生来源。

## Status log
- 2026-10-20 · UNDERSTAND→RESOLVE→PERSIST · Owner 授权一起开 Change 修复，指定本会话 Leader；核对产品导航建边与 replay 分离缺口，完成并行工作树快照。
- 2026-10-20 · PERSIST→PLAN · 以 design.md 单列具体模型/接口及真实买方；产品事实源与 Replay 消费职责确定，开始专项 RED。Codex 委派绑定缺席，委派授权待 Owner 回复，Leader 先直接执行。
- 2026-10-20 · PLAN→IMPLEMENT · 完成产品 ViewportTransition 与 accepted 前后观察计算；Kernel 核对实际 receipt/binding 后接入；Replay 经 KernelRunDriver 操作链消费产品关系。修复小滚动判定、无资源 ID 的滚动目标描述漂移、迟到 capture、非有限坐标，RED/GREEN 证据保留。
- 2026-10-20 · IMPLEMENT→REVIEW→IMPLEMENT · Leader 审查发现嵌套滚动区可误判外层位移；补反例先 RED，产品改为明确 OverlappingScrollRegions，复验 GREEN。独立评审未运行，不标记 reviewed。
- 2026-10-20 · IMPLEMENT · Kernel 981、Host 370、Agent.Dsh 201、Simulation 244 全通过；28 个场景覆盖通过，其中 20 个仅更新认证字段、8 个 descriptive 未改，期望/执行载体不变；覆盖与认证检查通过。生成的 HTML/JSON 已用浏览器核验，来源/范围/未知声明保留，待独立评审。

- 2026-10-21 · REVIEW→VERIFY→CLOSED · 当前Leader完成独立两轴审查；一条Spec猜测经实码短路和报告script反证并撤回。冻结源码追加World19/19及Host重放/隔离5/5通过；review/verify分开留存于evidence/eng-opt-001/，不改原未知配准限制，不提交推送。
