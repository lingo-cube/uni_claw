# PlanTree 输入逐项追溯矩阵

> 静态分解，不是运行结果。UNVERIFIED 表示尚无新高层 PlanTree 的适配证明；旧文档锚点和 JSON status 不用于判 PASS。

## 150 条推理场景

| ID | 场景 | 候选形状/处理位置 | 责任 | 必须证明 | PlanTree结果 |
|---|---|---|---|---|---|
| SR-001 | WiFi 开关单步：找到开关→tap→重看→确认 | Achieve(开关期望态) | 任务策略 | 动作后验证与目标证明 | UNVERIFIED |
| SR-002 | 设置页两级导航：主页→设置→子页 | Sequence(导航阶段) | 任务策略 | 动作后验证与目标证明 | UNVERIFIED |
| SR-003 | 列表滚动寻找目标项 | Traverse(列表发现目标) | 任务策略 | 动作后验证与目标证明 | UNVERIFIED |
| SR-004 | 长列表深度遍历至穷尽 | Traverse(发现穷尽) | 任务策略 | 动作后验证与目标证明 | UNVERIFIED |
| SR-005 | 输入框聚焦并键入 | Sequence(聚焦、输入、校验) | 任务策略 | 文本输入能力不能由tap推导，需具体执行及验证适配 | UNVERIFIED |
| SR-006 | 返回键回到上级并确认容器 | Step(返回并验证) | 任务策略 | 系统返回与工具栏返回需分别验证；当前Android driver无独立PressBack词汇 | UNVERIFIED |
| SR-007 | 期望态已满足→零 Effect 完成 | Observe(已满足则零动作) | 任务策略 | 动作后验证与目标证明 | UNVERIFIED |
| SR-008 | 多步 Golden Path（开app→改设置→验证） | Sequence(开App、设置、验证) | 任务策略 | 动作后验证与目标证明 | UNVERIFIED |
| SR-009 | 截图迟到（界面已变） | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-010 | OCR 把"Wi‑Fi"读成"Wi‑Fi"外字符 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-011 | 目标被手指/ toast 部分遮挡 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-012 | 空帧 / 黑屏输入 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-013 | 低置信度检测 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-014 | vision service 崩溃 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-015 | 推理超时 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-016 | 同屏两次观察不一致 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-017 | 识别出不存在元素（幻觉） | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-018 | 坐标系漂移（分辨率/旋转） | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-019 | 深色模式致模板失配 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-020 | 动态刷新中采样（半渲染帧） | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-021 | 回放与 live 差异超阈 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-022 | 双感知源矛盾判定 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-023 | producer 自报 confidence 虚高 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-024 | 感知正常但 capture time 伪造 | 同一任务计划＋共用观察/补证/停止分支 | 共同执行闭环 | 新鲜观察、补证或停止，不凭置信度继续 | UNVERIFIED |
| SR-025 | 同名"确定"按钮出现两处 | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-026 | 目标元素消失（列表刷新） | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-027 | 滚动后目标位移（bounds 过期） | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-028 | 同构列表项（每行同图标同文案） | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-029 | 弹窗覆盖原目标 | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-030 | 文字部分匹配（"设置"≠"高级设置"） | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-031 | 语义描述歧义（多入口同名功能） | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-032 | 目标处于禁用态 | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-033 | 目标在折叠区未展开 | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-034 | 动画进行中锚点移动 | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-035 | authorization 迟到于 binding 失效后 | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-036 | grounding 返回 Insufficient | 同一任务计划＋当前目标接地分支 | 共同执行闭环 | 唯一当前目标与有效依据，否则零新动作 | UNVERIFIED |
| SR-037 | 未授权动作请求 dispatch | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-038 | Grant 过期后 dispatch 到 gate | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | Grant过期：必须按当前授权机制补证，不沿用旧D/✅作为当前结论 | UNVERIFIED |
| SR-039 | 撤销后未投递 attempt 阻断 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-040 | 不可逆动作（发送/购买/删除）无 Grant | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-041 | 系统权限弹窗出现更宽选项 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-042 | 页面内容声称"已获授权" | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-043 | 越权 effect class 请求 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-044 | safe-stop 进行中收到 dispatch | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-045 | terminal 后迟到 candidate binding | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-046 | human 抢占：用户手动操作中 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 用户抢占检测及在途对账需实证，树结构不负责解决 | UNVERIFIED |
| SR-047 | 同 Contract View 二次激活 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-048 | 合同外目标请求（scope 外） | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-049 | budget 超限继续请求 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-050 | gate 收到失效 judgment 消费 | 同一任务计划＋每步安全/范围执法 | 共同执行闭环 | 授权与作用域逐动作检查；撤销/抢占另外补证 | UNVERIFIED |
| SR-051 | dispatch 后 receipt 迟到 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-052 | UnknownOutcome：结果未知 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-053 | 重启后发现 CommittedPending | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-054 | journal 尾帧 torn | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-055 | 同 effect 重复投递企图 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-056 | 迟到的更正观察（claim 修正） | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-057 | 观察乱序到达 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-058 | 跨 run 迟到反馈 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-059 | journal 文件损坏 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-060 | 进程死亡后同机重开读取 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-061 | 提交失败→零 driver 调用 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-062 | Dispose 后驱动迟到回调 | 同一任务计划＋在途/等待/对账出口 | 执行可靠性 | 结果对账；不盲重发；跨进程续行不得冒充已支持 | UNVERIFIED |
| SR-063 | 义务全满足→Completion | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-064 | receipt 成功但世界未变 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-065 | verified effect 但 mandatory 未清 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-066 | 失败证据充分→Failure | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-067 | 无法继续→SafeStop | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-068 | 证据不足→不猜分类 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-069 | 并发终局提案 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-070 | terminal 精确先验、不复活 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-071 | Runtime Outcome 恰一次 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-072 | provider 宣称完成 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-073 | Control 宣称无剩余工作 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-074 | 无 mandatory 义务的空 completion 企图 | 局部计划结果→既有任务证明链 | 任务证明 | 计划结束、receipt成功、任务完成分别判定 | UNVERIFIED |
| SR-075 | 首屏铸根容器 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-076 | 同屏重看保持容器身份 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-077 | 滚动后内容变化→新屏幕身份 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-078 | 弹窗=新容器且遮蔽原容器 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-079 | dialog 消失回归原容器 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-080 | 重渲染后同实例延续 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-081 | 文件子树作为世界（跨域） | Traverse(文件域；需具体适配) | World与领域适配 | 文件系统跨域反例；需具体文件观察和执行适配，不能计为Android UI支持 | UNVERIFIED |
| SR-082 | 机器人局部地图持续性（跨域） | 地图域策略未适配 | World与领域适配 | 机器人地图跨域反例；当前无真实第二域适配证据 | UNVERIFIED |
| SR-083 | LogicalItem 跨呈现连续 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-084 | 候选被正面证伪（Contradicted） | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-085 | demand 消失→不判 Ended | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-086 | 容器签名多匹配 | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-087 | signature 任何变化=新身份（滚动敏感） | Traverse/任务节点＋World身份适配 | World与领域适配 | 需验证滚动/返回后的身份；已发现Settings返回签名与receipt形状不一致 | UNVERIFIED |
| SR-088 | 旧 revision 不复活为 current | Traverse/任务节点＋World身份适配 | World与领域适配 | revision-local引用与证据支持的连续性 | UNVERIFIED |
| SR-089 | 先发生后证据（回填 provenance） | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-090 | 更正不抹历史 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-091 | 状态开关历史可溯 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-092 | record supersession 链 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-093 | 显式 invalidation | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-094 | 时间相邻不判因果 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-095 | 迟到更正改写 belief | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-096 | 同时编辑冲突并存 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-097 | 版本漂移下旧观察使用 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-098 | 标定漂移下坐标解释 | Owner事实演化输入，不新增历史控制节点 | Evidence与World | 保留历史/冲突；不把相邻当因果 | UNVERIFIED |
| SR-099 | 36 观测开放遍历 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 开放遍历必须证明发现与覆盖条件，不能靠ForEach可嵌套推导 | UNVERIFIED |
| SR-100 | no-progress 判定 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-101 | viewport 穷尽安全返回 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-102 | 咨询预算 16 步撞顶 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-103 | 自适应多轮重定向 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-104 | 每步再验证循环 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-105 | 有界候选穷尽安全停 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-106 | 遍历中弹窗干扰 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 有限展开、覆盖缺口、准确续行和安全中断 | UNVERIFIED |
| SR-107 | 恢复后重入遍历 | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | 同实例恢复也要验证位置、事实及在途动作，不能重放已消费动作 | UNVERIFIED |
| SR-108 | 断点续跑（跨进程） | Traverse＋预算/中断/续行 | 任务策略与执行可靠性 | Plan跨进程恢复超出当前已接受首版承诺，不能以journal存在判通过 | UNVERIFIED |
| SR-109 | 合法激活→自驱开始 | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-110 | 重复激活幂等零副作用 | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-111 | 激活前 Kernel 完全静止 | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-112 | admission ≠ activation | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-113 | contract 显式新 version 取代 | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-114 | terminal 后重用同 contract 拒绝 | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-115 | 单 run 多 observe/act cycle | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-116 | 第二次 emission 企图 | 既有Run激活与终态控制，不新增任务节点 | Run生命周期 | 不重复激活、不在终局后复活 | UNVERIFIED |
| SR-117 | recall 作为 prior 引导假设 | 独立Memory/生命周期依赖或非产品治理 | 单列范围 | Memory是独立适配依赖，不是PlanTree节点结构购买的能力 | UNVERIFIED |
| SR-118 | 历史 EvidenceRef 不建 current claim | 独立Memory/生命周期依赖或非产品治理 | 单列范围 | 历史EvidenceRef不能直接建立current claim | UNVERIFIED |
| SR-119 | 跨 session 目标延续 | 独立Memory/生命周期依赖或非产品治理 | 单列范围 | 跨session延续须按授权和基数规则处理，不由PlanTree扩大范围 | UNVERIFIED |
| SR-120 | 相同摩擦 ≥2 次→LEARNING HOOK | 非产品UI任务 | 单列范围 | 开发Harness学习流程：非产品UI执行适配，单列不计支持率 | NOT_APPLICABLE_GOVERNANCE |
| SR-121 | 跨 run 经验影响观察策略 | 独立Memory/生命周期依赖或非产品治理 | 单列范围 | 跨Run记忆影响需独立能力及契约，不计菜单遍历支持 | UNVERIFIED |
| SR-122 | 历史决策被推翻→ADR supersede | 非产品UI任务 | 单列范围 | ADR治理流程：非产品UI执行适配，单列不计支持率 | NOT_APPLICABLE_GOVERNANCE |
| SR-123 | 伪装确认弹窗诱导确认 | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-124 | 钓鱼页面诱导 Grant 型操作 | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-125 | 截图重放注入 | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-126 | 伪造 CaptureTime | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-127 | 内容声称系统权限 | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-128 | sealed trace 篡改 | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-129 | 未 sealed artifact 入 importer | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-130 | journal 篡改 | 同一任务计划＋完整性/安全拒绝出口 | 共同执行闭环 | 页面内容不是授权；完整性与来源信任分别检验 | UNVERIFIED |
| SR-131 | 对抗弹窗 × 不可逆动作 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-132 | 迟到更正 × 终局后（企图改写） | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-133 | 多候选 × 越权 class | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-134 | 感知幻觉 × 串行屏障 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-135 | journal 重启 × 多候选 pending | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-136 | 弹窗 × 遍历预算耗尽 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 弹窗与预算交叉：必须有组合运行轨迹，不能仅取独立结论合取 | UNVERIFIED |
| SR-137 | human preemption × in-flight attempt | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 抢占与在途交叉：不假设halt等于撤销已投递Effect | UNVERIFIED |
| SR-138 | signature 漂移 × safe-stop | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-139 | 期望态已满足 × 用户已手动完成 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-140 | 双源矛盾 × 终局证明 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-141 | 撤销 × 已投递 in-flight | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 撤销与在途交叉：已投递结果对账，不允许回滚承诺或盲重发 | UNVERIFIED |
| SR-142 | 乱序 × 因果链重建 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-143 | 空洞 completion × 义务检查 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-144 | provider 崩溃 × 遍历中断恢复 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | Provider失联与恢复交叉：有限等待、位置对账和不能续行出口待证 | UNVERIFIED |
| SR-145 | torn 尾帧 × 续跑判定 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-146 | 幻觉元素 × grounding 唯一候选 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-147 | 低置信 × 不可逆动作 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-148 | 多 run 并发企图 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-149 | 同屏变化 × 观察中断续看 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |
| SR-150 | 一切正常但 goal 语义不可验证 | 相关策略与共用执行分支的交叉轨迹 | 组合验证 | 分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹 | UNVERIFIED |

