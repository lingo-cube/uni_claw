# CORE-016 — Core 契约第二 realization tracer（跨领域可证伪性）

lifecycle_state: verifying · disposition: none · depth: decision-heavy · base: 6508de39

## Intent

用第二个领域 realization（有界测试侧 tracer、零产品代码）压测 Core 候选
模型，检验「Core 是跨领域核心」这一中心主张；为 pending gates
（非 Slice BasisReference / ResourceVersion）提供第一个真实域输入。
上游方向裁决：2026-09-20 架构推荐（2→1 顺序：第二域先于 UI 形状对齐）。

## Scope（grill round 1 定稿，2026-09-20）

- 域 = 文件系统：Segment=目录子树、Slice=列目录窗口快照、Evidence=readdir/stat、
  Claim=文件状态命题、Effect=fs 操作、TargetBinding=ResourceVersion basis
  （测试侧可观察的 length + mtime 复合键）
- 新测试程序集 `tests/UniClaw.FileSystemRealization.Tests`；**仅引用
  UniClaw.Core + 测试框架**，零 Kernel 引用（csproj 级可执法）
- 测试侧 tracer；零产品代码；不修 Core

## Decisions（grill round 1，全按建议）

| # | 决策 | 依据 |
|---|---|---|
| D1 | 域 = 文件系统（vNext.1 原生 Slice 用例，最便宜，正打 ResourceVersion 门） | grill Q1 |
| D2 | 依赖硬边界 = 仅引 UniClaw.Core，编译级排除 Kernel——独立 realization 的检验价值所在 | grill Q2 |
| D3 | 验证范围 = 表达保持 + 演化保持；C 字段门只开 ResourceVersion basis；Attempt 请求快照/执行端/授权依据保持未提供不伪造 | grill Q3 |
| D4 | 落点 = 新测试程序集（零 Kernel 依赖为 build 层事实；GEV-004 D1 / RFS-001 D23 先例） | grill Q4 |
| D5 | 反例处置 = tracer 只产证据与反例记录；Core 候选修正走后续独立 change（CORE-011 先例） | grill Q5 |

## Acceptance（grill 定稿）

1. 表达保持：文件域五记录 + ResourceVersion basis 可表达（目录/列窗/命题/操作/绑定）
2. 演化保持：新证据、更正、迟到反馈后判断一致（先更新后转换 = 先转换后更新）
3. ResourceVersion basis 真实输入；realization 完成 stale 判定并投影后，
   `CoreInvariants.CanDispatch` 拒绝该次 dispatch
4. 双向验证矩阵在第二域复刻：realization→Core 投影语义保留；Core 约束→realization 满足检查
5. 零 Kernel 依赖编译级证明（csproj 引用清单 + closure 检查）
6. 零产品代码；不修 Core；反例（若有）落 evidence + qspec 修订建议

## Verification

- level: DETERMINISTIC
  - method: `Closure_TracerAssemblyReferencesOnlyCoreAmongUniClawAssemblies`
  - expected: tracer 程序集的 `UniClaw.*` 引用集合等于 `{UniClaw.Core}`
  - actual: 通过
  - evidence ref: `tests/UniClaw.FileSystemRealization.Tests/FileSystemRealizationTests.cs`
- level: SCENARIO
  - method: 文件系统 realization S1–S8（真实临时目录、受控 mtime、mini-world + projection seam）
  - expected: 目录/切片/证据表达、历史保留、append-only Claim 更正、world-owned identity、非 Slice ResourceVersion、realization stale 判定、Unknown 保持、双序必要语义一致
  - actual: 9/9 通过（S1–S8 + closure）
  - evidence ref: `tests/UniClaw.FileSystemRealization.Tests/`
- level: DETERMINISTIC
  - method: 既有回归：Core、Simulation、Agent 测试项目
  - expected: 不引入回归
  - actual: Core 14/14、Simulation 132/132、Agent 17/17 通过
  - evidence ref: 2026-09-20 本地 `dotnet test --no-restore`
