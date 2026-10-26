# RUNTIME-TASK-001 — Task 组件整体方案（域模型 · 待敲定 · 待修正）

> **定位**：本文是 [state.md](state.md) 的方案视图——把跨会话讨论定下的域模型、
> 流程闭环、职责矩阵，以及待敲定/待修正项收拢成一份可维护的文档。
> **事实真相源仍是 state.md**（D1–D15、O1–O5、Acceptance）；与 state.md 冲突时以
> state.md 为准。推演过程见 [discussion-record.md](discussion-record.md)。
> 时点：2026-10-26，base `c3f1833c`，RESOLVE 阶段，尚无实现。
> 2026-10-26 架构评审：总体保留、架构主体不推倒；补齐交接契约（§1.7，D16/D17）。

四项要求与章节映射：职责清晰 → §1.1；流程闭环 → §1.4；易于维护 → §2/§5；
符合架构原则 → §6。

## 1. 域模型（全部已定案）

### 1.1 概念与职责矩阵

| 概念 | 是什么 | 管（职责） | 不管（边界） | 身份 | 存储 | 生命周期执法 |
|---|---|---|---|---|---|---|
| **Task 定义**（D14 单物种） | 可重复发起的工作的规范：意图正文 + 引用集（fixture/acceptance 必选、scenario 可选）+ 归类 + 状态 | 意图复用；材料归属锚 | 不承载执行过程；不铸造身份；**无版本字段**（D11）；验收细节只经引用指向、不复制 | taskId（被引用） | workbench `tasks.json`（意图为主）/ catalog manifest（引用集为主）＝同物种两种不完整 realization，合并方向 Host-owned Catalog（留门） | workbench: draft/active/archived；catalog: 声明式 |
| **Scenario**（场景） | 测试与能力基线资产：可重复检验声明（期望快照 + 载体绑定 + 三层认证摘要） | 行为基线；验收判据来源 | **不持有任何 task/实例引用**（零耦合）；不做执行 | `SCN-*` | `scenarios/` + 资产目录（captures 等） | 认证三摘要；version bump；status 须与 TRX 真值一致；sensitiveReview fail-closed |
| **TaskInstance**（D13） | 一次发起（立项） | 幂等（同 key 重入同实例、同 key 异输入显式拒绝——D17）；尝试身份；绑 session；**钉声明输入**（D11：落 Store、立项时持久化） | 不是执行本身；不管执行内容 | Runtime 铸 `ti-`（调用方不得传入/覆盖，ADR-0045） | TaskInstanceStore（append-only） | append-only；绑定后不可改绑 |
| **ProductSession** | 一次产品执行的 correlation root | 承载契约形成；关联 Goal/Contract/Run/Outcome | 不等于 instance；不等于 run | Runtime 铸 | ProductSessionStore | 二绑 run 409 硬挡 |
| **Run** | 一次执行 | 记**实际执行内容**指纹（D10 修正：不限于 codeSha，须覆盖未提交工作区，形状 PLAN 定——D17 契约 2）；产出执行材料（journal/trace/captures/报告） | 不拥有 task/session 身份；目录名不参与身份（D8） | Runtime 铸 runId（落 metadata，不占路径段，D3） | run 目录 + `runtime-runs/` 投影 | journal 帧链 + head 原子提交；默认不删 |
| **Fixture** | 执行时被消费的资产（设备状态/安全策略/golden bundle/真值数据） | 执行前置条件 | 不含验收判据 | manifest fixtureRef | 仓库资产目录 | manifest contentHash 双检 |
| **Acceptance** | task 侧的验收引用 | 声明"怎样算通过" | 当前无独立工件（见 F7） | acceptanceRef | 无（部分指回场景认证期望） | — |
| **TestSet / Catalog** | 引用组合容器 + 版本轴（Catalog Revision） | 组织 task/fixture/acceptance/scenario 关系；提供可定位版本 | 不取得执行 authority | testSetRef + version | `testsets/*/manifest.json`（临时声明式） | version/sourceRevision 由 workbench 发起时核对 |

### 1.2 关系拓扑：三条单向边（关系的全部）