## 28 条场景库条目

| ID | 场景 | 载体 | SR引用 | PlanTree结果 |
|---|---|---|---|---|
| SCN-BARRIER-001 | Two-step serial barrier positive | golden-bundle | SR-104 | UNVERIFIED |
| SCN-BARRIER-002 | Missing middle evidence → barrier blocks second effect | golden-bundle | SR-105 | UNVERIFIED |
| SCN-BARRIER-003 | Post-action contradicts desired state → second effect blocked | golden-bundle | SR-134 | UNVERIFIED |
| SCN-PERC-001 | Fast perception final → verified completion | none | SR-001 | UNVERIFIED |
| SCN-PERC-002 | Fast misjudge → slow corrects → completion | none | SR-009 | UNVERIFIED |
| SCN-PERC-003 | Fast-only misjudge → fail closed, no completion | none | SR-017 | UNVERIFIED |
| SCN-PERC-004 | Irrelevant + exact duplicate evidence → no revision growth | none | SR-012 | UNVERIFIED |
| SCN-PERC-005 | Basis conflict + uncertainty change → grows revision | none | SR-022 | UNVERIFIED |
| SCN-PERC-006 | Slow late arrival → belief revision via claim evolution | none | SR-056 | UNVERIFIED |
| SCN-PERC-007 | Four terminal completion states strictly distinct | none | SR-063 | UNVERIFIED |
| SCN-PERC-008 | Still wrong or conflicting → ambiguity, zero clicks | none | SR-086 | UNVERIFIED |
| SCN-POLICY-001 | P1 immediate termination — policy adopted, first fresh observation already satisfies termination (0 applications) | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-002 | P2 repeated application — claim evolution 24->23->22->20 over two verified applications to policy success | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-003 | P3 no match — ClaimInSet out of direction domain (19) stops policy, reconsult at PolicyInvalidated | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-004 | P4 bounds exhausted — MaxApplications=1 consumed, temp unchanged, reconsult at PolicyInvalidated | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-005 | P5 guard violated — ObservationUnchanged(temp,1) trips after consecutive unchanged rounds (2 applications) | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-006 | P6 guard Unknown — conflicted claim (fixed-scope value change) never satisfies guard, zero dispatch | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-007 | P7 verification failure midway — post-action contradicts DesiredState, existing VerificationFailed transition voids policy | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-008 | P8 fresh-world change between applications — container identity drift invalidates semantic lease, zero new effect | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-009 | P9 forbidden effect class — template EffectClass outside contract rejected by V6d at consultation | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-010 | P10 policy budget exceeds contract — MaxApplications > StepsRemaining rejected by V6c | golden-bundle | 未标注 | UNVERIFIED |
| SCN-POLICY-011 | P11 unknown AST — foreign predicate kind mapped to rogue node through the real seam, rejected by V6a | golden-bundle | 未标注 | UNVERIFIED |
| SCN-WIFI-001 | WiFi switch off→on (happy path) | golden-bundle | SR-001 | UNVERIFIED |
| SCN-WIFI-002 | WiFi already on → zero effect | golden-bundle | SR-007 | UNVERIFIED |
| SCN-WIFI-003 | Duplicate activation → single Primary Run | golden-bundle | SR-047 | UNVERIFIED |
| SCN-WIFI-004 | Missing post-action stimulus → legal waiting | golden-bundle | SR-052 | UNVERIFIED |
| SCN-WIFI-005 | Cancel during wait → SafeStop | golden-bundle | SR-067 | UNVERIFIED |
| SCN-WIFI-006 | WiFi switch off→on (generated, template-equivalent) | golden-bundle | 未标注 | UNVERIFIED |

