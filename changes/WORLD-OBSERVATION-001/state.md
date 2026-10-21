# WORLD-OBSERVATION-001 — 页面身份补证、元素归属与Traversal恢复闭环
lifecycle_state: closed · disposition: implemented · depth: decision-heavy · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
落实World既有ObservationNeed设计：页面身份不足时停止依赖该身份的推进，补证并经World确认页面及元素归属，再验证导航到达，最后恢复Traversal。解决RUNTIME-PLAN-006中有新控件却沿用旧页归属、动作验证与目标页确认脱节的问题。用户于2026-10-21交接授权从persisted推进协议细化、实现、评审与验收至范围内CLOSED；不提交或推送。

## 核心闭环（用户明确要求，不得省略）
```text
页面身份未确认
→ 暂停依赖该身份的推进
→ 补充识别，按预算等待
→ World采信并确认页面及元素归属
→ 验证导航到达
→ Traversal继续
```
此顺序是设计、实现与验收主线。tap送达、route字符串变化、模型返回成功、旧current anchor尚在，均不能替代确认与验证。

## Scope / Out of Scope
- 范围：World派生的信息缺失消费路径；当前观察的身份/元素归属；Control有界补证与暂停；Assurance导航到达验证；Traversal接回原待执行位置；必要的观察适配、仿真、协议校验及有界真实样例。
- 必须证明有XML但身份不足、无XML但视觉/文字/布局依据足够两种成功补证路径。证据不足时保持未知，不承诺任意页面都可识别。
- 范围外：子页滚动发现与穷尽、完整Settings范围的Goal证明、通用Recovery平台、全部XML采集诊断、所有覆盖层/包含关系生命周期、跨进程恢复、替换整套PlanTree/World架构、模型/代理静默切换及提交推送。

## Decisions（Q1-Q4，用户2026-10-21确认）
1. Q1=A：先完成这一条纵向闭环，相邻问题保留独立后续范围。
2. Q2=A：以真实身份不足为buyer启用World缺失信息消费路径；优先复用现有消费面与ObservationDirective，表达不足时显式补最小协议，不让Host只凭XML存在与否猜World缺什么。
3. Q3=A：保留观察与元素结构，未知归属不自动挂旧页面；依赖该身份的操作及Traversal不可消费。确认本次观察关联后，在新revision中提供已归属元素；旧revision不可变。
4. Q4=A：有XML/无XML两种成功路径及不足、超时、错帧、迟到负向路径均为必验；仿真和协议校验必需，真实线路/设备证据如实标明覆盖限制。
5. XML可选；有XML且Fast充分时不必调用Slow。用户实施期补充：无XML成功路径必须经慢感知兜底，慢模型实际请求参数必须为high；未获high请求回执不能算此路径成功。不足时由Control安排有界补证，Effect Gate不调用或等待模型。
6. 页面类别、标题、模型confidence或预期页面不能单独建立实体身份。补证作为accepted evidence参与World判别；当前Jev仅辅助已知页的挂接路径不足以单独满足本Change。
7. 当前未确认元素不能靠owner=null自然获得安全性；须明确消费屏障，同时保留既有不依赖页面身份的合法用途。单条普通内容record的Insufficient也不机械清空整页身份。
8. 同capture证据保留原provenance、相关性与依据；场景变化或相关性不足重新观察。错帧/迟到不能解锁不匹配或过期的等待；不跨capture拼接当前真值。
9. 补证后重入原导航的待验证位置，不重发已投递点击，保留attempt/遍历位置/已消耗预算。恢复不隐式增加总预算。
10. 预算耗尽仍未知，交既有Recovery/任务策略处理；不默认成功、不擅自点Back、不把所有领域finding统一升级为Run失败。导航身份确认与任务声明的预期符合性保持区分。

## Owner-Authority impact / ADR refs
World拥有身份、元素归属与缺失信息派生；Evidence Ledger拥有admission；Control拥有观察调度与暂停；Assurance拥有消费充分性和导航到达判断；Effect Boundary拥有绑定/投递；Traversal拥有遍历进度；Goal拥有完成条件。driver仅组合，不新增Owner。

继承UIWorld基线§13–16、ADR-0010（消费相对freshness）、ADR-0012（实际transition prior≠意图）、ADR-0019（Kernel自驱）、ADR-0030（有界异步补证）、ADR-0049（预期与效果验证分离）及RUNTIME-PLAN-002执行合同。本次把既有deferred需求落到真实buyer，不改变这些权威边界，不另造平行页面图、临时页面身份系统或provider直写World路径。

## Acceptance（实施前可判定）
- A1 有XML身份不足：重放SIMs/All apps类输入，未知阶段没有旧页错误归属、依赖身份的Effect或Traversal推进；补证后页面/元素归属有本次依据，再完成独立到达验证并继续。
- A2 无XML成功路径：以真实截图派生的视觉/文字/布局依据运行同一正式执行链，确认身份与元素归属、验证到达并继续；不是只证明模型能输出页面名称。模型/设备证据与仿真分别标注。
- A3 暂停隔离：保留未知归属观察；无container条件grounding、直接Act、Traversal等相关消费路径不能绕过身份屏障；合法且不依赖该身份的观察/查询保持可用。
- A4 异步负向：证据不足、歧义、超时、错capture/cycle/session、切页后迟到、重复返回、取消均不误恢复、不把新元素挂旧页、不创造无证据身份或导航边。
- A5 有效复用：同capture补证关联通过时新revision出面，旧revision不变；场景改变重新观察。验收不以全局IsFresh替代消费相对Assurance判断。
- A6 到达与恢复：页面及元素归属确认仍不足以绕过Assurance；到达验证前Traversal深度/游标/覆盖不推进。恢复不重复点击，不清空已消耗预算，不追溯授权过期attempt。
- A7 契约：缺失信息消费面明确Producer/Consumer、观察/修订/待验证操作的相关性、缺失/迟到/失效与预算语义；P2/P3及既有正式codec、公开面/协议/文档/生成物一致；不把provider命令写成World需求。
- A8 仿真与协议校验通过；有界真实样例包含A1/A2的成功及关键失败证据。真实样例安全边界沿原任务授权，未知/危险控件不点；证明本Change范围，不声称完整Settings覆盖或全仓全绿。

