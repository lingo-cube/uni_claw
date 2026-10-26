# RUNTIME-TASK-001 — Task 组件一等化：身份、本地状态布局与版本归属
lifecycle_state: implementing · disposition: none · depth: decision-heavy · base: c3f1833c8d76bea0bcbc3faac0b9bb8568018387

> 本 Change 由一次跨会话设计讨论产出（推演见 [discussion-record.md](discussion-record.md)；
> 方案视图见 [solution.md](solution.md)——域模型/职责矩阵/流程闭环/待敲定/待修正）。
> 2026-10-26 架构评审通过，**进入 PLAN**：D1–D17 已定；O2–O5 记录为"本轮采用
> 选项（PLAN 基线，PLAN 内可调）"；交接契约见 solution.md §1.7；
> 计划见 [plans/RUNTIME-TASK-001.md](../../plans/RUNTIME-TASK-001.md)。

## Intent（WHAT / WHY）

把 Task 从"隐式存在于 Runtime 身份铸造流程里的一个概念"提升为一等组件，并明确三件事：

1. **Task 为什么存在**——它承担什么其他单位不可替代的职责；
2. **本地状态如何按 task 组织**——当前 `runs/` 下产物按时钟命名、无 task 归属，
   "这个 run 是干嘛的"无法回答（用户原话），版本也没有挂载点；
3. **版本如何随实例管理**——用户裁决："针对一个场景修复优化也是针对一个 task，
   这样版本就可以随着 instance 进行额外管理，职责更清晰"。

现状缺陷（已核实，非推测）：`HostRunner` 的文档注释写 `./runs/<runid>/`
（`src/UniClaw.Host/HostRunner.cs:28`），实现却写 `run-{yyyyMMdd-HHmmss-fff}`
（同文件 `:120`）；CLI 路径完全不传 `Launch`，因此命令行产出的 run
目录零身份、也没有 `metadata.json`（只有 HTTP 路径写，`RuntimeHttpServer.cs:718`）。

## Scope

- Task 的一等定位：TaskDefinition / TaskInstance / ProductSession / Run 的基数、
  权威归属与不可推导性。
- 本地状态布局：按 task 组织 run 产物；介质根命名；无 task 的 run 兜底。
- run 出生身份：`taskId` / `runId` / `productSessionId` / `changeId` + 版本指纹，
  命令行与 HTTP 两条路径都要写。
- 版本归属：声明侧（instance）与实际执行侧（run）分记。
- task 定义在存储树中的表达方式（引用 + digest，非副本）。

## Out of Scope

- 多 Goal、多 Run、parallel Run、continuation 语义——基线 §22 明文"暂不锁定"，
  本 Change 不启用、不预置结构。
- 改变身份权威归属：`identity/`、`runtime-runs/` 仍持有身份与投影，存储树不取得身份。
- 通用 Host-owned Task Catalog 的完整实现（CONTEXT.md 记录"尚没有统一的
  Host-owned Task Catalog schema/resolver"，其 buyer 未定）。
- 存量 run 数据迁移（默认不动；是否迁移见 O3）。
- 真机 / 模型 / 设备侧行为。

## Decisions（已确认）

### 用户裁决（不可由实现方推翻）

- **D1** `runs/` 只是介质根，**没有业务含义**；task 才有业务含义。
  → 介质根需要改名，避免"装着 tasks 的目录叫 runs"这种误导。
- **D2** 布局按 task 分，形状为 `<task>/<taskInstanceId>`。
- **D3** `runId` **不占路径段**（用户裁决："runId 好像都不需要"）。
- **D4** 版本随 instance 管理，职责更清晰。
- **D11（O1 定案，2026-10-26 用户裁决）TaskDefinition 不带版本（模型 B）**。
  版本由 D10 两层指纹承载：instance 立项时钉**声明输入**（taskRef / test set
  revision / 场景 version+digest），run 记**实际执行**（codeSha / profileRevision）；
  "第几版"是对同一 task 的 instance 序列按声明指纹去重的**派生视图**，不落存储。
  场景↔实例关联为**单向快照**：instance 立项时钉当时解析到的场景引用
  （id+version+digest），场景资产不持有实例信息；同一 task 的多实例各钉各的，
  场景文件后续演进只影响之后立项的实例；反向查询（场景→尝试）走 store/投影，
  不靠目录名（D8）。升级路径保持开放：将来出现真实 catalog buyer 时可加法补
  version 字段（新写新格式 + 旧读旧格式为本仓既有演进模式）。
