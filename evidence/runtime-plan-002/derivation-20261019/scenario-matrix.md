# D1-D16机制：重新推导的逐项矩阵

> 当前源重新解析；只是归属、条件和反例义务。UNVERIFIED不等于已支持，拒绝/暂停也不算任务成功。旧passing和锚点没有转为本次PASS。

## 28条场景库：逐条处理与原期望

| ID | 新机制组合 | 必须保持的行为/缺口 | 原载体 | 原效果数/终态分类 | 实现 |
|---|---|---|---|---|---|
| SCN-BARRIER-001 | 有序交互A/验证A/交互B/验证B；各act可挂检查 | A验证前B零投递，后置handler不能提前；两次效果由原Goal证明；K3,K5 | golden-bundle | 2/Completion | UNVERIFIED |
| SCN-BARRIER-002 | 交互A后缺中间证据，停留等待/受控停止 | 不跑A成功后置、不投递B；等待和终局按原Owner处理；K3,K6 | golden-bundle | 1/SafeStop | UNVERIFIED |
| SCN-BARRIER-003 | 交互A后世界与期望矛盾 | A不被handler返回覆盖为成功，B不投递，缺口/再决策保留；K3,K8 | golden-bundle | 1/None | UNVERIFIED |
| SCN-PERC-001 | 观察目标/交互/新观察/原Goal评估 | 感知事实进入World后再匹配；文本检查不替代Goal；K4,K8 | none | 1/Completion | UNVERIFIED |
| SCN-PERC-002 | Fast错误→补充证据→World更正→重新绑定 | 检查结果与旧观察关联，不把旧通过迁移到新目标；K2,K4 | none | 1/Completion | UNVERIFIED |
| SCN-PERC-003 | 缺少纠正证据，规则/检查未知 | 不因唯一候选或handler存在而点击；原决策失败出口保留；K4,K6 | none | 0/None | UNVERIFIED |
| SCN-PERC-004 | 无关/重复观察不制造新工作 | 去重由Evidence/World保证，handler记录不能靠新ID无限重查；K2,K4 | none | 1/Completion | UNVERIFIED |
| SCN-PERC-005 | 依据冲突→World修订→重评规则和检查 | 冲突与Unknown保留；不得选择后项假装已知；K2,K4 | none | 1/Completion | UNVERIFIED |
| SCN-PERC-006 | 迟到更正→World演化→按当前依据续行 | 旧handler结果保留来源，相关变化使继续条件重评；K2,K4 | none | 1/Completion | UNVERIFIED |
| SCN-PERC-007 | 完成/失败/安全停止/证据不足分别处理 | Traversal Result不是第五种统一成功，也不代替原终局证据；K8 | none | 1/Completion | UNVERIFIED |
| SCN-PERC-008 | 仍错误或冲突→未知/暂停 | 不确定目标零点击；不将Finding当事实纠正器；K4,K6 | none | 0/None | UNVERIFIED |
| SCN-POLICY-001 | 目标态已满足→只读核对→零交互 | 无需为挂handler制造点击；原Goal仍独立证明；K3,K7,K8 | golden-bundle | 0/Completion | UNVERIFIED |
| SCN-POLICY-002 | 复用目标态策略，24→23→22→20，每次动作独立验证 | 同按钮可再次操作，visited不禁止必要重复；每个act有自己的检查输入；K2,K3,K7 | golden-bundle | 2/Completion | UNVERIFIED |
| SCN-POLICY-003 | 目标态规则域外19→无匹配→暂停/再咨询 | 无规则不自动生成动作；不同于选择fallback完成目标；K7,K8 | golden-bundle | 0/None | UNVERIFIED |
| SCN-POLICY-004 | 局部应用预算耗尽→保留消耗和缺口 | 1次后不再动作，不因修订重置预算；K6,K7 | golden-bundle | 1/None | UNVERIFIED |
| SCN-POLICY-005 | 连续无变化→既有停滞守卫→停止推进 | no-progress是失败/缺口，不是发现穷尽或目标已满足；K1,K7 | golden-bundle | 2/None | UNVERIFIED |
| SCN-POLICY-006 | 守卫冲突Unknown→零新投递 | 不能当false跳到其他操作；检查Unknown按声明继续条件处置；K4,K6 | golden-bundle | 0/None | UNVERIFIED |
| SCN-POLICY-007 | 中途验证失败→暂停原执行/再决策 | 成功后置handler不执行；旧Effect不因修订被重放；K3,K5 | golden-bundle | 1/None | UNVERIFIED |
| SCN-POLICY-008 | 两次动作间identity/lease漂移→绑定失效 | 前置通过不沿用旧身份；继续前重检，零新Effect；K2,K3 | golden-bundle | 0/None | UNVERIFIED |
| SCN-POLICY-009 | 采纳前拒绝越权EffectClass | handler引用不能绕过既有拒绝，不能给越权能力换包装；K6 | golden-bundle | 0/None | UNVERIFIED |
| SCN-POLICY-010 | 采纳前拒绝超过Run授权的策略工作额 | 新handler工作也需计入相应资源，不从计划参数获额；K6 | golden-bundle | 0/None | UNVERIFIED |
| SCN-POLICY-011 | 拒绝未知AST/操作/谓词 | 不把任意operation字符串反射成调用；typed接口有限声明；K4,K6 | golden-bundle | 0/None | UNVERIFIED |
| SCN-WIFI-001 | 未达目标→操作switch→独立验证→后置检查 | 点击授权与元素语言检查分别执法，Goal不由handler宣布；K3,K4,K8 | golden-bundle | 1/Completion | UNVERIFIED |
| SCN-WIFI-002 | 当前已on→只读检查，零Effect | 前/后检查挂在只读处理也可工作，不能多切一次；K3,K7 | golden-bundle | 0/Completion | UNVERIFIED |
| SCN-WIFI-003 | 重复激活→沿原Run基数拒绝 | handler/查询不能启动第二个执行链；K3,K6 | golden-bundle | 1/Completion | UNVERIFIED |
| SCN-WIFI-004 | 投递后缺stimulus→等待新观察 | 未验证不推进成功后置handler，也不重发switch点击；K3,K5,K6 | golden-bundle | 1/None | UNVERIFIED |
| SCN-WIFI-005 | 等待时取消→原SafeStop/在途对账 | 取消不倒转已发生Effect；后续handler不得自动续行；K3,K5 | golden-bundle | 1/SafeStop | UNVERIFIED |
| SCN-WIFI-006 | 与001模板同义的新输入路径 | 生成来源不等于运行证明；必须用新机制真实路径重新验证；K3,K8 | golden-bundle | 1/Completion | UNVERIFIED |

