# Agnes + YOLO + Jev UI 自动化测试：详细功能清单与当前代码复用评估

> Status: COMPLETE / READ_ONLY_INVENTORY
> Authority: NONE

日期：2026-10-20。状态：只读盘点完成，方案待评审。Authority：NONE。关联：`UI-TEST-REUSE-001`。

## 1. 结论与适用范围

**当前已经有相当完整的 Android 观察—决策—执行—验证基础，可以复用；真正新增工作集中在 Agnes 宿主接入、Jev 局部判定、通用测试语义与执行管理。不能把全部功能都算成新开发，也不能认为更换宿主后无需适配。**

本清单默认被测对象是 Android/车机 Android 的 GUI，包含两种任务：明确步骤/断言的回归测试，以及给定范围的探索遍历。普通 Android Settings 的历史证据不自动代表 AAOS 车辆功能、实车、驾驶状态或多屏环境已认证。

本轮只读源码、测试和既有运行产物，没有安装/启动 Agnes、调用 Jev、训练 YOLO、操作设备或重跑产品测试。用户说“模型配好了”属于本次输入，尚不等于本项目源码已有 Jev adapter 或本轮调用通过。

### 1.1 “直接复用”取决于是否保留当前后端

| 采用方式 | 真正能复用的部分 | 额外工作与代价 |
| --- | --- | --- |
| **A：Agnes 承载 UniAgent，保留现有 .NET 执行后端和 Python 感知服务** | 本文标记 D 的执行、证据、World、Assurance、driver、账本及单 Run 报告可保留 | 新宿主桥、Jev 判定接入、设备动作和测试功能缺口；需验证新链路 |
| **B：只运行 Agnes 与 Python 感知，不启动现有 .NET 后端** | Python YOLO/OCR 服务、模型资产、测试语料、JSON/YAML 数据与部分独立工具可复用 | .NET 执行/验证组件只能作为实现与验收参考；要移植或重写相应职责，不能直接 import 到 TypeScript |

本次建议先按 A 列出最小改动量，作为比较基线。若选择 B，应另列拟保留、删除与重写的职责，再评估是否值得；本报告不裁决替换已接受的产品架构。

### 1.2 三个外部组件与我们的职责

| 部分 | 职责 |
| --- | --- |
| Agnes | 模型/工具会话、插件装配、宿主记录、取消与恢复基础；当前已调研版本尚无 Jev adapter |
| YOLO + OCR/UI 树 | 产生控件、文字、几何与原始状态候选；需要当前页面语义与来源关联 |
| Jev | 对给定状态与有界选项作选择/评分/判定；可辅助动作选择、是否重观察、是否升级处理 |
| 我们的测试系统 | 测试定义、设备操作、运行状态、目标绑定、授权检查、断言、覆盖与报告；决定何时调用模型、如何处理失败 |

**Jev 不是设备 driver。** 实际点击/滑动由 Android 执行端完成。Agnes 内部调用 Jev 属于认知实现，Jev 输出不直接产生设备动作授权或测试通过结论。上游角色边界参见[既有调研](../research/agnes-harness-fit-assessment-20261020.md)。

## 2. 如何阅读清单

优先级：**P0**＝首个单设备/单应用测试闭环；**P1**＝通用 App 与探索遍历扩展；**P2**＝规模化测试需要时再做。优先级是建议，不代表用户已要求实现所有条目。

复用等级：

- **D 直接复用**：保留当前实现及依赖，改变装配/数据即可使用；仍需新组合回归，非“已通过 Agnes 验收”。
- **A 需适配**：已有实质实现，但含 DSH、Settings、设备或格式假设。
- **P 部分已有**：只有子能力、数据结构或特定场景实现，需要补充行为。
- **N 待新增**：本轮产品目录与相应工具检索范围内未找到可用实现，或外部组件接线尚未实现。

来源列中的 Sxx 在第 5 节给出源码、符号起始行及测试入口。验收描述是未来实现时应证明的行为，不能读成已经通过。

## 3. 详细功能清单

### 3.1 用例、目标与测试资产（7 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| T01 | 测试目录：能按 Project/TestSet/Task 找到具体用例及版本 | P0 / D | S01：manifest + 引用校验；S02：Run 中保存逻辑引用 | 用现有目录承载目标 App 用例；不能把引用名当执行结果 |
| T02 | 用例目标与允许范围：运行前固定目标、允许/禁止动作和证明条件 | P0 / D | S03：ExecutionContract/RunModel | 将测试需求映射到合同与义务；自然语言目标不自动成为可执行断言 |
| T03 | 独立执行实例：同一次启动重试不重复创建实例，不同执行可追溯 | P0 / D | S02：TaskInstanceStore/ProductSessionStore/RuntimeRunStore | Agnes session 显式绑定产品身份；不沿用伪造的 dshSessionId |
| T04 | 前置条件校验：设备、页面、账号/权限等条件不满足时明确阻塞 | P0 / P | S01：fixtureRefs；S04：Settings config 与环境脚本 | fixtureRefs 只是声明；补目标 App 的可执行检查及环境不满足的结果 |
| T05 | 测试准备与清理：成功、失败、取消后都按声明尝试恢复，并报告恢复失败 | P1 / P | S04：隔离模拟器启停与局部清理 | 通用 setup/teardown、App 数据准备/恢复、原状态保存与权限边界未完整实现；恢复不是自动回滚任意外部效果 |
| T06 | 参数化用例：数据版本×用例参数展开为独立可追踪执行 | P2 / N | S01：已有静态清单与 scenarioRefs | 定义参数输入、数据集版本、展开规则；不能把 simulation scenarioRefs 当作产品参数化执行器 |
| T07 | 测试需求追踪：每条验收要求关联断言及证据，未测范围明确列出 | P1 / P | S01：acceptanceRefs；S03：Run obligations；S22：报告关联 | 补跨用例需求覆盖矩阵与未覆盖原因，不按模型自述计算通过率 |