- **D12（2026-10-26 用户确认）场景 = 独立资产，被 task 引用**（manifest
  `scenarioRefs`），不属于 task 定义内容；task 定义（workbench 侧）只有标题/
  需求/归类/状态，且当前无内容编辑路径（`repository.js` 仅 create/updateStatus）。
- **D13（2026-10-26 用户确认）TaskInstance = 立项**（幂等 + 身份 + 绑 session），
  不是执行本身；再执行 = 新 instance（与 ADR-0045 一致）。重试定义已于
  2026-10-26 评审收口为两分法：**当前提交重试**（同 key 且同声明输入 → 复用
  原记录）与**再次执行**（新 instance）；不做通用恢复机制（原"推迟强化"
  待办就此关闭）。
- **D14（2026-10-26 用户裁决，Q1）Task 单物种**。Task 定义 = 意图正文（需求
  文本）+ 引用集（fixture 与 acceptance 必选、scenario 可选）+ 项目归类 + 状态。
  workbench task（tasks.json：有意图无引用集）与 catalog task 条目（manifest：
  有引用集、意图仅 displayName）是同一物种的两种不完整 realization，合并方向
  = Host-owned Test Set / Task Catalog（CONTEXT.md:1071-1074 已预留）。仿真条目
  与真机条目不是两个物种：差异由引用组合（带不带 scenarioRefs）与执行基底
  投影决定，不设 task 类型字段（避免与引用组合两处真相）。
- **D15（2026-10-26 用户裁决，Q2）回归可发起：留门不修路**。模型承认"跑一次
  基线回归"是一次可发起的 task 尝试——仿真只是 run 的一种执行基底（与真机
  并列）；本 Change 不实现仿真链接入发起链（真实 buyer 如带审计的 nightly
  回归出现再立项），布局与身份设计不得预先排除该可能。
- **D16（2026-10-26 评审修正）快照证据 ≠ 第二真相**。D7 禁止的是**可编辑的
  重复权威**（两处都能改、都自称权威）；立项/执行侧钉扎的**不可变时间点
  快照**（append-only、带来源与摘要、永不回写权威源）属于证据（provenance），
  不构成第二真相。O5 的 reconstruct 选项因此在架构上成立；task.json 仍为
  引用 + digest 形态（权威源唯一且可编辑）。
- **D17（2026-10-26 评审裁决）交接三契约**——方案主体保留，补齐边界时序与
  数据承诺（展开见 solution.md §1.7）：
  1. **立项确认**：Runtime **先持久化** instance 身份 + 声明输入钉扎（落
     TaskInstanceStore，扩展 TaskInstanceRecord 或伴生记录，PLAN 定形），
     **后返回成功**；同 idempotencyKey 重入复用原记录，**同 key 但声明输入
     不同 → 显式拒绝**（input-conflict），绝不静默覆盖；run 目录
     metadata.json 只是投影；未产生 Run / 启动失败的实例靠 Store 可查询。
  2. **执行交接**：时序沿既有权威链不动（Session → 契约细化 → accepted
     Contract View → Legal Activation → Run，ADR-0045/P24；session 二绑 409
     保持）；Run 记录**实际实现指纹**，须能区分"同提交、不同工作区"
     （共享脏工作区为本仓常态）。
  3. **结果交接**：Run Outcome（执行终局，run 产物）/ 任务验收（判定者
     **显式**：仿真类=对拍场景认证期望的执行者、真机类=验收规则/人工；
     结果记录归 instance 侧，不靠 run 产物自证）/ 报告交付（required-reports
     体系不变）三者分离，各留来源与状态；反馈成新场景必须过显式认证。

### 技术裁决（本轮核实后确认）

- **D5 两层，不预置 run 层。** 依据：`session → run` 被硬挡——同 session 绑第二个 run
  返回 409 `product-session-already-bound`（`RuntimeHttpServer.cs:198-200`）；
  "任务结束后再次执行"按 ADR-0045 造**新的** TaskInstance，不是给老 instance 加 run。