## Constraints / Assumptions / Alternatives
- 共享脏现场保护；文档入口快照与480份src/tests源码指纹见evidence/entry，不回退并行工作。产品变更须在实施入口重新核对。
- 复用已有IAssociationStrategy、World reconciliation、不可变revision、当前观察/grounding及验证缝；record/字段与算法在实施前细化，不把候选实现当用户已冻结协议。
- 未选：一次包含所有邻接缺口；只靠Host材料数量触发补证；丢弃已识别结构再全部重采；仅XML成功路径验收。
- 工程细化若需要改变Owner、把预期当观察、放宽授权、跨capture拼当前事实或扩大总预算，返回RESOLVE。

## Verification
```yaml
level: LIVE_SCOPED
method: 实施入口增量双轴评审；严格codec/World/Control/Assurance/Traversal确定性与仿真；协议图与生成物；本任务lease上真实截图/模型/动作样例
expected: A1-A8全部范围内成立；XML与无XML均补证确认归属后独立验证再恢复；实际慢感知high；负向不误恢复且不重复投递
actual: 最终部署XML与无XML均receipt1/verified1/depth1；实际5个匹配DSH请求header为high；受控错session verified0/depth0，真实超时保持未知；Kernel相关17/17、Host相关6/6、DSH high6/6、共享执行场景7/7；全量Simulation257/257、Host406/406、Host.Dsh51/51、插件51/51；协议生成和图审计通过。全量Kernel1028/1029与Agent.Dsh209/212的4项既有失败单独保留，不宣称全仓全绿。
evidence: report.md；review.md；evidence/verification-summary.json；evidence/final-source-hashes.json；evidence/live-high-xml-final/；evidence/live-high-final/no-xml/；evidence/slow-high-final-request-headers.json；evidence/existing-suite-failures.json
```

## Residual risks / 下一步
无本Change范围内阻断。无XML样例为真实截图与实际OCR/YOLO，主动在输入边界不提供XML，不声称设备XML采集物理失败。真实负向包含受控错session及模型超时；超时/迟到/重复/取消完整矩阵以确定性与仿真覆盖。成功导航因局部预算停止在awaiting-decision，不声称完整Settings Goal或滚动穷尽。高强度慢模型可能超过30秒期限，当前保留未知并交既有Recovery策略，不放宽预算。后续独立处理全量既有fixture/Composition Root漂移。未提交、未推送；本任务设备与专用服务已清理，凭据/代理/主Agent默认强度未改。

## Status log
- 2026-10-21 UNDERSTAND→RESOLVE：复验WORLD-SEMANTICS-002的12文件无漂移、当前相关测试17/17通过，核对六步未接通及现有消费缝。
- 2026-10-21 RESOLVE→RESOLVED：用户Q1/Q2推荐、Q3推荐、Q4=A全部确认；核心六步再次明确，无新增待裁决的人歧义。
- 2026-10-21 RESOLVED→PERSIST：范围、Owner、验收与限制落盘；同步术语/基线后续buyer说明和生成索引。当前停在persisted，未进入产品实现。

- 2026-10-21 PERSIST→PLAN：复验HEAD一致，保存implementation-entry完整tracked diff及源码before快照；工程载荷与切片见execution-contract.md。

- 2026-10-21 PLAN→IMPLEMENT：P27/当前capture载荷、World身份消费屏障、Control子预算和原StepVerify恢复、Host语义供货接线进入实现；过程RED保留在evidence/。

- 2026-10-21 IMPLEMENT追加所有者要求：无XML必须慢感知兜底且实际模型请求high。既有Jev样例降为对照，不冒充high验收；沿原DSH provider/model接入同一SemanticPerception供货缝，high限隔离slow请求，主决策默认强度不变。

- 2026-10-21 IMPLEMENT→REVIEW：完成P27、当前capture归属与UI屏障、原StepVerify有界补证、Assurance后Traversal、DSH隔离slow实际high；增量按入口快照审查。修正重绑依据/ID与标题一致性，双轴终审无阻断（review.md）。
- 2026-10-21 REVIEW→VERIFY：最终部署XML及无XML真实high成功，各一次点击/独立验证/depth1；受控错session与实际超时保持depth0。负向矩阵和回归、协议/场景认证证据落盘；初轮Jev/旧二进制/实验故障不计high成功。
- 2026-10-21 VERIFY→CLOSED：Leader核对A1-A8四元组及全部证据边界，范围内通过；保存最终61文件增量与72文件监测指纹，清理本任务lease和专用DSH服务。全仓既有4项失败单独记录；无提交推送。
