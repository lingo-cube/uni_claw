# RUNTIME-PLAN-004 — UniAgent计划生成、Profile与UI测试Skill
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: bce0ec3a

## Intent（WHAT/WHY）

Owner于2026-10-20授权分两个Change顺序实施。本Change承接Q20-Q26已讨论的生成/Profile/Skill契约，让UniAgent依据任务、当前World事实和已提供目录生成可交给Kernel执行的PlanTree或精确Act，并通过独立期望检验真实生成行为。

## Scope / Out of Scope

范围：结构化Profile职责定义/loader；同源指导装配；UI自动化Product Skill指导流程；校验反馈和有界修订；场景适配矩阵及协议、真实Kernel链仿真、真实UniAgent生成三层验收。实施顺序依赖RUNTIME-PLAN-003通过验收。

范围外：重新设计Kernel权威、固定未来页面脚本、为目录齐全预造策略/能力、多Skill选择平台、降低独立测试期望、跨平台无条件完备声明。

## Decisions

- Q20-Q26语义依据：[集中契约](../RUNTIME-PLAN-002/generation-profile-skill-contract.md)、[生成讨论](../RUNTIME-PLAN-002/generation-discussion.md)、[Profile/Skill讨论](../RUNTIME-PLAN-002/profile-skill-discussion.md)。本轮Owner授权解除这些文档此前“仅讨论、等待开启”的限制。
- Profile定义稳定职责与规划原则，Product Skill定义UI领域指导，Task定义本次目标/独立期望/授权/预算，协议定义精确载荷；各有唯一源。
- 目录首轮提供，反馈与进度按阶段/策略提供；简单明确任务允许Act，不要求所有任务写循环或多轮咨询。
- 不编造来源、能力或未知页面，不修改目标/授权/测试期望以换取通过；恢复不从头重放已执行动作。

## Acceptance

- A1：Profile稳定语义职责可解析且同源装配到真实请求；modelRoles不冒充语义职责；Skill正文引用正式协议且无重复权威。
- A2：场景矩阵覆盖动态菜单、开关只读、已满足零Effect、缺能力不编造、未知/危险节点不放行、简单Act、拒收修订保持期望；每项说明适配/限制/缺口。
- A3：正式协议合法/非法载荷与producer→codec→consumer、真实Kernel链仿真通过。
- A4：实际UniAgent生成采用验收前预注册的固定模型/输入/独立期望、重复次数和数值门槛；原始输出、拒收/修订、失败统计、消耗保留，不以脚本Agent或注入hash代替。
- A5：PlanTree+Act覆盖声明限于所选有限有界场景及真实适配；独立Review与相关回归通过，文档/hash/引用同步。

## Constraints / Owner-Authority impact

依赖003完成后再派发产品实现。量化门槛、具体字段和生成装配技术点在003稳定后核对并在调用前持久化；未知数值不伪造成已冻结。原Capability/Kernel/Run/World/Control/Assurance/Effect职责不变，真实模型配置不静默替换。

## Assumptions / Alternatives / Residual risks

沿用已确认单primary Skill与配置钉扎；不新建选择平台。三层验收不能互相替代。真实生成可能暴露表达/适配缺口，须如实保留并按Owner边界处理，不宣称数学意义覆盖全部UI测试。

## Verification

verification:
  level: ENVIRONMENT
  method: "003验收后冻结实施计划与真实生成评估注册；分别运行协议、Kernel链仿真、实际UniAgent生成评估"
  expected: "A1-A5满足，原始输出及独立期望可审计，覆盖结论限定真实场景集合"
  actual: "A1-A5通过：Profile25/Prompt24/Skill4，Flash enabled/low；真实首稿20/21、最终21/21，每类3/3；21份实际模型输出经正式codec和Kernel仿真全部通过；独立审计236项源无漂移、目标/授权未改、必需原始证据齐全；双轴review无未解决发现"
  evidence: "evidence/runtime-plan-004/verification.json；results.md；generation/epoch-007/batch-assessment.json；generation/epoch-007/kernel-consumer-assessment.json；generation/epoch-007/leader-evidence-audit.json；review-4/"

## Status log

- 2026-10-20 · UNDERSTAND→PERSIST · Owner已授权顺序推进，承接既定Q20-Q26；待003验收后进入本Change PLAN/IMPLEMENT。

- 2026-10-20 · PLAN→IMPLEMENT · 003通过，入口快照完成，字段和门槛冻结，分派三项有边界工作。

- 2026-10-20 · IMPLEMENT · Profile v2及同源identity完成，公共Load缝17/17；Product Skill3及实际插件回归45/45。专用3081重载，当前Schema/Prompt22/Skill3真实握手与scope通过，未调用模型；生成评估与真实产物消费者仍在完成。

- 2026-10-20 · REVIEW→VERIFY · Profile charter防原地修改、实际DSH工件钉扎、Skill正式协议引用、拒绝理由独立判据已修复。Agent 36/36、Agent.Dsh 206/206、Host.Dsh非Live 40/40、Simulation非生成消费246/246、Node 48/48；双轴review当前0未解决。正式调用前核对DSH真实session.v4取证结构，修正后冻结epoch-002；当前真实模型调用0。

- 2026-10-20 · VERIFY→IMPLEMENT · epoch-002真实预检通过，正式调用前取证源码交接竞态被hash拒收；epoch-003暴露评估工具遗漏显式Detach（0模型调用），通过现有peer.DetachAsync补上自身会话清理并独立review通过。epoch-004预检通过、正式21次完成，原始首稿17/21、最终17/21未达门槛；消费者5项3通过2失败，动态样本局部预算12不足，执行5个Effect后未回根页。完整失败留存。另有两项oracle false-negative待按语义修正，不能重写旧批结果；Prompt23/Profile24补明确字段及本地预算指导，门槛和case保持不变，后续另冻结新epoch。

- 2026-10-20 · IMPLEMENT→REVIEW→VERIFY · epoch-005原始首稿18/21、最终19/21失败，消费者3/5通过：真实损坏JSON被泛化成no-submit造成修订缺失错误原因；只读样本超一次修订且伪造obs锚，动态14本地额度仍5/6Effect未回根页。Prompt24/Profile25采用既有本Plan默认额度128的有界规划指导，明确无trace锚则省略completion；评估断言以假锚反例收紧，RED1fail→GREEN8/8。DSH adapter只将实际turn/end错误加入既有一次nudge，不补JSON/不扩重试；新测RED→GREEN1/1、原nudge/fence2/2，双轴review4均0。Host45/45、Agent36/36、Kernel本地2/2通过，epoch-006冻结并已通过真实预检，正式21运行中。

- 2026-10-20 · VERIFY→CLOSED · epoch-006同源off配置仍首18/21终19/21失败，动态实例已能完整完成；在已有授权范围，仅把宿主Flash思考切enabled/low。epoch-007的236项源、输入和门槛与006相同，真实参数已在请求头取证；首20/21、终21/21、每类3/3，真实Kernel消费21/21、consumer5/5，24个Effect回执及12例零Effect。唯一Schema拒收后1次修订成功，目标/原始反馈/授权/证据审计通过，费用unknown。当前相关回归Profile36/36、Host45/45、Node49/49，场景认证28/0违规，双轴review0遗留。四轮正式批次共84咨询和5次成功预检保留，未重写失败。A1-A5四元组详见verification.json；Scope完成，无阻塞Human Decision。