- **D6 session 不进路径。** session 是 correlation root，没有只属于它的产物；
  进路径会得到四层深度却零可寻址收益。
- **D7 task 定义在存储树中存引用，不存副本。** 目录只记 `taskId` + 定义来源 + digest。
  存副本会产生第二真相，违反 AGENTS.md 对"各维护一份副本"的禁止。
- **D8 身份权威留在 store，路径只做定位。** 与 `storage-inventory.md`
  "不能靠目录名猜身份"、`CONTEXT.md:1025`、`PNL-005` 一致。
- **D9 无 task 的 run 走显式兜底段**（CLI 路径不传 `Launch`，README 记为 `_untasked/`），
  并要求可统计其数量——数量高说明 task 层没被真正用起来。
- **D10 版本指纹分两层**（2026-10-26 评审修正措辞）：instance 记**声明**的输入
  版本（task 引用 / test set revision / 场景 version+digest）；run 记**实际执行
  内容**指纹（不限于 codeSha——须覆盖实际执行的源内容，含未提交工作区，
  形状 PLAN 定）。两层的比较发生在**同一 task 的相邻尝试之间**：声明层相同
  而实际层不同 → 改了代码；声明层变化 → 输入变了。原表述"同一 instance 重跑
  换代码"与 D13/ADR-0045（再执行=新 instance、session 二绑 409）矛盾，废除
  该例；"只记一层则无法回答这次绿是改了 scenario 还是改了代码"的结论不变。

### 目标布局

```text
<state-root>/                      ← 介质根（改名，不再叫 runs/）
├── tasks/
│   └── <taskId>/
│       ├── task.json              ← taskId + 定义来源 + digest（引用，非副本）
│       └── <taskInstanceId>/      ← 一次尝试 = 一次执行
│           ├── metadata.json      ← runId / productSessionId / changeId / 版本指纹
│           ├── *.journal.jsonl(+.head)
│           └── ...
├── _untasked/                     ← 无 task 的 run 兜底
├── identity/                      ← 身份权威，平铺
├── runtime-runs/                  ← 投影索引，平铺（跨 task 查询用）
└── required-reports/              ← 必交付报告，按 deliveryId 平铺
```

路径上只有两个有业务含义的层级：**task（归属）、instance（尝试）**。

## Assumptions

- 一个 TaskDefinition 可被实例化多次（`CONTEXT.md:1016`；`repository.js:86` 的
  `task.instances[]`）——这是本 Change 分组层的正当性来源。
- `TaskInstance → ProductSession` 当前 1:1；磁盘格式**预留** 1:N
  （`ProductSessionStore.cs:103` 重载时 `_byTask` last-wins，无唯一约束）。
  该"预留"是准确描述，**不得**改写为"当前 1:1"（见 Alternatives 被拒项 6）。
- `ProductSession → Run` ≤ 1，由 409 硬挡。
- 基线 §22 的"多 Run"属产品模型层（多 Goal / parallel / continuation），
  **不等于** `instance → run` 1:N。

## Alternatives（含被拒）

1. **三层 `<taskId>/<taskInstanceId>/<runId>/`** — 被拒。原理由是"为将来的多 Run
   零迁移留位"，复核后不成立：再执行造新 instance（ADR-0045）；session 二绑 409；
   而"将来补一层"在本仓库是**既有演进模式**（Evidence v1→v2、World v1→v3、
   Resource v2→v4、run-state v1→v2 均为新写新格式 + 旧读旧格式并存），
   不是不可逆障碍。为未锁定的需求预置层级，违反 AGENTS.md §1.5 原则 5。
2. **`runs/<taskInstanceId>/` 平铺** — 被拒。丢掉 task 归属这一层唯一的业务含义。
3. **task 定义存副本** — 被拒，两处真相。
4. **`<taskId>/<instance>/<sessionId>/<runId>/` 四层** — 被拒，session 无独立产物。
5. **维持平铺 + 只给 run 盖身份章** — 保留为最小替代方案。不选它是因为它放弃了
   "一个 task 的全部材料在一处"这个用户目标；但若布局改动代价超预期，可降级采用。
