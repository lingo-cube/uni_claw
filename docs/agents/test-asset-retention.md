# 开发测试资产的归属、保留与收尾

本文件是开发侧诊断、重放、仿真和验证资产的统一规则。UniFlow拥有生命周期；本规范只规定资产如何保存、引用、归档和清理。Change状态仍只由`changes/<Change-ID>/state.md`定义。产品任务的正式结果、恢复来源及必交付报告来源沿用各自Owner契约。

## 1. 归属与存储

每份过程资产必须归属于实际开发任务；每次真实执行使用独立Run ID，记录测试范围、源码/输入/配置指纹及执行原因。资产角色不由后缀或目录名推断。

| 位置 | 内容与寿命 |
|---|---|
| `changes/<Change-ID>/state.md` | 任务状态、验收与结果引用；不复制成另一套状态 |
| `changes/<Change-ID>/assets.json` | 任务资产索引：所属Run、用途、大小、SHA256、保留规则、基线引用和归档位置 |
| `changes/<Change-ID>/evidence/` | 诊断/验收所需最小证据与紧凑摘要；必要结果可压缩，随任务处置 |
| `testsets/`、既有正式fixture和`scenarios/` | 正式输入、Oracle及场景；独立于开发任务，继续使用已有manifest |
| 仓库外的任务工作根下`<Change-ID>/runs/<Run-ID>/` | 本轮raw、logs、results、scratch；创建者负责收尾 |
| 仓库外的归档根下`<Change-ID>/` | 有持续用途的任务归档包及内部文件清单，不形成永久散落的evidence库 |

新运行按任务隔离目录，跨仓库工作根也必须隔离。根路径由本地执行配置提供；共享清单使用逻辑根和相对路径，不记录凭据或把主机路径当资产身份。现有测试入口尚有仓库内输出目录：迁移前按明确文件清单处理，不能整目录移动或清空他人共享输出。

`assets.json`采用`schemaVersion: test-assets.v1`。最少字段：`changeId`、`runs`、`baselineRefs`、`archives`。每个Run记录`runId`、`purpose`、`sourceDigest`、`inputDigest`、`configDigest`、文件清单；每文件记录所属运行目录内的`path`、`kind`、`bytes`、`sha256`及`retention: run|task`。`retryOf`和验证批次标识按实际重复/重试需要记录。它是工具清单，不新增任务状态或产品协议。

- `run`：本轮结果整理和必要证据提取完成后可清理；不是进程一退出就无条件删除。
- `task`：仍供诊断、复验、审查或验收消费，随任务生命周期保留。
- 正式基线仅列`baselineRefs`，不归开发任务所有。晋升基线须按原审批/认证规则完成转存和依赖迁移，不能只改retention字符串。

第一版沿用JSON/JSONL/XML/PNG/TRX等文件原格式；目录索引与归档不改原始内容。verify-change 已接首个自动生产/读取切片：每次真实运行生成清单与收尾回执；tools/test-assets 可按旧布局定位并校验。首片保留验收材料，归档/清理及其他入口仍按各实现Change验收，不因清单存在即宣称完成。

## 2. 每次操作结束都收尾

诊断、重放、仿真、验证每次返回结果前均执行本节，包含失败、取消和提前停止。它是操作收尾职责；不等到整个Change完成，也不以清理成功替代测试成功。

1. 确认本轮写入已停止；由创建者枚举本轮产生的文件、独占目录、字节和用途。仍在写入/未知归属的材料保留并说明，不终止他人进程来清理。
2. 先保存结果、失败及跳过摘要、输入/源码/配置关联，再确定必须继续消费的最小证据。正式基线、封存case和下游仍需的引用优先保护。
3. 将资产逐项归入保留、归档、删除或未决；相同内容只作为去重线索。不得将资产自己的出站来源链接、旧整树快照中的偶然路径，误当实际使用；Owner核对须包含现存Change、计划/WorkItem中的明确归属和交付记录。
4. 对有归档用途的大材料先打包，再解包核对路径、字节数及每文件SHA256。只有归档本体与现有消费者的定位/读取方式都验证可用后，才能删除原件；只有归档指针不等于引用仍可消费。
5. 对已授权、明确属于本任务且无继续用途的临时/重复资产执行删除。删除前记录并复核当前字节与摘要，限制在所属目录；越界引用、符号链接或并行漂移不能靠忽略检查处理。源文件或他人产物不属于临时输出。
6. 核对已删路径确实不存在、保留项仍可读且摘要匹配，记录实际处置结果。清理失败保留未处理项及原因，不写“已清理”；不更改测试、诊断或业务结论。

已确认的任务自有临时清理沿既有授权执行，无须每轮重复询问。需要删除他人材料、未证归属或仍被消费的来源时，先解决归属和依赖；本规范不授权全仓扫描后盲删，也不授权Git内部维护或产品运行数据回收。

