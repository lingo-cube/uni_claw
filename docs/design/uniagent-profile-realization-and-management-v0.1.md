# UniAgent Profile 实现与管理落地设计 v0.1

> Status: DRAFT（grilling 定稿：17 项裁决已闭合，待 change 执行；未授权实现）
> Authority: NONE（与 ADR / baseline 冲突时以后者为准；ADR-0041 随 PRF-1a 落）
> Date: 2026-10-07 · 输入：同日五份文档 + ADR-0040 + 所有者方向指令
> （Profile 本体不依赖 DSH）+ /grill-with-docs 两轮裁决 + 业界一手对照审计

## 0. 方向裁决（本设计的脊梁）

**UniAgent Profile 是产品级、host-neutral 的定义文件**：用一份模板化配置+描述
告诉 UniAgent「你是谁、可用什么、需要哪些模型角色、组装哪些工件」。DSH 不是
它的 owner，只是第一个 realization 消费方——adapter 把这份定义**映射**成 DSH
的 provider/model/endpoint/preset。将来 Codex-backed simulation realization
消费同一份定义做不同映射（对齐 `uniagent-realization-baseline-v0.1` §4：
Conformance Surface 不得依赖宿主 Plugin/Profile/transport 概念）。

术语已定稿于 CONTEXT.md：**UniAgent Profile** / **DSH Realization Binding** /
**Task Profile**（与宿主 deployment profile、能力 facts、设备 DeviceProfile
明确互斥）。

## 1. 目标形态：四层装配

```text
L1 产品定义（host-neutral，产品 owner）
   product/
   ├── AGENTS.md                 极简边界声明（Q15：产品资产；Harness 语义禁入；
   │                             provider 名禁入 profile）
   ├── profiles/uniagent-prod.yaml   身份/能力词汇/模型角色/装配引用/修订号
   ├── prompt/…                  静态产品 prompt 工件（PRF-2）
   └── policy/…                  危险动作 policy 工件（PRF-4）
        ↓ 被
L2 产品加载与身份（产品代码）
   src/UniClaw.Agent/Profile/    产品 profile 类型 + loader + 编译身份（自
   Agent.Dsh 上移；Q5）          UniClaw.Agent.Dsh → UniClaw.Agent 增
                                 ProjectReference，单向无环，ADR-0008 不挡）
   （模型缝已在：UniClaw.Kernel/Capability/ModelManagement.cs + LogicalProfileId）
        ↓ 被
L3 DSH adapter（realization detail）
   .dsh/product/uniagent-prod-bindings.yaml   角色偏好序→provider/model +
                                             service.baseUrl（Q2：与 dev 的
                                             .dsh/model-bindings.yaml 彻底分家）
   src/UniClaw.Agent.Dsh/        映射代码；transport 类型留此
        ↓ 装配进
L4 宿主 deployment（宿主操作者，不动）
   ~/.dsh/profiles/web/cordis.patch.yml       插件/preset/provider 目录/宿主开关
```

产品 profile 模板（字段名以切片实现为准）：

```yaml
# product/profiles/uniagent-prod.yaml — host-neutral
schemaVersion: "uniagent.profile/v1"
profileId: uniagent-prod              # 编译身份核对（现有机制原样；文件名=profileId，Q1）
profileVersion: "1"                   # 协议兼容版本（不变）
profileRevision: 1                    # Q8：PRF-1b 声明位先行，PRF-5 接执法
identity: { name: UniAgent, description: … }
capabilities: [submit_decision]       # 冻结工具词汇（编译 manifest 核对）
modelRoles:                           # Q4：key 必须是编译 LogicalProfileId 值域，
  agent.decision:       { required: true }    # 未知 key 加载即 fail-closed
  slow.semantic.text:   { required: true }    # required=true 缺绑定 → Profile Boot
  slow.semantic.visual: { required: false }   #   拒启；required=false 缺 → 诚实
assembly:                             #   NotConfigured（现状 visual 语义）
  promptManifest: product/prompt/uniagent@<hash>
  safetyPolicy: product/policy/android-settings-forbidden-actions@<hash>
observability: { tracePolicy: product-run-correlated }
```