6. **把 `TaskInstanceStore` 注释里的"预留 1:N"改成"当前 1:1"** — 被拒，且是
   设计讨论中我方提出的错误建议，单独记录以防复发：原句讲 `taskInstance → productSession`，
   而"多 Run 不锁定"讲的是 Run 这条不同的边；用一条边的不锁定去改写另一条边的预留，
   等于换边 + 把互补信息（结构允许 vs 当前用法）降级为单一现状描述，净减信息。
7. **模型 A：TaskDefinition 带 `version`/`revision`（O1）** — 被拒（用户裁决
   模型 B，2026-10-26）。会变的输入（场景/测试集/代码）不流经 task 定义且已各有
   版本机制（SCN `version` + 认证 digest、manifest `version`/`sourceRevision`、
   `runtimeSourceHash`）；定义内容当前无编辑路径；workbench 存储整文件重写无历史，
   版本号回退即丢；且需"版本→内容"解析器（= Out of Scope 的 catalog），与
   CONTEXT.md `Catalog Revision` 词条形成第二版本轴。将来真有 catalog buyer 时
   可加法升级（B 不锁死 A）。

## Open questions（进入 PLAN 前必须答）

- **O1 TaskDefinition 是否带版本？——已定案：模型 B**（用户裁决 2026-10-26，
  见 D11；模型 A 否决依据见 Alternatives 7）。
- **O2 路径段放 `taskId`（不透明、安全）还是可读 slug + ID 后缀？**
  O1=B 后路径零版本压力，收窄为纯命名权衡。可读名的代价：长度、非法字符、碰撞、
  PII、Windows MAX_PATH。`taskRef` 是自由中文，
  不可直接做路径（`RuntimeHttpServer.cs:300-303` 把 requirement 塞进 `taskRef.label`）。
  → **本轮采用（PLAN 基线）：slug + ID 后缀**（slug 取 displayName/taskRef 尾段，清洗+截断）。
- **O3 存量 run 数据怎么办？** 不迁移 / 一次性迁移 / 新旧双读兼容。
  注意 `.required-reports/*/head.json` 存的是**绝对** `SourceDirectory`，
  且 `RequiredReportDeliveryPaths` 是 fail-closed，迁移不是 `mv` 能了事。
  → **本轮采用（PLAN 基线）：不迁移 + 工具双读**（旧平铺留原地，新 run 落新布局）。
- **O4 "task 组件提高优先级"的落地范围与顺序？**
  候选顺序：(a) 统一 task 身份（消除三前缀 known deviation：workbench `psi-*`、
  runtime 铸造 `ti-*`、RuntimeRunStore 缺省合成 `task-instance-*`）
  → (b) 存储布局 → (c) Workbench 接线 → (d) 版本模型。
  O1=B 后 (d) 缩水为"D10 指纹落地"（instance 立项时钉声明输入）。
  → **本轮采用（PLAN 基线）：(a)→(b 含 d)→(c)**；切片划分见 plans/RUNTIME-TASK-001.md。
  域模型梳理（D14/D15）产出的断点清单（O4 靶子，2026-10-26）：
  1) 两套 task 定义表面互不相认（workbench 有意图无引用集 / catalog 有引用集
     无意图正文），无 resolver 互通；
  2) instance 双编号（psi-*/ti-*）+ 合成兜底前缀（task-instance-*）；
  3) 引用解析只在 workbench 侧（index.js:186-196 解析 manifest），Runtime 只
     透传（RuntimeHttpServer.cs:161-162/227），run 记录无 version/成员/digest
     ——D11 钉扎的落点为 **TaskInstanceStore**（立项时持久化，D17 契约 1；
     metadata.json 仅投影）；
  4) 仿真基线 task 条目无发起链消费（跑回归=dotnet test，不铸 instance/run）；
  5) acceptance 无独立工件（部分指回场景认证期望，部分为裸字符串、语义活在
     测试代码）。