| 边 | 时机 | 方向 | 性质 |
|---|---|---|---|
| ① 引用边 | 声明 task 时 | task 定义 →(scenarioRefs)→ 场景 | **可选**；被引用场景必须已存在 |
| ② 钉扎边 | instance 立项时 | 实例 →(id+version+digest 快照)→ 场景 | 逐实例、一次性、终身不改；有则钉、无则空 |
| ③ 生成边 | 行为被认可后 | 执行材料 →(显式提炼+认证)→ 新场景版本 | 反馈环；**搬材料不搬身份** |

不变式：**task 认识场景，场景谁都不认识**；关联只存 task 侧（引用+钉扎），单一真相。
场景数据中从实例产物来的部分：期望六数（← run 终局观测）、执行载体（← captures）、
ScenarioStimulus（← sealed trace）；经转换门（提炼+认证），非拷贝；固化后独立演进。

### 1.3 版本模型（D10/D11，模型 B）

| 层 | 记什么 | 回答 |
|---|---|---|
| **instance（声明）** | task 引用 + test set revision + 场景/fixture 的 version+digest | "这次尝试**针对**什么" |
| **run（实际）** | 实际执行内容指纹（不限于 codeSha，覆盖未提交工作区；形状 PLAN 定） | "真的**跑了**什么" |

- Task 定义无版本字段；会变的资产各自带版本机制（场景 version+三摘要、test set
  version、代码 hash），instance/run 只**钉引用**，不发明版本。
- "第几版" = 派生视图（同一 task 的实例序列按声明指纹去重数出），不落存储。
- 两层缺一不可：只记一层答不了"这次绿是改场景还是改代码"——比较发生在
  **同一 task 的相邻尝试之间**（D10 修正：不存在"同 instance 重跑"，
  再执行＝新 instance）。

### 1.4 流程闭环（声明 → 立项 → 执行 → 验收 → 反馈）

```
[声明] Task 定义 + 场景库 + TestSet（repo / workbench；人写）
   ↓ 发起（人/定时器）：workbench 解析 manifest → Runtime 铸 ti- → session → run
[立项] 先持久化后成功：instance 身份 + 声明输入落 TaskInstanceStore（D17 契约 1，待落地 F3）；同 key 异输入显式拒绝
   ↓
[执行] run 在基底上跑（真机 / 仿真），记实际版本；
       产物落 <task>/<instance>/（待落地 F4/F5）；无 task 落 _untasked/（D9）
   ↓
[验收] acceptance 引用，判定者显式（仿真类→对拍场景认证期望；真机类→验收规则/人工），结果归 instance 侧（D17 契约 3）
   ↓
[反馈] 被认可行为 →（显式提炼+认证）→ 新场景版本 → 回到声明层，新基线
```

资产对偶（先验/后验，验收即对拍）：

| 场景侧（期望） | 实例侧（观测） |
|---|---|
| 期望快照六数 | run 终局观测六数 |
| 载体（固化输入） | 输入侧 journal（world 初始/stimuli） |
| 认证三摘要 | 两层指纹（声明 digest + codeSha） |

**材料向上成基线（生成门），基线向下当判据（对拍）；身份与存储始终隔离。**

### 1.5 基数链（已核实）

Task 1:N TaskInstance；TaskInstance 1:1 ProductSession（磁盘预留 1:N，该注释不得
改写，见 state.md Alternatives 6）；Session ≤1 Run（409 硬挡）；再执行 = 新
instance（ADR-0045）；基线 §22 的"多 Run"是产品模型层概念，不等于 instance→run 1:N。

### 1.6 目标存储布局（已定，未实现）

```text
<state-root>/                      ← 介质根（改名，不再叫 runs/，D1）
├── tasks/<taskId>/
│   ├── task.json                  ← taskId + 定义来源 + digest（引用非副本，D7）
│   └── <taskInstanceId>/          ← 一次尝试
│       ├── metadata.json          ← runId/productSessionId/changeId/两层指纹
│       └── *.journal.jsonl(+.head)
├── _untasked/                     ← 无 task 的 run（D9，数量可统计）
├── identity/ · runtime-runs/      ← 身份权威与投影（不因布局改变，D8/A3）
└── required-reports/
```

### 1.7 交接契约（2026-10-26 评审补齐，D17）

流程闭环的三个交接点——不加组件，只把边界上的**时序与数据承诺**写完整。
评审结论：总体设计保留，架构主体不推倒重来。