DSH 绑定文件（Q2；modelSelection 块 CAP-007 语义原样整块迁移）：

```yaml
# .dsh/product/uniagent-prod-bindings.yaml
modelSelection: { selected: …, choices: {…}, profiles: {…} }
service: { baseUrl: http://127.0.0.1:3080 }
```

## 2. Gap → 改动落点（2026-10-07 核对）

| # | 面 | 现状 | 改动落点 |
|---|---|---|---|
| G1 | 产品定义住 DSH 目录 | `.dsh/profiles/uniagent-prod.yaml` | → `product/profiles/`（PRF-1b） |
| G2 | provider/endpoint 混入产品 profile | modelSelection+service 在同一文件 | → `.dsh/product/` 绑定文件；`FromProfile` 改双输入（PRF-1b） |
| G3 | 产品身份类型住 adapter 程序集 | 五个类型在 `Agent.Dsh/ProtocolModels.cs` | → `src/UniClaw.Agent/Profile/`；transport 类型留下（PRF-1a；引用面已核对闭合：src 5 + tests 4 文件） |
| G4 | loader 在 adapter | `UniagentProdYaml` | 拆产品 loader + DSH 绑定 loader，各自 fail-closed（PRF-1b） |
| G5 | 模型角色必需性写死代码 | `DshModelManagement` 缺省注册逻辑 | → `modelRoles` 声明（PRF-1b） |
| G6 | prompt 无版本、每轮内联 | `consultationPrompt()` | → 版本化 manifest + scoped section（PRF-2） |
| G7 | 开发上下文泄漏未验证 | includeHarnessIdentity/includeRuntimeContext 默认开 | → 探针 evidence + 常驻回归测试（PRF-3） |
| G8 | 危险动作 policy 是 testset fixture | testsets/android-settings/… | → `product/policy/` + 消费方加载点校验（PRF-4） |
| G9 | 审计链缺失 | 仅 schema hash + handshake | → 初始化 envelope（PRF-5） |
| G10 | 任务 profile 也在 `.dsh/` | settings-coverage 等 | 第二批同向裁决（Q3），不阻塞主线 |

结构不变式（一条不放松）：编译身份 fail-closed 交叉核对、manifest hash
handshake、`ROUTING_UNAVAILABLE` 禁静默降级、CAP-007 偏好序/env 覆盖语义、
restrict 空允许清单。

## 3. 落地切片（Q17：PRF-1 拆 1a/1b，失败回退粒度小）

### PRF-1a — 产品身份类型上移（纯机械搬迁，行为零变）

- **Scope**：`UniagentProdProfile`/`ProductProfile`/`CapabilityManifest`/
  `ProductCapabilities`/`ProductProtocolVersions` 从 `Agent.Dsh` →
  `UniClaw.Agent/Profile/`；加 `Agent.Dsh → Agent` ProjectReference；
  transport 类型（ProtocolStamp/Handshake*/DshServiceEndpoint）与全部引用、
  测试跟随；**ADR-0041 随本 change 落**（内容=本文全部裁决+被拒项）。
- **Acceptance**：全解测试绿（行为零变可证）；`UniClaw.Agent.csproj` 无
  Agent.Dsh 引用；ADR-0041 入 docs/adr/。

### PRF-1b — 产品 profile 与 DSH 绑定解耦（行为变更）

- **Scope**：建 `product/`（AGENTS.md + profiles/uniagent-prod.yaml 含
  modelRoles + profileRevision 声明位）与 `.dsh/product/` 绑定文件；拆双
  loader（各自 fail-closed；modelRoles 未知 key / required 缺绑定 → Profile
  Boot 拒启）；`FromProfile` 双输入；composition root 跟随；删旧
  `uniagent-prod.yaml`（Q6：不留 stub）；env 双轨——
  `UNICLAW_UNIAGENT_PROD_CONFIG`（产品 profile）+
  `UNICLAW_UNIAGENT_DSH_BINDINGS`（DSH 绑定）。