- **O5 `task.json` 的引用是否足够支撑 replay？**（O1=B 后收窄）
  replay 链 = 声明指纹 + 定义来源引用 + run 层 codeSha/profileRevision；digest
  提供验证（漂移哨兵）不提供恢复。剩余为标准选择：verify-only（漂移即响亮失败）
  vs reconstruct（instance 钉输入快照——与 required-reports `inputs/` 同构，非 D7
  禁止的定义副本）。已核实：requirement 原文今天已进 run metadata（PNL-011）；
  workbench 定义源内容当前不可变。
  → **本轮采用（PLAN 基线）：verify-only**（D16 后 reconstruct 架构无障碍，待真实 buyer 再升级）。

PLAN 内定形项（2026-10-26 只读评估产出；实现形状，非架构缺口，切片 0 落定）：

- **T1 声明块**：由 workbench 计算（manifest 解析结果）并随 launch 请求提交，
  Runtime 原样持久化为"声明"（不引入 Runtime 侧 catalog resolver）；
- **T2 实际实现指纹**：哈希路径集与 launch 性能预算（须区分同提交不同工作区）；
- **T3 验收记录**：instance 侧存储形状（与钉扎同记录或伴生）；
- **T4 instance 查询端点**：`GET /api/uniclaw-runtime/tasks/{taskInstanceId}`
  （已核实：现有端点清单无 instance 读端点）；
- **T5 声明指纹比较范围（评审 P-A）**：参与＝意图正文 digest / testSet
  revision / fixture(id+version+digest) / scenario(id+version+digest) /
  acceptanceRefs；排除＝displayName / 时间戳 / 临时环境字段；钉扎材料须与
  实际执行所用材料一致（不一致记分歧，不静默）；
- **T6 验收权威边界（评审 P-B）**：instance 侧只记录不判定——不改写 Run
  Outcome、不替代 UniAgent Goal Evaluation；每条结果可追溯判定者/规则版本/
  对应实例与 Run/证据。保存位置 ≠ 判定权。

## Acceptance（骨架，四元组随切片补全）

- **A1** 每个新 run 的目录路径可直接读出 task 与 instance；`metadata.json` 含
  `runId` / `productSessionId` / `changeId` / 两层版本指纹；CLI 与 HTTP 两条路径都满足。
- **A2** 无 task 的 run 落入显式兜底段，且数量可统计。
- **A3** 身份权威不因布局改变：移动或重命名 run 目录不影响 `identity/` 与
  `runtime-runs/` 的查询结果。
- **A4** 旧布局 run 仍可被工具读取（若 O3 选择不迁移）。
- **A5** 目录名不参与身份推导，有测试守卫（新增一条反向测试：篡改目录名后身份查询不变）。
- **A6** 交接契约可判定（D17；评审 P-C；四元组随切片 1/2 补全）：① 同 key 同
  声明输入 → 复用原记录；② 同 key 异声明输入 → 显式拒绝（input-conflict）；
  ③ 重启后钉扎不变；④ 未产生 Run 的实例可查询；⑤ 同提交不同实际实现可区分；
  ⑥ 旧布局 run 与旧报告来源仍可读取。

## Constraints

- 保留共享脏工作区与并行业务改动；不 reset / clean / 全仓 stage。
- 不改基线 §22 已声明"暂不锁定"的语义，也不借用布局隐式锁死它。
- 身份、幂等、未决、交付来源的保护不因布局调整而削弱。
- 目录、索引、结果都不是第二生命周期。

## Owner / Authority impact

- Task / Session / Activation 的身份铸造与权威仍在
  `TaskInstanceStore` / `ProductSessionStore` / `ActivationRecordStore`；
  存储树与目录名不取得任何身份 authority。
- Host 继续只负责介质、作用域与释放，不接管领域初始化语义。
- 不因目录名、transcript 或 Host session 推导 Product identity
  （`CONTEXT.md:1025`、`PNL-005:29`、`HostRunner.cs:36-38`）。

## Architecture / Source refs

- `docs/architecture/product-architecture-baseline-l0-l3.md` §22「暂不锁定」（line 1060）
- `docs/adr/0045-runtime-task-instance-session-and-agent-contract-sequence.md`
- `docs/adr/0044-runtime-session-identity-and-protocol-status.md`、`0046`、`0055`
- `CONTEXT.md` Task Workbench 段（line 1016–1031）
- `changes/RUNTIME-IDENTITY-001/state.md`、`changes/RUNTIME-STORAGE-002/state.md`、
  `changes/RUNTIME-STORAGE-002/storage-inventory.md`、`changes/PNL-005/state.md`