- level: DETERMINISTIC
  - method: Kernel 全量测试项目
  - expected: 既有测试保持通过，或失败可归因于本 Change 之外的既有问题
  - actual: 406/407 通过；唯一失败为既有 `DocsMetadataTests`，指出未被本 Change 修改的 `docs/design/uniflow-gate-recalibration-proposal-v0.1.md` 与 `v0.2.md` 缺少 Status/Authority 头
  - evidence ref: 2026-09-20 本地 `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore`

## Status log

- 2026-09-20 · created · 方向经架构推荐 + 人批准开立；grill round 1
  frontier 五问已预分类进 shadow 台账（事件 #2），待人裁决。
- 2026-09-20 · grill-round-1-done · 人裁决 5/5 全按建议、零 override
  （台账事件 #2 已回填）；决策级 frontier 已空——剩余细节（场景集枚举、
  tracer 骨架）属可推导设计工作，归 to-spec。下一步：起草文件系统域
  realization tracer 规格（场景集 + Core 映射义务），spec 评审为下一 Gate。
- 2026-09-20 · to-spec-drafted · `spec.md` v0.1 落盘：mini-world + 投影缝
  形态（非直接构造 Core 记录）、字段级映射表（Clause 无买家不建；
  ResourceVersion 复合键 Length:LastWriteTimeUtcTicks，受控 mtime 显式
  声明）、场景集 S1–S8（对齐契约 §7 + 演化双序）、closure 执法
  （GetReferencedAssemblies 白名单 {UniClaw.Core}）。待 spec 评审 Gate。
- 2026-09-20 · spec-review·CHANGES_REQUIRED · 两处主体缺陷被拒（台账
  事件 #3，override=是）：S6 把版本比较误归 `CoreInvariants.CanDispatch`
  （源码核实属实——只检 Disposition+HasFixedBasis）；S8 双序等价未限定
  比较对象；另 S3 表述可诱导原地改 Claim、ResourceVersion 键需条件注明。
- 2026-09-20 · spec-v0.2 · 四项按评审文本修正（溯源表 spec §9）；评审
  闭合条件满足。PLAN（TDD 垂直序）：
  ① csproj 骨架 + closure RED（引用集 != {UniClaw.Core} 即失败）
  ② FileSystemWorld + FileSystemCoreProjection 最小面 → S1 GREEN
  ③ S2–S8 逐场景 RED→GREEN（S6 先 realization 有效性判定、后投影断言）
  ④ 全量回归 + 验证四元组落档。IMPLEMENT 待人放行。
- 2026-09-20 · implementation-started · 人放行；新增测试侧
  `UniClaw.FileSystemRealization.Tests`，未修改 `src/`、Core、Kernel 或存储。
- 2026-09-20 · implementation-verified · S1–S8 与 closure 9/9 通过；Core
  14/14、Simulation 132/132、Agent 17/17 通过。Kernel 仅剩既有
  `DocsMetadataTests` 元数据失败（406/407），不归因于本 Change；保持 VERIFY。
- 2026-09-20 · leader-review · 三发现已修：F1（blocker）新程序集未入
  solution——全量回归不含它，slnx 补行；F2 S8 指纹窄于 spec 至少比较清单
  ——扩为含 Binding 固定依据/可发送性/Unknown 的双序指纹；F3
  DocsMetadataTests 失败债主为 GATE-001 proposal 文档（缺 Status/Authority
  头），本会话补头清债。worker 环境无法写 .git/index，提交由 Leader 会话
  代执行。
- 2026-09-20 · leader-verify · 全量第一手复跑 579/579（Core 14 + Agent 17
  + FileSystemRealization 9 + Simulation 132 + Kernel 407）；四元组落
  `evidence/2026-09-20-core-016-file-system-realization-tracer.md`。
  检验结论：Core 候选在第二域未被证伪（通过≠跨领域充分；不触发冻结/
  升格）。lifecycle → verifying·awaiting-batch-closure。
