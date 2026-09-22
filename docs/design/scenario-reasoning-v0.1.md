> Status: DRAFT / CANDIDATE
> Authority: NONE
> 日期: 2026-09-22 · 方法: 场景参数扫描 × 等价类推理（静态；引用既有测试锚点，未新增运行）
> 谱系: 与 `capability-coverage-derivation-v0.1.md`（VNext 场景库 × 实跑 × 接口）同族；
> 执法对照 `docs/analysis/invariant-enforcement-matrix.md`（47 行）；
> 定理层论证见 2026-09-22 会话（T1–T6），本文件是其场景实例化层。

# 场景推理 v0.1 —— 150 实际场景中的可靠性与设计正确性论证

## 0. 方法论与诚实声明

三层论证栈：**定理层（归纳证明）→ 矩阵层（执法审计）→ 场景层（本文件：撞击与反例猎取）**。

- 场景**不能证明**可靠性——那是定理与测试的职责。场景做两件只有它能做的事：
  ①把定理实例化为具体世界事件，检验公理到现实的**映射是否成立**；
  ②主动猎取反例（F8 已产出过真实反例并喂给了 buyer——这正是本法有效的证据）。
- **等价类方法**：150 场景由 7 轴参数网格系统枚举；深度推理在 13 个等价类（C1–C13）
  上进行，类成员共享转移结构与不变量压力，仅参数增量不同。150 篇定制散文会退化为
  样板文，等价类才是参数扫描的数学诚实版本（决策 D1）。
- 锚点记号：`✅file` 既有可执行测试；`◐登记` 既有登记缺口（F8/F9/恢复编排）；
  `D` 基线 DEFERRED（矩阵 DEFERRED 行）；`NB` NO_REAL_BUYER。

## 1. 场景空间七轴

```text
X1 环境行为   稳定 / 漂移 / 突变(弹窗·切走) / 对抗(伪装)
X2 感知可靠性 完整 / 部分 / 错误 / 迟到 / 缺失
X3 绑定歧义   唯一 / 多候选 / 无候选 / 候选失效
X4 权限压力   已授权 / 未授权 / 撤销 / 越权 / 不可逆
X5 生命周期   激活前 / 执行中 / 验证悬挂 / 终局前 / 终局后
X6 时间       同步 / 迟到回执 / 迟到更正 / 乱序 / 重启
X7 历史      首见 / 重现 / 更正 / 冲突
```

覆盖准则（由构造保证，守护脚本查结构）：47 条不变量每条至少被一个场景的压力轴命中；
T1–T6 每条至少被两个等价类实例化；三个登记缺口与三条 DEFERRED 各有专属场景组。

## 2. 场景枚举（150 项，13 组）

### G1 基线正常流（C1 · 8 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-001 | WiFi 开关单步：找到开关→tap→重看→确认 | X1稳定 X3唯一 | ✅HostLiveFullTests |
| SR-002 | 设置页两级导航：主页→设置→子页 | X1稳定 | ✅GoldenScenarioBundles |
| SR-003 | 列表滚动寻找目标项 | X1稳定 | ✅GoldenScenarioBundles |
| SR-004 | 长列表深度遍历至穷尽 | X1稳定 X5执行中 | ✅LOOP-001 系 |
| SR-005 | 输入框聚焦并键入 | X1稳定 | ✅词表内（DSE-003） |
| SR-006 | 返回键回到上级并确认容器 | X1稳定 | ✅PressBack 词表 |
| SR-007 | 期望态已满足→零 Effect 完成 | X5执行中 | ✅DesiredStateSatisfactionTests |
| SR-008 | 多步 Golden Path（开app→改设置→验证） | X1稳定 X6同步 | ✅DeterministicScenarioTests |