## 18 个交互需求样例（另一个推导视角，不与前两表相加计覆盖率）

| ID | 需求 | 候选形状 | 适配缺口 |
|---|---|---|---|
| A1 | 全菜单遍历回归（整树进入/返回/记录） | Traverse | 高层Traverse缺失，需动态结构与安全分类证据 |
| A2 | 空调温度 24→20（连按减号） | Achieve | 已有Policy仿真，不等于真实车机温度适配 |
| A3 | 导航「回家」（主页→地图→输入→出发） | Sequence | 文本输入与地图状态适配未证明 |
| A4 | 物理按键回归（音量+ 连按 N 次断言音量条） | Achieve | 物理输入与量化反馈未证明 |
| A5 | 仪表/中控双屏主题切换且两屏一致验证 | Sequence | 窗口字段不等于双屏采集和联合验证已支持 |
| A6 | 驾驶模式限制（车速下禁用项不可点，验证禁用） | Traverse | 检查禁用走观察；负向点击测试需要单独明确授权 |
| B1 | 打开 Wi-Fi（golden path） | Sequence | WiFi实证只限原范围，不验证新PlanTree协议 |
| B2 | 清空通知中心（连续清除/滑动） | ForEach | 清除有状态影响；单独授权与不可清除项退出待证 |
| B3 | 给某人发消息（开app→搜索→输入→发送） | Sequence | 发送授权、目标消歧和执行适配待证 |
| B4 | 表单填写（10 字段逐项输入校验） | ForEach | 字段依赖、错误反馈、敏感值脱敏和提交待证 |
| B5 | 登录含图形验证码 | 语义决策后续行 | 不能凭任意策略叶子声称已适配；需具体感知能力 |
| B6 | 电商翻页找特定商品（滚动→检测→判断循环） | Traverse | 找到即返回匹配证据；购买不继承搜索授权 |
| B7 | 系统升级：等待→重启→验证版本 | Sequence+Await | Await不等于跨进程恢复，升级需另行授权 |
| C1 | 按物理按钮至目标态（电源键循环至灯绿） | Achieve | 跨域推演，无本轮真实适配证据 |
| C2 | 抓取-放置序列（N 物体 pick→place） | ForEach+Sequence | 跨域推演，动作/反馈适配未证明 |
| C3 | 装配：对齐+插入（力反馈引导） | 专用反馈算法候选 | 不放进菜单DFS；无具体执行协议不能计覆盖 |
| C4 | 抓空后重新定位（物体偏移） | 语义决策后续行 | 必须回到新观察和新绑定，跨域适配未证明 |
| C5 | 标定流程（按序 Touch N 个采样点） | ForEach | 跨域推演，点位与反馈适配未证明 |

