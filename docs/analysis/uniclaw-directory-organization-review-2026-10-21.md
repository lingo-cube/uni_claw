# UniClaw 组件目录组织审查（2026-10-21）

> Status: COMPLETE / READ_ONLY_REVIEW
> Authority: NONE

## 结论

现有根目录按“产品代码 / 产品声明 / 外部实现 / 工程工具 / 开发治理 / 验证数据”分区，方向合理，应保留。当前问题主要是导航不完整、局部职责平铺、历史命名与真实职责不一致，以及源码/运行产物混放；不需要先把整个仓库改成另一棵树。

对熟悉历史的人，当前目录基本可用；对新维护者和新 Agent，还不能只看目录就确定模块职责、真实入口、源码真相与产物位置。建议先补导航和来源声明，再处理源码树内的运行产物，最后逐组件做局部目录整理。

本轮依据当前 `bce0ec3a` 工作树（含并行未提交内容）、仓库职责基线、源码 README 归属测试及 docs 放置规则进行只读盘点。未移动、删除目录或修改产品源码；仅新增本报告，补齐上一份审查报告的元数据和两份报告的索引。下述目标布局是候选，不是已接受的目录迁移决定。

## 一、分区逐项判断

| 当前路径 | 判断 | 直观性/管理问题 | 建议 |
|---|---|---|---|
| `src/` 六个 .NET 项目 | 基本合理 | Agent、Kernel、Host 与 DSH Adapter 命名可区分；但缺 `src/README.md` 统一导览 | 保留项目划分，增加依赖图与模块入口；不先拆程序集 |
| `web/uniclaw-workspace/` | 清晰 | canonical Core、controller、view-model、UI、adapter 已分区 | 保留；根入口显式说明 DSH 使用它，而非独立 Workspace |
| `dsh/` | 基本清晰 | 产品集成插件与 `.dsh/` 宿主配置容易混淆；decision-channel 还携带发布副本 | 补“源码/发布副本/安装位置”对应表 |
| `.dsh/`、`product/` | 职责合理 | 同一功能声明与绑定分处不同路径，需知道关联关系 | 保持分离，以功能 ID 做导航，避免为了集中而混放 provider/model/endpoint |
| `platforms/perception/` | 命名不理想 | 实际是独立感知 Provider，读目录容易误认成通用平台层 | 按现有基线候选迁往 `providers/perception/`，需独立变更，当前入口不动 |
| `schemas/` | 职责合理，命名不均衡 | `workspace/` 按产品语义命名，`agt-002/` 按历史 Change 命名；根部缺总索引 | 先索引当前 schema→生成源→消费者；后续再评估语义化目录名 |
| `tools/` | 大体合理 | 稳定验证入口、生成器、环境脚本、日期实验分区不足；缺总入口 | `tools/experiments/` 的已有隔离值得保留，先补支持级别/输入输出/命令导航 |
| `tests/` | 项目级合理，项目内不均衡 | Kernel 根部 45 个 C# 文件与 World/Runtime/Effects 等子目录并存 | 新测试按责任域或明确跨域场景归档；历史测试分批整理 |
| `testsets/`、`scenarios/`、`evidence/` | 分离正确 | 三者看似相近，但分别是测试资产、场景定义、实测证据；运行证据体量大 | 保留，补 evidence 入口/索引/留存说明；不合并为一个 tests/data |
| `docs/architecture/`、`adr/`、`design/`、`analysis/`、`prd/` | 分类正确 | 头部状态和索引没有完全跟上内容增长 | 修导航与索引，不重新建第二套文档体系 |
| `research/` 与 `docs/research/` | 含义重叠 | 分别有 2、7 个受版本管理文件，没有目录级 README 说明区别 | 按文档内容和状态归并到已定义文档类别；保留跳转和历史引用，不直接批量搬 |
| `changes/`、`plans/`、`workitems/` | 管理语义清楚 | 是意图、执行计划和瞬时委派的不同生命周期 | 保留，避免一起放进通用 tasks/ |
| `.agents/` | 正确 | Harness 控制面与产品实现结构隔离 | 保留 |
| `.perception/`、`bin/`、`obj/` | 正确 | 已作为生成/缓存状态处理 | 不作为源码清理目标 |

## 二、最重要的组织问题

### 1. 总导航缺失，必须靠历史知识拼出全仓库

现场没有根 `README.md`，也没有 `src/README.md`、`tools/README.md`、`tests/README.md`、`schemas/README.md`、`web/README.md`。AGENTS.md 是约束入口，不适合承担全部开发者导航。

具体成本：寻找“语义感知”需要知道 Kernel/Capability、Kernel/Perception、Host/Perception 和外部 Provider 的关系；寻找工作台要横跨 web 与 dsh；单看 `UniClaw.Kernel.slnx` 名称又不容易知道它已包含全部六个产品项目和八个测试项目。