### 3.2 Android 设备与操作（7 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| D01 | 设备选择与预检：固定 serial，断开/未授权/能力缺失时不误选其他设备 | P0 / P | S04：模拟器脚本；S05：测试侧 LiveDeviceSelector | 选择器在 tests；补产品侧连接、预检和会话管理，按目标机型声明能力 |
| D02 | 截图：获取 PNG、尺寸、capture identity 与来源；失败不伪造空画面 | P0 / D | S06：AdbScreenshotAcquisition | 接入测试循环，保持截图与后续目标定位关联 |
| D03 | UI 树采集：解析文本/resource-id/class/bounds/原始状态，重复标识不误认为唯一 | P0 / D | S07：UiAutomatorDump | 无树/受限树时明确降级；不假定 Canvas、WebView 等一定暴露完整语义 |
| D04 | 点击与上下滑动：坐标合法才派发，超时与明确失败分开记录 | P0 / D | S08：AdbEffectDriver/AdbLiveEffectDriver | 使用现有目标绑定与 driver；set-switch 投递本身不证明状态已切换 |
| D05 | 启动/结束 App、返回/主页、文本输入 | P0 / P | S05：live 测试有临时启动命令；S08：产品 driver 动作集有限 | 把分散命令变为受控设备动作；系统返回与“点页面返回按钮”分开；需处理输入法与特殊字符 |
| D06 | 坐标与视口匹配：旧截图几何不匹配时零误投递 | P0 / D | S09：CoordinateSpace/DeviceViewportResolver | 现有几何匹配检查可保留；不据此认证全部旋转情形，多显示屏、车机副屏及旋转组合另验收 |
| D07 | 长按、拖拽、多指、按键与多屏操作 | P2 / N | S08：当前支持集未覆盖这些通用动作 | 按真实用例逐项扩展；不通过任意 shell 工具绕过既有动作约束 |

### 3.3 感知、页面与控件理解（8 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| P01 | UI 控件检测：图片→类别/框/分数，并能定位实际使用的权重 | P0 / D | S10：YOLO inference；Android UI 与 ScreenParser 两份 `.pt` 当前可见 | 不是新建推理服务；需在目标 App/车机样本上测误检漏检，不因权重存在宣称加载/准确率通过 |
| P02 | OCR：图片→文字与位置，语言和模型配置可定位 | P0 / D | S11：RapidOCR 路径、OCR ONNX/字典 | 英文基础可用；中文混排、小字号、字体及业务字符串需要目标语料验证 |
| P03 | 本地视觉服务：启动、探活、版本与失败分类明确 | P0 / D | S12：FastAPI 服务、VisionServiceHost/Client/Transport | 保留 UDS/loopback 契约；Agnes 经桥调用，不必复制 Python 推理代码 |
| P04 | 同帧多源融合：YOLO/OCR/UI 树正确关联，歧义与冲突可见 | P0 / D | S13：FusionEngine/OccurrenceAssociator/lineage | 同帧关联不是跨帧跟踪；保留 capture/空间/来源校验 |
| P05 | 控件语义与状态：区分标签、可操作目标、开关状态与未知 | P0 / A | S07、S14：typed hierarchy、语义 checked；S15：Settings feed | 通用 App 的图标含义、文本—控件关系与精确状态证明需要适配；Wi-Fi two-state proof 不外推；provider 的 confidence 当前未进入 LiveVisionStrategy observation payload，不能假定 Jev 已能读取 |
| P06 | 观察新鲜度：决策采用的截图、页面与目标属于同一有效观察范围 | P0 / D | S13、S16：对齐门、Evidence/World revision；S19：Assurance | 重连/弹窗/页面跳转后重新观察；时间戳或高 confidence 不能单独保证有效 |
| P07 | 页面稳定与视觉连续性：动画/加载中等待，跨截图目标关联有依据 | P1 / P | S16：World 跨 revision 关联、导航边/视口关系已有 | 纯视觉 tracker、稳定窗口和动画完成检测需补；不能误称当前 World 完全没有跨观察关联 |
| P08 | 感知复用与评测：同图不重复计算，模型变化可重放样本比较 | P1 / D | S17：StrategyObservationCache；S10：evaluation/validation/reports | 已有资产可作基线；新增目标 App 标注、状态/定位指标及模型选择评估；训练不是第一步的默认要求 |

### 3.4 Agnes、Jev 与规划接入（7 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| M01 | Agnes 产品会话：创建/恢复/取消/卸载与 Product Session/Run 正确对应 | P0 / A | S18：已有 DSH realization 的生命周期与超时/迟到隔离 | 实现 Agnes adapter；SDK 连接关闭不等于产品 Run 结束，不直接复制 DSH session API |
| M02 | 受控决策输出：只接受本轮一次合法决定，错 ID/迟到/重复/纯文本被拒绝 | P0 / A | S18：DecisionChannel + submit_decision + runtime guard | Agnes 插件重新实现工具注册与限制，验证晚注册和恢复路径仍受约束 |
| M03 | Profile/Skill/模型角色装配：产品工件固定并可核对，宿主配置单独绑定 | P0 / A | S20：host-neutral Profile/Charter/Skill；DSH ModelManagement | 产品内容可复用；Agnes 的角色解析、provider/endpoint、实际 prompt 挂载与工件握手需适配 |
| M04 | Jev 局部判定：当前状态+稳定候选键→选择/评分/无法确定 | P0 / N | 本轮 src/platforms/dsh 产品实现检索未发现 Jev adapter | 接入 API、结果 schema、超时/限额、低质量或不可用处理；先测局部场景；Jev 不执行 ADB |
| M05 | 目标式任务分解：自然语言目标→方向与有界计划 | P1 / A | S20：Product Skill/Charter；S21：现有 AgentDecision.Plan/PlanTree | 明确步骤用例可由代码推进；开放任务需 Agnes 通用模型生成计划，Jev 不能自动承担任意长计划生成 |
| M06 | 组合计划：顺序、条件、有限循环、动态候选与暂停续行 | P0 / D | S21：当前 Runtime Plan/Traversal 与正式 codec | Kernel 控制流已有；Agnes 侧需输出当前正式协议，不能继续沿用旧“只支持单 Act”假设 |
| M07 | 模型反馈与重规划：拒绝原因、新观察、剩余额度可形成有界修订 | P1 / A | S18、S21：consultation context、一次性反馈、计划版本/位置 | 迁移 DSH 侧反馈装配；分别记录 transport retry、模型修订、产品再咨询，避免重复推进 |