### G2 感知异常（C2 · 16 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-009 | 截图迟到（界面已变） | X2迟到 | ✅FreshnessEnforcementTests |
| SR-010 | OCR 把"Wi‑Fi"读成"Wi‑Fi"外字符 | X2错误 | ✅EvidenceToBeliefTests |
| SR-011 | 目标被手指/ toast 部分遮挡 | X2部分 | ✅UIWorldAssociationTests |
| SR-012 | 空帧 / 黑屏输入 | X2缺失 | ✅FailClosedScenarioTests |
| SR-013 | 低置信度检测 | X2部分 | ✅FastScreenArmContractTests |
| SR-014 | vision service 崩溃 | X2缺失 | ✅VisionServiceHostTests |
| SR-015 | 推理超时 | X2缺失 | ✅VisionServiceClientTests |
| SR-016 | 同屏两次观察不一致 | X2错误 X7重现 | ✅WorldModelCanonicalOracleTests |
| SR-017 | 识别出不存在元素（幻觉） | X2错误 | ✅EvidenceToBeliefTests |
| SR-018 | 坐标系漂移（分辨率/旋转） | X2错误 | ✅DescriptorMatcherDriftTests |
| SR-019 | 深色模式致模板失配 | X2部分 | ✅PER-008 管线 |
| SR-020 | 动态刷新中采样（半渲染帧） | X2部分 | ✅post-action 观察策略 |
| SR-021 | 回放与 live 差异超阈 | X2错误 | ✅LOOP-001 twin |
| SR-022 | 双感知源矛盾判定 | X2错误 X7冲突 | ✅ClaimEvolutionTests |
| SR-023 | producer 自报 confidence 虚高 | X2错误 | ✅ObservationIngressTests |
| SR-024 | 感知正常但 capture time 伪造 | X2错误 X4对抗 | ◐（anti-spoof trust policy 登记项） |

### G3 绑定与接地歧义（C3 · 12 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-025 | 同名"确定"按钮出现两处 | X3多候选 | ✅UIWorldGroundingSeamTests |
| SR-026 | 目标元素消失（列表刷新） | X3无候选 | ✅GroundingView 面向 |
| SR-027 | 滚动后目标位移（bounds 过期） | X3候选失效 | ✅DescriptorMatcherDriftTests |
| SR-028 | 同构列表项（每行同图标同文案） | X3多候选 | ✅UIWorldContinuityTests |
| SR-029 | 弹窗覆盖原目标 | X1突变 X3候选失效 | ✅遮挡=新容器（F7 世界面） |
| SR-030 | 文字部分匹配（"设置"≠"高级设置"） | X3多候选 | ✅Matcher 语义 |
| SR-031 | 语义描述歧义（多入口同名功能） | X3多候选 | ✅Candidate Binding 不下穿 |
| SR-032 | 目标处于禁用态 | X1稳定 X3唯一 | ✅Admissibility 面 |
| SR-033 | 目标在折叠区未展开 | X3无候选 | ✅ObservationNeed 内部 |
| SR-034 | 动画进行中锚点移动 | X3候选失效 | ✅PostAction 观察 |
| SR-035 | authorization 迟到于 binding 失效后 | X3候选失效 X6迟到 | ✅Outcome12 同构 |
| SR-036 | grounding 返回 Insufficient | X3无候选 | ✅ProductAssociationStrategyTests |

