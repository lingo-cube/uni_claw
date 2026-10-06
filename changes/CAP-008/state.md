# CAP-008 — Model Management 对齐 Capability 可执行契约 + 组件开发纪律 skill

lifecycle_state: closed · disposition: none · depth: standard · base: 63804f7a

## Intent（WHAT/WHY）

所有者指令（2026-10-06，三轮）："能力插件应继承 ICapability（如 IModelManagement :
ICapability）；先审计当前实现是否符合 Capability Hub 既有设计；组件开发规定固化为
明确要求甚至 skill"；"ICapability 是否完备（自动注册等）；每能力一协议，职责清晰、
高内聚低耦合、可替换可插拔"；"生命周期要有定义（复用 .NET 机制）；该有健康检查的
要有、接口要体现"；"自查/grill 后落地"。

审计结论（对照 CAP-002 可执行契约 + docs/capability-hub + ADR-0035/0038）：

- ✅ 符合：信任域/scope、descriptor 协议+角色、fail-closed、ADR-0038 三层、
  realization 显式声明借用。
- ❌ 不符合：`ModelManagement` 未实现 `ICapability`（CAP-006 走 description-only
  路径绕开可执行契约）；声明与运行时缝是两个脱节事实。
- 📄 缺位：seam 设计 §4.0.1 候选表无模型路由类别；协议体系只对感知有 registry 执法。
- 生命周期：机制完备（迁移表+事实序号）但驱动空转（全仓无能力走过 Active/Closed）；
  capability 级运行期健康无上报。Hub 其余留白（push 订阅/typed Resolve/task-scoped
  API）均有决策记录，属有意留白。

## Scope（含所有者三轮补充后的完整范围）

- Kernel：`IModelManagement : ICapability, ICapabilityHealthCheckable`（操作面成员）；
  `ModelManagement` 实现之（Description 可注入、缺省 Canonical；`WithDescription`
  = 同一活注册表的重述视图；`CheckHealth` 拉式聚合 Unknown/Healthy/Degraded/Unhealthy）；
  `ModelManagementProtocol` 协议常量；`ICapabilityHealthCheckable` +
  `CapabilityHealthReport` 健康能力面（mixin，不进 ICapability 根）；拉推分离
  （探测=拉，事实=owner 提交，Hub 只记账——ADR-0035 不破）。
- Registry 执法：`CapabilityCategory.ModelRouting` 新类别；协议执法（scope、恰一协议
  =Model Binding Resolution@1.0、角色镜像、依赖-关系镜像）；实例执法（双向：声明
  ModelRouting 必须实例注册且实现 IModelManagement；IModelManagement 实例不得用
  其他类别）；**ModelRouting 拒绝 description-only**（P2 修正）。
- Host 组合根：实例注册（Register(ICapability)，声明与运行时同一事实；Resolve 可
  取回）；协议常量改以 Kernel 为单一真相（Host 常量为别名）；Program.cs 显式传
  adapter 侧 RealizationName（消除双写）。
- 生命周期定义（RL1-RL3）：管理面事实（Commit）× .NET 资源面机制
  （IAsyncDisposable，幂等）映射；Hub 永不调用 Dispose；不引入 IHostedService/
  DI（闭包禁词纪律）；ModelManagement 按 RL1 声明"常驻无资源、Registered 即稳态"。
- 文档：seam 设计 §4.0.1 补第十类 Model Routing / Binding Management（九类→十类）。
- Skill：`.agents/skills/capability-component/SKILL.md`（LOCAL_UNICLAW；判断门/
  继承位势 L0-L3/七步流程/协议质量门 R4-R7/生命周期 RL1-RL3/执法点/有意留白清单/
  worked examples；不复制 capability-hub 真相源）。

## Out of scope

