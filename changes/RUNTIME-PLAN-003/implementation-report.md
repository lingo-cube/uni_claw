# 执行协议与异步 handler 交付

## 做了什么

同一份Plan现在可执行同步或真正延迟完成的typed语言handler。调用等待时可查询Run记录，完成后自动接回原操作/handler位置；前置顺序、独立动作验证与后置顺序保持。

## 原来的问题

driver用Task.Run后同步Wait，没有Running调用状态。TypeScript把必填null生成unknown，正式文档另有两处非法null。单看Task返回不能证明持续异步执行成立，还需Host和Simulation接回同一个driver。

## 这次修改

- ProtocolSchema生成器正确表达null，正式文档示例通过Schema。
- RunDriverInputs提供语言专用异步缝；原同步接口保留线程池兼容。ILanguageInspector业务协议不变，无新能力/Registry/万能Execute。
- KernelRunDriver提供DriveAsync；Drive仍可恢复，未完成时返回WaitingForHandler。两入口共享串行门闩，竞争调用不重复推进；查询读取Run immutable视图。
- PlanLanguageHandlerCall封装完成交接、单调完成时间、协作取消及迟到异常观察。Run按callId维护Running到单一终态；暂停继续重检使用新callId，不重发已验证动作。
- SettingsCoverageRunner及TraversalLanguageInspection、SimulationHost实际接线。Schema/TypeScript/DSH镜像/Prompt/Profile引用、状态机投影、索引及词汇同步。

## 验证结果

隔离基线六套C#1795/1795：Kernel962、Simulation220、Agent.Dsh201、Host350、Agent29、Host.Dsh非Live33。DSH Node45/45运行于同源schema48eedf及prompt20。新增异步仿真4项已在220内，不重复计数。

共享目录六套1858/1858，包含并行WRC新增场景，测试期间Kernel/Agent源码无漂移；其生成Schema按当前World类型为e18399、Prompt/Profile修订21，身份见protocol/shared-final-identities.json。两环境使用同一份本Change运行时代码，差异与原始结果见evidence/runtime-plan-003/verification.json。

异步测试实际覆盖：慢检查Running可查、只读零Effect、有序续行、验证后才启动后置、继续不重复调用/扣费、双向截止、超时/取消迟到不写、requirePass独立及并发入口拒绝。独立Standards/Spec最终均无确认未处置问题，方法/预期/实际/证据按A1-A6落盘。

## 新发现

实现中发现并修复：optional与requirePass组合可能绕过继续条件；可选非阻断绑定异常错误暂停；成功完成时间未正确发布；异步入口可能并发推进。均保留RED/GREEN证据。本地调用截止衡量typed结果交接，不将调度/交接超时误报为业务不符合或宣称provider物理未完成。

初次隔离副本混入两份并行World文件，后续又遇路径别名缓存问题；均只纠正自有验收副本，失败日志保留。两处精确修订号断言从19随装配修订更新，未放宽断言。并行World源码及其职责登记由原任务处理，本Change未接管。

## 下一步

本Change已CLOSED，进入RUNTIME-PLAN-004：结构化UniAgent职责、UI Skill生成流程与真实生成评估。当前结果限于协议及确定性仿真，不包含真实模型/设备结论；跨进程恢复、并行handler及provider内部完成时间均不在本次承诺内。未提交推送。
