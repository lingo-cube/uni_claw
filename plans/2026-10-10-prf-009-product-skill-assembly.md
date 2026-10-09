# PRF-009 Product Skill 领域指导装配

> PlanType / Status: ADOPTED
> References: `changes/PRF-009/state.md`, `docs/adr/0041-uniagent-profile-product-definition-and-dsh-binding-separation.md`, `docs/adr/0040-agent-selected-task-capability-binding.md`, `docs/design/uniagent-profile-realization-and-management-v0.1.md`

## 目标

把首个领域级 Product Skill `android-automotive-ui-testing` 接到现有
UniAgent Profile 和 DSH Product Prompt 装配中，形成可校验、可审计、可复现的
Session 初始化快照。Skill 只提供规划与执行指导；现有
Task Profile → `ExecutionContract` → `ExecutionContractView` 仍是唯一任务契约。

## 现状缝

1. `UniAgentProfileYaml` 已校验 Product Profile 的身份、能力、工件 hash，
   `ProfileAssembly` 是现有工件引用容器。
2. `uniclaw-decision-channel` 在启动时加载版本化 Product Prompt，并通过
   `systemPrompt.section` 注册 scoped section；每轮 `consultationPrompt` 只组装
   Kernel 投影的动态上下文。
3. `InitializationEnvelope` 已按 Product Session first-run-wins 记录 Profile、
   protocol、Prompt、Policy 和 model route。

## 设计与实现步骤

### 1. Skill 工件与 Profile allowlist

- 新增 `product/skills/android-automotive-ui-testing/manifest.yaml`、
  `guidance.md`，manifest 以 `schemaVersion: uniagent.skill/v1` 声明版本、可读 `name`、
  `displayName`、`revision`、`description`、`guidance` 和有序 `references`。
- Skill artifact 的 `sha256` 采用确定性字节规则：manifest.yaml、guidance.md、
  manifest `references` 顺序列出的文件依次写入 SHA-256；缺文件、空 name、路径
  越界、未知 reference 或重复 reference 均拒绝。
- `product/profiles/uniagent-prod.yaml` 增加 `allowedSkillRefs` 映射；每项包含
  `revision`、`path`、`sha256`，映射键就是可读 Skill `name`。Loader 读取 Skill
  manifest，校验 name、revision、路径、hash 和 hash 重算；未知/重复/非法引用
  fail-closed。
- Profile 类型增加 `SkillReference` 与 `AllowedSkillRefs`，保持 host-neutral，
  不引入 provider/model/endpoint、Task、Memory 或 Effect 字段。

### 2. DSH 静态 Skill section

- 在 `dsh/uniclaw-decision-channel/skill/android-automotive-ui-testing/` 放置与
  canonical `product/skills/...` 逐字节一致的 manifest/guidance 副本，并生成
  package-local `skill-hash.txt`；测试校验同步和 artifact hash。
- 插件启动时加载一个 boot-pinned Skill artifact（默认上述领域 Skill；可由
  `skillArtifact` 配置选择 package-relative 目录），校验 manifest、guidance、
  references、hash、name/revision；失败即拒载，不允许运行中切换。
- 通过现有 `systemPrompt.section` 只注册一次
  `uniagent-prod:skill:<name>`，Product Prompt section 保持原样；每轮不重复 Skill
  文本，动态 `AgentDecisionContext` 仍由 Kernel/Host 生成。
- 记录 Skill name/revision/hash 到 handshake/consult 可观察状态，供现有测试和
  envelope 对照；不扩展产品 Agent protocol schema 或 `AgentTaskInitialization`。

### 3. 初始化快照与回归

- `InitializationEnvelope` 在首个 Session Run 写入 `skill` 对象
  (`name`/`revision`/`sha256`)，后续 Run 保持首值。
- 补 Profile/Skill loader、DSH prompt isolation/hash、envelope first-run-wins
  测试；保留现有 capabilitySelection 一次性测试和 Task Profile→ExecutionContract
  测试不变。
- 同步 README/CONTEXT 中的工件 owner、hash、启动钉扎和 fail-closed 规则。

## WorkItem DAG

```text
WI-PRF009-001  Product Skill artifact + Profile allowlist/loader
        └──────────────┐
WI-PRF009-002  DSH section mount + InitializationEnvelope snapshot
```

两个 WorkItem 的代码边界互不重叠；002 读取 001 冻结的 Skill artifact/hash
形状，但不修改 Profile loader。Leader 在两个结果合并后运行全解验证。

## 明确不做

- 不修改 `ExecutionContract`、`ExecutionContractView`、`AgentDecisionContext`、
  `AgentDecision` union 或 `AgentTaskInitialization`。
- 不实现 Memory、Recall schema、Task Catalog、Task→Skill resolver 或热切换。
- 不新增 Settings/Wi-Fi/页面/车型/OEM/坐标级 Skill，不把 Skill 变成 capability
  或 Effect authority。