原expectations、咨询次数和certification完整保存在inventory；没有执行新机制，不主张保持旧成本。

## 150条SR：当前机制中的义务

| ID | 场景 | 处理位置/决定 | 推导义务 | 范围/缺口 | 实现 |
|---|---|---|---|---|---|
| SR-001 | WiFi 开关单步：找到开关→tap→重看→确认 | 任务操作/阶段/D11-D14 | 目标态操作不是按元素只处理一次；先看是否已满足 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-002 | 设置页两级导航：主页→设置→子页 | 任务操作/阶段/D11-D14 | 操作目标/期望明确；前后handler不替代独立验证 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-003 | 列表滚动寻找目标项 | 任务操作/阶段/D11-D14 | 操作目标/期望明确；前后handler不替代独立验证 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-004 | 长列表深度遍历至穷尽 | 任务操作/阶段/D11-D14 | 发现完备和末端依据尚需typed契约；无新项不等于穷尽 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-005 | 输入框聚焦并键入 | 任务操作/阶段/D11-D14 | 输入能力/敏感数据/输入后验证独立适配，tap不购买输入 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-006 | 返回键回到上级并确认容器 | 任务操作/阶段/D11-D14 | 返回动作与返回位置验证均有真实能力；不存在逆边不能假定可回 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-007 | 期望态已满足→零 Effect 完成 | 任务操作/阶段/D11-D14 | 零交互只读处理也可挂handler，不得额外点击 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-008 | 多步 Golden Path（开app→改设置→验证） | 任务操作/阶段/D11-D14 | 操作目标/期望明确；前后handler不替代独立验证 | CONDITIONAL_OBLIGATION/K3,K7 | UNVERIFIED |
| SR-009 | 截图迟到（界面已变） | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-010 | OCR 把"Wi‑Fi"读成"Wi‑Fi"外字符 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-011 | 目标被手指/ toast 部分遮挡 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-012 | 空帧 / 黑屏输入 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-013 | 低置信度检测 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-014 | vision service 崩溃 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-015 | 推理超时 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-016 | 同屏两次观察不一致 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-017 | 识别出不存在元素（幻觉） | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-018 | 坐标系漂移（分辨率/旋转） | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-019 | 深色模式致模板失配 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-020 | 动态刷新中采样（半渲染帧） | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-021 | 回放与 live 差异超阈 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-022 | 双感知源矛盾判定 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-023 | producer 自报 confidence 虚高 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-024 | 感知正常但 capture time 伪造 | Evidence/World输入/D2,D3,D13 | 观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功 | CONDITIONAL_OBLIGATION/K4 | UNVERIFIED |
| SR-025 | 同名"确定"按钮出现两处 | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-026 | 目标元素消失（列表刷新） | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-027 | 滚动后目标位移（bounds 过期） | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-028 | 同构列表项（每行同图标同文案） | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-029 | 弹窗覆盖原目标 | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-030 | 文字部分匹配（"设置"≠"高级设置"） | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-031 | 语义描述歧义（多入口同名功能） | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-032 | 目标处于禁用态 | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-033 | 目标在折叠区未展开 | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-034 | 动画进行中锚点移动 | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-035 | authorization 迟到于 binding 失效后 | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-036 | grounding 返回 Insufficient | 当前目标绑定/D11,D13,D16 | 目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标 | CONDITIONAL_OBLIGATION/K2,K3,K4 | UNVERIFIED |
| SR-037 | 未授权动作请求 dispatch | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-038 | Grant 过期后 dispatch 到 gate | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-039 | 撤销后未投递 attempt 阻断 | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-040 | 不可逆动作（发送/购买/删除）无 Grant | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-041 | 系统权限弹窗出现更宽选项 | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-042 | 页面内容声称"已获授权" | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-043 | 越权 effect class 请求 | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-044 | safe-stop 进行中收到 dispatch | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-045 | terminal 后迟到 candidate binding | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-046 | human 抢占：用户手动操作中 | 原安全与Run边界/D6,D12,D15 | 用户抢占检测和已投递结果对账，不假定暂停撤销物理动作 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-047 | 同 Contract View 二次激活 | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-048 | 合同外目标请求（scope 外） | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-049 | budget 超限继续请求 | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-050 | gate 收到失效 judgment 消费 | 原安全与Run边界/D6,D12,D15 | handler不授权；逐动作有效授权/范围/预算/gate执法 | CONDITIONAL_OBLIGATION/K6 | UNVERIFIED |
| SR-051 | dispatch 后 receipt 迟到 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-052 | UnknownOutcome：结果未知 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-053 | 重启后发现 CommittedPending | 在途对账/执行可靠性/D6,D13 | 恢复journal与handler位置须共同对账；本轮不承诺跨进程恢复 | RECOVERY_DEPENDENCY/K3,K5 | UNVERIFIED |
| SR-054 | journal 尾帧 torn | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | RECOVERY_DEPENDENCY/K3,K5 | UNVERIFIED |
| SR-055 | 同 effect 重复投递企图 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-056 | 迟到的更正观察（claim 修正） | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-057 | 观察乱序到达 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-058 | 跨 run 迟到反馈 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-059 | journal 文件损坏 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | RECOVERY_DEPENDENCY/K3,K5 | UNVERIFIED |
| SR-060 | 进程死亡后同机重开读取 | 在途对账/执行可靠性/D6,D13 | 同机重新打开进程不等于同一存活Traversal暂停恢复 | RECOVERY_DEPENDENCY/K3,K5 | UNVERIFIED |
| SR-061 | 提交失败→零 driver 调用 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-062 | Dispose 后驱动迟到回调 | 在途对账/执行可靠性/D6,D13 | 投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排 | CONDITIONAL_OBLIGATION/K3,K5 | UNVERIFIED |
| SR-063 | 义务全满足→Completion | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-064 | receipt 成功但世界未变 | Assurance/终局证明/D7,D8,D15 | receipt成功不证明世界改变，后置检查通过也不代替Goal | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-065 | verified effect 但 mandatory 未清 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-066 | 失败证据充分→Failure | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-067 | 无法继续→SafeStop | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-068 | 证据不足→不猜分类 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-069 | 并发终局提案 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-070 | terminal 精确先验、不复活 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-071 | Runtime Outcome 恰一次 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-072 | provider 宣称完成 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-073 | Control 宣称无剩余工作 | Assurance/终局证明/D7,D8,D15 | 无剩余已知工作不证明已知图就是完整世界 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-074 | 无 mandatory 义务的空 completion 企图 | Assurance/终局证明/D7,D8,D15 | handler返回、遍历结束、receipt与Goal满足分别判断 | CONDITIONAL_OBLIGATION/K8 | UNVERIFIED |
| SR-075 | 首屏铸根容器 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-076 | 同屏重看保持容器身份 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-077 | 滚动后内容变化→新屏幕身份 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-078 | 弹窗=新容器且遮蔽原容器 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-079 | dialog 消失回归原容器 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-080 | 重渲染后同实例延续 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-081 | 文件子树作为世界（跨域） | World身份/任务去重/D1-D3,D11 | 文件世界需实际观察/动作/验证适配；不计Android UI支持 | CROSS_DOMAIN/K2 | UNVERIFIED |
| SR-082 | 机器人局部地图持续性（跨域） | World身份/任务去重/D1-D3,D11 | 机器人地图/连续反馈不能仅由菜单节点抽象购买 | CROSS_DOMAIN/K2 | UNVERIFIED |
| SR-083 | LogicalItem 跨呈现连续 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-084 | 候选被正面证伪（Contradicted） | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-085 | demand 消失→不判 Ended | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-086 | 容器签名多匹配 | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-087 | signature 任何变化=新身份（滚动敏感） | World身份/任务去重/D1-D3,D11 | 源码中的World连续性优先；不能把旧压力描述签名变化即新身份当规范 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-088 | 旧 revision 不复活为 current | World身份/任务去重/D1-D3,D11 | 沿用World有依据身份，去重仍须区分任务、上下文及剩余深度 | CONDITIONAL_OBLIGATION/K2 | UNVERIFIED |
| SR-089 | 先发生后证据（回填 provenance） | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-090 | 更正不抹历史 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-091 | 状态开关历史可溯 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-092 | record supersession 链 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-093 | 显式 invalidation | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-094 | 时间相邻不判因果 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-095 | 迟到更正改写 belief | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-096 | 同时编辑冲突并存 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-097 | 版本漂移下旧观察使用 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-098 | 标定漂移下坐标解释 | Evidence/World演化/D2,D3,D13 | 历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估 | CONDITIONAL_OBLIGATION/K2,K4 | UNVERIFIED |
| SR-099 | 36 观测开放遍历 | Traversal发现/暂停/D4-D8,D12 | 新页由既有规则机械展开；累计事实/独立进度/发现覆盖分别有依据 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-100 | no-progress 判定 | Traversal发现/暂停/D4-D8,D12 | 停滞与穷尽分开：no-progress可导致暂停，但非全覆盖证据 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-101 | viewport 穷尽安全返回 | Traversal发现/暂停/D4-D8,D12 | 完整发现、深度、返回、进度保留、预算与结束缺口可核对 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-102 | 咨询预算 16 步撞顶 | Traversal发现/暂停/D4-D8,D12 | 完整发现、深度、返回、进度保留、预算与结束缺口可核对 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-103 | 自适应多轮重定向 | Traversal发现/暂停/D4-D8,D12 | 完整发现、深度、返回、进度保留、预算与结束缺口可核对 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-104 | 每步再验证循环 | Traversal发现/暂停/D4-D8,D12 | 完整发现、深度、返回、进度保留、预算与结束缺口可核对 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-105 | 有界候选穷尽安全停 | Traversal发现/暂停/D4-D8,D12 | 完整发现、深度、返回、进度保留、预算与结束缺口可核对 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-106 | 遍历中弹窗干扰 | Traversal发现/暂停/D4-D8,D12 | 完整发现、深度、返回、进度保留、预算与结束缺口可核对 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-107 | 恢复后重入遍历 | Traversal发现/暂停/D4-D8,D12 | 同实例恢复仍重检操作attempt与handler位置，不从整个act开头重发 | CONDITIONAL_OBLIGATION/K1,K2,K5,K8 | UNVERIFIED |
| SR-108 | 断点续跑（跨进程） | Traversal发现/暂停/D4-D8,D12 | 跨进程续跑是本轮外恢复适配，不由D6推出 | RECOVERY_DEPENDENCY/K1,K2,K5,K8 | UNVERIFIED |
| SR-109 | 合法激活→自驱开始 | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-110 | 重复激活幂等零副作用 | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-111 | 激活前 Kernel 完全静止 | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-112 | admission ≠ activation | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-113 | contract 显式新 version 取代 | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-114 | terminal 后重用同 contract 拒绝 | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-115 | 单 run 多 observe/act cycle | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-116 | 第二次 emission 企图 | Run生命周期/D4-D6 | 只在合法原Run内推进；查询和handler不激活第二Run或复活终局 | CONDITIONAL_OBLIGATION/K3,K6 | UNVERIFIED |
| SR-117 | recall 作为 prior 引导假设 | 独立Memory/治理/D2,D3,D6 | 记忆是prior，不构成当前handler输入事实或权限 | MEMORY_DEPENDENCY/K7 | UNVERIFIED |
| SR-118 | 历史 EvidenceRef 不建 current claim | 独立Memory/治理/D2,D3,D6 | 历史引用不能建立current claim或当前可操作目标 | MEMORY_DEPENDENCY/K7 | UNVERIFIED |
| SR-119 | 跨 session 目标延续 | 独立Memory/治理/D2,D3,D6 | 跨session目标延续需独立授权/恢复/基数契约 | MEMORY_DEPENDENCY/K7 | UNVERIFIED |
| SR-120 | 相同摩擦 ≥2 次→LEARNING HOOK | 独立Memory/治理/D2,D3,D6 | 开发Harness学习流程，不是UI任务执行适配 | NON_UI_GOVERNANCE/K7 | NOT_APPLICABLE |
| SR-121 | 跨 run 经验影响观察策略 | 独立Memory/治理/D2,D3,D6 | 跨Run经验独立适配，不用永久visited跳过新任务检查 | MEMORY_DEPENDENCY/K7 | UNVERIFIED |
| SR-122 | 历史决策被推翻→ADR supersede | 独立Memory/治理/D2,D3,D6 | ADR治理流程，不是UI任务执行适配 | NON_UI_GOVERNANCE/K7 | NOT_APPLICABLE |
| SR-123 | 伪装确认弹窗诱导确认 | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-124 | 钓鱼页面诱导 Grant 型操作 | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-125 | 截图重放注入 | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-126 | 伪造 CaptureTime | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-127 | 内容声称系统权限 | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-128 | sealed trace 篡改 | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-129 | 未 sealed artifact 入 importer | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-130 | journal 篡改 | 来源完整性与权限/D3,D12,D15 | 页面文字/Finding不授权；完整性检测仍依赖独立能力 | CONDITIONAL_OBLIGATION/K4,K6 | UNVERIFIED |
| SR-131 | 对抗弹窗 × 不可逆动作 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-132 | 迟到更正 × 终局后（企图改写） | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-133 | 多候选 × 越权 class | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-134 | 感知幻觉 × 串行屏障 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-135 | journal 重启 × 多候选 pending | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | RECOVERY_DEPENDENCY/K2,K3,K5,K8 | UNVERIFIED |
| SR-136 | 弹窗 × 遍历预算耗尽 | 交叉转移验证/D6,D8,D13-D16 | 弹窗与预算同时触发需组合轨迹；先检查权限/在途而非盲目关闭 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-137 | human preemption × in-flight attempt | 交叉转移验证/D6,D8,D13-D16 | 抢占在途不等于撤回已发生Effect；恢复先对账 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-138 | signature 漂移 × safe-stop | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-139 | 期望态已满足 × 用户已手动完成 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-140 | 双源矛盾 × 终局证明 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-141 | 撤销 × 已投递 in-flight | 交叉转移验证/D6,D8,D13-D16 | 撤销后禁止新投递，已有在途结果保留不能回滚历史 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-142 | 乱序 × 因果链重建 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-143 | 空洞 completion × 义务检查 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-144 | provider 崩溃 × 遍历中断恢复 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-145 | torn 尾帧 × 续跑判定 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | RECOVERY_DEPENDENCY/K2,K3,K5,K8 | UNVERIFIED |
| SR-146 | 幻觉元素 × grounding 唯一候选 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-147 | 低置信 × 不可逆动作 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-148 | 多 run 并发企图 | 交叉转移验证/D6,D8,D13-D16 | 保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-149 | 同屏变化 × 观察中断续看 | 交叉转移验证/D6,D8,D13-D16 | 新观察改变处理前提时重评规则/handler结果，不永久visited | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |
| SR-150 | 一切正常但 goal 语义不可验证 | 交叉转移验证/D6,D8,D13-D16 | Goal不可验证时保持未证明；handler完成不能补足缺失判据 | CONDITIONAL_OBLIGATION/K2,K3,K5,K8 | UNVERIFIED |