### 3.5 执行、约束与恢复（8 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| E01 | 单 Run 测试循环：观察→决定→检查→动作→验证→继续/终局 | P0 / D | S03、S21：KernelRunDriver/RunModel | 保留既有 .NET 后端时可复用；若选纯 Agnes 新引擎需另实现相同必要行为 |
| E02 | 唯一目标绑定：候选不唯一、观察过期或目标未知时不误点 | P0 / D | S08、S16：EffectBoundary/CanonicalBinding | Jev 选择映射到当前候选键，再走现有 binding，不能直接把 bbox 当授权 |
| E03 | 操作限制：只允许本用例范围内动作，未知/禁止项明确拒绝 | P0 / A | S19：通用 Assurance；S23：Settings action/popup policy | 通用执法可保留，危险动作分类与目标 App 白名单需重新声明和验证 |
| E04 | 资源与等待上限：步数、咨询、handler 超时、续额有记录 | P0 / D | S21：AgentPlanPolicy/预算复核/异步 handler | Agnes/Jev 各调用边界要映射预算；模型费用硬上限另见 R06，不等于步数额度 |
| E05 | 幂等咨询：同 correlation 和相同输入只采纳一次，冲突拒绝 | P0 / A | S24：IConsultationJournal + Host 文件实现 | 语义与存储可复用；新宿主传输重试和完整 Plan union 持久化要往返验证 |
| E06 | 未知执行结果：发送后没回执仍为未决，不盲目再点击 | P0 / D | S25：IReliableExecutionSource/FileExecutionJournal | 已有准备、提交、回执、迟到关联与 pending 发现；不是自动恢复完成器 |
| E07 | 暂停/取消/进程重启恢复：恢复前核对实际页面、历史尝试及剩余工作 | P1 / P | S21：进程内计划位置；S24/S25：持久账本 | 组合恢复协调仍需补充；明确何时查询、重观察、停止、新 Attempt 或人工处理 |
| E08 | 异常处理：弹窗、导航失败、停滞分别处理，恢复预算耗尽可解释 | P1 / A | S23：Settings policy/runner；S21：recovery/handler | Settings 已有局部策略；通用 App 弹窗含义、恢复入口与进度指标需要适配 |

### 3.6 断言、测试结果与业务核验（7 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| V01 | 动作后效果验证：只接受动作后新观察与明确预期谓词 | P0 / D | S19：VerifyPostActionEffect/ExpectedEffect | 复用通用机制，按 App 提供真实 observed facts；receipt 不等于效果达成 |
| V02 | 基础 UI 断言：元素存在/缺失、文字、启用、选中、页面到达 | P0 / P | S14/S19：字段、predicate 与部分场景验证 | 补统一面向用例的断言输入、匹配规则、否定/不可观测语义及逐条证据 |
| V03 | 业务断言：例如实际系统开关、保存结果或业务记录与 UI 一致 | P1 / A | S15：Wi-Fi 专用状态证明与测试契约 | 每类业务另提供观测与比较；“界面显示关闭”与“设备功能已关闭”的验收必须明确 |
| V04 | 视觉回归：截图基线、遮罩、动态区域、差异阈值与人工复核 | P2 / N | 本轮产品目录未找到通用视觉基线比较器；已有感知评测不等于视觉回归 | 按真实需求新增；模型判断不能自动代替版本化基线与差异证据 |
| V05 | 语言/文本检查：按目标语言与忽略区输出问题及证据 | P1 / A | S26：LanguageInspection + allowlist/binding/traversal handler | 已有检查类型与接缝，适配目标页面/语言；不是所有文案正确性与翻译质量检查 |
| V06 | 用例结论：义务满足才通过，失败/阻塞/无法确认保留原原因 | P0 / A | S19：OutcomeProof；S20：GoalEvaluation；S22：报告 | 将现有产品 Outcome/GoalEvaluation 投影为测试结果；避免把 runtime terminal 与测试 PASS 合并 |
| V07 | 防误判验证：已达标不操作、相似目标不误点、未知不冒充通过 | P0 / D | S01：Settings 反例用例；S19/S21：场景/仿真测试 | 复用反例与测试结构；换 App、YOLO 权重、Agnes/Jev 后用相同失败类型重新认证 |