## 3. 三类流程的保留边界

| 完成出口 | 先保留/移交 | 当轮应清理 |
|---|---|---|
| 诊断返回 | 结论、所需等级的证据、可核对diagnosis packet及其全部有效引用、仍待定位问题的最小复现 | 无下游用途的临时日志、过量采样、独占构建/探针目录、可重建导出与重复源码副本 |
| 重放/修复证明返回 | 不可变case/输入/独立Oracle，规定的RED/GREEN及重复执行记录、候选差异、packet与有效引用 | 解包/编译缓存、过量中间actual、重复输入副本及无继续用途的候选工作目录 |
| 仿真/验证返回 | 所选测试范围、最终结果、失败/跳过与计数、源码/输入/配置绑定、最终验收要求的运行集合 | 消费完毕的中间TRX副本、重复coverage/report导出、多轮完整actual和独占构建副本 |

诊断结束不等于问题已修；仍将用于下一步的最小复现和候选工作材料不是多余资产。封存case、独立预期或有效packet不能为了腾空间重写hash、清空输入或留下悬空路径。旧路径不能迁移时，保留原件并记录理由；不假装已有归档读取支持。

## 4. 反复测试

一次真实执行一个Run ID，不覆盖上次结果。相同源码/输入/配置的重复执行可归同验证批次；重试用`retryOf`关联。任一输入、实现或配置变化须新批次，保留实际变化关系。

每次摘要保留失败、跳过和重试次数。普通中间成功运行只保留必要摘要；未解决的不同失败保留最小复现，不能只留下最后一次通过。最终验收要求连续多次或多配置结果时，应保留所需完整运行集合，允许压缩为一套证据；旧版本通过不替代当前失败。

## 5. 生命周期与引用

- 操作结束：当轮先收尾，再返回诊断包、重放包或验证结果；有意保留的材料写清继续用途，不算未完成清理。
- UniFlow VERIFY结束：复核任务汇总清单及已执行处置；CLOSED前完成资产收尾，未决清理如实记录，不能只列计划便称完成。不增加第九状态。
- 任务资产退役：按§12的只读计划和显式执行缝处理；其他任务、文档及正式基线的有效依赖、原来源和外部逻辑引用继续保护。旧片的归档/工作副本释放边界不代替任务退役，也不改写原Run条目或历史hash。
- 归档或显式退役：保留原事实内容不变，通过索引追加归档位置/摘要或退役说明；旧结果是历史记录，不冒称已删文件仍可重验证。

本次规则不追认存量孤立判断。存量仍按具体所有权与引用核对，`closed`、未跟踪、老日期、相同hash或“看起来像日志”均非独立删除判据。

## 6. 轻量回执

每个Run留一份任务内的处置回执或在既有结果索引中记录同等字段，避免复制多套清单。必须区分计划与实际，可按结果聚合。

```json
{
  "schemaVersion": "test-asset-finalization.v1",
  "changeId": "EXAMPLE-001",
  "runId": "run-001",
  "operation": "diagnosis",
  "completed": true,
  "kept": [],
  "archived": [],
  "deleted": [],
  "pending": [],
  "deletedBytes": 0
}
```

非空条目记录相对路径、字节、SHA256及处置/保留理由；归档还记录包定位和包SHA。摘要必须标明算法，Git blob SHA-1不能写成SHA256。`deletedBytes`是源文件逻辑字节数，不自动等于实际物理空间释放。无多余资产时可如实写零，不为凑流程生成快照或重新运行测试。

## 7. 现有入口的执行顺序

`verify-change`、重放CLI和诊断工具的调用者，须先完成TRX读取、coverage、失败信号、packet封装和其他真实消费者，再按本规范收尾。现有check-only入口不因本规范获得隐式删除权限；缺自动适配时由本次开发执行者显式执行清单并核对，不能把“工具未实现自动清理”当作免除收尾职责的理由。

实现新的自动适配时复用同一归属和保留规则，不另造生命周期、不提前删消费者需要的材料、不把清理失败改写为产品行为回归。该自动适配、存量批量迁移和产品运行数据回收不由本规范本身实现。

## 8. 开发布局首个实现入口

本地 JSON 配置字段为 version=1、绝对 parentRoot、稳定 repositoryKey、相对 workPath/archivePath，以及 categoryPaths(raw/logs/results/scratch)。CLI --asset-config > UNICLAW_TEST_ASSET_CONFIG > XDG_CONFIG_HOME/uniclaw/test-assets.json（未设置 XDG_CONFIG_HOME 时 ~/.config）。非法不回退，tools/verify-change CHANGE --print-asset-config 或 tools/test-assets resolved 可查询。

