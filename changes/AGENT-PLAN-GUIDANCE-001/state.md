# AGENT-PLAN-GUIDANCE-001 — 动作字段与重新观察的产品说明补全
lifecycle_state: closed · disposition: none · depth: standard · base: 7769d11de
diagnosis: {path: evidence/diagnosis.packet.json, sha256: 783b674f47a821031029fa786b875655296e391bde240f79f5372a8674ae2f47}

## Intent（WHAT/WHY）
normal-003的原始提交把返回文字填入targetRole且漏填targetDescriptor；运行时正确拒绝后，Agent已在下一轮修正。实际装配的产品说明未讲清角色与目标描述的区别、通用可选字段与Host要求的区别，也未说明Reobserve会废弃剩余Plan。补全既有行为说明，不将整轮失败归因于单一遗漏。

## Scope / Out of Scope
修改canonical产品payload说明及其DSH同步副本，递增prompt/profile修订号、更新hash引用和活跃装配测试。沿用公开Product Prompt加载/实际section与per-turn挂载、Profile加载和InitializationEnvelope缝验证。只修改本Change及明确拥有的10个产品/测试文件，INDEX经既有生成器更新。
不修改schema、converter、Kernel/World/Grounding/Effect、Host guard、模型绑定、Skill或SIM-037旧输入/Oracle/证据；不改变role-only匹配或Reobserve执行语义。不运行产品模型、服务、模拟器、真机，不提交/推送，不宣称SIM-037通过。

## Decisions
- targetRole取公开元素角色，targetDescriptor取元素文字/语义描述；不把标题、原生ID或文字当角色。候选绑定沿用精确$candidate及当前已声明source；未来页面事实不能预编造。
- targetDescriptor在通用wire中仍可选，不豁免当前Host/policy的要求；说明已有Settings策略要求普通点击、返回和滚动均有目标描述。不给新定位权限、不把通用字段改成必填。
- Reobserve废弃当前Plan所有未执行内容，获取观察后回决策；不会自动执行尾部，后续安排应基于新上下文提交。Checkpoint/ContinuePlan的既有暂停续行含义保持。
- canonical来源为product/prompt/uniagent-prod/；DSH副本逐字节同步。Profile27→28、Prompt26→27，schema与Skill不变。
- 共享checkout已有改动按入口摘要保护；中断期间并行漂移单独记录，不回滚。构建/过程结果使用本任务私有工作目录。

## Acceptance
A1 真实装配的section与per-turn提示包含动作字段区别、Host条件和Reobserve丢弃余项语义；旧说明在针对性检查中为RED，新说明为GREEN。期望取自既有类型/guard/control语义，不以模型响应作期望。
A2 canonical与DSH副本、manifest/hash、Profile引用与revision一致，Profile加载和初始化审计记录使用新装配，已有哈希漂移拒启检查保持。
A3 既有运行时和schema不变；旧normal-003、其他共享改动与非拥有文件受保护。准确保留失败/跳过、审查与资产回执；本片完成不关闭SIM-037。

## Plan
固定入口/诊断→实际挂载检查RED→最小说明及装配修订→针对性Node/Profile/初始化验证→双轴审查→SHA保护及资产收尾。

## Verification
level: CONTRACT + DETERMINISTIC
method: Node既有挂载/工件公开缝，C#公开Profile与初始化记录缝；固定候选源码、输入/输出SHA及共享文件保护核对。
expected: A1–A3
actual: 旧实际section检查1例RED，新候选Node装配/哈希检查9/9、Profile19/19、初始化3/3通过（均零跳过）；Profile28/Prompt27已绑定。双轴审查各0发现，归档解包/消费者及共享保护核验通过，私有构建与临时资产已实际清理。仅本说明修复完成，不宣称模型能力或SIM-037通过。
evidence: evidence/verification.json；evidence/review-disposition.json；evidence/finalization.json；report.md

## Status log
- 2026-10-26 UNDERSTAND：用户授权修改/确定性验证；建立准备目录与入口快照后，按用户指令暂停，未改产品或运行测试。
- 2026-10-27 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户明确恢复并允许简单工作委派；拥有文件无漂移，并行修改保留。按已确认既有语义限定说明补全，使用既有公开缝，不新增接口或执行行为。
- 2026-10-27 PLAN→IMPLEMENT：实际section挂载检查在旧工件上1例失败（缺字段/控制语义），保存red-mount；补两段既有语义说明，更新装配及活跃断言，Runtime/schema不改。
- 2026-10-27 IMPLEMENT→REVIEW：私有源码副本上，Node9/9、Profile19/19、初始化3/3通过；两个Prompt哈希检查和拥有diff空白检查通过，转入两轴只读审查。
- 2026-10-27 REVIEW→VERIFY：独立Spec0、Standards0，拥有10文件候选与实际执行源码一致；归档解包逐SHA及还原后的两份Prompt消费者通过。
- 2026-10-27 VERIFY→CLOSED：31项范围内确定性检查全部通过、零Skip；原normal-003 packet及输入保持。1200份相关源码/工件与10份去重执行程序集保存在已解包校验的归档；任务独占原构建目录已实际删除，324保护项执行期间无漂移。仅关闭本片，不关闭SIM-037，不宣称模型/设备验证或全仓全绿。