## 18个交互样例：任务形状与适配边界

| ID | 需求 | 新机制组合 | 范围 | 尚需证明 |
|---|---|---|---|---|
| A1 | 全菜单遍历回归（整树进入/返回/记录） | Traversal规则选择导航/只读/标记；各操作挂元素检查 | CONDITIONAL_MAPPING | 发现末端、共享入口、返回身份、深度和K1-K6具体契约 |
| A2 | 空调温度 24→20（连按减号） | 既有目标态策略＋重复减号操作及其检查 | COMPOSITION_REQUIRED | visited不能取代目标态循环；温度反馈与滞后判据 |
| A3 | 导航「回家」（主页→地图→输入→出发） | 有序任务阶段＋地图/输入/出发操作 | DOMAIN_ADAPTER_REQUIRED | 地图、键盘和出发语义非菜单DFS提供 |
| A4 | 物理按键回归（音量+ 连按 N 次断言音量条） | 目标态或定次物理输入＋反馈检查 | DOMAIN_ADAPTER_REQUIRED | 物理按键能力和音量条可验证状态 |
| A5 | 仪表/中控双屏主题切换且两屏一致验证 | 主题操作＋双屏关联的联合后验 | DOMAIN_ADAPTER_REQUIRED | 不能把两份独立handler通过直接当同一时刻双屏一致 |
| A6 | 驾驶模式限制（车速下禁用项不可点，验证禁用） | 禁用项只读处理＋状态检查handler | CONDITIONAL_MAPPING | 驾驶上下文需依据；负向点击另需明确授权 |
| B1 | 打开 Wi-Fi（golden path） | 期望态处理；已满足只读，否则交互后验证 | COMPOSITION_REQUIRED | 不得为挂语言检查点击已on开关 |
| B2 | 清空通知中心（连续清除/滑动） | 动态通知集合＋明确授权的清除操作/检查 | COMPOSITION_REQUIRED | 清除会改变集合；不可清除项须列缺口 |
| B3 | 给某人发消息（开app→搜索→输入→发送） | 搜索→目标消歧→输入→发送的阶段计划 | DOMAIN_ADAPTER_REQUIRED | 发送授权和目标身份独立，搜索不继承发送许可 |
| B4 | 表单填写（10 字段逐项输入校验） | 按依赖顺序处理字段，各输入绑定前后检查 | COMPOSITION_REQUIRED | 字段依赖/敏感输入/错误反馈不能靠UI兄弟顺序决定 |
| B5 | 登录含图形验证码 | 验证码识别/语义决策→明确续行 | DOMAIN_ADAPTER_REQUIRED | 缺能力时暂停不算登录成功；无具体适配不可计覆盖 |
| B6 | 电商翻页找特定商品（滚动→检测→判断循环） | 列表发现＋目标匹配；命中后返回证据 | CONDITIONAL_MAPPING | 无限列表未找到不等于不存在；购买不在查找授权内 |
| B7 | 系统升级：等待→重启→验证版本 | 升级操作→外部等待→重启后重建/验证 | BEYOND_LIVE_RESUME_SCOPE | 同一存活执行暂停不保证跨进程恢复 |
| C1 | 按物理按钮至目标态（电源键循环至灯绿） | 目标态重复按物理按钮＋灯色反馈 | CROSS_DOMAIN | 机械域动作/观察适配未证 |
| C2 | 抓取-放置序列（N 物体 pick→place） | 按物体集合组合抓取/放置及反馈 | CROSS_DOMAIN | 物体身份、抓取结果、物理安全适配未证 |
| C3 | 装配：对齐+插入（力反馈引导） | 有界领域反馈控制调用，异常再决策 | CROSS_DOMAIN | 连续力控需要具体策略，不能塞入菜单遍历证明 |
| C4 | 抓空后重新定位（物体偏移） | 抓空→新观察/新绑定→修订后续行 | CROSS_DOMAIN | 旧物体位置失效、在途结果和机械适配未证 |
| C5 | 标定流程（按序 Touch N 个采样点） | 有序采样点处理＋每点校验 | CROSS_DOMAIN | 标定/坐标系/可达性及反馈适配未证 |