### G4 权限与安全（C4 · 14 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-037 | 未授权动作请求 dispatch | X4未授权 | ✅ControlToEffectTests |
| SR-038 | Grant 过期后 dispatch 到 gate | X4撤销 | D（Grant Phase 6） |
| SR-039 | 撤销后未投递 attempt 阻断 | X4撤销 | D（Grant） |
| SR-040 | 不可逆动作（发送/购买/删除）无 Grant | X4不可逆 | D（Grant + commit point） |
| SR-041 | 系统权限弹窗出现更宽选项 | X4越权 | D（A47 检测 Phase 6/7） |
| SR-042 | 页面内容声称"已获授权" | X4越权 X1对抗 | D（untrusted 隔离） |
| SR-043 | 越权 effect class 请求 | X4越权 | ✅Effect Gate 面 |
| SR-044 | safe-stop 进行中收到 dispatch | X5终局前 | ✅Outcome11 同构 |
| SR-045 | terminal 后迟到 candidate binding | X5终局后 | ✅Outcome11/12 |
| SR-046 | human 抢占：用户手动操作中 | X1突变 X4 | ◐（preemption 检测 Phase 6/7） |
| SR-047 | 同 Contract View 二次激活 | X5激活 | ✅KernelRunDriverTests |
| SR-048 | 合同外目标请求（scope 外） | X4越权 | ✅Admissibility |
| SR-049 | budget 超限继续请求 | X5执行中 | ✅Run 预算面 |
| SR-050 | gate 收到失效 judgment 消费 | X4撤销 X6迟到 | ✅Freshness 判决时效 |

### G5 时间异常与可靠执行（C5 · 12 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-051 | dispatch 后 receipt 迟到 | X6迟到回执 | ✅Outcome12 |
| SR-052 | UnknownOutcome：结果未知 | X6 | ✅DispatchSeamSpecificationTests |
| SR-053 | 重启后发现 CommittedPending | X6重启 | ✅ReliableExecutionJournalRestartTests |
| SR-054 | journal 尾帧 torn | X6重启 | ✅ReliableExecutionJournalTests |
| SR-055 | 同 effect 重复投递企图 | X6 | ✅LinkRetry=新 Attempt 语义 |
| SR-056 | 迟到的更正观察（claim 修正） | X6迟到更正 | ✅ClaimEvolutionTests |
| SR-057 | 观察乱序到达 | X6乱序 | ✅Temporal/Causal ref 面 |
| SR-058 | 跨 run 迟到反馈 | X6迟到 | ◐（恢复编排 Host 后继） |
| SR-059 | journal 文件损坏 | X6重启 | ✅torn=未成功语义 |
| SR-060 | 进程死亡后同机重开读取 | X6重启 | ✅Commit Boundary 测试 |
| SR-061 | 提交失败→零 driver 调用 | X6 | ✅execution-commit-failed fail-closed |
| SR-062 | Dispose 后驱动迟到回调 | X6迟到 | ✅迟到不复活（Delivery Closure 面） |

### G6 终局与证明（C6 · 12 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-063 | 义务全满足→Completion | X5终局前 | ✅Outcome1 |
| SR-064 | receipt 成功但世界未变 | X2错误 X5 | ✅Outcome2 |
| SR-065 | verified effect 但 mandatory 未清 | X5终局前 | ✅Outcome4 |
| SR-066 | 失败证据充分→Failure | X5终局前 | ✅Outcome5 |
| SR-067 | 无法继续→SafeStop | X5终局前 | ✅Outcome6 |
| SR-068 | 证据不足→不猜分类 | X5终局前 | ✅Outcome7 |
| SR-069 | 并发终局提案 | X5终局前 | ✅Outcome8 |
| SR-070 | terminal 精确先验、不复活 | X5终局后 | ✅Outcome9 |
| SR-071 | Runtime Outcome 恰一次 | X5终局后 | ✅Outcome10 |
| SR-072 | provider 宣称完成 | X1对抗 X5 | ✅Outcome14 |
| SR-073 | Control 宣称无剩余工作 | X5终局前 | ✅Outcome15 |
| SR-074 | 无 mandatory 义务的空 completion 企图 | X5终局前 | ✅Outcome18 |

