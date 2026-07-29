## Context

UniClaw 当前已经有 Android Emulator 脚本、ADB 截图与动作适配、`PageAnalyzer`、`TraversalPlan`、`TraversalEngine`、屏幕状态和 JSONL trace，但缺少负责组装这些组件的可运行 Host，也缺少把真实设备测试输入、逐步证据和问题反馈保存成稳定资产的约定。部分既有实现仍未达到已归档规格，例如入口策略存在假成功路径，ADB 错误可能被折叠为“不可滚动”，真实 provider 测试也没有穿过完整执行链。

本 change 以 Android AOSP Settings 为首个产品验收对象。场景 1 验证目标定位闭环；场景 2 在严格边界下验证一级菜单枚举和危险操作跳过。设计遵守现有 Core/Device/Provider 依赖方向，不修改 `TraversalState`、`GlobalState`、`TypeHint`、`SelectionState`、`SpanType` 等锁定枚举。

## Goals / Non-Goals

**Goals:**

- 提供一个显式、可取消、可重复的 emulator-first 运行入口。
- 每一步严格执行“观察 → 分析 → 生成短计划 → 安全判定 → 执行 → 再观察 → 验证”。
- 让两个 Settings 场景使用版本化、可校验、可冻结的输入资产。
- 让危险、未知、越界动作在进入 ADB 前被确定性拦截，并留下证据。
- 让成功、失败、阻断和取消均产出完整结果与可定位问题。
- 先完成场景 1，再通过多轮真实运行收敛问题，最后启用场景 2。

**Non-Goals:**

- 不做无限深度的整个 Settings 树递归遍历；场景 2 仅覆盖 Settings 首页可发现的一级入口及其只读页面快照。
- 不在本 change 中实现任意 App、iOS、真机农场或默认 CI 中的 GUI 测试。
- 不允许 AI 独立决定安全性，也不允许 AI 输出绕过确定性策略的原始 ADB 命令。
- 不修改 Core 锁定 enum，不把 Device/Provider 具体实现引用引入 Core。
- 不以首次运行全绿为目标；测试暴露的问题必须结构化记录并进入后续迭代。

## Decisions

### 1. 使用独立 Host 作为组合根

新增 `src/UniClaw.Host/` 可执行项目，引用 Core、Device 和所选 Provider，并负责配置、DI、命令解析、设备选择、取消与退出码。Core 保持平台无关，Device 继续拥有 ADB 具体实现。

首批命令：

- `doctor --device <serial>`：只读检查 ADB、启动状态、截图、UIAutomator 和 provider 配置。
- `analyze --device <serial>`：只截图并输出 `PageAnalysis`，不执行设备动作。
- `run --scenario <file> --device <serial> --output <dir>`：执行单个场景。
- `run --scenario <file> --repeat <n>`：串行重复运行并生成迭代汇总。

模拟器启动保持显式。调用方可先执行现有 `scripts/android-emulator.sh start`；Host 不因 `run` 隐式创建或下载 AVD。若未来增加显式 `--start-emulator`，必须复用项目 emulator boundary，并记录本次运行是否拥有该进程；只有拥有者可以自动停止。

备选方案是把测试入口放入 xUnit。拒绝该方案，因为真实 provider、设备生命周期、Ctrl+C、运行资产和非零退出码属于产品 Host 责任，不适合由测试进程承担。

### 2. 场景是版本化 JSON，运行时冻结输入

场景文件位于 `scenarios/android-settings/`，使用 System.Text.Json 可直接处理的 JSON，不引入 YAML 依赖。V1 使用受校验的字符串词汇而非新增 enum。

公共字段：

- `schemaVersion`、`scenarioId`、`description`
- `appPackage`、`entryStrategy`
- `mode`：`locate_one_item` 或 `enumerate_first_level`
- `target` 与本地化 aliases
- `boundaries`：允许页面、最大深度、最大步骤、最大滚动次数、最大时长
- `allowedActions`
- `safetyPolicy`
- `successCriteria`
- `resetProcedure`

