# ADR-0041：UniAgent Profile 为产品级定义，DSH 只做映射绑定

状态：accepted（2026-10-07，PRF-001；设计输入：
`docs/design/uniagent-profile-realization-and-management-v0.1.md`，17 项裁决经
/grill-with-docs 闭合并经业界一手审计）

UniClaw 的 UniAgent 配置此前物理上住在 DSH 侧：产品定义
（profileId/capabilities）与 DSH 绑定（provider/model/baseUrl）混在
`.dsh/profiles/uniagent-prod.yaml` 一个文件里，加载与编译身份
（`UniagentProdProfile`/`CapabilityManifest` 等）都在 adapter 程序集
`UniClaw.Agent.Dsh`。这与 `uniagent-realization-baseline-v0.1` §4「调用方
不得依赖宿主 Profile/Plugin/transport」方向相反：产品定义被宿主目录和
adapter 程序集拥有，第二个 realization（Codex simulation）无法消费同一份
定义。

## Decision

1. **UniAgent Profile 是产品级、host-neutral 的装配描述**：身份、冻结能力
   词汇、模型角色必需性（`modelRoles`，key 必须是编译 `LogicalProfileId`
   值域，未知 key 加载即 fail-closed）、产品工件引用与 `profileRevision`。
   它永不含 provider/model 名、服务端点或运行态。落点
   `product/profiles/uniagent-prod.yaml`（PRF-1b 引入；文件名=profileId）。
2. **产品身份类型归产品程序集**：`ProductProtocolVersions`、
   `CapabilityManifest`、`ProductCapabilities`、`ProductProfile`、
   `UniagentProdProfile` 上移 `src/UniClaw.Agent/Profile/`（namespace
   `UniClaw.Agent.Profile`）；transport/绑定类型（ProtocolStamp、
   Handshake*、ModelConfiguration、UniagentProdConfiguration 等）留
   `UniClaw.Agent.Dsh`。新增 `Agent.Dsh → Agent` 单向 ProjectReference
   （ADR-0008「Kernel 永不引用 Agent」不受影响）。
3. **DSH 只是映射消费方**：provider/model 偏好序与 service 端点属
   `.dsh/product/uniagent-prod-bindings.yaml`（CAP-007 语义原样整块迁移），
   与开发 Harness 的 `.dsh/model-bindings.yaml` 彻底分家。换 provider/端点
   不改产品 profile；产品 profile 出现 provider 名 = 违规。
4. **`profileRevision` 进程级钉扎**：组合根加载一次、进程内不可变，换
   revision = 重启 Host.Dsh。**revisit 触发**：出现多会话并发不同 revision
   的真实 buyer（ADR-0026 buyer-first）再升会话级。
5. **有意识偏离业界 tools[] 明细块**：OpenAI Agents SDK / Google ADK /
   AutoGen 均在 agent 配置声明 tools 列表及 schema/执行面明细。UniClaw 不
   采：工具词汇以编译 manifest（handshake hash）+ restrict 空允许清单 +
   ADR-0040 task-scoped binding 三层机械执法，profile 复制 schema 只会制造
   第二真相源。profile 的 `capabilities:` 冻结词汇清单保留（它是指纹，不是
   目录）。
6. 术语定稿见 CONTEXT.md：UniAgent Profile / DSH Realization Binding /
   Task Profile（任务 profile 第二批同向搬出 `.dsh/`，语义不变）。

## Consequences

- 后续切片按设计文档执行：PRF-1b（拆文件+双 loader+modelRoles+删旧
  yaml+env 双轨 `UNICLAW_UNIAGENT_PROD_CONFIG` /
  `UNICLAW_UNIAGENT_DSH_BINDINGS`）、PRF-2（静态 prompt 版本化 + scoped
  section）、PRF-3（开发上下文隔离探针+常驻回归）、PRF-4（危险动作 policy
  升格 `product/policy/`）、PRF-5（revision 执法+初始化审计 envelope）。
- 第二 realization 直接消费同一份产品 profile 做不同映射；Conformance
  Surface 不再被宿主配置概念倒灌。
- 每次 consult 可经初始化 envelope 重建当时装配（protocolSchemaHash /
  productPromptRevision / safetyPolicyRevision / runtimeProfileRevision /
  modelRoute，PRF-5）。

## Rejected alternatives

- **维持现状（产品定义住 `.dsh/` + adapter 程序集）**：方向性违反
  realization baseline §4；第二个 realization 被迫复制或反向依赖 DSH。
- **产品身份类型进 Kernel**：违反 ADR-0008 单向纪律（Kernel 永不引用
  Agent 类型）；身份是 UniAgent 语义，不是 Kernel 语义。
- **per-session 热加载 revision**：无真实 buyer（ADR-0026）；引入同进程
  多 revision 的对账复杂度。
- **采纳业界 tools[] 明细块**：见 Decision 5——已有更强机械执法面，复制
  schema 制造第二真相源。
- **产品 profile 内嵌 prompt/policy 正文**：只引用版本化工件（hash 钉扎），
  正文归各自 owner（PRF-2/PRF-4）。