### G7 身份与连续性（C7 · 14 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-075 | 首屏铸根容器 | X7首见 | ✅UIW-005 S1 |
| SR-076 | 同屏重看保持容器身份 | X7重现 | ✅ProductAssociationStrategyTests |
| SR-077 | 滚动后内容变化→新屏幕身份 | X7 | ✅逐字节权衡（UIW-005 D2） |
| SR-078 | 弹窗=新容器且遮蔽原容器 | X1突变 | ✅F7 世界面 |
| SR-079 | dialog 消失回归原容器 | X7重现 | ✅signature 匹配 |
| SR-080 | 重渲染后同实例延续 | X7重现 | ✅B 类（重渲染场景） |
| SR-081 | 文件子树作为世界（跨域） | X7 | ✅FileSystemRealizationTests |
| SR-082 | 机器人局部地图持续性（跨域） | X7 | NB（无第二运行时域买家） |
| SR-083 | LogicalItem 跨呈现连续 | X7重现 | ✅UIWorldContinuityTests |
| SR-084 | 候选被正面证伪（Contradicted） | X7冲突 | ✅ContinuityResolutionOutcome |
| SR-085 | demand 消失→不判 Ended | X7 | ✅LogicalItem 语义 |
| SR-086 | 容器签名多匹配 | X3多候选 | ✅Ambiguous 词汇 |
| SR-087 | signature 任何变化=新身份（滚动敏感） | X7 | ◐F8（归一化 buyer 已到） |
| SR-088 | 旧 revision 不复活为 current | X7 | ✅revision 单 current |

### G8 发生语义与证据演化（C8 · 10 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-089 | 先发生后证据（回填 provenance） | X6 | ✅A 类（门开关历史） |
| SR-090 | 更正不抹历史 | X7更正 | ✅CLE-001 supersede |
| SR-091 | 状态开关历史可溯 | X7 | ✅A 类 |
| SR-092 | record supersession 链 | X7更正 | ✅CORE-016 S3 |
| SR-093 | 显式 invalidation | X7更正 | ✅同上 |
| SR-094 | 时间相邻不判因果 | X6 | ✅Temporal/Causal 区分 |
| SR-095 | 迟到更正改写 belief | X6迟到更正 | ✅ClaimEvolutionTests |
| SR-096 | 同时编辑冲突并存 | X7冲突 | ✅Conflict 记录（D 类） |
| SR-097 | 版本漂移下旧观察使用 | X7 | ✅D 类（版本漂移） |
| SR-098 | 标定漂移下坐标解释 | X2错误 | ✅D 类（标定漂移） |

### G9 遍历与决策形状（C9 · 10 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-099 | 36 观测开放遍历 | X1稳定 X5 | ◐F9（决策形状缺口） |
| SR-100 | no-progress 判定 | X5执行中 | ✅Traversal 面 |
| SR-101 | viewport 穷尽安全返回 | X5 | ✅F10 单步屏障 |
| SR-102 | 咨询预算 16 步撞顶 | X5执行中 | ◐F9 |
| SR-103 | 自适应多轮重定向 | X1漂移 | ◐F9（演进证据已到） |
| SR-104 | 每步再验证循环 | X6同步 | ✅TwoStepBarrierTests |
| SR-105 | 有界候选穷尽安全停 | X3无候选 | ✅SafeStop 面 |
| SR-106 | 遍历中弹窗干扰 | X1突变 | ◐F7+F9 叠加 |
| SR-107 | 恢复后重入遍历 | X5 | ◐恢复编排 |
| SR-108 | 断点续跑（跨进程） | X6重启 | ◐恢复编排 |

### G10 激活与基数（C10 · 8 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-109 | 合法激活→自驱开始 | X5激活 | ✅KernelRunDriverTests |
| SR-110 | 重复激活幂等零副作用 | X5激活 | ✅Invariant 44 测试 |
| SR-111 | 激活前 Kernel 完全静止 | X5激活前 | ✅P1 Non-Activation |
| SR-112 | admission ≠ activation | X5激活前 | ✅同上 |
| SR-113 | contract 显式新 version 取代 | X5 | ✅Contract View 语义 |
| SR-114 | terminal 后重用同 contract 拒绝 | X5终局后 | ✅generation 语义 |
| SR-115 | 单 run 多 observe/act cycle | X5执行中 | ✅cycle 数不改变基数 |
| SR-116 | 第二次 emission 企图 | X5终局后 | ✅Outcome10 |