## UI逐字段：22节点＋7窗口

| 记录 | 字段 | 新机制输入义务 |
|---|---|---|
| UiNodeObservation | OccurrenceRef | handler目标/观察精确关联；跨capture重定位，不作永久去重键 |
| UiNodeObservation | ParentOccurrenceRef | 本次capture内层级；不能当页面导航或稳定父路径 |
| UiNodeObservation | WindowOccurrenceRef | 处理项与窗口作用域关联；缺能力不假造单窗口 |
| UiNodeObservation | SiblingOrder | 本屏枚举线索；不等于稳定任务顺序/身份 |
| UiNodeObservation | DrawingOrder | 遮挡和次序线索；缺失不猜可点击 |
| UiNodeObservation | Class | 规则类型线索；不单独判定导航/安全 |
| UiNodeObservation | ResourceId | 关联线索；跨revision连续性仍由World提供 |
| UiNodeObservation | Package | 任务/应用/窗口作用域校验；不授权操作 |
| UiNodeObservation | Text | 元素语言handler输入；文字覆盖不等于全像素文字，页面文字不授权 |
| UiNodeObservation | ContentDescription | 可声明文本检查输入，保留来源及敏感处理 |
| UiNodeObservation | Hint | 可选文本输入；Unsupported不宣称已检查 |
| UiNodeObservation | Checkable | 只读/可检查属性；不推出允许切换 |
| UiNodeObservation | Checked | 只读记录/期望态结果；缺失/partial不补false |
| UiNodeObservation | Enabled | 当前操作前提/只读检查；未启用不自动测试点击 |
| UiNodeObservation | Selected | 选择状态检查；不与Checked互换 |
| UiNodeObservation | Focused | 输入/窗口上下文；变化后需重评操作目标 |
| UiNodeObservation | Scrollable | 发现动作线索；不构成末端/穷尽证明 |
| UiNodeObservation | Clickable | 交互线索；不推导navigate或safe |
| UiNodeObservation | Focusable | 聚焦线索；不证明输入driver或提交已支持 |
| UiNodeObservation | VisibleToUser | 当前可见性依据；未知不推Absent |
| UiNodeObservation | Password | 沿已有敏感脱敏规则处理，不为语言检查泄露原值 |
| UiNodeObservation | Bounds | 当前坐标框架定位；检查后漂移须重绑，不当身份 |
| UiWindowOccurrence | WindowOccurrenceRef | 处理项与窗口作用域关联；缺能力不假造单窗口 |
| UiWindowOccurrence | Bounds | 当前坐标框架定位；检查后漂移须重绑，不当身份 |
| UiWindowOccurrence | Package | 任务/应用/窗口作用域校验；不授权操作 |
| UiWindowOccurrence | Title | 窗口语义线索；标题不是唯一身份/授权 |
| UiWindowOccurrence | Focused | 输入/窗口上下文；变化后需重评操作目标 |
| UiWindowOccurrence | Visible | 窗口当前可见性；不证明所有控件可操作 |
| UiWindowOccurrence | DrawingOrder | 遮挡和次序线索；缺失不猜可点击 |