### 3.7 自动探索与覆盖（6 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| C01 | 页面访问与返回：验证进入后才推进路径，返回要与父页相符 | P1 / D | S16：导航记录；S21：TraversalControl/TraversalExecution | 通用遍历机制可保留；页面身份和目标 App 导航投影另适配 |
| C02 | 动态入口发现：当前页可操作候选→待探索集合，未来页面不事先编造 | P1 / A | S23：SettingsTraversalProjection/PlanFacts；S21：动态规则 | 需要 App 的页面与控件分类、稳定键、操作条件；不是扫描检测框即算可测试入口 |
| C03 | 环、重复入口和深度限制：有限规则执行，不无限返回同页 | P1 / D | S21：Traversal 状态、预算、仿真 | 需校准身份与进度指标；同名页面不一定同页，同页新内容也不能全部去重掉 |
| C04 | 滚动与发现完整性：保留已发现范围，无法证明穷尽时明确 partial | P1 / P | S16：视口/滚动关系；S23：scroll bound/coverage | 跨页滚动终点、虚拟列表与加载新项的穷尽证明待按场景补；不宣称全 Settings 或全 App 覆盖 |
| C05 | 覆盖统计：已测/未测/排除/未知及各自原因可追溯 | P1 / A | S23：SettingsCoverageLedger/Report | 复用账本模式；重新定义用例/需求/页面/控件的分母，避免把步骤成功率当需求覆盖率 |
| C06 | 遍历结束与测试结束分离：走完有界范围不自动判目标达成 | P1 / D | S21：partial/gaps；S19：终局义务 | 报告显式并列呈现 traversal status、发现完整性、断言和最终结果 |

### 3.8 报告、工作台与用量（7 项）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| R01 | 每步证据：当前页、动作、回执、前后截图、验证与失败原因互相可定位 | P0 / D | S22：Run artifacts、report correlator；S23：StepArtifact | 保持 provenance；加入 Agnes/Jev 的候选键与判断结果引用 |
| R02 | 单次运行导出：同一输入生成一致的 JSON 与 Markdown 报告 | P0 / D | S22：tools/gen-run-report.py 实际写 report.json/report.md | 现有输入形状可直接用；新宿主字段映射需适配，缺失内容诚实标记 |
| R03 | 工作台查看：任务→实例→运行→截图/证据，来源缺失可见 | P1 / A | S27：DSH Task Workbench + artifacts.js | UI/RPC/Typert/sessionController 含 DSH 假设；为 Agnes 接前后端及 session 映射，不能原包直装 |
| R04 | 测试集报告：多个独立用例的结果、失败聚类、重试历史聚合 | P2 / N | 现有工具以单 Run 为主，未找到通用 suite aggregator | 新增 suite/report projection；重试成功与首轮失败均保留 |
| R05 | 耗时与模型用量：按调用记录 latency、model、input/output/cache tokens | P1 / P | S28：PlanGenerationTrajectory/elapsed-usage 历史产物 | 有实验 harness 采集基础；移入通用产品观测并覆盖 Agnes、Jev 与失败调用 |
| R06 | 费用核算：按提供方、版本、时段、缓存和重试汇总实际/估算费用 | P2 / N | S28：现有 cost 明确为 unknown | 新增计价与币种/账单核对；tokens 不等于已有费用统计，未取得 usage 不猜金额 |
| R07 | 日志诊断与重放：失败能定位首次偏离，静态重建与重新执行分开 | P1 / P | S16：World reconstruction；S22/S29：trace/scenario/replay | 已有不同层次工具；不把截图/World 重放称为可回放真实设备 effect 或完整像素视频 |

### 3.9 批量运行与长期维护（6 项，按需求引入）

| ID | 功能与可判定行为 | 优先级 / 复用 | 当前依据 | 还需实现或适配 |
| --- | --- | --- | --- | --- |
| O01 | 批量顺序运行：测试集逐用例运行，每项都保留独立结果 | P2 / N | S01/S02：目录及单 Run 启动已有 | 补 suite runner 与汇总；可以为每个 case 新建独立 Session/Run，无需因此改变单 Session 基数 |
| O02 | 多设备并行与排队：同设备互斥、设备失联后任务归属明确 | P2 / P | S04：已有开发模拟器生命周期/租约规范 | 开发环境租约不等于产品调度器；补产品设备资源协调、队列和设备间隔离 |
| O03 | CI/版本矩阵：App/系统/机型/语言版本可复现，运行条件完整记录 | P2 / P | S29：确定性场景认证、已有命令/工件校验 | 补产品测试触发、安装包来源、矩阵选择、设备资源与流水线报告 |
| O04 | 结果未知与重试治理：不盲重试状态切换，重试不掩盖不稳定 | P1 / P | S24/S25：账本与 Attempt 关联 | 补 case 级 retry policy、flaky 分类、跨进程恢复及人工处理入口 |
| O05 | 模型/策略更新回归：版本可固定，同一语料对照，回退原因可解释 | P1 / A | S10/S20/S29：模型资产、Profile/Skill pin、仿真/样本 | 新增 Jev/Agnes 固定版本与对照，按任务成功/误动作/恢复/成本评估，不能只看输出格式 |
| O06 | 真实车机专项：副屏、车载输入、车辆信号、驾驶限制与恢复边界 | P2 / N | 本轮普通 Android/Settings 证据不足以覆盖这些事项 | 由真实设备与测试目标决定接入面，单独验收；不从通用 ADB 或模拟器结果外推 |

共 **63 项功能检查点**：D 直接复用 25 项、A 需适配 16 项、P 部分已有 14 项、N 待新增 8 项。这是行为清单，不是 63 个新组件；多个条目可由同一模块承担，已有执行责任不得为了清单再复制一套。各项规模和依赖不同，以上数量不换算为完成率、代码复用率或开发工期。

## 4. 最值得复用什么，以及哪些不能直接搬

### 4.1 优先保留的实现

1. **Python 感知 provider**：YOLO/OCR 推理、模型路径、配置变体、几何回映、health/version 与评测语料（S10–S12）。这是跨语言复用成本最低的一组。
2. **Android 采集与 tap/swipe driver**：截图、UI 树、坐标映射与投递分类（S06–S09）。补动作能力比重写已有安全几何路径更小。
3. **Kernel 执行与验证**：合同、PlanTree/Traversal、控制/约束、World/Evidence、后置谓词与终局（S03、S16、S19、S21）。保留后端可直接复用；纯 TS 重写只能复用规则与测试参考。
4. **可靠记录**：咨询幂等 journal、Effect/Attempt 准备和回执历史、迟到反馈（S24–S25）。已有“记录/发现”，恢复决策仍需协调。
5. **测试资产和报告**：TestSet/Task 引用与反例、单 Run JSON/Markdown 导出、仿真与重放样例（S01、S22、S29）。报告并非全部待新建。

