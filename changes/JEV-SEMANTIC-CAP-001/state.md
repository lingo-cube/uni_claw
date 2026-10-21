# JEV-SEMANTIC-CAP-001 — 可替换语义感知执行协议与Jev实现
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent / Scope
当前会话负责Semantic Perception执行载荷、L2执法与Jev供货实现；World图关系/Slice/边已按用户授权另建会话01a15a5f-bc04-7b92-93e2-7ec00a397854，以WORLD-SEMANTICS-002独立实现并联合验收。设计阶段先grill，实施以U5为准。

2026-10-21新增实施授权：用户要求进入实现并将上面两项联合验收。原“暂不实现”是此前设计阶段约束，本轮已被替代；以现有插件替换为目标，不另拆调用方业务接线Change。World会话已收到继续实现任务，限定只改World/关联及其测试；本会话负责协议/感知/Jev与共享文档。

## 已确定的用户指令
- U1（用户，2026-10-21）：语义感知协议补充+Jev实现，与World消费侧分开处理。
- U2（用户，2026-10-21）：先设计方案，暂不实现。
- U3（用户，2026-10-21）：必要时组合grilling与domain-modeling；World侧单独会话。
- U4（用户，2026-10-21，修订Q5的范围理解）：自动遍历与World关联应通过替换既有能力插件使用新实现，不另拆调用方业务接线；World相关另立独立Change。后续用户澄清“放WORLD相关的一起就行”，归组见../WORLD-CHANGES.md。
- U5（用户，2026-10-21）：进入实现，语义能力与World关联联合验收，继续保留独立Change、共同消费协议和证据。

## 当前事实与约束
- 入口原状：ISemanticPerception是协议marker；本轮补充兼容执行载荷。文本语义属于Fast YOLO/OCR+Slow Text组合，Jev是realization，不另立模型专属Product协议。
- 现有SlowConsultationRequest冻结且不携带expected value/model hint；RequiredClaim仅Subject+Field；P22禁止ControlIntent/expected page，模型输出不得直接写World或建立身份/导航边。
- 若设计允许Jev读取明确预期，需要明示意图与观察的协议分区/结果消费规则并评审冻结；不能塞Reason或JSON字符串绕行。
- PER-022已有所有者裁决：两个Fast strategy接口与感知载荷冻结合并收为internal；设计时保留，U5实施授权后本轮执行，公开live构造入口保留。
- 近期实验：明确前页/元素类型/动作/预期后4页原15道无歧义题全对，另历史落地4/4、构造对照8/8；小样本、预期提示、人工语义输入和单次响应限制均保留。
- 共享工作区大量其他修改；本轮逐文件保存进入前内容/diff，只改本Change及共享必需登记，World由独立会话拥有。

## 第一轮裁决（用户2026-10-21答复：按推荐）
| ID | 当前问题 | 推荐方向 | 所有者答案 |
|---|---|---|---|
| Q1 | 首版判定粒度/任务范围 | 页面用途与页面级前后语义变化，控件细粒度另期 | Accepted，用户，2026-10-21 |
| Q2 | 例外/白名单如何表达 | 首版不加全局豁免；需要的例外由任务显式声明并留痕 | Accepted，用户，2026-10-21 |
| Q3 | 预期来源及是否参与Jev判断 | 调用方声明为意图可参与；观察结论与符合性评估分开，先评审协议修订 | Accepted，用户，2026-10-21 |
| Q4 | 自相矛盾的语义判断如何处理 | 保留有依据的独立结果，冲突的符合性判断标冲突/不确定，不挑选有利答案 | Accepted，用户，2026-10-21 |
| Q5 | 首期宿主接入到什么程度 | 可注册/可调用/可替换+历史回放和真实线路验证 | 用户2026-10-21后续明确：自动遍历/World关联通过既有能力插件替换，不另拆业务接线工程；World相关独立Change归组 |

## Assumptions / Alternatives
第一轮方向获用户接受后，经U4修订消费范围、U5授权实施；工程载荷明确于execution-contract.md。传输复用不代表所有慢感知共享相同题型。实际页面类别准确性仍属实验性质，不能从接口/入账测试推导模型判断必然正确。

## Q5范围修订（以后续U4为准）
- 自动遍历消费：维持既有Traversal消费协议，通过组合/配置替换语义能力实现使用Jev；不另创建“自动遍历接线”Change，不把provider名、HTTP载荷或新判断分支散入调用方。
- World关联消费：维持既有World身份/关系所有权和插件消费方式；World侧对关联职责、契约缺口和替换实现单独设计，见WORLD-SEMANTICS-002。语义provider不直接写身份或造边，不另造Slice图。
- “可替换”是设计与验收约束，不是已证明当前所有缝均充分；若源码核验发现既有协议/替换点不足，在各自Change显式记录和裁决，不能把业务改造包装成配置替换。
- 已有WORLD-SEMANTICS-001是完成的职责检查与证据记录，不重复建检查档；后续World设计另立WORLD-SEMANTICS-002。按用户后续澄清，将World相关Change集中列入../WORLD-CHANGES.md，不移动、合并或关闭其他记录。

## Verification
level: ENVIRONMENT
method: L1注册/调用/替换与输入执法确定性测试→Jev HTTP协议/失败测试→实际Jev历史布局回放→P2/World身份/边联合测试→既有回归与scenario再认证
expected: 意图不被投影为事实；缺输入/错帧/迟到/冲突诚实处理；供货替换不改调用方；World实际Owner保持；真实Jev线路和构造对照分开证明
actual: 组件实现和联合验收完成；13项Kernel语义契约+3项World联测+1项公开面检查通过；Host400/400、Simulation250/250、DSH兼容3/3；Kernel1011/1014，3项既有范围外RED保留。真实.NET Jev最终12例用途11/12、关系12/12、符合性11/12+1Conflicted、P2→Slice12/12且身份不变/无导航边。双轴评审阻塞项已修复复查，scenario 28文件0违规。详见report.md和verification-summary.json。
evidence: evidence/jev-semantic-cap-001/；execution-contract.md；../WORLD-SEMANTICS-002/