- 存量感知 description-only 注册不追溯（known gap：组合能力 L1 形状待管线组件稳定）。
- 不做 typed Resolve、push 订阅、task-scoped API、自动驱动机制（有意留白清单）。
- 不扩自动注册禁词执法到 Kernel/Host（skill 纪律条款 + 可选执法项记录）。
- 非 DSH 决策通道 transport（前置：点名 provider）。

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | 可执行契约 | IModelManagement : ICapability(+健康面)；实例注册发布 + Resolve 取回 + 无状态分叉（重述视图共享活注册表） |
| A2 | 协议/实例执法 | ModelRouting：接受合法实例；拒绝 description-only/错协议/错类别/非 IModelManagement 实例（双向） |
| A3 | 健康能力面 | CheckHealth 四态聚合 + ApplyHealth 联动 + 拉推分离 |
| A4 | 生命周期 | 合法驱动链样本（Registered→Ready→Active→Draining→Closed）+ 非法迁移拒绝；RL1 声明入 state |
| A5 | 可替换性 R5 | 同一 IModelManagement 消费闭包喂 DSH/replay 两实例，行为契约互换 + 同失败语义 |
| A6 | 组合根 | Program.cs 实例注册 + adapter 常量消除双写；capability-facts 形状不变 |
| A7 | skill + 文档 | capability-component SKILL.md 落地且已进目录；§4.0.1 十类 |
| A8 | 兼容 | 全量测试绿；白名单 +4 型后执法绿 |

## Verification（2026-10-06 回填）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet build UniClaw.Kernel.slnx` | 0 error | 0 error、0 新增警告 | 本地构建 |
| DETERMINISTIC | Kernel.Tests 818（新增：契约/描述/G6 回归/健康四态/注册执法 5 例） | 全绿 | PASS 818/818 | `dotnet test tests/UniClaw.Kernel.Tests` |
| DETERMINISTIC | Host.Tests 172（新增：实例注册同一事实/生命周期合法链） | 全绿 | PASS 172/172 | `dotnet test tests/UniClaw.Host.Tests` |
| DETERMINISTIC | Agent.Dsh.Tests 154（新增：R5 可替换性执法） | 全绿 | PASS 154/154 | `dotnet test tests/UniClaw.Agent.Dsh.Tests` |
| DETERMINISTIC | 全量 + 场景哈希 | 全绿 | Simulation 188/188（CAP-008 再认证 20 块）；Core 14/Agent 17/FSR 9；合计 1372/1372 | `dotnet test UniClaw.Kernel.slnx` |
| CONTRACT | skill 登记 | 目录可见、frontmatter LOCAL_UNICLAW | 会话技能目录已列出 capability-component | 目录快照 |

## Grill 记录（落地前红队，按所有者要求执行）

- **G6（真 bug，已修）**：WithDescription 经公开 ctor 重建会丢偏好序（同 profile
  Register 覆盖）；且拷贝字典导致 ApplyHealth 后原实例与注册实例状态分叉——
  测试红队抓到分叉。修正：私有同表视图 ctor（共享活字典）；回归测试钉住
  （偏好序保留 + 双向可见 + 声明独立）。
- G3 白名单 4 型排序位精确插入；G8 生命周期样本打在测试 fake 上（避免与 RL1
  "ModelManagement 停 Registered"自相矛盾）；P1 审计表述降级（自动注册执法仅
  Agent.Dsh）；P2 ModelRouting 拒绝 description-only。

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 三轮所有者指令（继承纪律/完备性
  审计+每能力一协议/生命周期+健康）+ grill 红队（G6/P1/P2）；审计结论入 Intent。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY · 落地：Kernel 缝契约化（接口+健康面+
  协议常量+同表视图）→ Registry ModelRouting 执法 → 组合根实例注册 → 白名单 +4 →
  测试 8 新例（含 G6 回归与 R5 执法）→ §4.0.1 十类 → skill 落地（目录已生效）→
  场景再认证 → 全量 1372/1372。跨会话：ObserverProjectionTests 断言随并行会话
  yaml 中间态（deepseek-official）再同步一次，归属其 change。待所有者验收后 CLOSED。
- 2026-10-06 · CLOSED（所有者验收，2026-10-06）· 验收基线：全量测试绿（最终核验 1372/1372、build 0 error）；acceptance A1-A8 均有四元组证据（含 grill 红队 G6 回归）；skill 已进目录生效；场景哈希已按 CAP-008 再认证。感知存量 description-only 作 known gap 记录，留白清单归 skill 维护。CAP 三部曲（006 声明 → 007 选择能力 → 008 可执行契约）就此收官。
