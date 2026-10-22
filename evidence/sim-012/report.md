# 最近一周录制接入回放（SIM-012）

## 做了什么
将最近一周（上海时间2026-10-16至22）的三条代表录制接入现有公开运行链：SIMs、All apps、Default apps。沿用当前产品Kernel，没有修改产品源码或原录制。

## 原来的问题
三次录制末步均记为verified=true，但原Run并未完成。epoch-002终止于traversal-revision-required，epoch-003为consultation-exception，epoch-009明确记录Default apps前后容器未变、navigation-cycle及不明popup。旧event-trace诊断还记录了三个新页面的控件归属旧容器；它采用旧内部反射路径，不能替代当前公开链路的产品验证。

## 这次修改
`RecentRecordingImportTests.cs`以公开Host parser→typed projector→独立Ledger→UiHierarchyOccurrenceStrategy核对原始XML与冻结导入资产。每条固定原录制preregistration、相邻布局XML、coverage-steps、settings-trace及facts的SHA。Simulation没有引用Host，也没有复制XML parser或使用反射。

`RecentRecordingReplayTests.cs`将冻结typed text/content-description及actionable candidates适配为PageCaptureObservation，通过KernelRunDriver→Ledger→World→grounding→Effect→Assurance。输入没有canonical owner或World ID。身份、Agent决策、Delivery是明确标注的受控替身，固定回放时钟不冒充采集完成时刻。每例受控Plan只做一个前向hop（MaxLocalOperations=1）。

独立expected记录控件、深度、收据数与终态；布局/身份构造不读取expected。SCN-REPLAY-007通过完整参数展示名加入场景发现。

## 验证结果

|阶段|状态|点击收据|验证数|深度|新帧控件归属|
|---|---|---:|---:|---:|---|
|缺少身份|WaitingForInput|1|0|0|ownerless|
|身份capture错误|WaitingForInput|1|0|0|ownerless|
|同Run补齐匹配身份|TerminalNotProven|1|1且verified|1|新页面，与旧页面不同|

三条均有相同行为；旧World revision及未解决revision的序列化快照保持不变。TerminalNotProven表示没有证明整个Goal完成，不能将这次单hop效果验证说成整段遍历成功。

专项Host导入3/3、公开运行链3/3。独立故障注入仅把受控producer的post signature换成旧页signature；固定XML、expected及产品源码保持不变，三条均在`recoveredPage.containerId == previousPage.containerId`处失败。这证明oracle可检出签名误复用，不是旧产品binary的修复前RED。

当前源码独立快照完整验证：八工程2070/2070（Simulation283、Agent36、Agent.Dsh212、Core14、FileSystem9、Host416、Host.Dsh56、Kernel1044），Simulation完整套件只运行一次；35/35场景（20 certified + 15 descriptive），新增三个完整参数名均精确join到同次TRX。产品Host、Simulation及测试使用同一个构建Kernel DLL。完整验证入口与结果见`full.txt`、`module-results.json`、`recent-discovery.json`、`runtime-artifact-check.json`。

首版测试自身的nullable断言错误和超出单hop的返回动作已修正并保留失败日志；没有把它们当作产品缺陷。完整入口首次缺参数及未restore的环境失败也保留，补齐入口条件后才得到PASS。

## 新发现 / 限制
1. 本次纳入三条代表录制，未宣告最近一周全量录制已盘点或自动导入。
2. 原XML没有逐帧墙钟；窗口资格依据原preregistration内容frozenAtUtc与对应Run。前一帧来自上一动作的post，并无独立preActionCapture字段；因此这是相邻原布局切片，未声称整段原始Run精确重放。
3. 正确身份来自受控感知替身；尚未证明真实Host标题提取、跨包popup判别或身份signature生成已修复，也没有重跑原模型。旧签名producer故障仍由独立oracle检出，不能把受控正例当作其产品修复。
4. 旧XML的checked=false仍保持Unknown，未成为关闭状态证明。真实Stay awake补采仍按先前决定另行安排。

## 下一步
应先为真实Host身份生成与popup判别确定公共离线重放入口，消费这些原XML，复现具体producer差异，再在责任模块修复；原模型整段Run回放另需明确输入/决策录制完整性。当前`SettingsTraversalLiveFeed`初始化仍绑定设备采集/服务warmup，不能为离线测试直接实例化或绕到内部方法。这里记录的是下一段工程工作，不把它纳入本次已完成的三条受控消费者回归。
