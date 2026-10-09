# `@uniclaw/dsh-decision-channel`

UniClaw Product 的 DSH 决策通道。它创建任务级 DSH session，并把会话归入可配置的 DSH 项目，同时为 Product 和 Slow 对话写入可配置标题。

## 配置

配置写在 DSH web profile 的宿主插件行，不写在插件源码中：

```yaml
- id: uniclaw-decision-channel
  name: '@uniclaw/dsh-decision-channel'
  config:
    workspaceTitle: UniClaw Product Tasks
    workspaceKey: UniClaw Product Tasks
    workspaceReuse: true
    sessionTitle: 遍历设置菜单
    slowSessionTitle: 遍历设置菜单 · Slow
    autoCloseTurn: false
```

| 字段 | 作用 | 缺省值 |
|---|---|---|
| `workspaceTitle` | DSH 侧边栏中的项目名 | `UniClaw Product Sessions` |
| `workspaceKey` | 共享项目目录键；同一键在不同 Product task 间复用项目 | `workspaceTitle` |
| `workspaceReuse` | 是否按 `workspaceKey` 复用测试项目 | `false` |
| `sessionTitle` | 任务级 Product 对话标题 | `UniClaw Product Consultation` |
| `slowSessionTitle` | 临时 Slow 对话标题 | `${sessionTitle} · Slow` |
| `autoCloseTurn` | 成功提交后是否主动 cancel 当前物理 DSH turn | `false` |
| `promptMount` | 静态产品 prompt 的挂载方式（PRF-003）：`section`=preset 行注册 scoped system-prompt section，每轮只带动态内容；`per-turn`=每轮携带全部静态文本（回退通道，宿主两行都要设，避免 section+每轮重复） | `section` |
| `skillArtifact` | boot 时加载的 package-relative Product Skill 目录或 `{path,name,revision,sha256}` 快照；校验通过后只挂载一次 scoped section，运行中不热切换 | `skill/android-automotive-ui-testing` |

标题和键字段必须是非空字符串，布尔字段必须是布尔值。插件通过 `workspaceRegistry.create` 和
`sessionController.rename` 写入 DSH 的正式持久化服务，不直接改 storage 文件。
`workspaceReuse: true` 时，`workspaceKey` 对应的 canonical workspace path 会由
DSH 的 registry 幂等复用，多个测试 task 共用同一个项目；每个 task 仍创建自己的
Product session，并用 `sessionTitle` 命名。`autoCloseTurn` 默认关闭，避免共享
服务被 Product task 主动回收；只有显式设为 `true` 才会在工具提交后调用 cancel。

静态产品 prompt（身份/输出纪律/payload 形状）不再写在 `consultationPrompt()`
里：canonical 工件在仓库 `product/prompt/uniagent-prod/`（manifest.json 声明
`promptRevision` 与协议 schema 耦合哈希），包内 `prompt/` 副本必须与其逐字节
一致（同步测试执法）；改 prompt 必须经 `python3 tools/prompt-manifest-hash.py
product/prompt/uniagent-prod` 重算哈希并递增 `promptRevision`，schema 变更
未同步 bump manifest 会在插件加载时 fail-closed。

领域 Skill 的 canonical 工件在仓库 `product/skills/android-automotive-ui-testing/`，
包内副本必须逐字节同步。启动时校验 `schemaVersion`、可读 name、revision、
guidance、references 和 SHA-256，并以 `uniagent-prod:skill:<name>` 注册一次 scoped
section；`per-turn` 回退模式会把同一 guidance 与静态 Product Prompt 一起放入每轮内容。
Skill 只提供观察、规划、受控执行、验证和证据指导，不授予额外 Capability、Effect 或
Memory 权限。Profile allowlist 和 Runtime initialization envelope 会记录其
name、revision、sha256。

修改 profile patch 后，需要重新启动专用测试服务才会生效：

```bash
DSH_TEST_PORT=3081 dsh/test-service.sh
```

不要用 3080 做这项验证；`dsh/test-service.sh` 会拒绝 3080，并且不会停止已有进程。

## 验证

```bash
node --test dsh/uniclaw-decision-channel/tests/plugin.test.mjs
dsh/deploy.sh --check-only
```

插件只接受四类 Product 输出：`act`（有序计划步骤）、`policy`（策略规则）、
`noAction` 和 `defer`。每次成功咨询的回执都带有 `requestId`、`generation`、
`productRunId`、`decisionId`、`schemaHash`；策略额外带 `policyId`。因此可以从
一次咨询回到对应的 Product Run、DecisionId、schema 版本和策略，而不是只看到
一段无法关联的模型文本。`maxRounds` 与 `maxApplications` 必须为正数；缺字段、
越界值、错误 DecisionId 或额外字段都会 fail closed。插件测试覆盖四类成功输出、
边界拒绝、会话映射和回执关联。