**契约 1：立项确认（Admission）**

| 承诺 | 内容 |
|---|---|
| 先持久化后成功 | Runtime 先把 instance 身份 + 声明输入钉扎写入 **TaskInstanceStore**（扩展 TaskInstanceRecord 或伴生记录，PLAN 定形），再返回成功；持久化失败＝发起失败，不存在"成功但无记录" |
| 幂等与冲突 | 同 idempotencyKey 重入 → 返回**原记录**（含原声明输入）；**同 key 但声明输入不同 → 显式拒绝**（input-conflict），绝不静默覆盖或改写 |
| 投影非权威 | run 目录 metadata.json 只是钉扎的**投影**；未产生 Run / 启动失败的实例靠 Store 查询（不查目录） |
| 比较范围（P-A） | 声明指纹参与：意图正文 digest、test set revision、fixture(id+version+digest)、scenario(id+version+digest)、acceptanceRefs；**排除** displayName、时间戳、临时环境字段；钉扎材料须与实际执行所用材料一致（不一致记分歧，不静默） |

**契约 2：执行交接（Execution handoff）**

- 时序沿既有权威链不动：Session → 契约细化 → accepted Contract View →
  Legal Activation（P24 幂等）→ Run（ADR-0045）；session 二绑 run 409 保持，
  不放宽基数。
- Run 侧记录**实际实现指纹**：须能区分"同提交、不同工作区"（共享脏工作区是
  本仓常态）——codeSha 之外覆盖实际执行的源内容（如工作区内容摘要），
  具体形状 PLAN 定。

**契约 3：结果交接（Result handoff）**

三件事分离，各有来源与状态：

| 结果 | 是什么 | 判定/产出者 | 记录归属 |
|---|---|---|---|
| Run Outcome | 执行终局（状态/指标） | Run / Kernel | run 产物（journal / 终局记录） |
| 任务验收 | "是否达到 task 的 acceptance" | **显式**：仿真类＝对拍场景认证期望的执行者；真机类＝验收规则/人工（PLAN 定形） | instance 侧（与钉扎同 Store 或伴生）——验收结果不靠 run 产物自证 |
| 报告交付 | required-reports | 既有交付体系 | deliveryId / 不可变版本 + head 原子替换（不变） |

反馈成新场景：必须过显式认证（certify 门），无自动晋升。

**权威边界（P-B，评审补入）**：instance 侧**只记录不判定**——不得改写 Run
Outcome，不得替代 UniAgent 的 Goal Evaluation；每条验收结果可追溯判定者、
规则版本、对应实例/Run 与证据。**保存位置 ≠ 判定权。**

## 2. 已定案总表（D1–D17，详证见 state.md）

| | 裁决 | | 裁决 |
|---|---|---|---|
| D1 | `runs/` 只是介质根，需改名 | D9 | 无 task 的 run 走 `_untasked/` 且可统计 |
| D2 | 布局 `<task>/<taskInstanceId>` 两层 | D10 | 版本指纹两层（声明/实际） |
| D3 | runId 不占路径段 | D11 | **TaskDefinition 不带版本（模型 B）**；单向快照钉扎 |
| D4 | 版本随 instance 管理 | D12 | 场景＝独立资产，被 task **可选**引用，零耦合 |
| D5 | 不预置 run 目录层 | D13 | TaskInstance＝立项，非执行；重试定义待强化（待办） |
| D6 | session 不进路径 | D14 | **Task 单物种**（意图正文+引用集；不设类型字段） |
| D7 | task 定义存引用不存副本 | D15 | 回归可发起：留门不修路 |
| D8 | 身份权威在 store，路径只定位 | D16 | 快照证据≠第二真相（D7 禁的是可编辑重复权威） |
| D17 | 交接三契约：立项确认 / 执行交接 / 结果交接（§1.7） | | |

词汇表已落册（CONTEXT.md）：新增 **Scenario** 词条（测试与能力基线资产）；
**UniClaw Task** 词条更新为单物种表述。

## 3. 待敲定（进入 PLAN 前必须裁决；建议仅供参考）