**先做什么**：根 README 只列仓库地图、产品入口、验证入口和权威文档链接；src README 给程序集依赖图和组件→代码映射；tools/tests/schemas 索引承载各自入口。只做导览，不复制基线语义。

**验收**：新维护者能从一个入口找到产品启动、组件实现、契约源、对应测试与证据；常用模块最多通过两到三次目录/链接跳转定位。

### 2. 架构组件和物理目录的对应关系不够直观

- Capability Plane 是 T1 peer，代码却在 `UniClaw.Kernel/Capability` 与 `Perception` 两个平级目录。
- `UniClaw.Kernel/Capability/CapabilityHub.cs` 同时承载 Hub、根接口、感知协议接口、description 和类别校验。
- `UniClaw.Kernel/Outcome/` 物理独立，README 却将它归入 Run Model。
- `UniClaw.Core/` 与 `UniClaw.Kernel/Core/` 各有职责，名字容易让人误认成两个“核心层”。

这不是依赖越界的直接证明，而是导航和变更归属成本。已有 Kernel README 维度表能解释，应该保留并让上级入口可达。

**先做什么**：建立一份权威链接驱动的组件映射，明确“逻辑角色≠程序集”。暂不新建 `UniClaw.Capabilities` 项目，不为让目录像架构图而引入新的发布/依赖边界。

**后续候选**：能力代码继续增长时，按真实能力聚合协议与实现，Hub 管理逻辑独立；不要先按 Interfaces/Models/Services 横向拆散每个能力。Outcome 如整理回 Run，保持公开类型与协议不变；必须先核对引用与 ownership 测试。

### 3. Host 与 Runtime 局部平铺，缺少按变化原因定位的目录

Host 根部当前有 13 个 C# 文件：HostRunner、LivePerception、UiAutomatorDump、UiHierarchyOccurrenceStrategy、SettingsTraversalLiveFeed、SettingsPageIdentity 等并列，混合组合根、观察采集、观察策略和 Settings 任务接入。Host/Perception 又只有新的 Jev 语义 Adapter，容易误以为那里包含完整感知接入。

Runtime 根部当前有 11 个 C# 文件，Agent 决策词汇、咨询 journal、Policy、Plan、Traversal、handler 与主 Driver 并列。找某个变更面时仍需全文搜索。

**候选局部组织**：

```text
src/UniClaw.Host/
├── HostRunner.cs / Program.cs         组合入口
├── Observation/                      截屏、XML、feed 与观察策略
├── Perception/                       Provider-specific 语义供货 Adapter
├── Capability/                       能力注册与 task binding
├── Runtime/                          身份、激活、journal 持久化
└── SettingsCoverage/                 Settings 任务接入、覆盖、策略、投影

src/UniClaw.Kernel/Runtime/
├── KernelRunDriver.cs                唯一主推进入口
├── Consultation/                     决策词汇、context、journal 缝
├── Policy/                           策略协议与求值
└── Plan/                             Plan、Traversal、handler 执行词汇
```

这是候选分组，不是逐文件迁移表。Observation/ 与 Perception/ 的区分必须先写清“采集/接线”与“具体语义供货”，如果仍需大量双向跳转，就不应该为了整齐强行分开。少量文件的职责已经明确时，也不必各建一个空目录。

**关键限制**：移动文件不会解决上轮审查中的 Kernel judgment 或 Driver 状态耦合；目录整理和职责修复应分别验收。

### 4. 源码树内存在受版本管理的运行产物

现场 `src/UniClaw.Host.Dsh/runs-d9/` 有 **164 个 tracked 文件**，包括 run 目录、capability facts、coverage report/steps 等；同项目还存在 untracked `evidence/`。`.gitignore` 只匹配该项目的 `runs/`，没有覆盖 `runs-d9/` 这种变体。

**管理影响**：源码查找与运行记录混杂；新开发者不易判断何者是测试输入、何者是历史证据；只忽略一个固定目录名不能约束运行产出位置。

**建议**：新运行统一写入明确的运行数据根；需版本化保留的精选验证产物进入对应 `evidence/<change>/<run>/`，有 source/revision/correlation；静态测试输入进入 testsets。历史 runs-d9 必须先扫描引用、哈希、读取消费方和复现用途，再迁移并同步路径。`.gitignore` 对已经 tracked 的文件不起迁移作用，也不能靠放宽 ignore 丢失证据。

`platforms/perception/evaluation/reports/` 还有 133 个 tracked 文件。Provider 自有评估报告可以合理共置，不与源码树内的临时 runs 一概而论；应明确哪些是版本化基线、哪些是可再生预测/临时输出，不能直接删除或全量移走。