每次 run 将已解析和规范化的场景复制为 `scenario.snapshot.json`。后续修改源场景不会改变历史运行的解释。

备选方案是直接把每个场景写成 C# 测试。拒绝该方案，因为场景将无法独立版本化、人工审阅、重放或供未来其他执行器消费。

### 3. 长期意图与逐步短计划分离

场景在启动时被确定性编译为现有 `TraversalPlan`，表达入口、范围、完成条件和预算。每次截图分析后，执行器基于当前 `PageAnalysis`、Traversal 状态和场景边界生成一个 `step-plan.json`，最多包含一个设备动作及其预期变化。

运行循环：

1. 捕获 `before.png` 与 `before.xml`。
2. 生成并保存 `analysis.json`。
3. 生成并保存 `step-plan.json`。
4. 将候选动作送入确定性安全门。
5. 若允许，执行动作；若拒绝，记录 skip 且不调用 ADB action。
6. 捕获 `after.png`、`after.xml` 和新的页面分析。
7. 保存 `verification.json`，判断预期页面/状态变化是否发生。
8. 更新 traversal 状态并继续、成功结束或分类失败。

备选方案是首屏分析后一次生成完整路径。拒绝该方案，因为设置菜单会因 Android 版本、屏幕尺寸、滚动位置和 provider 识别差异发生变化，长计划容易陈旧且难以审计。

### 4. 安全采用“确定性门控 + 默认拒绝”

安全门位于所有真实动作到 `IActionExecutor`/ADB 的唯一前置位置。规则输入包含动作类型、目标文字、页面路径、行为语义、坐标来源、场景允许动作和剩余预算。判定优先级固定为：

1. 边界或预算违反 → 拒绝。
2. 危险结构/关键词命中 → 拒绝。
3. 动作不在场景 allowlist → 拒绝。
4. 必要目标信息缺失或识别置信不足 → 拒绝。
5. 已知安全导航、返回或滚动 → 允许。
6. 其他未知情况 → 拒绝。

V1 允许 `click` 已识别的一级导航行、`back` 和受预算约束的 `scroll`。V1 拒绝 toggle、输入、长按、安装、卸载、禁用、清除数据、删除账户/凭据、重置、恢复出厂、格式化、购买/支付、授权和开发者破坏性操作。危险词库是版本化配置，但结构语义优先于文本；AI 安全判断只能增加拒绝，不能放行确定性拒绝。

场景 2 遇到危险一级入口时不进入该页面，只记录 `skipped`。进入普通一级页面后只采集标题和可见菜单，不点击其内部控制。

### 5. 场景 2 的“所有项”采用明确可证明的边界

“遍历所有项”定义为：从 Settings 首页起，在最大滚动/步骤/时长预算内，枚举滚动到列表末端所发现的所有唯一一级入口。唯一键由规范化文本、可选 resource-id 和首页页面身份组合；坐标不作为唯一身份。

对每个安全入口执行“进入 → 采集页面标题与可见条目 → 返回首页”。返回后必须验证 Settings 首页身份和滚动恢复/重新定位结果。若无法证明到达列表末端，则结果为 incomplete/failed，不得声称全量完成。

### 6. 运行资产采用 append-friendly、按步骤隔离的目录

默认输出：

```text
artifacts/runs/<scenario-id>/<run-id>/
├── manifest.json
├── scenario.snapshot.json
├── plan.json
├── steps/
│   └── 0001/
│       ├── before.png
│       ├── before.xml
│       ├── analysis.json
│       ├── step-plan.json
│       ├── safety-decision.json
│       ├── after.png
│       ├── after.xml
│       └── verification.json
├── trace/
│   ├── session.json
│   └── trace.jsonl
├── issues.jsonl
└── result.json
```

`manifest.json` 保存 run ID、iteration ID、parent run ID、git revision、场景哈希、设备、Android 版本、provider/model、开始时间和资产版本。`issues.jsonl` 采用只追加记录，包含 category、phase、step、severity、summary、evidence paths、fingerprint 和 disposition。`result.json` 是权威最终摘要，并通过相对路径引用证据。