### 4.2 明确需要适配或新增的部分

- **Agnes 产品 adapter**：会话生命周期、唯一输出、工件握手、能力/工具限制、取消和迟到隔离。现有 DSH 实现是参考与验收基线，不能直接作为 Agnes 实现。
- **Jev 判定 adapter**：有界问题与候选键、请求/响应、不可用/超时、结果质量检查和审计。不把 Jev 当 ADB driver，也不靠更换模型配置跳过协议。
- **目标 App 语义与通用断言**：页面身份、控件关联、前置/后置条件、业务状态观测、允许动作、Unknown。当前 Settings 代码是一个具体实现，不是任意 App 通用库。
- **设备动作补全与准备恢复**：App 生命周期、系统返回、输入、稳定等待，以及 case 前后置恢复；不能散落成任意 shell 指令。
- **规模化能力**：通用参数矩阵、suite 调度/聚合、设备并行及费用归一化。只在实际需要时引入。

### 4.3 特别容易误判的四处

| 已有事实 | 不能据此声称 |
| --- | --- |
| `.pt`/ONNX 权重、推理代码和历史评测在树中 | 当前机器已加载成功、当前 App 识别准确率达标 |
| UI 树含 checked 字段；Settings feed 注入 Wi-Fi 精确性 proof | 所有 Android 开关均可精确认证；Wi-Fi proof 可用于任意开关 |
| fixtureRefs/acceptanceRefs 可校验 | 通用 setup/teardown、数据生成与断言执行器已完成 |
| journal、trace、报告和恢复控制词汇存在 | 重启后能自动判断真实结果、自动恢复并完成测试 |

## 5. 源码、测试与可复用资产导航

下列锚点来自本轮当前工作区，包含未提交并行修改；不是对应 HEAD 的不可变提交内容。链接定位到本轮核对的起始行，检索时优先看符号。