- **Acceptance**：产品 profile grep 不到 provider 名/baseUrl；两 loader
  fail-closed 用例过；handshake/manifest hash 行为不变；全解回归过。

### PRF-2 — 静态产品 prompt 版本化 + scoped seam

- **裁决（Q10/Q11）**：manifest 分段 = 身份 / 输出纪律 / **payload-shape
  纪律**（自每轮文本迁入）；manifest hash 与 protocol schema hash 耦合，
  同 change 双 bump；每轮只剩 context JSON + allowedEffects 行 + decisionId
  行。挂载主选 (a) scoped system-prompt section（DSH `systemPrompt` 服务
  scoped section 已证实可用），实现探针不通再退 (b) 每轮从版本化 manifest
  组装。
- **Acceptance**：manifest 变更 → revision+1 且旧 session 不受影响；hash 不
  匹配 fail-closed；consult 事件含 productPromptRevision；schema bump 未
  bump manifest → 校验失败。

### PRF-3 — 开发上下文隔离验证

- **裁决（Q12）**：探针机械列举 uniagent-prod 作用域 assembly sections；
  产出双份——evidence 归档 + **常驻确定性回归测试**（sections 仅含产品段，
  泄漏复发即 RED）。泄漏向量已定位：`includeHarnessIdentity`、
  `includeRuntimeContext`、persona prefix/suffix；修复手段=关闭/影子。
- **Acceptance**：探针 evidence 零泄漏或修复后复验；回归测试入套件。

### PRF-4 — 危险动作策略升格

- **裁决（Q14）**：canonical 副本迁 `product/policy/`（testset 留场景引用）；
  校验器放**消费方加载点**（Host runner 读 actionPolicy 时 fail-closed：分类
  完备/目标模式合法/默认拒绝存在），不预造 Kernel 公共缝、不进 tools/。
- **Acceptance**：非法 policy 拒跑；Settings coverage 行为与测试零回归；
  policy 带 safetyPolicyRevision。

### PRF-5 — profileRevision 执法 + 初始化审计 envelope

- **裁决（Q9/Q13）**：revision **进程级钉扎**（组合根加载一次，换 revision
  = 重启 Host.Dsh，运维规则明示；**revisit 触发**：出现多会话并发不同
  revision 的真实 buyer 时再升会话级）。envelope 落
  `src/UniClaw.Host.Dsh/runs/<productSessionId>/initialization.json`
  （protocolSchemaHash/productPromptRevision/safetyPolicyRevision/
  runtimeProfileRevision/modelRoute），`.gitignore` 增该 runs/ 路径；需评审
  副本走 evidence select，两纪律不混。
- **Acceptance**：改工件不 bump revision/hash → 启动失败；envelope 与实际
  装配一致；全解回归过。

### Deferred — Memory 接入

不落。触发条件：Memory System canonical schema 冻结（P20/P21）+ 真实
buyer；届时独立 change 增 `context` 块并补 envelope 的 memoryRecallIds。

### 第二批 — 任务 profile 同向搬迁（G10/Q3）

PRF-1b 落地后独立 change；语义不变（ADR-0040），仅位置与引用。

## 4. 裁决记录（/grill-with-docs 两轮 + 业界审计，全部按推荐闭合）

