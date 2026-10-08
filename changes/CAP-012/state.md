# CAP-012 — Language Inspection 能力组件（capability-component skill 首次全新实战）

lifecycle_state: closed · disposition: none · depth: standard · base: 63804f7a

## Intent（WHAT/WHY）

所有者指令（2026-10-06）："使用这个（capability-component skill）做一个语言检查的
capability——比对文本是否符合语言格式。"

skill 首次全新能力实战（CAP-006~009 为存量改造）。按七步纪律全程执行，本 change
同时是 skill 的实地检验。

## 买方五问（skill 第 1 步）

| 问 | 答 |
|---|---|
| 谁消费结果 | Runtime Integration 诊断面（post-commit 检查；§8.1 先例：ObservationAccepted → Finding）；买方证据=所有者指令 |
| 输入 | `LanguageInspectorRequest`（同 capture 有界文本投影 + expectedLanguage；既有 Host-neutral 契约，IntegrationContracts.cs） |
| 输出 | `Finding`（disposition Pass/Violation/Unknown + coverage/diagnostic；既有契约） |
| 是否动主权威 | 否（Finding 默认非权威；进 Assurance 须另立 Promotion——§8.1 明文冻结） |
| 失败策略 | 缺输入/不完整 → Unknown；不支持的 expectedLanguage → Unknown（诚实不猜）；无文本可检 → Unknown |

## Scope

- Kernel L1：`ILanguageInspector : ICapability`（操作面 `Inspect(request) → Finding`，
  同步确定性）+ `LanguageInspectionProtocol` 常量（id `runtime.language-inspector`，
  协议 Language Inspection@1.0——沿用 Simulation fixture 同名协议）。
- Kernel L2：`LanguageFormatInspector`（确定性 Unicode 脚本规则：expectedLanguage
  归一化前缀 → en=Latin / zh=Han；字母字符须属期望脚本，数字/标点/空白中立；
  违例 Finding 带发生项定位；ctor 可注入声明 + canonical 缺省 + WithDescription
  同表视图先例）。R7 健康豁免（纯计算），RL1 常驻无资源。
- Registry 执法（ModelRouting 先例同构）：`CapabilityCategory.LanguageInspection`
  新类别；scope=RuntimeIntegration、恰一协议、角色镜像、依赖-关系镜像（与
  ModelRouting 公共形状提取共享 helper）；**拒绝 description-only**；双向接口
  一致性（声明该类别必须 ILanguageInspector 实例；反之亦然）。
- Host 组合根：`RuntimeIntegrationCapabilityComposition`（ADR-0035 三域独立——
  Runtime Integration 域独立 Registry + 实例注册）；Program.cs 建立第二注册表，
  capability-facts 落盘合并双域事实。
- 白名单 +3 型；文档同步（hub README 实现现状 / Kernel Capability 代码归属 /
  skill worked example 增 CAP-012）。

## Out of scope

- 不支持语言的扩表（fr/ja/… 等真实买方出现再加——Unknown 诚实）。
- 不接 Assurance/Effect（Finding 非权威边界）。
- 不做异步/批量调度（post-commit 订阅属 Hub push 留白）。

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | L2 规则契约 | en/zh 各 Pass/Violation；混脚本报违例带 occurrence 定位；缺输入/不支持语言/无文本 → Unknown；Finding 形状（sourceOwner/disposition/status） |
| A2 | Registry 执法 | 实例注册往返（RuntimeIntegration 域）；description-only/错 scope/错协议/非 ILanguageInspector 实例/接口-类别错配均 fail-closed |
| A3 | 组合根 | 独立域注册表 + Resolve 取回；Program.cs facts 合并双域 |
| A4 | R5 可替换性 | 同一 ILanguageInspector 消费闭包喂 L2 与 stub 双实现，契约互换 |
| A5 | 兼容 | 全量测试绿；白名单 +3；场景再认证 |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · §8.1 协议冻结与 Simulation fixture 先例取证完毕；编号避让并行会话（CAP-010/011 已被其占用）；七步启动。


## Verification（2026-10-06 回填）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet build` | 0 error | 0 error、0 新增警告 | 本地构建 |
| DETERMINISTIC | Kernel.Tests 841（LanguageFormatInspectorTests 9 新例：en/zh Pass/Violation、locale 归一化、违例带 occurrence+码点、三类诚实 Unknown、canonical 形状、注册执法 3 例、R5 双实现互换） | 全绿 | PASS 841/841 | `dotnet test tests/UniClaw.Kernel.Tests` |
| DETERMINISTIC | Host.Tests 178（RuntimeIntegrationCapabilityCompositionTests 3 新例：独立域实例注册、显式实例+重复 fail-closed、跨域隔离） | 全绿 | PASS 178/178 | `dotnet test tests/UniClaw.Host.Tests` |
| DETERMINISTIC | 全量 + 场景哈希 | 全绿 | 1401/1401（Simulation 188 含 CAP-012 再认证 20 块） | `dotnet test UniClaw.Kernel.slnx` |

实现期修正：params 命名参数与 null 元组推型两处编译错（测试代码层）；单协议类别
执法形状提取为共享 helper（ValidateSingleProtocolCategoryShape，ModelRouting 行为
不变——其既有测试全绿证明）。