截图、UI XML、prompt/response 和异常在写盘前执行 secret/PII 最小化；API key、Authorization header 和原始 provider credential 永不进入资产。

### 7. 迭代串行执行，问题按指纹聚合

同一设备上的 emulator smoke 串行执行，避免共享 ADB serial、Settings 状态和输出目录冲突。每次运行前执行场景 `resetProcedure`，至少回到 Settings 首页并验证入口页面。`--repeat` 为每个子运行分配独立 run ID，再输出聚合报告：

- 成功率与连续成功数
- 每一步耗时和动作数
- 新增、重复、已消失的问题指纹
- 安全拒绝次数和原因
- 无法证明完成的原因

验收顺序是：离线 contract/unit → mock provider + emulator → real provider 单次 → 场景 1 连续 10 次 → 场景 2 单次与问题收敛 → 场景 2 连续 10 次。真实 provider 结果不作为默认无凭据 CI 的前置条件。

### 8. 既有规格缺口在组合前收敛

实现时先修复与闭环直接相关的已有规格偏差：

- `EntryPolicyExecutor` 必须真实执行入口动作并验证等待条件，不能伪成功。
- ADB command 统一处理 serial、timeout、cancellation、stdout/stderr 和结构化错误。
- `IScreenStateProvider` 必须区分 ADB/解析失败与真实 no-scroll/end-of-list。
- 所有 action、safety skip、AI call、page analysis 和故障均关联到同一 trace/session。

这些工作是让实现满足现有 canonical specs，不在本 change 中改写其既有要求。

## Risks / Trade-offs

- [Android Settings 在版本/OEM/语言间差异大] → V1 固定 AOSP emulator 版本与语言，并把 aliases、设备信息和场景哈希写入 manifest；OEM 扩展后续独立提案。
- [视觉模型输出不稳定] → 保存截图、UI XML、原始规范化分析和验证证据；使用短计划与重复运行暴露波动。
- [危险词库漏判] → 结构语义和 allowlist 优先，未知默认拒绝；场景 2 不操作子页控件。
- [安全规则误杀导致覆盖不足] → 结果区分 skipped 与 failed，并在报告中列出每个拒绝原因，不以放宽默认策略换取通过率。
- [运行资产体积增长] → 每次 run 独立目录并允许配置保留策略；删除/压缩策略不属于本 change。
- [回到首页或滚动恢复不稳定] → 每个入口后验证页面身份，失败即停止当前入口并记录问题，不盲目继续。
- [真实 provider/API 不可用] → mock provider emulator smoke 作为确定性主回归，real provider 作为受保护的显式层。

## Migration Plan

1. 先引入场景 schema、资产 schema 和纯函数安全策略，以单元测试锁定契约。
2. 收敛 ADB runner、入口策略和 screen-state 错误语义。
3. 新增 Host 的 `doctor`/`analyze`，验证只读链路和 trace。
4. 实现场景 1 运行循环，用 mock provider 完成 emulator smoke，再接 real provider。
5. 连续运行场景 1，按问题指纹修复直到满足稳定性门槛。
6. 启用场景 2，先验证危险跳过，再验证一级菜单覆盖和完成证明。
7. 将稳定场景加入显式 emulator 测试入口，保留默认 unit/simulation 测试集。

回滚时可移除 Host 和场景入口而不改变 Core 的 canonical 数据模型；Device/Traversal 的规格对齐修复应保留。运行资产带 schema version，旧结果保持只读，不做破坏性迁移。

## Open Questions

- V1 基线暂定使用项目当前配置的 AOSP emulator、英文 Settings 和单一 serial；具体 API level 在 apply 开始时从现有 AVD 配置固化到场景 manifest。
- 场景 1 的默认目标暂定为 `About phone`，但目标作为场景参数，可在不改代码的情况下切换为 `Network & internet` 或 `Battery`。