### G11 记忆与历史影响（C11 · 6 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-117 | recall 作为 prior 引导假设 | X7 | D（Memory 未实现） |
| SR-118 | 历史 EvidenceRef 不建 current claim | X7 | D（同上） |
| SR-119 | 跨 session 目标延续 | X5 | ✅显式拒绝（基数 1:1:1） |
| SR-120 | 相同摩擦 ≥2 次→LEARNING HOOK | X7 | ✅uniflow A8 |
| SR-121 | 跨 run 经验影响观察策略 | X7 | D（Memory） |
| SR-122 | 历史决策被推翻→ADR supersede | X7 | ✅ADR 机制（实跑多次） |

### G12 对抗与完整性（C12 · 8 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-123 | 伪装确认弹窗诱导确认 | X1对抗 | ✅untrusted ≠授权源（D 面深化） |
| SR-124 | 钓鱼页面诱导 Grant 型操作 | X1对抗 X4不可逆 | D（Grant Phase 6） |
| SR-125 | 截图重放注入 | X1对抗 | ✅IntegritySha256 面 |
| SR-126 | 伪造 CaptureTime | X1对抗 X2 | ◐（trust policy 登记项） |
| SR-127 | 内容声称系统权限 | X1对抗 | D（A47） |
| SR-128 | sealed trace 篡改 | X1对抗 | ✅seal/import fail-closed |
| SR-129 | 未 sealed artifact 入 importer | X1对抗 | ✅fail-closed（§24.9） |
| SR-130 | journal 篡改 | X1对抗 | ✅torn/integrity 语义 |

### G13 交叉组合（C13 · 20 项）

| # | 场景 | 压力轴 | 锚点 |
|---|---|---|---|
| SR-131 | 对抗弹窗 × 不可逆动作 | X1对抗 X4不可逆 | D（Grant+A47 交叠） |
| SR-132 | 迟到更正 × 终局后（企图改写） | X6迟到更正 X5终局后 | ✅Outcome13 |
| SR-133 | 多候选 × 越权 class | X3 X4 | ✅Admissibility 面 |
| SR-134 | 感知幻觉 × 串行屏障 | X2错误 X6 | ✅屏障阻第二动作 |
| SR-135 | journal 重启 × 多候选 pending | X6重启 X3 | ✅未决发现+re-ground |
| SR-136 | 弹窗 × 遍历预算耗尽 | X1突变 X5 | ◐F7+F9 |
| SR-137 | human preemption × in-flight attempt | X1 X4 | ◐preemption+对账 |
| SR-138 | signature 漂移 × safe-stop | X7 X5 | ✅SafeStop 证据化 |
| SR-139 | 期望态已满足 × 用户已手动完成 | X1 X4 | ✅DesiredState 零 Effect |
| SR-140 | 双源矛盾 × 终局证明 | X2错误 X5 | ✅Conflict 不静默 |
| SR-141 | 撤销 × 已投递 in-flight | X4 X6 | D（Grant 对账语义） |
| SR-142 | 乱序 × 因果链重建 | X6 | ✅Temporal/Causal |
| SR-143 | 空洞 completion × 义务检查 | X5 | ✅Outcome18 |
| SR-144 | provider 崩溃 × 遍历中断恢复 | X2缺失 X5 | ◐恢复编排 |
| SR-145 | torn 尾帧 × 续跑判定 | X6重启 | ✅未成功≠损坏 |
| SR-146 | 幻觉元素 × grounding 唯一候选 | X2错误 X3唯一 | ✅binding 绑 revision |
| SR-147 | 低置信 × 不可逆动作 | X2部分 X4不可逆 | D（Grant 前置信门槛） |
| SR-148 | 多 run 并发企图 | X5 | ✅at-most-one Run |
| SR-149 | 同屏变化 × 观察中断续看 | X7 X6 | ✅revision 单 current |
| SR-150 | 一切正常但 goal 语义不可验证 | X5终局后 | ✅Undetermined⇒NeedsFollowUp |