## 16个新反例：专门检验handlers机制

| ID | 压力 | 轨迹 | 所需行为 | 缺口 |
|---|---|---|---|---|
| H01 | 普通规则遮蔽危险节点规则 | 普通项先T，危险项也T→只选首项 | 安全gate仍独立拒绝不合规动作；规则选中不授权 | K6 |
| H02 | 前项Unknown、后项导航True | 首项可能匹配→不能直接选后项 | 保留未知，按约定暂停/报告缺口，不当false | K4 |
| H03 | 单元素语言检查范围不足 | 选A但能力只给整capture汇总通过 | 只有关联到A及字段覆盖的结果才能声称A检查完成 | K4 |
| H04 | 前置通过后目标漂移 | 检查A→弹窗/列表更新→旧坐标 | 重新绑定并走原安全链；旧检查不能授权新对象 | K2,K3 |
| H05 | 动作未验证便观察提交 | 任一新capture提交→误跑后置handler | 先证明该操作完成及验证，不将post-commit等同AfterVerified | K3 |
| H06 | 原元素操作后消失 | 点击A→新页无A→后置仍引用A | 输入缺失保持未知；显式结果目标不能悄悄替换原目标 | K3,K4 |
| H07 | 后置不满足后的恢复 | 点击已验证→检查失败→暂停→恢复 | 从可核对的handler位置继续，已投递act不从头重放 | K5 |
| H08 | 同一共享页不同剩余深度 | root→A→C先到深度2；root→C再到深度1 | 保留浅路径带来的展开机会，否则漏D | K2 |
| H09 | 相同页不同入口检查义务 | 入口X/Y都到同页，只visited页 | 入口文字/导航边义务单独计，不能只检查目标页 | K2,K8 |
| H10 | 虚拟列表重复末屏 | 连续无新项但后续还可加载 | no-progress不能自动作穷尽证据，报告未穷尽 | K1 |
| H11 | 已查状态后来改变 | switch只读检查→外部用户改态→再进入 | 相关版本/状态改变使结果重评，永久visited无效 | K2 |
| H12 | 可选能力缺失与必需能力失效 | 可选无绑定/运行中必需失联 | 前者显式跳过并报告；后者暂停，不自动替换 | K4,K6 |
| H13 | 只读节点挂检查 | 读switch→检查文字/记录Checked | 零点击；缺Checked不补false；记录覆盖范围明确 | K3,K4 |
| H14 | handler隐藏Effect或卡住 | 前置handler调用tap/永久等待 | 有Effect走原链；全部工作受预算/超时，不能绕过 | K6 |
| H15 | 暂停在途并遇取消/撤销 | 已投递→结果未知→取消→尝试续行 | 先对账，取消不倒转已发生动作，不自动重发/续行 | K3,K5 |
| H16 | 全部已知项完成但还有禁入分支 | frontier空＋回首页＋handler全过 | 只报告限定范围成果与未探索，不单独证明全Goal | K1,K8 |

以上清单相互重叠，不合并成支持率。H场景是作者构造的反例需求，不是产品已执行测试。