## Status log（续）

- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED（所有者验收同轮）· 七步全程
  落地：L1+L2+协议常量 → 类别执法（共享 helper）→ 独立域组合根+Program.cs 双域
  facts → 白名单 +3 → 测试 12 新例 → 文档三入口同步 + skill worked example →
  场景再认证 → 全量 1401/1401。skill 首次全新实战通过，无需修订（见交付报告的
  检验备注）。

## 重开记录（2026-10-06 所有者纠偏）

所有者反馈：本 change 走了路径 B（全新能力）却**未过细节拷问门**——粒度（菜单/
菜单项/文本项）、白名单/豁免机制、期望语言来源等领域语义设计决策被实现方自行
拍板，属语义假设缺陷（uniflow A7：Review 语义缺陷 → RESOLVE）。处置：

1. skill 判断门已改双路径 + 细节拷问门（本 change 为教训案例入 skill）。
2. L1/L2/注册执法骨架保留（协议面 Language Inspection@1.0 与 Finding 契约
   不受影响）；**规则语义（粒度/白名单/来源）按所有者对拷问轮的裁决重定**，
   实现随之修订。验证四元组在重定后重新回填。

## Decisions（细节拷问门裁决，2026-10-06 所有者两轮）

| # | 裁决 | 要点 |
|---|---|---|
| D1 | 粒度（Q1） | 文本项级原子判定 + 两层豁免：菜单级忽略区（Wi-Fi 列表等 SSID 任意语言区，按既有页面词汇声明）+ 全局术语白名单（WLAN 等跨语言名词） |
| D2 | 白名单（Q2） | 全局词表独立文件（`.dsh/profiles/language-inspection.yaml`），词条级、版本控制；先不做模式/分层 |
| D3 | 期望语言来源（Q3） | 任务配置声明为判定源（沿 settings-coverage 先例）；设备 locale 读取仓库不存在，列诊断增强另议 |
| D4 | 违例处置（Q4） | §8.1 既有冻结：非权威 Finding，进判定须另立 Promotion |
| D5 | 集成（Q5） | 能力本轮就绪；runner 接线（每 capture post-commit 喂文本投影 + findings 落盘）独立小 change；`languageInspection` 任务配置块随接线落地（当前无消费者不预造） |
| D6 | 语言集（Q6） | en/zh；混合容忍来自白名单而非扩语言集 |
| D7 | 剖面形状（Q7） | Summary + 生效配置 + 影响披露 + **Limitations**（四件） |
| D8 | 白名单缺失缺省（Q8） | 照跑 + 披露影响（违例照报，profile 写明原因）——不把"没配词表"放大成"能力不可用" |
| D9 | 剖面消费面（Q9，所有者修正推荐） | **uni agent 为消费面**（只有它有智能分析能力）：能力供结构化事实（落盘为事实源），用**模板/提示词引导 agent** 表达能力边界与范围；事实归能力、表达归 agent。最小查询入口不预造 |

新增 Kernel 面：`ICapabilityProfileReporting`（第三个可选 mixin，与 Health 同构：
拉式只读、Hub 不参与）。语言检查为首个带剖面能力。

## Verification（重定后回填，2026-10-06）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet build` | 0 error | 0 error、0 新增警告 | 本地构建 |
| DETERMINISTIC | Kernel.Tests 845（新增 4 例：白名单整词剥离/子串不豁免/剖面四件含 D8 披露/空白词条 fail-closed/接口面） | 全绿 | PASS 845/845 | `dotnet test tests/UniClaw.Kernel.Tests` |
| DETERMINISTIC | Host.Tests 178（组合根回归：默认实例自动加载白名单文件） | 全绿 | PASS 178/178 | `dotnet test tests/UniClaw.Host.Tests` |
| DETERMINISTIC | 全量 + 场景哈希 | 全绿 | 1405/1405（Simulation 188 含再认证 20 块） | `dotnet test UniClaw.Kernel.slnx` |
| CONTRACT | 剖面事实源 | Program.cs 双落盘点写 capability-profiles.json（D9：agent 消费） | 已接线（coverage/traversal 两处） | src/UniClaw.Host.Dsh/Program.cs |

## Status log（续）

- 2026-10-06 · RESOLVE（重开）· 细节拷问门两轮九裁决（D1-D9）落档；skill 同步
  （剖面面 + agent 消费模板，D9 修正推荐：消费面=uni agent）。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 按裁决重定：Kernel 第三
  能力面 ICapabilityProfileReporting（CapabilityProfile.cs，+3 白名单型）→
  语言检查升级（白名单整词剥离 + DescribeProfile 四件 + D8 缺省披露）→
  白名单文件 .dsh/profiles/language-inspection.yaml（WLAN 等 8 词条）+
  Host 加载器（缺文件照跑）→ 组合根默认加载 → Program.cs 剖面落盘。
  D5 遗留：runner 接线（每 capture post-commit + languageInspection 任务块）已在
  CAP-013 落地；Agent 选择 transport 已由 CAP-014 的
  `AgentTaskEnvelope.task.initialization` 固化，能力 profile 首次请求投影由
  CAP-015 固化。