| # | 问题 | 选项 | 建议 | 依据/风险 |
|---|---|---|---|---|
| **O2** | task 路径段命名 | 纯 `taskId` vs 可读 slug+ID 后缀 | **slug+ID 后缀**：slug 取 displayName/taskRef 尾段（清洗+截断），后缀保唯一与可查 | 满足 U8"打开目录读出 task"；`taskRef` 是自由中文不可直接作路径；需处理非法字符/长度/PII/Windows 限制 |
| **O3** | 存量 run 数据 | 不迁移 / 一次性迁移 / 双读 | **不迁移 + 工具双读**（旧平铺留原地，新 run 落新布局） | 本仓既有演进模式（Evidence/World/Resource/run-state 四次先例）；迁移触碰 required-reports **绝对路径 + fail-closed**，风险大收益小 |
| **O4** | 落地顺序 | (a) 身份统一 (b) 布局 (c) Workbench 接线 (d) 指纹落地 | **(a) → (b，含 d) → (c)**：布局路径段编码 task 身份，须先统一（三前缀）；钉扎落点就是 (b) 的 metadata，同步实现最自然 | 断点映射见 §4 |
| **O5** | 重放标准 | verify-only vs reconstruct（存输入快照） | **verify-only 起步**：digest 作漂移哨兵 + requirement 原文已在 run metadata（PNL-011）；PLAN 时核实"是否存在只凭 run 树重放的消费者"，有则升级 reconstruct（D16 确认快照＝证据、架构无障碍，仍待真实 buyer） | 证据见 state.md O5 收窄表述；D16 |
| **待办** | 重试的精确定义 | — | **已收口（2026-10-26 评审）**：两分法＝提交重试（同 key 同输入复用）/ 再次执行（新 instance）；不做通用恢复机制 | D13 |

> **2026-10-26 评审**：O2–O5 记录为"本轮采用选项（PLAN 基线，PLAN 内可调）"：
> O2＝slug+ID 后缀；O3＝不迁移+双读；O4＝(a)→(b 含 d)→(c)；O5＝verify-only。
> 重试按两分法收口。可判定验收（评审 P-C）入 state.md A6；比较范围（P-A）与
> 验收权威边界（P-B）入 §1.7。计划见 `plans/RUNTIME-TASK-001.md`。

## 4. 待修正（现状缺陷 → 修正动作 → 归属）

| # | 现状缺陷（已核实） | 修正动作 | 归属 |
|---|---|---|---|
| F1 | 两套 task 定义表面互不相认（workbench 有意图无引用集 / catalog 有引用集无意图正文），无 resolver 互通 | 合并为统一 Catalog；本 change 只接身份缝 | **留门**（Host-owned Catalog，Out of Scope）+ O4(c) |
| F2 | 三前缀编号：`psi-*`（workbench 兼容）/ `ti-*`（Runtime 铸造）/ `task-instance-*`（RuntimeRunStore 缺省合成） | 统一 task 身份，消除 known deviation（ADR-0045 已记录） | **O4(a)** |
| F3 | 引用解析只在 workbench 侧（index.js:186-196）；Runtime 只透传，run 记录无 version/成员/digest（PNL-003 缺口） | 钉扎落 **TaskInstanceStore**（立项时持久化，D17 契约 1）；metadata.json 仅投影 | **O4(d)** |
| F4 | `HostRunner` 文档写 `./runs/<runid>/`、实现写 `run-时间戳`；CLI 路径不传 `Launch`，run 零身份、无 metadata.json | 文档实现对齐；CLI 与 HTTP 双路径都写 metadata | **O4(b)** |
| F5 | 介质根叫 `runs/` 误导（装着 tasks 的目录叫 runs） | 改名（D1）；注意 `runDirName` 29 处调用点与三层契约 | **O4(b)** |
| F6 | 仿真基线 task 条目无发起链（跑回归=dotnet test，不铸 instance/run） | **不修**（模型留门） | D15 留门 |
| F7 | acceptance 无独立工件（部分指回场景认证期望，部分为裸字符串、语义活在测试代码） | 将来 Catalog 课题 | 留门 |
| F8 | 新场景不记录出生执行（provenance 缺口：能追溯致因 change/srRef，不能追溯源 run） | **不做**（无 buyer；致因 change 够审计） | 记录在案 |
| F10 | 实际执行指纹仅 codeSha，无法覆盖未提交工作区（共享脏工作区为本仓常态） | 实际实现指纹须区分"同提交不同工作区"（形状 PLAN 定，D17 契约 2） | **O4(d)** |
| F11 | 验收"有引用"但判定者与结果记录归属未定义 | 契约 3：判定者显式、结果归 instance 侧（PLAN 定形） | **O4(d/c)** |