- `src/UniClaw.Host/HostRunner.cs`（:36-38、:120）、`src/UniClaw.Host/Program.cs`
- `src/UniClaw.Host/Runtime/{TaskInstanceStore,ProductSessionStore,RuntimeRunStore}.cs`
  （TaskInstanceStore 铸 `ti-` 前缀 :48；RuntimeRunStore 缺省合成 `task-instance-` :58）
- `src/UniClaw.Host.Dsh/RuntimeHttpServer.cs`（:189、:198-200、:718）
- `dsh/uniclaw-task-workbench/{schema/task-protocol.schema.json,src/repository.js}`
- `scenarios/{schema.json,README.md}`（场景的权威定义：v2 schema 真校验 + 认证/承载纪律）
- `scenarios/SCN-WIFI-001.json`（场景资产形状：`version` + expectations + 认证 digest）
- `testsets/simulation-baseline/manifest.json`（Repository Test Catalog：
  taskRef → scenarioRefs/fixtureRefs/acceptanceRefs；testset `version`/`sourceRevision`）
- `CONTEXT.md:1071-1078`（Test Set / Repository Test Catalog / Catalog Revision 词条）
- `docs/reports/PNL-003-observability-project-management-gap-report.md`
  （testSetRef 无 id/version 的已登记缺口）

## Residual risks

- 路径不再自证"一个目录 = 一次执行"，必须读 `metadata.json`；多 run 时代来临时
  老目录语义只能靠 metadata 解释。
- 介质根改名会触及 `runDirName` 相关的 29 处调用点（`RuntimeToolHost*.cs`）与
  tool / HTTP / CLI 三层契约。
- 若 O3 选择不迁移，新旧两种布局会长期并存，工具必须双读。
- `Workbench instanceId（psi-*）` 与 `Runtime taskInstanceId` 两套编号是
  ADR-0045 已记录的 known deviation；布局按 task 分之后，这个断点会变成路径级的可见问题。

## Verification

```yaml
verification:
  level: CONTRACT
  method: "源码与基线逐条核对：基数（TaskInstanceStore/ProductSessionStore/RuntimeHttpServer 409 分支）、
           布局相关调用点计数（runDirName 29 处）、交付路径 pin（RequiredReportDeliveryPaths 绝对路径 + fail-closed）、
           基线 §22 原文、ADR-0045 原文、CONTEXT.md Task Workbench 段"
  expected: "每个裁决都能指到具体文件行；被拒方案有可证伪的理由；未决项不被伪装成已决"
  actual: "D1–D10 全部有行级依据；两处我方早期错误建议（runId 三层、改'预留 1:N'）已单列并给出反证；
           O1 已定案为模型 B（D11，依据见 Status log 2026-10-26 续轮）；O2–O5 保留为未决，
           未以任何布局或注释隐式决定"
  evidence: "本文件 Assumptions / Alternatives / Architecture refs 引用行；
             discussions 全文见 discussion-record.md"
```

本 Change 尚在 RESOLVE，无实现、无测试运行；上述为文档与源码事实核对级别，
不构成实现验收。

## Status log

- 2026-10-26 UNDERSTAND→RESOLVE：跨会话设计讨论收口。用户提出按 task 分存储、
  版本随 instance 管理、runId 不需要占路径；本轮核实基数（含 409 硬挡与 schema 预留）、
  基线 §22 未锁范围、以及 `HostRunner` 文档/实现不一致与 CLI run 零身份的缺陷。
  产出目标布局与 D1–D10；O1–O5 留待 PLAN 前裁决。未改任何源码。