## Implementation Plan
1. 公开Semantic Perception执行载荷与UniPerception可替换执行缝；先通过注册/调用及输入约束测试。
2. Jev Host Adapter与显式组合，使用注入的配置/绑定/HTTP，不读取Harness凭据；失败分类和真实源标题校验。
3. 只投影当前语义观察，保留父证据及叶闭包；World按已验证Owner路径消费，配合另一会话联合验收。
4. PER-022收层及公开面/基线/CONTEXT同步；回归、实际Jev回放、代码评审和scenario认证后据实际结果关闭。

## Acceptance / 四元组
| 行为 | method | expected | actual / evidence |
|---|---|---|---|
| 执行与输入执法 | SemanticPerceptionCapabilityTests | 注册调用；错帧/过期/取消/超时/重复诚实，冲突分离；禁止当前先验/128KiB请求界 | 13/13；green-review-input.log（另含3项World+1项公开面） |
| Jev适配及R5替换 | JevSemanticPerceptionTests | HTTP/schema/model错误零观察；两种供货走同一L1消费方 | 9/9；green-jev-expanded.log；Host全量400/400 |
| 跨组件关联 | SemanticCapabilityWorldIntegrationTests + .NET replay | 仅观察进P2；World补语义不冒充身份/边；迟到不贴新页 | 确定性3/3；真实12/12；green-joint-contract.log/live-results.json |
| 外部供货 | 新.NET adapter + 历史/构造布局 | 线路/输出可检查，观察/关系/预期分开，不宣称生产准确率 | 最终12成功；用途11/12，关系12/12，符合性11/12+1Conflicted；live-v2/summary.json；初版保留live-summary.json |
| 公开面与PER-022 | whitelist + build | 新载荷公开，两内部strategy接口移出；live入口保留 | whitelist通过；green-joint-contract.log |

## Residual risks / 新发现
- 真实线路错例p04-constructed_other：App notifications列表被识别为通知总览purpose_02；标题仍为App notifications，初版confidence0.64、最终0.76仅诊断。最终p13-historical识别对但符合性回答自相矛盾，被L2标Conflicted。入账/关联正确不等于语义准确，均保留请求/响应，不按阈值或重试抹除。
- 4历史页+8构造对照、同一开发集、单次响应、前页/元素类型人工标注且提供预期，非独立准确率或真机遍历验收。
- 新实现提供相同注册/调用/替换缝与示例；没有擅自替换运行中DSH供货配置，也未启动设备或修改设置。既有旧Slow管线保持兼容，其具体消费需由组合根选择新执行载荷。

## Status log
- 2026-10-21 UNDERSTAND→RESOLVE：用户授权分会话职责检查和必要设计访谈；世界模型会话已创建并确认启动。本地读取grill-with-docs及其grilling/domain-modeling技能（当前工具集无独立Skill调用器，按文件组合执行），首轮所有可问前沿落档；等待用户回答，不实施。
- 2026-10-21 RESOLVE：用户答“按推荐”，Q1-Q5记为Accepted；用户另询问自动遍历/World关联含义，本轮解释二者的消费职责及首期范围。继续设计，不因接受首轮方向进入实现。
- 2026-10-21 RESOLVE修订：用户纠正消费接入应为能力插件替换，撤回“自动遍历/World另做业务接线”的解释；World检查已有WORLD-SEMANTICS-001，后续单立WORLD-SEMANTICS-002，与另外两会话Change的归组成员待用户提供。仅文档，无实现。
- 2026-10-21 RESOLVE归组：用户澄清按World相关统一放一起，WORLD-CHANGES.md导航清单已建立，归组问题已解决；Q5按插件替换设计，本轮未改产品。
- 2026-10-21 RESOLVE→PERSIST→PLAN：用户新增实施与联合验收授权；execution-contract.md明确已接受语义约束的工程载荷与分工，11文件内容/diff恢复快照落盘；World独立会话开始继续实施。进入垂直契约测试与最小实现，不重问已接受Q1-Q5。
- 2026-10-21 PLAN→IMPLEMENT：L1执行载荷/L2关联预算去重/Host Jev adapter/P2 projector落地；RED→GREEN包含缺类型、缺投影以及冲突分支错误保留SamePage的真实反例。PER-022、公开面、源码登记、词汇/组件基线同步；新.NET真实回放保留单个类别误判。
- 2026-10-21 IMPLEMENT→REVIEW→IMPLEMENT：规范轴无阻塞；需求轴F1当前标签先验/F2无总字节界被反例证明，修复后13项契约及Host9项通过，独立复查两项均消除；字段与128KiB界同步合同/基线。改变请求分区后重跑真实线路，保留前一轮及新增冲突结果。
- 2026-10-21 IMPLEMENT→REVIEW→VERIFY：双轴评审完成；Host400、Simulation250、DSH兼容3通过，Kernel1011/1014，仅保留三项既有/并行范围外失败；World源码12文件冻结哈希无漂移。20个有执行载体场景刷新、28文件认证检查0违规，期望/其他场景内容无改动。
- 2026-10-21 VERIFY→CLOSED：本次组件范围及与WORLD-SEMANTICS-002的联合验收完成，证据与六段交付见report.md。3个全仓失败明确归因而非抹除；语义误判/冲突为已披露实验限制，不升级为权威。未改变运行中DSH配置、未操作设备、未提交推送。