| Q | 裁决 | 业界审计 |
|---|---|---|
| 1 | `product/profiles/uniagent-prod.yaml`，文件名=profileId | 中性（无强制惯例） |
| 2 | `.dsh/product/uniagent-prod-bindings.yaml` | ✅ 部署配置与 agent 定义分离是惯例 |
| 3 | 任务 profile 第二批搬 | — |
| 4 | modelRoles key=编译值域 fail-closed；required 缺绑定 Boot 拒启 | ✅ 构造期 fail-fast 是惯例 |
| 5 | 身份类型上移 `UniClaw.Agent/Profile/` | ✅ |
| 6 | 旧文件删除不留 stub；env 双轨 | ✅ |
| 7 | 砍 `tools[]` 明细块，保留冻结词汇 | ⚠️ **有意识偏离**（业界均声明 tools 列表；我们以编译 manifest+handshake+restrict 替代执法面）——写入 ADR-0041 |
| 8 | profileRevision 字段进 PRF-1b，执法在 PRF-5 | — |
| 9 | revision 进程级钉扎 | ✅ 构造/部署时钉扎是惯例；revisit 触发已注明 |
| 10 | payload-shape 归静态 manifest，与 schema hash 耦合 bump | ✅ 三家均 agent 级静态声明，无每轮重复 |
| 11 | scoped section 主选 + 每轮组装回退 | ✅ instructions 一律 system 级挂载 |
| 12 | 探针=evidence+常驻回归测试 | ✅ eval-in-CI 是默认纪律 |
| 13 | envelope 落 runs/<sessionId>/ JSON + gitignore | ✅ LangSmith thread→run+metadata 同构（本地文件版） |
| 14 | 校验器归消费方加载点 | ✅ 加载时校验是标准位 |
| 15 | `product/AGENTS.md` 极简边界声明 | ✅ 嵌套 AGENTS.md 是约定能力 |
| 16 | ADR-0041 随 PRF-1a 落（含 Q7 偏离标注、Q9 revisit 触发、被拒项） | — |
| 17 | PRF-1 拆 1a（纯搬迁）/1b（行为变更）两 change | ✅ |

业界审计来源：OpenAI Agents SDK Agents/Testing 文档、LangSmith Observability
concepts（run/thread/metadata），及本仓库 2026-10-07 业界研究文档已引用的
Responses prompt 版本、ADK Agent Config、AutoGen ComponentModel 一手来源。

## 5. 管理面（"管理"的日常答案）

| 工件 | owner | 变更流程 | 漂移执法 |
|---|---|---|---|
| 产品 profile（uniagent-prod.yaml） | 产品（change 评审） | 内容变更 → profileRevision+1；重启生效 | 产品 loader fail-closed |
| 产品身份+能力词汇（编译） | 产品代码（change 评审） | 代码变更 → handshake hash 随之变 | handshake 核对 + loader 交叉核对 |
| prompt / policy 工件 | 产品（change 评审） | 变更 → revision/hash 重算 | hash 自检 + envelope 审计 |
| DSH 绑定文件 | DSH realization（change 评审） | 换 provider/model/endpoint 不动产品 profile | FromProfile 解析 fail-closed |
| 宿主 deployment | 宿主操作者（本机） | 随宿主机制 | consult 期 selectModel fail-closed |
| 任务 profile | 任务作者 | 按任务自由；只引用不复制 | Host 消费期校验（ADR-0040） |

管理不变式：**每个可审计面恰好一个 owner、一份 canonical 副本、一个
revision；其余位置只允许引用。** provider 名出现在产品 profile = 违规。

## 6. 与基线的对齐检查

- `uniagent-realization-baseline-v0.1` §4：产品 profile 脱离宿主概念，
  **加强**该基线；§3.2「DSH 内部组合是 realization detail」= L3/L4，保持。
- ADR-0008：Agent → Kernel 单向不受影响；新增 Agent.Dsh → Agent 与
  「Kernel 永不引用 Agent」正交。
- ADR-0040：能力链路原样；产品 profile 不成为第二条能力入口。
- 产品架构 L0-L3：Profile 不进 Goal/Evaluation/Effect authority 链；
  industry 草案 `security` 块不进 profile（Kernel/Effect Guard + policy 工件
  机械执法，不退回 prompt 软约束）。
- 模型路由纪律（uniflow B4）：`modelRoles`（产品）与 DSH 绑定（adapter）
  正是该纪律在产品 realization 侧的实例化。
- AGENTS.md 禁止事项：`product/` 属产品侧资产，与 Harness 层互不渗透。