## 3. 等价类推理（C1–C13）

每类：代表场景 → 系统走法（转移迹）→ 数学论证 → 哲学审计 → 判定。

### C1 基线正常流（SR-001~008）
**走法**：observe→admit→reconcile(rev₁)→intent→candidate→canonical binding→judgment Sufficient→gate→dispatch→receipt→post-action observe→Verified Effect→obligations 清→Completion→emission→closure。
**数学**：全链每步对应 T1 授权见证与 T2 串行屏障的基例；SR-007 是 Desired-State Satisfaction（ADR-0017）的决策零动作路径——act 不使能即零 Effect，T1 空真。
**哲学**：信任的全部重量压在"验证后行动"上——平常路径也不豁免证明义务（Effect≠Completion）。
**判定**：✅ 全锚定（live/replay/golden 三形态齐）。

### C2 感知异常（SR-009~024）
**走法**：producer 输出降级为低质量 raw artifact → admission 只查结构完整性（不查真值）→ World Model 按 relevance 决定是否入 belief → Confidence 永不升级 truth（A11）→ 判不了就 Insufficient/Ambiguous，fail-closed。
**数学**：T3 的见证语法在源头执法：X2 任何取值都改变不了 Witness 谓词的语法闭包；SR-016/022 冲突走 Conflict 记录（A18），不静默覆盖 ⇒ 归纳步保持。
**哲学**：可谬论的制度化——感知不是叛徒，是不可全信的证人；系统对证词的怀疑不靠怀疑人格，靠管辖权分割。
**判定**：✅ 15 项 + ◐1 项（SR-024 CaptureTime 伪造 = anti-spoof trust policy，矩阵已登记）。**哲学与数学在 SR-024 分岔**：数学上 CaptureTime 是 provenance 输入可被伪造，哲学上"时间不可伪证"的信任假设已显式登记为扩展面——诚实。

### C3 绑定歧义（SR-025~036）
**走法**：多候选→Ambiguous 不铸身份；无候选→NoCandidate（≠KnownAbsent）；失效→binding validity 随 revision 派生判定终结，唯一后继 re-observe→re-ground。
**数学**：A24（Candidate≠Canonical）保证歧义无法变成权威；SR-035 授权迟到于失效 = Authorization 复合态解体（三合取任一失即不成立）⇒ T1 保持。
**哲学**：歧义是世界的常态而非错误；系统的回答是"承认看不清"，绝不 forced pick——认识论谦卑落在词汇表层面（四值判别，无 score）。
**判定**：✅ 全锚定。

### C4 权限与安全（SR-037~050）
**走法**：未授权请求→gate 拒绝（不重判）；越权 class→admissibility fail-closed；terminal/safe-stop 中→Delivery Closure 拒绝；重复激活→幂等返回同一 Run。
**数学**：T1 的使能条件在此组全部走"拒绝分支"——证明的强度恰恰在反例路径：每条反路径的使能条件都被证伪 ⇒ 不可达。SR-038~042 是 D 面（Grant/A47），矩阵 DEFERRED 行一一对应，**无虚报**。
**哲学**：许可先于权力；系统能做 ≠ 系统可做。不可逆动作的 commit point 留给 Grant——伦理学上"不可逆"是特殊范畴，配特殊仪式。
**判定**：✅8 + D5 + ◐1（SR-046 preemption 检测）。D 面是设计正确性的**显式欠条**而非漏洞。