- 2026-10-26 RESOLVE（续，新会话接手）：**O1 定案为模型 B**（用户裁决，D11）；
  确认 D12（场景=被引用的独立资产）/D13（instance=立项，非执行）；
  **待办：重试的精确定义由用户明确推迟强化**。本轮新核实（均实读文件）：
  workbench 定义内容无编辑路径（`repository.js` 仅 create/updateStatus）；tasks.json
  整文件 tmp+rename 重写无历史；`InstantiateRequest` 只带 taskId；场景资产自带
  `version`+认证 digest（`SCN-WIFI-001.json:15,30-37`）；manifest 带 testset
  `version`/`sourceRevision`（`manifest.json:5-6`）；CONTEXT.md:1071 Test Set 词条
  已要求 instance 指向 revision 但磁盘未落（PNL-003 登记缺口）。勘误：runtime 真实
  铸造前缀为 `ti-`（`TaskInstanceStore.cs:48`），`task-instance-<guid>` 是
  `RuntimeRunStore.cs:58` 的缺省合成兜底——discussion-record §2.2 按此理解；
  O4(a) 需覆盖三个前缀。O2/O5 按 O1=B 收窄。未改源码；本轮仅回填本文件。
- 2026-10-26 RESOLVE（续二，域模型梳理）：用户裁决 **D14（Task 单物种）**与
  **D15（回归可发起=留门不修路）**。本轮核实：workbench 发起链解析 catalog
  manifest（index.js:186-196；UNICLAW_TESTSET_ROOT→repo testsets/），Runtime
  只透传引用（RuntimeHttpServer.cs:161-162/227）；三份 manifest 中仿真基线
  7 条目仅测试链消费（不铸 instance/run），android-settings 5 + workspace-
  contract 1 条目走发起链；场景 schema 无 task 字段（对 task 零耦合，
  additionalProperties:false）；无场景条目证明 task 标配引用 = fixture +
  acceptance、scenario 可选（细化 D12/D14）。产出断点清单五条（见 O4）。
  待办：重试定义强化（前轮推迟）；场景词条与 UniClaw Task 词条更新草案已拟，
  待用户确认后入 CONTEXT.md。未改源码。
- 2026-10-26 RESOLVE（续三，词汇表落册）：按用户确认（含修正：场景库定位为
  "测试与能力基线"）写入 CONTEXT.md：新增 **Scenario** 词条（置于 Catalog
  Revision 之后）；**UniClaw Task** 词条更新为单物种表述（意图正文 + 引用集，
  fixture/acceptance 必选、scenario 可选，不设类型字段——D14）。未改源码。
- 2026-10-26 RESOLVE（续四，方案文档）：按用户要求整理整体方案为
  [solution.md](solution.md)：§1 域模型（职责矩阵/三条边/版本两层/流程闭环/
  基数链/目标布局）、§2 D1–D15 总表、§3 待敲定五项（O2/O3/O4/O5/重试定义，
  附建议）、§4 待修正 F1–F8（断点→动作→归属）、§5 维护纪律六条、
  §6 架构原则符合性、§7 证据索引。state.md 仍为唯一事实真相源。未改源码。
- 2026-10-26 RESOLVE（续五，架构评审处置）：用户评审结论——**总体设计保留，
  架构主体不推倒；先修正交接契约**。处置四项：(1) **D10 措辞修正**：废除
  "同一 instance 重跑换代码"例（与 D13/ADR-0045 矛盾，继承自前会话的表述
  缺陷，本轮评审揪出）；两层比较改述为"同一 task 相邻尝试之间"。(2) 新增
  **D16**：快照证据≠第二真相——D7 禁的是可编辑重复权威，不可变时间点快照
  属 provenance（O5 reconstruct 因此架构成立）。(3) 新增 **D17 交接三契约**：
  立项确认（先持久化后成功、同键异输入显式拒绝、钉扎落 Store、无 Run 实例
  可查询）；执行交接（权威链不动、实际实现指纹覆盖未提交工作区）；结果交接
  （Outcome/验收/交付三分离，验收判定者显式、结果归 instance 侧，反馈过
  认证门）。(4) solution.md 同步：新增 §1.7 交接契约、F3 落点修正、新增
  F10/F11、§5 补快照三要件纪律、D10/D16/D17 相关表述更新。流程闭环评级：
  概念链已有 → 交接契约已定义（待 PLAN 落实）。未改源码。
