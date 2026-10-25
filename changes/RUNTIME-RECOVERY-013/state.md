# RUNTIME-RECOVERY-013 — Control原遍历操作与完整状态历史
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3

## Intent / Scope
接012完成的Run事实，补Control自己拥有的原Traversal路径/候选分配/返回义务与队列的原操作历史及只读查询。不改变选择/剪枝算法，不以Run投影反灌Control。用户先前标记的路径优化仍在012/deferred-path-optimization.md，待恢复链路完成另行设计。

## Decisions
- 真实buyer：进程中断后查询原Control栈/分配、返回目标、规则/版本/调用序号、暂停/发现状态和待发布进度，不能从World图或Run已完成记录猜原控制现场。
- ControlLoop继续唯一拥有TraversalControl；每个原完整变更操作（ConfigureRules/Start/Select/Navigated/Returned/ReadOnlyCompleted/Pause/Resume/Abandon/Fail/Gap/Drain）在候选副本执行，先保存原输入/结果/完整状态，再发布；嵌套Gap/Pause归外层操作，不拆成提前确认。
- 新IControlTraversalJournal为显式原来源缝，Host文件adapter与内存测试adapter可替换；保存完整且版本化的原snapshot，不反射私有字段。只读读取不构造可执行Control、不重跑Select，不签发旧Intent或恢复Effect许可。
- 原Run身份/RecordingId及Control录制源必须可追溯；原Runtime身份归档版本化保存Control源GUID，旧归档无源明确不可用。只读Control历史查询与四方共同点分开，不宣称五方执行一致或续跑。
- 公共Driver/Control只读查询/journal/原文件读取为本轮测试缝；既有导航测试辅助只能注入来源，不增setter。持久故障在Control锁存，Kernel/Driver正常阻断，不能继续typed调用或Effect。

## Acceptance
1. 所有原Traversal变更入口都按完整操作确认，路径/候选/规则/返回/调用ordinal/发现/暂停/待发布队列及原输入、选择结果可只读查回；原算法与Run核心/执行事实语义不变。
2. 各操作确认前不发布，写前/后ack失败保持原发布状态并持续阻断，不重试原工作、typed检查或外部Effect；Select预留不误写成Run完成，Drain不误写成Run已确认。
3. 原身份/Run源/Control源、连续schema/序列/head/EOF与结构依赖严格核验，缺源/无初始化/非法尾部拒绝；原nonterminal进程实际Kill后新reader保留原栈/返回义务/队列及源SHA，旧归档不默认空现场。
4. 相关公开缝回归、双轴review、四元组与可复验记录齐备；非owned输入与012旧封存保持，无当前World/Assurance、Control Intent/其余Control子域、Runtime完整游标、Task/期限恢复、激活续跑或路径优化实现。

## Verification
level: DETERMINISTIC + SCENARIO
method: public Driver/Control historical query + original journal/file adapters + subprocess Kill
expected: A1–A4
actual: Host254/Kernel472/DSH5范围回归与定向64通过；两个实际nonterminal Kill位于原Control提交后/Owner收到ack前，每个两个新reader，原Run/Control GUID/身份、原输入/结果/19状态与Frame7项/队列/源SHA一致。确认前不发布，写入失败不启动typed/Effect的正负对照通过；旧无源/非法初始化/结果/work依赖/ordinal/head/EOF拒绝。Standards硬规范0/启发式0，Spec可行动项0；最终verify-before-close exit 0，23候选原输入/副本SHA、613非owned输入及010/011/012封存均保持。A1–A4完成；无五方共同执行点、Runtime游标、Task/期限或continuation/路径优化实现。
evidence: evidence/

## Status log
- 2026-10-24 UNDERSTAND→RESOLVE→PERSIST→PLAN→IMPLEMENT：核实Control实例确归ControlLoop，原Traversal所有变更仅内存；012封存125文件保持、现HEAD与dirty输入已锁。确定整操作候选确认、原源关联与只读查询范围；其余Control子域和Runtime游标延期。
- 2026-10-24 IMPLEMENT→REVIEW：20个owned文件及3个证据工件锁定23候选，原19状态/Frame7字段/12变更入口核验通过。公开Driver/原源只读查询通过；初始化造现场、选择结果/工作引用/ordinal的显式RED已转绿。Host254、Kernel472、DSH5回归通过；选择正对照typed1/Effect1与写前/写后ack失败两种typed0/Effect0。两个真实非terminal Kill均在原Control提交后/Owner收到ack前，各两新reader；原select预留和navigated子栈/返回/未决队列可查，未补造Run完成。独立verify-first exit 0；非owned与010/011/012封存保持，进入双轴评审。
- 2026-10-24 REVIEW→VERIFY：独立Standards硬规范0/启发式0；独立Spec缺失/扩域/实现错误0；23冻结SHA均匹配，候选未变，无待处置项。评审与验证分开，现作最终原源/证据核验，不重复已绿测试。
- 2026-10-24 VERIFY→CLOSED：最终verify-before-close.log exit 0，12完整变更入口/19原状态/Frame7项、两次实际非terminal Kill与四独立reader、原身份/源/head/EOF及源SHA、三组范围回归和保护清单匹配。四元组与失败尝试保留，无设备/模型/live/提交推送。下一工程动作：记录Runtime实际Plan采纳/版本/完整执行位置及接收Control工作的位置，随后再处理其余恢复缺口；不以本片历史读取开启续跑。路径剪枝按012已有标记延期到恢复链路完成后设计。