### C5 时间异常与可靠执行（SR-051~062）
**走法**：UnknownOutcome→唯一合法后继 re-observe；重启→journal 未决发现（无 ID 发现：CommittedPending/Dispatched）→重试=新 Attempt 同 Effect / 补偿=新 Effect；torn 尾帧=该提交从未成功。
**数学**：Commit Boundary 定义了"可恢复读取"的边界谓词；SR-061（提交失败零 driver 调用）是 fail-closed 的基例；T2 归纳在重启点依然成立——journal 是跨进程的验证义务存续证明。**SR-058 跨 run 迟到 = ◐ 恢复编排，唯一未闭合时间环**。
**哲学**：时间是可靠性的第一敌人；系统不假装同步世界，它把"不知道"铸成持久记录。append-only 是对历史的承诺：发生过的事只可被解释，不可被抹去。
**判定**：✅11 + ◐1（SR-058）。

### C6 终局与证明（SR-063~074）
**走法**：Outcome1–18 已是 18 个可执行终局判例，此处逐一映射场景；核心四反例：receipt≠完成（064）、effect≠完成（065）、宣称≠完成（072/073）、空洞≠完成（074）。
**数学**：T3+T4 的完整实例化区；SR-069 并发提案→exact-prior single-winner；SR-068"证据不足不是分类成员"= 证明系统可靠性的直接体现。
**哲学**：终局诚实是全系统的道德底线——宁可 Undetermined/NeedsFollowUp，不伪造任何一个终局分类。"知道停下"与"知道达成"同级重要。
**判定**：✅ 全锚定（18 判例可执行）。

### C7 身份与连续性（SR-075~088）
**走法**：association 四值判别；occurrence revision-local；LogicalItem 由 ContinuityDemand 购买 eligibility、evidence 建立；provider node/detection id 只是证据永不铸身份。
**数学**：A17/19/20（无并列 current truth、projection 派生）；SR-087 是 **F8 真实反例**：逐字节 signature 权衡在滚动连续性上付出代价——系统按设计走"新身份"安全侧，迭代 buyer 已登记。
**哲学**：同一性是形而上学最硬的问题；系统的回答是 operational 的——身份=可判别性的函数，判别不了就诚实地 New/Ambiguous。反例（F8）的存在恰证明设计会**被现实校准**而非僵死。
**判定**：✅12 + ◐1（F8）+ NB1（SR-082 机器人域：无第二运行时域买家，按 ADR-0026 不预造）。

### C8 发生语义与证据演化（SR-089~098）
**走法**：更正→supersession 新记录（历史保留）；冲突→双方 evidence 引用并存；时间相邻→只记 correlation，因果需验证。
**数学**：A8（Ledger 唯一 canonical 语义）+ append-only ⇒ 归纳步中历史集合只增不改，T3 见证链不可被事后篡改。
**哲学**：历史不可改写是认识论的宪法条款——记忆可以被重新解释，不能被重写。同时编辑（096）说：两个真相可以并存待裁决，静默覆盖即说谎。
**判定**：✅ 全锚定（A/D 类 VNext 库 12/15 ✅ 的细化）。

### C9 遍历与决策形状（SR-099~108）
**走法**：当前决策契约=每 Run 单咨询+≤16 步；穷尽→安全返回；屏障内再验证。
**数学**：T2 保证遍历内每步安全；**T6 在此组暴露边界**——预算内 liveness 成立，但 F9 证明 16 步装不下 36 观测的自适应遍历 ⇒ 进度性在"大世界+小预算"参数区不成立，需要决策形状演进（登记项），不是不变量失败。
**哲学**：全知与遍历是两回事；系统承认有限注意力（预算=注意力配给），撞顶时的升级是美德不是失败。
**判定**：✅4 + ◐6（F9/F7/恢复编排——全部已登记有主）。

### C10 激活与基数（SR-109~116）
**走法**：admission 建 Run State 但不自驱；legal activation 一次性幂等；terminal 吸收。
**数学**：T4 完整实例：latch 单调性、absorbing state、exactly-once emission、generation 语义。
**哲学**："开始"是一个郑重的言语行为（一次性、不可重复宣告）；系统把执行生命的开端做成仪式，防止无心复活。
**判定**：✅ 全锚定。