工作目录=parentRoot/workPath/repositoryKey/changeId/runs/runId，分类目录=本Run/categoryPaths[kind]；归档根=parentRoot/archivePath/repositoryKey/changeId。Run归档/恢复见§10，仍使用这一个布局来源。绝对分类/上跳/类别重叠/工作归档覆盖/链接逃逸均拒绝。改变配置只作用新Run，原Run以digest引用固定本机 ~/.local/state/uniclaw/test-asset-bindings 下的不可变布局绑定，不随profile或XDG_CONFIG_HOME改变；绑定文件应与工作根一并保留，丢失时明确报错，不猜当前根。

verify-change 的 TRX、coverage 和失败信号在同一 Run 完成消费，然后登记字节/SHA256和实际回执；取消会终止并等待本轮子进程组，保留部分计数与pending。首片文件默认 task 保留用于验收，deletedBytes=0，不扫描或清空旧 TestResults。来源指纹记录当时源码/测试输入字节，但不是全源码封存；活跃仓库输入继续保留，不允许凭这些指纹删除原始输入。

## 9. 重放与显式开发诊断的资产出口

`tools/test-assets replay CHANGE --case 文件`在调用侧使用同一布局和独立Run，原evolution-case.py、封存case/Oracle和REALIZATION不改写。源指纹同时记录当前实际字节与原声明SHA；输入变化开新批次，retryOf不得跨批次。CLI退出码与内部测试carrier退出码分别记录，TRX计数/失败消息和原replay.json先消费后finish，取消/超时不能被残留结果改成成功。

诊断适配通过显式DevelopmentAssetRequest接入；HTTP的developmentAssets只提供Change/config/retry，不替代既有模型路由或产品profile。无开发Owner的兼容调用仍按原路径，不能因此宣称所有旧诊断已迁移。begin/scope/finish由同一Python解析器处理，C#只消费已解析路径并在实际写出前拒绝链接重定向。产品原始report/facts/恢复与必交付来源不进入开发资产所有权；源码、输入和sealed case列为Run的protectedRefs，只有明确正式manifest继续列baselineRefs，不自动晋升。

`test-assets:CHANGE/RUN/相对路径`是开发资产引用；`tools/test-assets path-ref`按旧绑定、大小/SHA256得到本地路径供原消费者读取。诊断的正常返回、transport失败、显式取消和退出信号失败均在实际调用结束后尝试finish；finish失败通过单独字段/异常details说明，不替换原诊断结果。已完成Run拒绝再次finish以防重哈希；仍在写或未知归属的材料保留，不对存量或产品根执行清理。

## 10. Run归档与恢复读取

`tools/test-assets archive CHANGE RUN`保留原目录和文件内容，写独立ZIP并实际解包，校验完整成员集合、逐文件大小/SHA256、Run与原布局绑定，再调用已有TRX/失败信号读取器、packet原schema及其明确来源引用校验。索引只追加archives记录，不改历史文件清单、结果或摘要。包内外路径均受校验；重复/上跳/链接成员、错Run和内容漂移拒绝。未知格式仅声明字节保全，不冒称有该格式的领域消费者证明。

`verify-archive CHANGE ARCHIVE`只临时解包并核验，不恢复工作目录。`restore CHANGE RUN [--archive-id ID]`恢复原Run根；只补缺失文件，已有不同内容或未知文件/链接拒绝，不覆盖、不移走原Owner来源。`path`/`path-ref`读取归档Run时如有登记文件缺失，会先完整恢复，再验证字节及已知消费者；因此这两个读取入口可能发生受控本地写入。`resolved`不创建Run；`validate`仍只读，可从工作目录或临时解包检查可用性，不恢复目录，也不以好归档掩盖现有损坏文件。

文件字节可恢复与消费者依赖有效分开报告。外部输入丢失时仍可用restore取回原字节，但consumerStatus=pending、CLI返回1；validate的valid=false，path/path-ref拒绝以0返回成功。只有原Owner恢复正确输入并再次校验通过，才消除该未决；不会伪造或覆盖外部来源。工作树完整时validate检查工作数据，不据此声称备份包健康；备份另用verify-archive检查。

当前写入归档格式v2保留每文件mtimeNs，解包和最终恢复发布均保持原修改时间。现有覆盖率消费者用TRX时间拒绝陈旧结果，恢复不得把旧结果变成新鲜证据。早期v1包缺少精确mtime，继续兼容读取，恢复时采用保守Epoch 0并明确返回conservative-epoch-v1；旧包和摘要不重写。

本片不提供通用删源、自动GC或历史存量迁移；任务资产退役见§12。归档成功不构成删除许可；活动、未决、证明、待交付来源及独立baseline继续保护。资产文件恢复不授予产品续跑/执行权限，也不等于外部源码和输入的完整封存。旧片不追认任务删除能力，受保护材料仍按实际归属和消费者验收处理。

## 11. 明确登记的归档工作副本释放

