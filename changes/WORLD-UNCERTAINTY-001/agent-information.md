# 给Agent的四类未知信息

状态：用户已确定“提供相关信息给Agent”；以下为设计草稿，字段与协议尚未冻结，未实施。最新World聚焦与Q13延期不取消本要求：整体Goal评价接通后续处理，四类缺口经只读投影进入Agent实际咨询输入仍属本期World的必要消费出口，不要求同时改造整个运行系统。

## Agent需要看懂什么

1. 现在已经确认了什么；还有哪个对象、哪个问题不能确定。
2. 不能确定的原因，以及这些依据适用于哪次观察、哪个范围。
3. 哪些当前事项因此受阻，哪些尚不受影响；对应判断来自哪个消费Owner。
4. 实际独立补证尝试总数、结果分布和持续时间，例如“3次：2次超时，1次返回后仍不足”（仅展示形式示例）；这些执行事实不能靠revision次数、重传次数或模型自述推算。Q8已接受，无法取得来源时明确未采集。
5. 当前仍可用的补证/咨询资源及期限，按Control与Runtime既有记录提供，不能因咨询重置。
6. 信息来源及本轮发生的变化：当前相关、仍未解决的问题随每次咨询提供；本轮解决/不再相关的变化简要告知，历史可追溯。Q9已接受，不相关或移出视图不能伪装为已经解决；相关性不明时保留并标明。

Agent收到信息后可以提出补看、换路、暂停或结束等既有决策；任何新观察仍经原证据/World链，动作仍经原Control/Assurance/Effect链。缺口不携带直接许可，World不输出provider命令。

## 人话示例（虚构场景，非运行证据）

当前确认是某个列表页面，这次观察看到了10项；总量和终点尚未确认，因此还不能判断指定列表范围是否已看全。当前任务若要求覆盖整个列表，这一证明仍缺失；若只要求读取已经看到的某一项，是否充分由对应消费Owner判断。补证执行、余量和当前依据引用由实际记录提供，缺字段时写未采集，不填0或猜答案。

## 已核实的实际输入链

- Kernel在NeedDecision阶段获取ProjectExecutionOverview（KernelRunDriver.cs:496），组合上下文给决策接口。
- ExecutionOverviewProjection已有KnownItems/Unknowns/Blockers及SourceFactRefs（ConsultationTypes.cs:141-155）；目前主要为字符串与图投影，并无四类统一的当前缺口载荷。
- SettingsCoverageOverviewProjector.cs:141-151已有页面身份未知和范围未穷尽的字符串摘要；这些是Host投影，不能替代World对当前缺口的派生。
- DshAgentAdapter.cs:153-160将AgentDecisionContext放入DecisionRequest；DshOpenedHttpPeer.cs:187-195序列化完整Context进入正式decision请求。
- dsh/uniclaw-decision-channel/src/index.js:604-634的consultationPrompt直接将request.context JSON加入模型收到的内容。把四类摘要接到正式上下文后存在既有传输/渲染路径，但严格schema、生成物与Prompt耦合仍须同步验证。
- DshSlowConsult.BuildPrompt属于慢感知提案供货，不是Product Agent决策入口；本设计不以它证明Agent已看见未知。
- ContextReferenceIndex/SourceFactRefs仅证明存在索引。本轮未找到在正式DSH决策Agent中按这些引用读取任意缺口详情的既有工具；首期应直接提供足够摘要，按需详情查询若需要则单独明确真实读缝，不假定引用天然可读。
- 动态未知随相关咨询更新；持续未解决则按已接受Q7在安全边界主动复核。availableCapabilities仍只在Run首次咨询提供能力目录，不承载动态缺口。
- 实际尝试的计数必须核对执行来源。现有ObservationRecoveryControl.RequestsUsed是调度子配额，不天然等于模型实际调用次数；NextInput返回等待或中断也不能杜撰成一次有结果的补证。
- 主动提醒按Q10 Accepted A：既有授权内一轮有界补证结束后，到安全边界仍未解决则提醒一次；同一原因没有新结果时不反复请求决策，出现新真实补证结果、受影响事项或相关性变化才允许再次复核。新尝试仍失败也算新结果，普通revision、重传或重复证据不算。问题仍按Q9持续提供，不因提醒去重而隐藏；不新增权限或重置额度。
- 完整Contains/Overlays生产继续沿既有延期边界，有真实消费需求再立切片。缺口解决沿原World证据标准，操作恢复再经Assurance，计数不参与投票真值。

## 拟验收四元组

method：通过真实Owner投影→正式AgentDecisionContext严格codec→decision transport→实际consultationPrompt检查四类摘要与来源；覆盖错capture/旧revision/重复证据/跨分支及余量变化。

expected：Agent实际收到当前相关未知及必要事实，能区分没有依据与没有问题、未采集与零；同因不重复触发、旧信息不当当前、咨询不增加权限或重置资源。

actual：仅完成源码读取与路径核实；未实现、未运行测试或外部模型，不声称真实模型已经接收到新增字段。

evidence：本文件所列源码锚点；未来正式载荷、Prompt输入样本及负向回归证据待实施后提供。