## UI 定义逐字段（22 节点字段、7 窗口字段）

| 记录 | 字段 | 计划消费义务 | 输入语义 |
|---|---|---|---|
| UiNodeObservation | OccurrenceRef | 当前目标引用；禁止跨capture复用 | 结构/可选特征，不冒充跨revision真值 |
| UiNodeObservation | ParentOccurrenceRef | 页面内结构特征；不是页面导航边 | 结构/可选特征，不冒充跨revision真值 |
| UiNodeObservation | WindowOccurrenceRef | 窗口作用域；Unsupported不可假设单窗口 | 结构/可选特征，不冒充跨revision真值 |
| UiNodeObservation | SiblingOrder | 枚举顺序特征；不是稳定身份 | 结构/可选特征，不冒充跨revision真值 |
| UiNodeObservation | DrawingOrder | 遮挡/次序依据；缺失保持未知 | 结构/可选特征，不冒充跨revision真值 |
| UiNodeObservation | Class | 类型线索；不单独授权或建立身份 | Observed/Unknown/Unsupported |
| UiNodeObservation | ResourceId | 关联/定位线索；不单独建立跨revision身份 | Observed/Unknown/Unsupported |
| UiNodeObservation | Package | 应用作用域证据 | Observed/Unknown/Unsupported |
| UiNodeObservation | Text | 语义线索；不是授权来源 | Observed/Unknown/Unsupported |
| UiNodeObservation | ContentDescription | 语义线索；敏感内容按策略脱敏 | Observed/Unknown/Unsupported |
| UiNodeObservation | Hint | 输入语义线索；Unsupported保持缺口 | Observed/Unknown/Unsupported |
| UiNodeObservation | Checkable | 可选状态能力；不等于允许切换 | Observed/Unknown/Unsupported |
| UiNodeObservation | Checked | 期望态/记录依据；partial与未知不当false | Observed/Unknown/Unsupported |
| UiNodeObservation | Enabled | 动作前提；禁用节点仍可能需要记录 | Observed/Unknown/Unsupported |
| UiNodeObservation | Selected | 选择状态；不等同Checked | Observed/Unknown/Unsupported |
| UiNodeObservation | Focused | 输入焦点与观察前提 | Observed/Unknown/Unsupported |
| UiNodeObservation | Scrollable | 继续发现能力；不能单独证明已穷尽 | Observed/Unknown/Unsupported |
| UiNodeObservation | Clickable | 可点击能力；不能推导navigate或安全 | Observed/Unknown/Unsupported |
| UiNodeObservation | Focusable | 可聚焦能力；不等于输入/提交已支持 | Observed/Unknown/Unsupported |
| UiNodeObservation | VisibleToUser | 可见性依据；Unknown不推Absent | Observed/Unknown/Unsupported |
| UiNodeObservation | Password | 敏感标志；脱敏与输入授权分别执法 | Observed/Unknown/Unsupported |
| UiNodeObservation | Bounds | 坐标空间内定位依据；不当身份或授权 | Observed/Unknown/Unsupported |
| UiWindowOccurrence | WindowOccurrenceRef | 窗口作用域；Unsupported不可假设单窗口 | 结构/可选特征，不冒充跨revision真值 |
| UiWindowOccurrence | Bounds | 坐标空间内定位依据；不当身份或授权 | Observed/Unknown/Unsupported |
| UiWindowOccurrence | Package | 应用作用域证据 | Observed/Unknown/Unsupported |
| UiWindowOccurrence | Title | 窗口语义线索；不能单独建立身份 | Observed/Unknown/Unsupported |
| UiWindowOccurrence | Focused | 输入焦点与观察前提 | Observed/Unknown/Unsupported |
| UiWindowOccurrence | Visible | 窗口可见性；不等于当前可操作 | Observed/Unknown/Unsupported |
| UiWindowOccurrence | DrawingOrder | 遮挡/次序依据；缺失保持未知 | 结构/可选特征，不冒充跨revision真值 |