## 5. 维护纪律（防方案腐化；违反即停）

1. **单向边不许加反向**：任何"场景改版通知 task""双向同步"机制一律拒绝——单向性就是维护方案。
2. **两套生命周期执法各管各的**：场景走认证链，实例产物走 journal 提交纪律；不造统一引擎/面板。
3. **默认不删**：无 GC/TTL/归档；老实例的钉扎引用着旧场景版本，删除即悬空。
4. **意图正文不抄验收细节**：验收只经 acceptanceRefs/scenarioRefs 引用；抄了就是两处真相。
5. **不为想象 buyer 预造**：provenance 字段、Sim/Product 类型字段、统一治理面板均无真实需求，出现 buyer 再立项（AGENTS.md §1.5 原则 5）。
6. **`TaskInstanceStore` "预留 1:N"注释不得改写**（换边+净减信息，state.md Alternatives 6）。
7. **快照三要件**（D16）：不可变（append-only）、带来源与摘要、永不回写权威源；
   权威源唯一且可编辑——快照是证据（provenance），不是第二真相。

## 6. 架构原则符合性

| 原则（AGENTS.md §1.5） | 本方案如何满足 |
|---|---|
| 1 一切遵循架构原则 | D7/D8 与 storage-inventory、PNL-005、CONTEXT.md:1025 一致；语义冲突停下裁决而非绕行（§3.2 教训已内化为 Alternatives 6） |
| 2 边界清晰、职责分明 | §1.1 职责矩阵；概念间仅经三条显式边连接，无绕缝直连 |
| 3 高内聚低耦合 | 关联单向、只存一侧；workbench/catalog 两 realization 可合并而不动核心（缝可替换） |
| 4 验收标准明确 | state.md Acceptance A1–A5 骨架先于实现；四元组待 O2–O5 定案补全 |
| 5 完备且可扩展、不为想象需求预造 | O1=B 不造第二版本轴；D15 留门不修路；F1/F6/F7/F8 显式留门并记录 |

另：布局不隐式锁基线 §22"暂不锁定"语义——runId 落 metadata 而非路径（D3/D5），
将来多 Run 时代按本仓既有"新写新格式+旧读旧格式"模式演进。

## 7. 证据索引（关键锚点）

- 基数：`RuntimeHttpServer.cs:198-200`（409）、`ProductSessionStore.cs:103`（预留）、
  ADR-0045（再执行=新实例）；`CONTEXT.md:1015-1031`（Task/Instance/Session 词条）
- 场景定义：`scenarios/schema.json`（v2 真校验）、`scenarios/README.md`（认证/承载纪律）、
  `scenarios/SCN-WIFI-001.json:15,30-37`（version+三摘要）
- 引用组合：`testsets/{simulation-baseline,android-settings,workspace-contract}/manifest.json`
  （三份：场景可选、fixture+acceptance 必选的实证）
- 发起链：`dsh/uniclaw-task-workbench/src/index.js:186-196`（manifest 解析）、
  `RuntimeHttpServer.cs:161-162/227`（Runtime 只透传）
- 身份前缀：`repository.js:20`（psi）、`TaskInstanceStore.cs:48`（ti）、
  `RuntimeRunStore.cs:58`（合成 task-instance-）
- 版本承诺：`CONTEXT.md:1071`（"Task Instance 必须能够指向实际采用的 Test Set revision"）、
  `docs/reports/PNL-003-observability-project-management-gap-report.md`（缺口登记）
- 现状缺陷：`HostRunner.cs:28/:120`、`Host/Program.cs`（CLI 零身份）——详证见 state.md

## 8. 下一步

2026-10-26 评审通过进入 PLAN：§3 五项已按"本轮采用选项"记录为基线；计划见
`plans/RUNTIME-TASK-001.md`（切片 0＝定形 → 切片 1＝admission 闭环 → 切片 2＝
布局与执行投影 → 切片 3＝workbench 接线）。布局落地时按 ADR 三判据评估是否
单独成 ADR（难以逆转 / 缺上下文困惑 / 真实取舍）。
