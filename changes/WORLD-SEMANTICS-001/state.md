# WORLD-SEMANTICS-001 — World 图关系、观察变化与语义消费侧检查
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
检查 World 的容器关系、前后观察、Slice、身份关联与语义结果消费边界，给语义感知协议设计提供当前实现依据及真实缺口。用户仅授权检查与设计，尚未授权修复或集成。

## Scope / Out of Scope
- 检查 CONTEXT、P2/P3/P4/P22、ADR-0012、World/UniKernel、关联 Host 实现与已有测试；复跑相关确定性测试，并在仓库外运行构造探针。
- 仅新建本 Change 的检查文档和证据；不改产品代码、既有测试、共享 CONTEXT/协议/ADR、changes/INDEX.md；不提交、不推送、不操作设备。
- Semantic Perception/Jev 的跨组件载荷设计归 JEV-SEMANTIC-CAP-001；本检查只提供消费侧约束与待证明行为，不另立协议。

## Decisions（已确立来源与本次范围选择）
- 继承已有权威边界：Perception 提交观察声明；Ledger admission；World 关联身份、接纳关系、派生 Slice；Assurance 验证操作效果；Effect Boundary 持有实际绑定与投递回执。
- P22 只携带实际 attempt/effect 的非证据 prior，禁止 expected page、Control plan；不借 Reason、签名 JSON 或 Observation 包装改变这一规则。
- 现有图与关联缝优先；不因需要语义推断而另建页面图、Slice 图或 World writer。
- 本次只证明当前文件上的行为，不把 dirty 工作区实现当成已合并基线，不把确定性测试当成真实设备/Jev 集成证明。
- report.md 的设计方向均为候选，不修改 Accepted ADR，也不构成实施授权。

## Acceptance
1. 明确容器、关系、观察、revision、Slice 的对应关系和 Owner。
2. 区分子页/返回/同页更新/滚动/覆盖层/未知；列出已有缝、当前限制与证据等级。
3. 分类前页、元素类型/名称、实际动作、预期页的来源，保留 P22 禁止项。
4. 检查迟到、错序、缺证据和冲突能否污染当前身份或建边；可复现结果与仅代码推断分开。
5. 只在本 Change 新增检查产物；产品与并行内容不由本会话写入。

## Assumptions / Alternatives / Owner-Authority impact
- 不把目的地语义相似等同于实体相同；导航历史不等同 Contains；页面发生变化不等同符合预期。
- 不选择“将 Jev 摘要直接写 ui.screen”“将符合预期当成导航成功”“由 Replay 修复 canonical 图”等捷径。
- 无 Owner/Authority 变更。当前检查不需要重裁既有 Owner/P22 规则；后续产品身份等价标准、父子关系买方和具体语义载荷须与上游设计共同评审。

## Verification
```yaml
level: DETERMINISTIC
method: 读取当前代码/协议；复跑 Kernel 6 组、Host 3 组相关测试；仓库外探针调用现有程序集；检查新增文件与被读文件哈希
expected: 职责与实现差异可定位；危险路径有明确输入输出，不修改产品
actual: Kernel 51/51、Host 9/9 通过；构造探针确认迟到身份回退、冲突与身份推进分离、同入口不同 occurrence 签名不稳定；文档写入前后116个被检查文件哈希一致；无真实设备或 Jev 产品集成新证据
evidence: report.md；evidence/world-semantics-{kernel,host}.trx；evidence/probe-results.jsonl；evidence/probe-reproduction.md；evidence/inspected-source-hashes.json；evidence/preservation-check.json
```

## Residual risks / 下一步
- 当前 navigation 验证不是“到达语义预期页”的完整证明；Contains/Overlays 在产品关联策略中尚无产出，缺少撤销/失效的实际买方验证。
- 迟到保护只在部分 typed hierarchy 路径成立；语义结果的当前适用性与身份冲突处理仍是接入前缺口。
- 下一步只做双方设计对齐与验收案例细化；任何修复另需用户授权。检查完成不代表相关实现已修复或新协议已冻结。

## Status log
- 2026-10-21 UNDERSTAND→RESOLVE：接收只读检查委派，核对 uni-harness dirty 现状；读取 uniflow、工作汇报规则、domain-modeling、既有协议与关联 Change。
- 2026-10-21 RESOLVE→PERSIST：仅新建本 World 检查 Change，保留事实与候选设计的区别；未重开已接受的 Owner/P22 规则。
- 2026-10-21 PERSIST→VERIFY：相关确定性测试 60/60 通过，仓库外 3 个构造探针揭示未覆盖缺口；检查报告与证据落盘，不进入产品实现。
- 2026-10-21 VERIFY→CLOSED：限定的职责/语义核验和候选设计约束已交付，116/116被检查文件哈希未变；未决产品语义与协议冻结留给后续双方设计，不将检查关闭表述为产品缺口已修复。