| 来源 | 当前实现与定位 | 相关测试 / 可读资产 |
| --- | --- | --- |
| S01 测试目录 | [Android TestSet](../../testsets/android-settings/manifest.json#L1)；[引用校验](../../tools/validate-testset-manifests.py#L61) | [TestSet 说明](../../testsets/README.md)；Wi-Fi/unknown/decoy 五种测试任务与 fixture/acceptance refs |
| S02 任务与运行身份 | [RuntimeRunStore](../../src/UniClaw.Host/Runtime/RuntimeRunStore.cs#L42)；[TaskInstanceStore](../../src/UniClaw.Host/Runtime/TaskInstanceStore.cs#L38)；[ProductSessionStore](../../src/UniClaw.Host/Runtime/ProductSessionStore.cs#L36) | [运行存储测试](../../tests/UniClaw.Host.Tests/RuntimeRunStoreTests.cs)；[身份存储测试](../../tests/UniClaw.Host.Tests/RuntimeIdentityStoreTests.cs) |
| S03 合同与 Run | [RunModel.AdmitContract](../../src/UniClaw.Kernel/Run/RunModel.cs#L144)；[ExecutionContract](../../src/UniClaw.Kernel/Run/ExecutionContract.cs) | [合同/预算反例](../../tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverAcceptanceV03RedTests.cs) |
| S04 环境与模拟器 | [启动脚本](../../tools/android/start-test-device.sh#L1)；[supervisor](../../tools/android/emulator-supervisor.py#L17)；[Settings config](../../src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs#L6) | [本地环境约定](../agents/test-emulator.md)；脚本本轮未执行 |
| S05 测试侧设备选择 | [LiveDeviceSelector](../../tests/UniClaw.Host.Tests/LiveDeviceSelector.cs)；[临时 App 启动](../../tests/UniClaw.Host.Tests/HostLiveFullTests.cs#L45) | [选择器测试](../../tests/UniClaw.Host.Tests/LiveDeviceSelectorTests.cs) |
| S06 截图 | [AdbScreenshotAcquisition.CaptureAsync](../../src/UniClaw.Kernel/Perception/AdbScreenshotAcquisition.cs#L50) | [PNG/采集测试](../../tests/UniClaw.Kernel.Tests/Perception/PngDecodeAndAcquisitionTests.cs) |
| S07 UI 树 | [UiAutomatorDump](../../src/UniClaw.Host/UiAutomatorDump.cs#L96) | [解析与唯一目标测试](../../tests/UniClaw.Host.Tests/UiAutomatorDumpTests.cs) |
| S08 动作投递 | [AdbEffectDriver](../../src/UniClaw.Kernel/Effects/AdbEffectDriver.cs#L67)；[AdbLiveEffectDriver](../../src/UniClaw.Kernel/Effects/AdbLiveEffectDriver.cs#L95)；[EffectBoundary](../../src/UniClaw.Kernel/Effects/EffectBoundary.cs#L67) | [滑动测试](../../tests/UniClaw.Kernel.Tests/Effects/AdbSwipeEffectTests.cs)；[driver 替换测试](../../tests/UniClaw.Kernel.Tests/Effects/EffectDriverReplaceabilityTests.cs) |
| S09 几何与视口 | [DeviceViewportResolver](../../src/UniClaw.Kernel/Effects/DeviceViewportResolver.cs#L24)；[CoordinateSpace](../../src/UniClaw.Kernel/Perception/CoordinateSpace.cs) | [视口投影测试](../../tests/UniClaw.Kernel.Tests/Effects/AdbViewportResolutionTests.cs) |
| S10 YOLO 与资产 | [run_yolo_on_image](../../platforms/perception/uniclaw_perception/yolo/inference.py#L37)；[provider config](../../platforms/perception/uniclaw_perception/config.py#L32)；[Android UI 权重](../../platforms/perception/models/yolo/android_ui_detection_yolov8/best.pt)；[ScreenParser 权重](../../platforms/perception/models/yolo/screenparser_v2/best.pt) | [评测目录](../../platforms/perception/evaluation/README.md)；[pipeline 测试](../../platforms/perception/tests/test_pipeline_config.py) |
| S11 OCR | [RapidOCR](../../platforms/perception/uniclaw_perception/ocr/rapid.py)；[英文 OCR ONNX](../../platforms/perception/ocr/models/en_PP-OCRv4_mobile_rec_infer.onnx) | [ScreenParser provider 测试](../../platforms/perception/tests/test_screenparse_provider.py)；语言及后端选择见 config.py |
| S12 服务接缝 | [server._run_pipeline](../../platforms/perception/uniclaw_perception/server.py#L162)；[VisionServiceHost](../../src/UniClaw.Kernel/Perception/VisionServiceHost.cs#L51)；[Client](../../src/UniClaw.Kernel/Perception/VisionServiceClient.cs)；[Transport](../../src/UniClaw.Kernel/Perception/VisionServiceTransport.cs#L20) | [服务 Host 测试](../../tests/UniClaw.Kernel.Tests/Perception/VisionServiceHostTests.cs)；[Client 测试](../../tests/UniClaw.Kernel.Tests/Perception/VisionServiceClientTests.cs) |
| S13 融合与关联 | [FusionEngine](../../src/UniClaw.Kernel/Perception/Fusion/FusionEngine.cs#L28)；[OccurrenceAssociator](../../src/UniClaw.Kernel/Perception/Fusion/OccurrenceAssociator.cs#L20) | [融合测试](../../tests/UniClaw.Kernel.Tests/Perception/Fusion/FusionEngineTests.cs)；[融合接入测试](../../tests/UniClaw.Kernel.Tests/Perception/Fusion/FusionP2IntegrationTests.cs) |
| S14 语义投影 | [LiveVisionStrategy](../../src/UniClaw.Kernel/Perception/LiveVisionStrategy.cs#L32)；[UiHierarchyOccurrenceStrategy](../../src/UniClaw.Host/UiHierarchyOccurrenceStrategy.cs) | [视觉投影对照](../../tests/UniClaw.Kernel.Tests/Perception/LiveVisionStrategyParityTests.cs)；[checked 语义测试](../../tests/UniClaw.Kernel.Tests/Perception/UiHierarchy/CheckedSemanticsTests.cs) |
| S15 Settings feed | [WifiToggleExactProof](../../src/UniClaw.Host/SettingsTraversalLiveFeed.cs#L23)；[Wi-Fi two-state 契约](../../testsets/android-settings/wifi-toggle-contract.json) | [近期真实接入工作记录](../../changes/RUNTIME-PLAN-005/state.md)；其局限不能被复用等级覆盖 |
| S16 World 与重建 | [WorldModel 导航记录](../../src/UniClaw.Kernel/World/WorldModel.cs#L103)；[视口变化](../../src/UniClaw.Kernel/World/WorldModel.Viewports.cs#L9)；[World 重建](../../tests/UniClaw.Simulation.Tests/WorldReconstruction.cs) | [导航图测试](../../tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverNavigationGraphTests.cs)；[WRC-003](../../changes/WRC-003/state.md) |
| S17 帧计算复用 | [StrategyObservationCache](../../src/UniClaw.Kernel/Perception/StrategyObservationCache.cs#L56) | [缓存/single-flight 测试](../../tests/UniClaw.Kernel.Tests/Perception/FrameComputationReuseTests.cs) |
| S18 宿主决策接入 | [DecisionChannel](../../src/UniClaw.Agent.Dsh/DecisionChannel.cs)；[DshAgentAdapter](../../src/UniClaw.Agent.Dsh/DshAgentAdapter.cs#L93)；[DshOpenedHttpPeer](../../src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs)；[DSH 插件](../../dsh/uniclaw-decision-channel/src/index.js#L904) | [生命周期测试](../../tests/UniClaw.Agent.Dsh.Tests/AdapterLifecycleTests.cs)；[会话复用](../../tests/UniClaw.Agent.Dsh.Tests/SessionReuseTests.cs)；[协议闭包](../../tests/UniClaw.Agent.Dsh.Tests/DecisionChannelClosureTests.cs) |
| S19 Assurance 与终局 | [动作后验证](../../src/UniClaw.Kernel/Assurance/RuntimeAssurance.PostAction.cs)；[动作前检查](../../src/UniClaw.Kernel/Assurance/RuntimeAssurance.cs)；[终局判断](../../src/UniClaw.Kernel/Assurance/RuntimeAssurance.cs) | [效果谓词测试](../../tests/UniClaw.Kernel.Tests/Assurance/ExpectedEffectTests.cs)；[滑动验证测试](../../tests/UniClaw.Kernel.Tests/Assurance/ScrollVerificationTests.cs) |
| S20 产品角色与工件 | [UniAgentProfile](../../src/UniClaw.Agent/Profile/UniAgentProfile.cs#L41)；[ProductProfile](../../src/UniClaw.Agent/Profile/ProductProfile.cs#L3)；[产品 Skill](../../product/skills/android-automotive-ui-testing/guidance.md)；[DshModelManagement](../../src/UniClaw.Agent.Dsh/DshModelManagement.cs#L45)；[UniAgent.Evaluate](../../src/UniClaw.Agent/UniAgent.cs#L38) | [Profile 测试](../../tests/UniClaw.Agent.Tests/UniAgentProfileYamlTests.cs)；[模型管理测试](../../tests/UniClaw.Agent.Dsh.Tests/DshModelManagementTests.cs)；[初始化 envelope](../../src/UniClaw.Host.Dsh/InitializationEnvelope.cs#L21) |
| S21 Plan 与遍历 | [KernelRunDriver](../../src/UniClaw.Kernel/Runtime/KernelRunDriver.cs#L356)；[PlanExecution](../../src/UniClaw.Kernel/Runtime/PlanExecution.cs#L3)；[TraversalExecution](../../src/UniClaw.Kernel/Runtime/TraversalExecution.cs#L9)；[DSH codec](../../src/UniClaw.Agent.Dsh/DecisionJsonConverters.cs#L636) | [Plan 测试](../../tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverPlanTests.cs)；[遍历测试](../../tests/UniClaw.Kernel.Tests/Runtime/TraversalExecutionTests.cs)；[预算复核](../../tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverBudgetRenewalTests.cs)；[DSH 协议与计划仿真消费](../../tests/UniClaw.Host.Dsh.Tests/PlanGenerationKernelConsumerTests.cs) |
| S22 单 Run 报告 | [gen-run-report.py](../../tools/gen-run-report.py#L184)；[写 JSON/Markdown](../../tools/gen-run-report.py#L994)；[RuntimeToolHost](../../src/UniClaw.Host.Dsh/RuntimeToolHost.cs#L195) | [报告消费测试](../../tests/UniClaw.Host.Dsh.Tests/RuntimeToolHostTests.cs)；报告是源数据投影，不是第二结果 Owner |
| S23 Settings 覆盖 | [SettingsCoverageLedger](../../src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs#L3)；[Runner](../../src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs#L32)；[ActionPolicy](../../src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs#L8)；[TraversalProjection](../../src/UniClaw.Host/SettingsCoverage/SettingsTraversalProjection.cs) | [覆盖账本测试](../../tests/UniClaw.Host.Tests/SettingsCoverageLedgerTests.cs)；[场景测试](../../tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs)；[配置样例](../../product/tasks/settings-coverage.yaml) |
| S24 咨询账本 | [ConsultationJournal](../../src/UniClaw.Kernel/Runtime/ConsultationJournal.cs#L31)；[FileConsultationJournal](../../src/UniClaw.Host/Runtime/FileConsultationJournal.cs#L16) | [幂等/冲突测试](../../tests/UniClaw.Kernel.Tests/Runtime/ConsultationJournalTests.cs)；[文件往返](../../tests/UniClaw.Host.Tests/FileConsultationJournalTests.cs) |
| S25 可靠执行记录 | [IReliableExecutionSource](../../src/UniClaw.Kernel/Effects/ExecutionSource/ReliableExecutionSource.cs#L103)；[文件执行账本](../../src/UniClaw.Kernel/Effects/ExecutionSource/FileExecutionJournal.cs#L58) | [EffectBoundary 接线测试](../../tests/UniClaw.Kernel.Tests/EffectBoundaryExecutionSourceTests.cs)；[journal restart 测试](../../tests/UniClaw.Simulation.Tests/ReliableExecutionJournalRestartTests.cs) |
| S26 语言检查 | [LanguageInspection](../../src/UniClaw.Kernel/Capability/LanguageInspection.cs)；[TraversalLanguageInspection](../../src/UniClaw.Host/Capability/TraversalLanguageInspection.cs)；[Allowlist](../../src/UniClaw.Host/Capability/LanguageInspectionAllowlist.cs) | [语言绑定](../../tests/UniClaw.Host.Tests/LanguageInspectionBindingTests.cs)；[后置检查](../../tests/UniClaw.Host.Tests/LanguageInspectionPostCommitTests.cs) |
| S27 工作台 | [Task Workbench](../../dsh/uniclaw-task-workbench/src/index.js#L260)；[artifacts.js](../../dsh/uniclaw-task-workbench/src/artifacts.js#L1)；[client](../../dsh/uniclaw-task-workbench/src/client.js) | [工作台测试](../../dsh/uniclaw-task-workbench/tests/workbench.test.mjs)；现为 DSH 插件与受限服务 |
| S28 模型用量 | [PlanGenerationLiveEvaluationTests](../../tests/UniClaw.Host.Dsh.Tests/PlanGenerationLiveEvaluationTests.cs#L211)；[真实 usage 样例](../../evidence/runtime-plan-004/generation/epoch-007/switch-readonly/01/elapsed-usage.json) | 样例记录 input/output/cache 与耗时，但 cost 为 unknown；不是已实现通用费用系统 |
| S29 仿真与回归资产 | [SimulationHost](../../tests/UniClaw.Simulation.Tests/SimulationHost.cs)；[ScenarioRunner](../../tests/UniClaw.Simulation.Tests/ScenarioRunner.cs)；[scenario certify](../../tools/scenario_certify.py)；[场景目录](../../scenarios) | [遍历场景](../../tests/UniClaw.Simulation.Tests/TraversalScenarioTests.cs)；[资产治理](../../tests/UniClaw.Simulation.Tests/AssetGovernanceTests.cs)；夹具结果不替代真实设备结果 |

## 6. 历史证据能证明到哪里

本节读取现有文件，不是本轮新运行的验收。报告保留明确日期/批次；后续并行工作变化时须重新核查。

| 证据 | 本轮实际读取到的内容 | 可用于什么 / 不能推出什么 |
| --- | --- | --- |
| [RUNTIME-PLAN-004 verification](../../evidence/runtime-plan-004/verification.json) | 7 类×3 次真实模型输出，首稿 20/21，最终 21/21；正式 codec + Kernel 仿真消费 21/21；费用 unknown | 说明已有完整生成/编码/仿真测试链；不是 21 次真实 Android 操作，更不是 Agnes/Jev 组合验收 |
| [RUNTIME-PLAN-005 epoch003](../../evidence/runtime-plan-005/live/epoch-003/batch-assessment.json) | 3 次有界遍历范围完成、12 个 effect verified、开关 0 effect；终局完成 2/3，第三次 MALFORMED_RESPONSE 后 no-response | 说明真实 UI 导航/观察/验证已有证据；该批整体验收 failed，不能只拿 12/12 宣称全链可靠通过 |
| [epoch003 第三次 trial](../../evidence/runtime-plan-005/live/epoch-003/03/trial.json) | coverageStatus=CoverageComplete，但 driveStatus=AgentDecisionFailed、Reason=no-response | 直接证明覆盖状态、局部动作验证与最终测试成功必须分开 |
| [PER-019 旧验证](../../evidence/per-019/verification-2026-10-06.md) | 记录异步 slow 结果迟到/路由不一致被丢弃，也保留当时 provider 不兼容失败 | 可参考异步门控与失败类型；旧 provider 状态不能覆盖后来的 PLAN004/005 结果或当前新配置 |
| [感知评测目录](../../platforms/perception/evaluation/README.md) | 当前树存在 validation/reports 与资产；旧 README 中“未迁移 evaluation”的句子已落后于现状 | 可复用离线语料和评测方法；不是当前设备/当前 App 的识别达标证明 |

本轮观察到 epoch004 只有预注册及初始工件，未读取到完成的 batch-assessment，不把进行中的工作升级为通过。Change state 字段也在并行更新，本报告以具体产物及所声明批次为准。

## 7. 推荐开发顺序与首个闭环

### 第一步：先把已有链路接到 Agnes

保留 Python 感知与 .NET 后端；用确定性模型/固定响应先证明 M01–M03、E05 的会话映射、唯一决策、超时/迟到和工件校验。此时不让新宿主直通任意 ADB，也不以真实模型效果掩盖协议错误。

### 第二步：在一个局部判断上接 Jev

选择一个可独立评价的问题，例如“当前候选中哪个是目标入口，或是否需要重观察”。输入必须带当前观察与候选键，输出只作建议，经 E02/E03 校验；测正确选择、歧义、不支持/超时与未知，不默认每步都需要 Jev。

### 第三步：跑一条有明确断言的真实测试

建议首个用例为“到达某设置页，只在状态不符合要求时切换目标，验证后按声明恢复”。若沿用 Wi-Fi 场景，可复用已有 fixture；若换 App，先补其状态证明和断言，不能借用 Wi-Fi proof。

| 必测情况 | 应观察的结果 |
| --- | --- |
| 目标存在且状态需改变 | 唯一定位→一次合规动作→新观察→断言通过 |
| 目标已达标 | 零多余切换，凭当前证据完成 |
| 同类相似目标 | 不误点；歧义保留并重观察/停止 |
| 弹窗遮挡或截图过期 | 原动作不继续用旧坐标，状态与原因可追溯 |
| 动作可能已发送但回执丢失 | 保留未知，先核查实际状态，不直接重复切换 |
| 取消、模型失败或清理失败 | 不冒充 PASS；动作、判断与恢复结果分别落盘 |

每次输出完整单 Run 报告，并对照动作前后证据、模型调用数、误动作、延迟与费用可得性。满足这一闭环后再做 P1 的通用断言/遍历；有真实买方时才做 P2 的矩阵、并行、CI 和车机专项。

## 8. 本轮核查与交付范围

| method | expected | actual | evidence |
| --- | --- | --- | --- |
| 三路独立源码盘点：设备/感知、执行/验证、任务/宿主/报告 | 每组有实现和测试锚点，避免仅凭目录名或旧状态判断 | 63 个唯一功能 ID，29 组来源；复用等级 D25/A16/P14/N8；已修正把 Jev 当执行器、把 fixture ref 当执行器等错误解释 | 本文第 3–5 节；临时盘点文件 `/tmp/ui-reuse-perception-device-20261020.md`、`/tmp/ui-reuse-execution-20261020.md`、`/tmp/ui-reuse-host-tasks-20261020.md` |
| 主 Agent 综合及独立执行/断言/遍历事实审阅 | D 的条件、恢复边界、覆盖/终局区别与 Jev 职责准确 | 独立审阅无阻碍交付的执行语义事实错误；正式稿仅保留有当前源码依据的判断 | 第 1、3、4、6 节；临时审阅 `/tmp/ui-reuse-execution-review-20261020.md` |
| 文档链接与范围静态核查 | 路径存在、行号有效、功能 ID 唯一、来源编号完整 | 133 个本地链接通过；63 个 ID 唯一；29 个来源编号均有定义 | 本文及 `/tmp/uniclaw-ui-reuse-document-check-20261020.json` |
| 工作区保护检查 | 不覆盖既有工作，只新增约定两份文档 | 1270 个已跟踪源码/测试/资产文件的 SHA-256 快照无变化；git status 仅增加本次报告及 Change 目录，无原条目消失 | 入口 `/tmp/uniclaw-ui-reuse-before-20261020.txt`、`/tmp/uniclaw-ui-reuse-source-snapshot-20261020.json`、核查输出 |

本轮验证等级为 CONTRACT：证明引用、范围和盘点的一致性，不证明新组合的产品运行。没有运行产品测试、模型、服务、设备或性能实验。测试文件仅为已有断言来源；第 6 节是历史产物核查。未跟踪文件未逐个作内容哈希；其他并行 Change 的状态与新产生的实时证据不由本次报告冻结。临时文件只作本轮工作记录，长期依据为本报告内的源码及已保存历史证据链接。