### C11 记忆与历史影响（SR-117~122）
**走法**：Memory 未实现；当前唯一跨时间影响通道=LEARNING HOOK（流程侧）与 ADR（治理侧）；跨 session 目标显式拒绝。
**数学**：基数约束 1:1:1 的封闭性（SR-119 是"拒绝"作为被测试的合法行为——负路径可执行）。
**哲学**：休谟问题的工程回答：过去不自动担保现在；未来实现 Memory 时，recall 只许当 prior 不许当证据——这条已经立法（A7），先于实现。
**判定**：D3 + ✅3。立法先于能力=设计正确性的特殊证明形态。

### C12 对抗与完整性（SR-123~130）
**走法**：内容权威性一律不采信（untrusted）；完整性靠摘要/seal/torn 语义机械判定； importer 只吃 sealed artifact。
**数学**：对抗输入在 T1/T3 中不可区分于普通输入（都不在见证语法内）⇒ 对抗者无法通过内容伪造授权或完成；能攻击的只剩 provenance 通道（SR-126 ◐ 登记扩展）。
**哲学**：不与骗子比聪明，比管辖权——骗子能说的任何话，在制度上都不构成任何权威行为。完整性哈希=有限但可声明的信任范围。
**判定**：✅5 + D2 + ◐1。对抗面的哲学（不采信内容）已生效，机制面（检测）按 Phase 推进。

### C13 交叉组合（SR-131~150）
**走法**：交叉场景=两轴同时压同一转移；判定规则：取两轴各自结论的**合取**——任一轴 fail-closed 则整体 fail-closed（AND 语义），无组合漏洞；凡组合涉及 D/◐ 面，组合继承其状态。
**数学**：合取封闭性来自使能条件的合取结构（Authorization=三合取；Witness=语法合取）——参数独立压力在合取下可叠加不可抵消 ⇒ 网格覆盖有组合保证。
**哲学**：现实从不单轴来犯；设计的考验在合取处。SR-150（一切正常但 goal 语义不可验证）是全文档的收束：系统最终交付的不是成功，是**可核验的诚实**。
**判定**：✅14 + D3 + ◐3，无未登记组合缺口。

## 4. 覆盖论证与合龙

```text
150 场景 = 13 组 × 7 轴网格的覆盖选择（分布：✅122 · ◐14 · D13 · NB1）
不变量命中：47/47（41 执法行各≥1 场景压中；3 DEFERRED 各有专属组 G4/G11/G12）
定理命中：T1×(C1,C4,C13)  T2×(C1,C5,C9)  T3×(C2,C6,C8)  T4×(C10,C6)  T5×(C6)  T6×(C9 边界)
登记缺口命中：F8(SR-087)  F9(SR-099/102/103)  恢复编排(SR-058/107/108/144)
反例实存：F8（已喂 buyer）——方法有效性的实证
与 capability-coverage-derivation 的关系：本文=其 A–E/F 判定的推理展开层；
  其"12/15✅+3◐"与本文 ✅/◐/D 分布一致，无矛盾。
```

## 5. 证伪条款

本推理在以下观测下认输并触发基线窄修：
1. 任何 ✅ 场景在真机出现"锚点测试通过但实际行为偏离"→ 公理-实现映射破裂（比反例更严重）；
2. ◐F9 修复后仍出现预算内无法终局的场景 → T6 公理集不完备；
3. D 面实现时发现 Grant/A47 语义与既有不变量冲突 → 基线 §24 窄修需求；
4. Undetermined 率在真实负载下超过可用阈值 → 诚实与能力的失衡，属产品判断非逻辑错误。

## 6. 维护规则

- 场景数保持在 100–200（守护 `tools/check-scenario-reasoning.py` 校验结构：行数、组归属、锚点词法、类章节四要素）；
- 新增登记缺口 → 对应场景行 ◐ 状态同步；D 面实现 → 该组改判并补锚点；
- 场景改判必须附反例或测试证据，不许凭感觉改 ✅。