- 2026-10-26 RESOLVE→**PLAN**（评审通过）：只读评估确认无架构级阻碍——
  admission 端点（POST /tasks，RuntimeHttpServer.cs:120-133）、TaskInstanceStore
  append-only + Load 重放、required-reports 端点体系均已核实存在；缺口仅
  instance 查询端点（T4，PLAN 项）。O2–O5 记录为"本轮采用（PLAN 基线）"：
  O2＝slug+ID 后缀、O3＝不迁移+双读、O4＝(a)→(b 含 d)→(c)、O5＝verify-only；
  重试收口为两分法（D13 待办关闭）。新增 Acceptance **A6**（契约可判定六条，
  评审 P-C）与 PLAN 定形项 **T1–T6**（评审 P-A/P-B 入 T5/T6）。计划落
  plans/RUNTIME-TASK-001.md（切片 0 定形 → 1 admission 闭环 → 2 布局与执行
  投影 → 3 workbench 接线）。lifecycle_state → planning。未改源码。
- 2026-10-26 PLAN→**IMPLEMENT（切片 1 admission 闭环）**：Worker＝glm-5.3-flash
  （zai-coding-cn，owner 指定覆盖 standard 默认绑定，WI-RUNTIME-TASK-001-S1，
  TDD）。落地：TaskInstanceRecord +Declaration/DeclarationFingerprint（canonical
  JSON+sha256，双端一致由冻结样例 f011bc6f…89d3e8e 锁定）；Create 冲突分支
  （同 key 异声明抛 TaskInstanceInputConflictException）；POST /tasks 接受
  declaration（v1 校验、409 input-conflict、先持久化后成功）；GET
  /tasks/{id}（定形 T4）；workbench 声明块组装+admission 先于 run 创建。
  **Leader REVIEW 通过**（预留 1:N 注释未动、零越界、复跑取证）并 Direct 补缺：
  runtime-provider 增 createTaskInstance 传输（postRaw+错误码透传）、admission
  的 taskInstanceId 回填 createRun（消除合成 task-instance-* 路径）。
  验证：Host.Tests 13/13（RuntimeTaskDeclaration|RuntimeIdentityStore）、
  Dsh.Tests 5/5（RuntimeTaskEndpoint）、workbench+provider 45/45，二次复跑稳定
  （首跑一次 instance-store-failed 偶发未复现，判环境瞬态）。Worker 新发现
  处置：provider 传输缺失→Leader 已接线；Windows 预存套件红（sharing
  violation / python3 不在 PATH / ESM 可移植性，stash 基线复核）→ 记录为
  环境问题，不属本切片，留待单独裁决；既有测试副产物目录（changes/
  RUNTIME-STORAGE-002/evidence/slice2/*）保留未动。lifecycle_state →
  implementing。
- 2026-10-26 IMPLEMENT（切片 2 布局与执行投影）：Worker＝glm-5.3-flash
  （WI-RUNTIME-TASK-001-S2，TDD）。落地：RunLayout（`tasks/<slug.hash8(taskRef)>/
  <ti|runId>/` + `_untasked/run-<ts>`，D2/D9；task.json write-if-absent，D7；
  CountUntaskedRuns，A2）；RunMetadata + ImplementationFingerprint（metadata v2
  由 HostRunner 单一写点、CLI/HTTP 双路径统一——F4 修死；T2 指纹＝commit+
  srcDigest，repo 探测失败双 null 不 fail）；LaunchContext +TaskRef/
  DeclarationFingerprint（S2-4，接切片 1 admission）；ResolveRunDir 契约升级
  （相对多段+包含性校验，旧单段名与旧 ownerRef 等价兼容，A4/A6-⑥）；根默认
  runs→state（CLI/HTTP 双入口+.gitignore，S2-8）；旧 run-* 原地共存不迁移。
  **Leader REVIEW 通过**（RunLayout/RunMetadata/ResolveRunDir/owner 校验逐行
  审，预留 1:N 注释未动，零越界）；复跑取证 Host.Tests 36/36、Dsh.Tests
  18/18（PATH 补 dotnet 后含原 2 条环境红转绿）。Worker 发现处置：
  InternalsVisibleTo 走程序集 attribute（Web SDK 限制，有先例）；artifact-set
  端到端因 python3 预存红仅单侧验证——**记为遗留核对点**（环境修复或切片 3
  后补跑）；python3 PATH 22 条与 sharing violation 环境红不属本 change。
  A1–A6 全部有测试锚定。切片 3（workbench 接线+psi 收口）待派发。
