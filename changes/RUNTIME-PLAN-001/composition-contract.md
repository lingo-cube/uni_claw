# RUNTIME-PLAN-001 — 条件与有界控制组合修订（2026-10-18）

性质：在D1-D10与ADR-0053内收敛HOW；Owner已授权直接推进，并要求结合UI与图灵机理解提高Kernel泛化。首片实现契约保留为历史，本文件规定本次增量。

## 执行模型

- 借用图灵机的“控制状态＋运行数据＋可判定转移”理解执行；不以图灵完备作为验收，不开放任意代码/跳转或无限存储。既有Driver仍为唯一推进者。
- UI是可变化的页面/控件图，不是预先固定的菜单列表。观察、已验证进入/返回与控件角色由原Owner提供；执行位置只描述程序进度，不能冒充UI事实。
- 增加IfItem(Condition, Then, Else)和RepeatItem(Body, MaxIterations)。Condition仅引用当前装配公布的名称；Owner以Satisfied/Violated/Unknown和来源证据回答，可针对当前候选判断。Unknown回Agent，不走Else；该判断不授予Effect许可。
- 静态有限块可组合If/Repeat/ForEach及既有叶操作；不支持自引用、动态递归、任意表达式。总节点/嵌套深度与局部展开工作量有界。不是另起行为树/状态机平台。
- Driver内部保存块帧，暂停后保留分支、重复次数、候选与父执行位置。当前Position增加确定的ControlPath，同版本ContinuePlan必须精确引用；工作量/历史不重置。旧线性与单层ForEach位置含义保持。
- 当前候选绑定穿过条件/重复块，内层ForEach可遮蔽并在结束后恢复父绑定。每次循环重新读Owner候选；不把未来页目标提前定位。结构推进和条件求值都消耗工作量。
- 动作依旧经当前guard、Control、定位、Assurance、Effect与独立验证；等待原Effect对账时不切换/重发。记录控件使用既有Observe语义，不切换开关、不自动声称功能已测。

## 本次验收及测试缝

沿用已批准的公开KernelRunDriver/RunDriverInputs、正式codec/Schema与Settings逐动作guard/journal缝，不新增测试私有实现。

1. 一个Plan在运行中观察到事实变化后选择不同分支；条件Unknown不发动作，条件缺证/未知名称拒绝或回Agent。分支必须来自Plan，不藏进场景固定流程。
2. 有界重复中套用条件及候选处理，运行中出现的新候选本地处理；检查点暂停/WaitingForInput保留完整父位置，错误ControlPath拒绝，继续不重复已确认动作。
3. Settings当前页候选读取既有Owner事实，允许范围内的新子页入口可被同一组合处理；返回根页后继续其他入口。开关/记录项与导航项区分，禁止项保留阻碍；无证据的未知页面不自动当已完成。
4. 结构不同的记录场景用同一核心运行另一种条件/重复组合，不在核心增加UI/记录特判。
5. 正式wire、schema/d.ts、Prompt双副本和hash同步；非法结构、无界/过深/循环对象、未知操作拒绝；旧合法Plan继续兼容。
6. 核心与Host/正式通道相关回归串行；Review/Verify分开。设备/模型结果另留ENVIRONMENT证据，未跑或失败如实保留。旧性能对照不作为本次新语义收益结论。

## 确定与待验证

Condition接口为只读投影，带当前候选参数及证据，不新增Capability或事实权威。具体UI条件目录与候选范围根据现有Settings配置/账本提供，不能以“非禁止”推断未知动作已被许可。若买方证明需要改变已接受权限、递归或恢复边界，再针对实际冲突回RESOLVE。

入口快照：`/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-plan-composition-ox56792x/`；149个相关文件副本、tracked patch与全工作区status已保存；不清理共享修改、不提交推送。