仅新verification调用者可在`begin CHANGE --operation verification --work-release-reason 理由`创建Run时，明确声明：工作副本可在归档后释放，没有未完成的物理路径读写者，后续读取统一通过`path-ref`。该声明只用于本次新Run，默认不启用；旧Run不回填授权。调用者须先停止自己的读写并完成消费者，不凭声明迁移其他Owner的来源。`release-work CHANGE RUN`只移除该Run工作根中的精确登记文件，保留整个任务、原索引/summary/finalization、外部来源和归档；它是可恢复工作副本的释放，不是证明、失败事实、任务或正式基线的删除。

释放前要求最终收尾完成、记录的sourceStable=true、v2归档实际解包及已知原消费者可读、源文件字节与mtime匹配。扫描仓库中的引用，包括被ignore的计划、WorkItem、文档、其他索引和证据；`.git`、构建/依赖目录不作为源引用面。只有指向已登记文件的完整`test-assets:CHANGE/RUN/path`逻辑引用可继续，其余Run提及或物理路径保守阻止；baseline/protectedRefs的上级目录依赖也保护。其他已登记Run的JSON（含外部来源provenance/packet）也读取核对，缺工作文件时只解包归档、不恢复；未知或漂移依赖保留。仓库外未登记及已排除目录的实际读写者须由创建者保证已结束，工具不宣称全机引用证明。引用扫描失败、未知/漂移文件、链接、多硬链接、未完成消费者或坏归档均保留工作副本并返回1/pending。

归档、恢复和释放通过仓库内同一Run锁串行，删除前逐项复核文件身份/字节并重查引用。回执追加到`assets.json.workReleases`，不修改原业务结果或封存文件摘要；删除前先持久化pending，每个实际unlink后追加deleted和逻辑字节数。中途失败不谎报成功，重试只删除仍在的文件，原缺失项列alreadyAbsent且不重复计数。只有空目录才移除，不递归清空。外部不遵守Run协议的并发写入仍可能造成pending，不能绕过漂移检查。

释放后`validate`可只读检查归档，`path/path-ref`会按§10重新物化整个Run；原绝对路径中的消费者必须先由该入口取得路径，再读取。再次释放仍要满足同样条件。逻辑删除字节不等于物理空间释放，归档也占空间。旧片不实现任务级联删除、通用临时文件自动GC或存量迁移；任务退役转§12。

## 12. 显式任务资产退役

任务资产退役只处理该Change自有、已登记且通过引用核对的开发证明，不删除Codex聊天、产品数据、正式基线或其他Owner材料。它不是GC、批量历史清理或迁移，也不把“看起来像日志”的文件变成删除候选。调用者须先结束目标任务自有读写；工具不宣称全机或跨Owner原子事务。

`plan-delete-task CHANGE`是只读盘点，按每个Run记录的不可变`parentRoot/workPath/archivePath/repositoryKey`及Change/Run/categoryPaths binding定位，不按当前profile或当前配置猜根。它输出确定的`planId`、候选文件身份/大小/SHA256、`kept`、`pending`、`alreadyAbsent`、Run状态、同Task依赖和外部/基线引用；`--out FILE`只写明确计划文件，不删除数据。所有外部`test-assets`逻辑引用、物理路径/别名/覆盖目录、baseline、原来源、活动/未决/不完整/未知材料均保护；保留消费者依赖递归保留，循环或不确定依赖不强删。旧binding缺失或归档成员/摘要无法按原布局核对时保留并报告。

`delete-task-assets CHANGE --plan FILE`只接受同Change、schema和`planId`有效的计划。执行前重算盘点；文件、引用、binding或元数据漂移即拒绝旧计划。开始退役后关闭该资产集合的新Run登记；已退役或退役中的Run读取、归档、恢复明确拒绝，仍保留的Run继续可读，既有活动Run可完成收尾。只对计划中仍一致的独占普通文件逐项unlink，拒绝链接/硬链接/未知文件；先写pending，再记录实际`deleted`、`alreadyAbsent`、逻辑字节和空目录移除。按消费者先于依赖处理，部分失败保留阻塞项，下一次必须生成新计划重试；不把计划数量冒充实际删除。

`state.md`、`assets.json`以及追加的`taskDeletions`回执始终保留为最小审计壳；原Run/归档条目、结果内容和历史hash不重写。回执含原引用图、portable相对位置、binding摘要和每次实际处置，主机绝对路径不作为跨机身份。退役拒绝复活：已标记退役的Run不会因当前profile变化重新可读或新建；退役中的Run失败时仅按新计划恢复可处理的未退役范围，不回滚已确认删除。

命令示例：

```text
tools/test-assets plan-delete-task CHANGE --out /tmp/CHANGE-delete-plan.json
tools/test-assets delete-task-assets CHANGE --plan /tmp/CHANGE-delete-plan.json
```