### 5. 文档索引已落后于实际内容

写入本报告前，现场统计：analysis 下 34 份 Markdown 正文，18 份没有在目录索引点名；design 下 33 份正文，10 份未进索引。计数包含当前工作树新增内容，是“索引未覆盖”，不代表这些文件都应删除或都属于过期文件。

上轮新建的模块边界报告也遗漏了规定的 Status/Authority 头部和索引。本轮已补齐自己的两份报告登记；没有扫改其他并行文档。

**建议**：新增文档同时同步索引；索引可从头部生成，但生成器和 schema 必须复用现有状态规则，不另造文档生命周期。旧稿 superseded 后保留明确替代链接，不能让“最新”靠文件日期猜测。

### 6. 测试目录与产品模块的镜像不足

Kernel.Tests 中已有 Capability/Runtime/Perception/World 等目录，但 RunIdentity、TerminalOutcome、ControlToEffect、EvidenceToBelief、UIWorld 系列仍散落根部。

**建议**：组件契约/单测优先与对应职责目录映射；跨组件场景单列，保留其跨域性质，不把所有测试硬塞进某一个 Owner。模拟测试留在 Simulation.Tests，前端/插件/Python 测试继续就近放在各自包内，没有必要集中到 .NET tests 根目录。

**验收**：从组件入口能找到对应测试，跨域场景能反查涉及的协议；移动不改变测试发现、fixture 路径、认证输入或执行结果。

### 7. 发布副本、历史命名和临时目录需要明确身份

- `product/prompt`、`product/skills` 和 `schemas/agt-002` 与 decision-channel 包里的发布副本同时存在：这是合理的源码→发布关系，应通过生成/拷贝校验管理，而不是删除其中一份。
- `dsh/unicloud-decision-channel/` 与 `uniclaw-decision-channel/` 名字相近；前者目前只有未跟踪 schema 文件，不能据此认定是重复产品。需明确所有权和用途后再处置，本轮保留。
- `research/`、`docs/research/` 与 docs 的既有五类放置规则之间没有清楚的总导航：按内容状态归类，不能凭目录名决定权威。
- 根部 `.tmp-*`、缓存、运行态是否忽略和保留，需看对应任务所有权；不因为目录看起来临时就自动清理。

## 三、建议保留的根目录骨架

```text
uni_claw/
├── README.md                         候选新增：全仓导航
├── AGENTS.md                         开发约束入口
├── src/                              .NET 产品源码及 Adapter
├── web/                              canonical Workspace 前端
├── dsh/                              DSH 插件与发布装配
├── providers/                        目标语义；当前仍为 platforms/perception
├── product/                          Profile、任务、Prompt、Skill、Policy 声明
├── schemas/                          跨语言协议、生成契约
├── tools/                            稳定工程工具与隔离实验
├── tests/                            .NET 可执行测试
├── testsets/                         静态测试资产
├── scenarios/                        场景定义与期望
├── evidence/                         精选可追溯验证记录
├── docs/                             架构、ADR、设计、分析与指南
├── changes/ / plans/ / workitems/     三种不同开发生命周期载体
└── .agents/ / .dsh/ / .perception/    控制面、宿主配置、生成状态
```

这棵树不表示 Memory、Capability Plane 或其他 T1 都必须有独立工程目录。目录、程序集、产品拓扑是不同维度，应通过导航建立对应关系。

## 四、执行顺序与验证

1. **先补导航**：根/src/tools/tests/schemas 索引、组件映射、文档登记和发布副本来源说明。成本低，不改变行为。
2. **再分离产物**：先建立新输出位置和保留规范，再迁移已有 runs-d9 的精选证据。迁移前保留 manifest/hash 和原路径映射。
3. **随后按实际组件整理**：Host、Runtime、Capability 和测试分别做小范围变更，不与业务修改混成一次大移动。
4. **最后处理历史名称**：platforms→providers、agt-002→语义名称、research 分类。仅 `platforms/perception` 在排除 evidence/changes/plans 后仍被 136 个 tracked 文件引用（包含文档，不等于 136 个运行依赖），迁移不是一次简单 rename。

已有管理能力应继续复用：`SourceReadmeOwnershipTests` 逐目录和文件 stem 执法源码归属；`DocsMetadataTests` 执法文档类型/状态；协议与 schema 生成检查负责副本一致性。归属表、路径常量和入口脚本都要随实际迁移同步，不能只让文件树看起来整齐。

本轮 method 为文件树、Git tracked/untracked 区分、直接 C# 文件数、索引覆盖与引用盘点；expected 为职责可定位、源码/产物可区分、当前/候选路径明确；actual 为大分区合理、上述七类管理摩擦成立。未运行测试，也未声称任何候选迁移已验证；未搬迁、删除或清理既有目录。
