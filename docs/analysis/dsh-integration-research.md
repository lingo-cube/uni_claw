# DSH 运行时嵌入 uni_claw（含两层 profile 定制）研究

> 状态：研究交付（2026-09-22 重写，以 **embedding** 为主线）。范围已由用户确认：
> 目标不是「DSH 作为 uni-harness 的开发 Host」为主，而是「把 DSH 运行时作为依赖嵌入
> uni_claw 产品侧（DSH-backed UniAgent = Product Realization，ADR-0022）」。profile
> 定制两层都算：DSH 安装 profile（bundles / cordis.patch.yml）与仓库侧适配 profile
> （角色模型绑定）。
>
> **范围（实现授权）边界**：本文件是 **R1 前 scoping**，不授权实现。
> `docs/architecture/uniagent-realization-baseline-v0.1.md:237-247`（§9 Deferred）明确
> 「Codex SDK/App Server 组合、DSH Profile/plugin/package 切分、Kernel Bridge transport、
> Tracer Bullet、产品实现」未获授权；ADR-0022 同。本研究的全部结论停留在可验证的选项
> 评估层面。
>
> **证据惯例**：每条关键结论带一手引用（`路径:行号`，DSH 侧相对 dk-harness 仓库根，
> uni_claw 侧相对 uni_claw 仓库根；git 历史用 `git show <rev>:<path>`）。标注
> **[已核实]** = 直接读源码/文档/运行实例确认；**[推断/取舍]** = 基于已核实事实的
> 分析结论，需后续裁决。

---

## 1. 结论摘要

1. **[已核实] 当前运行中的 DSH 实例 = 开发 Harness 侧的 web profile**（`~/.dsh/profiles/web`），
   挂了 `dsh-uniflow-agent-loop@1.1.0` 等 4 个插件，其中 uniflow 插件的
   `config.profileSource` 指向 `uni_claw/.dsh/profile-adapter/profile-source.yaml`——
   **该文件在 uni-harness 分支不存在**，目前是悬空配置 + health 降级态（§2.2）。
   这条"开发 Harness 侧"现状与产品 embedding 目标**必须区分对待**：产品嵌入不能复用
   这个 profile（会违反 ADR-0022 / baseline §6 的 Harness⊄Product 隔离）。
2. **[已核实] DSH 的发布形态是单 CLI 全量**：`@deepseek-ai/dsh`（0.1.1-rc.2）的
   `bin: {"dsh": "lib/bin.js"}`，其 `dependencies` 直接列出 `@deepseek-ai/dsh-base`、
   `@deepseek-ai/dsh-headless`、`@deepseek-ai/dsh-web-app` 等全部 bundle 包——**装一个
   CLI 即得全部 in-box bundle**，无需逐包组装（`apps/cli/package.json`）。
3. **[已核实] 非模板 profile 的创建方式是 `dsh plugin --profile <name> add <pkg>`**：
   首次运行自动 `initProfile`（`dsh.profile.bundles = ["@deepseek-ai/dsh-base"]`），随后
   reconcile 会把声明 `dsh.bundle` 的依赖（含 in-box bundle）追加进 bundles 层——因此
   产品 profile（如 `uniclaw-product`）可以用
   `dsh plugin --profile uniclaw-product add @deepseek-ai/dsh-headless`
   一条命令得到 **base + headless** 组合（§3.3）。
4. **[已核实] 嵌入式启动有三条路径**：headless 一次性
   （`dsh --profile <name> "job"`，fresh session + stdout 最后答案 + exit 0/1）、
   **stdio JSON-RPC 常驻 server**（`@deepseek-ai/dsh-sdk-jsonrpc-server` 插件 +
   自定义 `cordis.yml`，`examples/jsonrpc-agent` 姿势）、web app host（带 UI）。
   **[已核实] .NET 没有文档化 SDK 等价物**：`packages/sdk/` 只有 TS 三件套
   （client / protocol / server），Python SDK 的 bundled-runtime 是 Python 专属
   （`docs/user/guide/python-sdk.md:17`；`.NET` 绑定 grep 无命中）——C# 侧只能
   **spawn CLI** 或 **spawn JSON-RPC server + 自写 JSON-RPC client**。
5. **[推断/取舍] 推荐嵌入形态**：产品 profile `uniclaw-product`（base + headless）+
   Kernel（C#）每次任务 **spawn** `dsh --profile uniclaw-product "<job>"`，`DSH_HOME`
   指向仓库内隔离目录（sessions/settings/credentials 整体独立），凭证走
   `DEEPSEEK_API_KEY` env 注入；产品侧 WorkItem/Contract → DSH job 的翻译 + DSH
   session id → Product Run id 的**显式映射**放在 Kernel 与 DSH 之间的 adapter 层
   （未见任何 Profile/插件/状态目录引入）。JSON-RPC 常驻形态作为需要持续会话/事件订阅
   时的第二选项保留。
6. **[推断/取舍] 仓库侧适配 profile（层 2）**：Product 的 tier→provider/model 绑定
   **不得进共享层**（`model-routing.yaml` / SKILL / `schemas/`，MRB-001 约束），放
   Product 适配目录（如 `platforms/dsh/bindings.yaml`，形状同构于
   `.dsh/model-bindings.yaml`，复用校验工具）；`profile-source.yaml`（uniflow 插件
   消费的角色绑定）只在**需要插件强制闭环**时引入，产品基线下不必要。
7. **[已核实] 隔离语义由 DSH_HOME 天然提供**：settings/credentials/sessions 都以
   `$DSH_HOME` 为根（base patch `:75-101`、`settings-file/src/index.ts:56`、jsonrpc
   `cordis.yml` 的 `DSH_SESSION_ROOT`）——DSH_HOME 指向仓库内目录即完成
   state/instruction/lifecycle 的整体隔离（ADR-0022 §6 的要求）。
8. **[已核实] 一切嵌入实现仍是未授权工作**：baseline §9 与 ADR-0022 明确 Deferred
   （§9.3/§9.5），本研究只做 scoping。

---

## 2. 现状盘点

### 2.1 运行中的 DSH 实例（开发 Harness 侧）[已核实]

| 证据 | 内容 | 来源 |
|---|---|---|
| GUI 端口与引导 | `lsof -i :3080` → node 监听；`curl :3080/` 返回带 `window.__ModuleLoader__` 的 DSH web 引导 | 本机实测 |
| 插件 surface | `curl :3080/plugins/dsh-uniflow-agent-loop/client.js` → 200；`/plugins/@user/dsh-plugin-provider-usage/client.js` → 200 | 本机实测 |
| 会话证据 | 父/子会话 zstd 日志：web surface 来源、`agentPreset: standard`、`delegationDepth` | `~/.dsh/sessions/--Users-fran-Documents-Code-spacex-uni_claw--/` |
| profile 定义 | `dsh.profile.bundles = [base, web-app]`（installation-owned 模板，`packages/boot/app-boot/src/profile.ts:113-117`） | `~/.dsh/profiles/web/package.json` |

### 2.2 开发 Harness 侧插件栈与悬空配置 [已核实]

`~/.dsh/profiles/web/`：`dependencies` =
`@deepseek-ai/cordis`(link→dk-harness/vendor/cordis)、`@deepseek-ai/dsh-typert-protocol`
(link)、`@user/dsh-plugin-provider-usage`(file:)、`@user/dsh-plugin-task-notify`(link:)、
`dsh-mcp-manager@0.6.0`、`dsh-uniflow-agent-loop@1.1.0`(file: tgz)；`cordis.patch.yml`
手工 `insert` 4 行（mcp-manager / provider-usage / task-notify / uniflow-agent-loop），
其中 uniflow 行的 `config.profileSource` 指向
`/Users/fran/Documents/Code/spacex/uni_claw/.dsh/profile-adapter/profile-source.yaml`
（**uni-harness 无此文件**；`git ls-files .dsh/` 仅 `model-bindings.yaml`）。
`~/.dsh/profiles/headless/` 挂 `dsh-uniflow-agent-loop@1.0.0` + `uniflow-e2e-driver`
（版本与 web 不一致）。

这四个插件**全部无 `dsh.bundle` 声明、只有 `dsh.client`**（逐包核实），所以它们是
"plain dependency + 手工 patch 行"，不是 bundle 层成员。uniflow 插件当前处于
health `profile_source.loaded: false` 的降级态（`dsh-plugins/uniflow-agent-loop/src/index.js:37-45,90-100`），
且无人注册派发 envelope，强制闭环未激活。

> **[推断]** 上述整套是 **Development Harness 侧**的既有事实。产品 embedding 不应把
> uniflow-agent-loop / mcp-manager / 开发插件带入 Product Runtime——baseline §6 的
> instruction/tool 隔离要求 Product 侧是独立的 tool allowlist（§5.1）。

### 2.3 产品侧基线现状（FROZEN 约束清单）[已核实]

`docs/architecture/uniagent-realization-baseline-v0.1.md`（FROZEN，264 行）：

- **角色**：DSH-backed = Product Realization（§1，line 68）；
- **Realization Contract**（§2，line 72-91）：Product Session 内收意图、创建 Primary
  Goal/Criteria、P1 Contract admission、消费 immutable P18 Runtime Outcome、immutable
  Goal Evaluation、fail closed、`1 Product Session / 1 Primary Goal / 1 Primary Run`；
- **Host/Product 状态映射**（§5，line 142-157）：Host SessionId 不得隐式成为 Product
  SessionId；turn/step 不得冒充 Run；禁止"根据 transcript 反向考古 canonical state"；
- **Harness/Product 隔离**（§6，line 162-178）：AGENTS.md / coding Skills 不得成为
  Product instruction authority；Product Runtime 不得默认暴露 coding tools；
  Development Session 与 Product Session 不得相互 resume；
- **Conformance Surface**（§8，line 189-235）：必须一致轴（cardinality/Owner/Contract/
  Outcome/Evaluation/terminal/failure/correlation/evidence）；C1–C11 minimum scenarios
  （C2=Contract 非法 fail closed、C7=required provider 缺失不启动或 safe-stop、
  C8=approval 拒绝无绕过路径）；
- **§9 Deferred**（line 237-247）：DSH Profile/plugin/package 切分、Kernel Bridge
  transport、Tracer Bullet、产品实现**未获授权**（本研究的边界）。

uni-harness 现有 DSH adapter 落点（开发侧）：
`.dsh/model-bindings.yaml`（MRB-001 closed）、`model-routing.yaml`（共享层
capability→tier）、uniflow SKILL §B4 派发规则、`schemas/work-item.schema.json`、
`tools/validate-model-bindings.py`。

---

## 3. DSH 发布形态与 profile/依赖机制（一手）

### 3.1 发布形态：一个 CLI 即全部 bundle [已核实]

- `packages/…/apps/cli/package.json`：`name: @deepseek-ai/dsh`、`version: 0.1.1-rc.2`、
  `bin: {"dsh": "lib/bin.js"}`；`dependencies` 直列 `@deepseek-ai/dsh-base`、
  `@deepseek-ai/dsh-headless`、`@deepseek-ai/dsh-web-app` 以及全部 tool/provider 包
  （workspace:^ 在本仓；发布为 npm 时解析为版本）。
- 官方安装：`npx @deepseek-ai/dsh web`（`README.md:20`）。
- 含义：依赖 `@deepseek-ai/dsh` 一个包（版本钉住）即获得 headless 一次性运行、web
  surface、全部 in-box bundle 与 `dsh plugin` 管理面，无需自行组装 bundle 依赖
  （`apps/cli/reference/README.md:11` "In-box bundles always come from the same
  installation as the running dsh"）。

### 3.2 profile 构成与层叠 [已核实]

profile = `$DSH_HOME/profiles/<name>/` 目录：
`package.json`（`dsh.profile.bundles` 有序列表 + out-of-tree 插件依赖）、
`cordis.yml`（**每次启动重写的空根** `[]`，apps/cli/src/profile-boot.ts:60-64,98-103）、
`cordis.patch.yml`（用户 patch 层）、pnpm 管理的 `node_modules` + `pnpm-workspace.yaml`
（`packages/boot/app-boot/src/profile.ts:152-168`）。

组合顺序（`apps/cli/reference/README.md:9`、`docs/user/develop/basic/publish.md:112-128`）：
① bundle 层（`dsh.profile.bundles` 序）→ ② profile `cordis.patch.yml` → ③
`$DSH_HOME/cordis.patch.yml`（machine-local，所有 profile 共享）→ ④ `--patch` overlays。
bundle 解析双锚点：先 dsh 安装自身、再 profile `node_modules`；flat fallback
`$DSH_HOME/profiles/node_modules`（`profile.ts:344-355`、`:223-255`）。
patch entry 语法（`vendor/include/src/index.ts:78-127`）：`insert` 列表 / 按 `id` 整体
替换 `config`（不深合并）/ `disabled: true`；`!!js` 表达式允许。

### 3.3 非模板 profile 的创建方式（产品 profile 的关键路径）[已核实]

- 内置模板只覆盖 `web`、`headless`（`profile.ts:113-117`）；其他名字首次 boot 会 fail
  loud 并提示 `dsh plugin --profile <name> add <package>`（`profile.ts:376-384`；
  `apps/cli/reference/README.md:13`）。
- `dsh plugin --profile <name> <pnpm args>`：缺失时先 `initProfile(dir,
  PROFILE_TEMPLATES[name] ?? ['@deepseek-ai/dsh-base'])`（`apps/cli/src/plugin.ts:120-129`），
  然后转发 pnpm，成功后在 `dsh.profile.bundles` 对账：声明 `dsh.bundle` 的依赖加入层栈、
  bundle-less 依赖只装不激活（警告一次）、移除出栈（`reconcilePlugins` `plugin.ts:59-102`、
  `exportsPatch` `:36-57` 用双锚点 `resolveBundleDir` 判定）。
- **推论（关键）**：`@deepseek-ai/dsh-headless` 声明 `dsh.bundle.patch`（
  `packages/bundle/headless/package.json`），因此
  `dsh plugin --profile uniclaw-product add @deepseek-ai/dsh-headless` 首次运行会把
  profile 初始化为 `bundles=[base]`，再 reconcile 追加 `dsh-headless` → 最终
  `[base, headless]`，与 shipped headless 模板等价——**一条命令即可从零创建产品
  headless profile**。同理 `add @deepseek-ai/dsh-web-app` 得到 base+web-app。
  [推断] 这是产品 profile 的标准创建方式，应记入交付文档而非手工写 package.json。

### 3.4 插件安装与版本策略 [已核实]

`dsh plugin --profile <name> <args…>` 即 pnpm 转发（`runPlugin` `plugin.ts:120-167`，
initProfile 于 `:122-129`，spawnSync 于 `:142-159`）；相对路径锚定到调用方目录
（`anchorPathSpec` `:104-123`）；git 安装需 `allowBuilds` 白名单
（`apps/cli/reference/README.md:63`）。版本策略：生产安装 = npm 版本包钉住；
`file:`/本地 checkout 仅开发回路（uniflow-agent-loop README:65-67 及其 dsh-plugins
`docs/maintenance.md:46-56`）。
未发布的私有插件只能走 tgz/file:（现状 web profile 即此形态，且 `link:` 依赖机器锚定
dk-harness checkout，换机即坏）。

### 3.5 嵌入式运行入口（不依赖 GUI）[已核实]

| 入口 | 形态 | 证据 |
|---|---|---|
| headless 一次性 | `dsh --profile headless "<task>"`：fresh persisted Agent、flush Session、stdout 最后非空 assistant text、`turn/end completed→0 否则 1`、无监听端口 | `apps/cli/reference/README.md:30`；`packages/bundle/headless/README.md:7` |
| JSON-RPC 常驻 | `cordis.yml` 挂 `@deepseek-ai/dsh-sdk-jsonrpc-server` + `sdk-jsonrpc-server` 插件；stdio 专属协议，stdout 不得被 logger 占用 | `examples/jsonrpc-agent/cordis.yml`；`packages/sdk/server/src/index.ts`（`name='sdk-jsonrpc-server'`） |
| cordis.yml 直启 | SDK 姿势：spawn `node <dsh>/lib/bin.js <cordis.yml>`（非 profile）| `packages/sdk/client/README.md`（`DeepSeekHarness launch: {command:'node', args:['lib/bin.js','cordis.yml']}`） |
| web host | `dsh --profile web`（GUI/HTTP；产品若不要 UI 则不选） | `apps/cli/reference/README.md:67` |

嵌入式环境变量（JSON-RPC 文档）：`DEEPSEEK_API_KEY` / `DEEPSEEK_BASE_URL` /
`DSH_CWD` / `DSH_SESSION_ROOT`（JSONL session 目录）/ `DSH_SYSTEM_PROMPT` / `DSH_MODEL` /
`DSH_MAX_TOKENS_AS_SUCCESS`（`examples/jsonrpc-agent/README.md` "Runtime environment" 表）。

---

## 4. 嵌入形态选项（依赖接入 + 调用面）

### 4.1 依赖接入形态（问题 A）[已核实 + 推断]

uni_claw 是 **.NET 仓、无根 package.json**（根只有 `UniClaw.Kernel.slnx` 等）。JS 侧依赖
落点候选：

| 选项 | 形态 | 证据/依据 | 取舍 [推断] |
|---|---|---|---|
| (i) npm 依赖 `@deepseek-ai/dsh`（钉版本） | 在仓库某目录放一个最小 package.json（`dependencies: {"@deepseek-ai/dsh": "0.1.1-rc.2"}`）+ pnpm-lock | `apps/cli/package.json`（CLI 即全量） | **推荐**。落点建议 `platforms/dsh/`（对齐 `platforms/perception/` 的"平台 provider 进程，Kernel C# 零依赖该树、交互只经契约"定位，`platforms/perception/README.md:2-4`）。备选 `tools/dsh/`（工具链）或产品内 JS 宿主（需新产品组件）。 |
| (ii) 复用 dk-harness checkout（file:/link:） | `dsh plugin` 直接装本地 checkout / tgz | `docs/user/develop/basic/publish.md:75-110`（本地开发合法）；uniflow README:65-67（生产禁 file:） | 仅**开发回路**可用（E2E/冒烟）；生产/CI/换机失效（机器锚定）。不得作为交付形态。 |
| (iii) python-SDK 式 bundled runtime 的 .NET 等价物 | **不存在**：`packages/sdk/{client,protocol,server}` 全是 TS；Python bundled runtime 是 Python 专属（`python-sdk.md:17`；无 .NET 绑定） | `packages/sdk/` 实测 | .NET 只能 (a) spawn CLI（headless 一次性）或 (b) spawn `node <dsh>/lib/bin.js <cordis.yml>` 的 JSON-RPC server 进程 + **自写 JSON-RPC client**（协议包 `@deepseek-ai/dsh-sdk-protocol` 与 client 包为 TS 参考；C# 侧无现成 client）。 |

### 4.2 调用面（问题 B）[已核实 + 推断]

| 形态 | Kernel(C#) 驱动方式 | 会话/日志语义 | ADR-0022 映射适配 |
|---|---|---|---|
| **headless 一次性**（推荐主轴） | `Process.Start` spawn `dsh --profile uniclaw-product "<job>"`；env 传 `DSH_HOME`（仓库内隔离目录）、`DEEPSEEK_API_KEY`；读 stdout + exit code | 每次 fresh Agent/Session（CLI reference:30）；session JSONL 落在 `$DSH_HOME/sessions/`（base patch `:101` `root: dshHomePath('sessions')`）；`turn/end` reason 在 stdout 语义外可经 session 日志取 | **一次任务 = 一次 spawn** 天然贴合 `1 Product Session / 1 Primary Run`；DSH session id 必须被 Kernel/adapter **显式映射**为 Product Run correlation（baseline §5.1 line 144），不得直接充当 Product SessionId |
| **JSON-RPC 常驻** | spawn server 进程（node），C# 侧实现 JSON-RPC client（`initialize/prompt/request/shutdown`）；`sdk-client`（TS）是行为参考，Python SDK 是"owned-run"语义参考 | session 显式 id 打开/复用（sdk-client README：复用 id 延续对话）；`DSH_SESSION_ROOT` 指定日志目录；events/notifications 可订阅 | 会话复用语义需产品侧管控（每个 Product Run 开新 session id）；事件订阅可支撑 C3/C4/C5 conformance 观测；C# client 开发量是主要成本 |
| **web host** | 不推荐作产品嵌入（除非产品要 GUI） | Host/HTTP/browser 层 | 与 baseline §4（调用者不得依赖 transport/prompt/event 命名）冲突面大 |

进程生命周期：headless 一次性自带 bounded shutdown（`apps/cli/src/profile-boot.ts:207-299`
launcher 信号/`ctx.appExit`）；JSON-RPC server 由 `shutdown` RPC / EOF / 信号退出
（`packages/sdk/server/src/index.ts`）。[已核实]

### 4.3 状态 / 凭证 / 隔离映射 [已核实 + 推断]

- settings/credentials/sessions 都锚定 `$DSH_HOME`：`settings-file` 默认
  `<harness home>/settings.yaml`（`packages/settings/settings-file/src/index.ts:51-56`）、
  credentials `$DSH_HOME/.credentials.yaml`（base patch `:82-86`）、sessions
  `dshHomePath('sessions')`（`:101`）。→ **DSH_HOME 指向仓库内目录
  （如 `platforms/dsh/home/`）即完成 Product Runtime 与用户全局 `~/.dsh` 的
  state/settings/credit 隔离**（ADR-0022 §6 的落地手段）。副作用：仓库内出现
  sessions/credentials 文件 → 需 gitignore + 凭证走 env。
- 凭证注入：[已核实] base 的 `llm-deepseek` 行 `apiKeyEnv: DEEPSEEK_API_KEY`
  （base patch `:412`）→ env 注入（`DEEPSEEK_API_KEY`）即可，无需把
  `.credentials.yaml` 搬进仓库；[推断] 若产品要用 llm-pi-ai 多 provider，需在隔离的
  DSH_HOME 下自建 `settings.yaml`（`llm-pi-ai` 段 + `apiKeyEnv`）。
- session 语义：[已核实] 一次性模式每次 fresh session（无跨任务隐式复用）——恰好满足
  baseline §5 "Development Session 与 Product Session 不得相互 resume" 与 C3
  （Host resume ≠ Product Session 恢复）的安全侧；[推断] 常驻模式需显式 session id
  生命周期管理。

---

## 5. 两层 profile 定制方案（问题 C）

### 5.1 层 1：安装 profile（DSH side）[已核实 + 推断]

**目标组合**：`uniclaw-product` profile = `[base, headless]`（无 Host/HTTP/browser 层，
headless README 首段）。创建方式：`dsh plugin --profile uniclaw-product add
@deepseek-ai/dsh-headless`（§3.3 reconcile 自动得到 base+headless）。若产品需要 UI，
`add @deepseek-ai/dsh-web-app` 并在启动时给主机地址（产品默认不选）。

**插件挂载（product allowlist）**：[已核实] DSH 的 tool 行都在 base patch（`tool-*`），
profile 的 `cordis.patch.yml` 可以用 `disabled: true` / 整体替换 config 裁剪工具面
（§3.2 patch 语法；`apps/cli/reference/README.md:9` "a patch replaces the targeted row's
complete config"）。因此：
- **挂**：product 专用插件（Conformance Surface adapter —— 把 DSH session/turn 事件
  翻译为 Product records 的 adapter，见 §5.2），以及（若采用）uniflow 类强制插件；
- **不挂**：uniflow-agent-loop（开发 Harness 面的插件）、mcp-manager、
  provider-usage/task-notify 客户端插件、以及默认工具行中与 Product 无关的
  coding 工具（baseline §6 line 178 "Product Runtime 不得默认暴露 coding tools"）。

[推断] 允许集 = base 必要面（llm/session/persistence/credential/subagent 内核 →
headless runner 需要）+ 产品 adapter 插件；禁用面 = 全部 `tool-bash`/`tool-fs`/
`tool-subagent`/`tool-workflow` 等 coding 工具（disabled 行），把"模型能用什么"
压缩到产品授权集。注意 [已核实] `headless-runner` 行本身也有 `inject: [headlessStartup]`
（headless patch `:31-35`），产品 adapter 可同理以服务注入代替工具暴露。

**版本策略**：[已核实] npm 版本包钉住（`apps/cli/package.json` 版本单一来源；`dsh
plugin` reconcile 驱动），profile 的 `pnpm-lock.yaml` 入库存证；禁 `file:`（§3.4）。
本地开发用 `--patch` overlay 挂未发布插件，不进 profile 依赖。

### 5.2 层 2：适配 profile（仓库侧绑定 + Conformance adapter）[已核实 + 推断]

| 面 | 落点 | 依据 | 说明 |
|---|---|---|---|
| Product tier→provider/model 绑定 | 建议 `platforms/dsh/bindings.yaml`（或 `.dsh/product-bindings.yaml`），结构同构 `.dsh/model-bindings.yaml`（`primary/fallback`），tier 名对齐 `model-routing.yaml` | MRB-001 的形状（`.dsh/model-bindings.yaml` 全文）；共享层禁 provider/model 名约束（`changes/MRB-001/state.md:43`） | **[推断]** 复制形状、独立文件：Product 绑定是 Product 侧事实，与 Dev Harness 的 model-bindings 互不干扰；校验可复用 `tools/validate-model-bindings.py`（改参或加 `--bindings` 变体） |
| 角色绑定（decision_frontier 等） | 仅当启用插件强制闭环时：profile 行 `config.profileSource` 指向 Product 侧角色绑定文件 | `dsh-plugins/uniflow-agent-loop/src/binding.js:32-44`（机器块契约） | **[推断]** 产品基线（headless 一次性 + SKILL/workflow agent() 覆盖路径）**不需要** profile-source.yaml；插件强制闭环是可选增强，另出决议 |
| Conformance Surface adapter | 产品 profile 内挂载的一个**产品专用插件**（cordis 行 + `dsh.client` 可选），职责 = DSH session/turn 事件 → Product record 投影 / DSH session id → Product Run correlation 映射／fail-closed 翻译 | baseline §2/§4/§5 | **[推断]** 这是"DSH Profile/plugin/package 切分"在实现期的第一个授权项（当前未授权，§1 边界）；形态待 R1 决议 |

**[已核实] 约束保持成立的方式**：
- 「不建第二套 task system」（MRB-001 state.md:11-12）：Product 侧不引入 dispatch
  record / events.jsonl / session-run 状态目录；映射只做**投影**（baseline §5 line 159
  "语义 Owner 与物理存储分离"），Kernel 仍是唯一 task authority；
- 「共享层不出 provider/model 名」（MRB-001 state.md:43；SKILL §B4 line 165-166）：
  Product 绑定文件在 `platforms/dsh/`（产品组件目录），**不进** `model-routing.yaml` /
  `.agents/skills/` / `schemas/`；共享层维持 capability→tier 中立；
- baseline §6 隔离：Product profile 的 instruction 由 product 插件/prompt 提供，
  `AGENTS.md`/skills 不得进（line 175）。

---

## 6. 隔离与安全（问题 D）[已核实 + 推断]

1. **DSH_HOME 指向仓库内目录的副作用**（[推断]，基于 §4.3 已核实锚点）：`sessions/`
   `settings.yaml` `.credentials.yaml` `logs/` `mcp-servers.json` `.agent-presets/`
   全部与用户全局 `~/.dsh` 分离 → 互不污染；需要 `.gitignore`（sessions/logs/credentials
   + `.env`），或只映射到非版本库路径（如 `platforms/dsh/home/` gitignore）。
2. **凭证**：优先 env（`DEEPSEEK_API_KEY`，base patch:412 已核实）；llm-pi-ai 多 provider
   在隔离 Homing 的 settings.yaml 配 `apiKeyEnv`。[] 仓库内不落明文凭证。
3. **fail-closed 对照**（已有 FROZEN 语义 vs 一次性模式，均为[已核实]事实 + [推断]映射）：
   - C2（Contract 非法 fail closed，baseline line 226）：headless 没有 Product contract
     概念 → Product 侧由 Kernel 先做 Contract admission（P1），非法即不 spawn；
     [推断] spawn 前 gate 是 adapter 职责；
   - C7（required provider 缺失，line 231）：[已核实] DSH `llm.listProviders()` 缺失 →
     请求抛错 / headless 退出非 0（`apps/cli/reference/README.md:9` fail loud）→ 可观察
     fail-closed 结果；
   - C8（approval 拒绝/取消，line 232）：[已核实] headless 一次性**无 approval UI**
     （base approval 服务在但 surface 无交互）→ [推断] 产品侧 approval 语义经 adapter
     在 Kernel 侧实现，DSH 侧以进程退出/无输出呈现（具体 lifecycle vocabulary deferred，
     baseline line 232 原话）。
4. **权限面**：新 session 默认 `workspace-write`（`apps/cli/reference/README.md:83`）；
   [推断] 产品 profile 可用 patch 把 sandbox row 收紧或放宽 —— 属 R1 决议项，本研究
   不选型。

---

## 7. 验证与验收（问题 E）

验证以 uni_claw 验收四元组习惯（method/expected/actual/evidence，MRB-001
`changes/MRB-001/state.md:46-51` 的 Verification 形态）组织：

| Method | Expected | Actual（判据） | Evidence |
|---|---|---|---|
| spawn 冒烟（真实凭证） | `dsh --profile uniclaw-product "<job>"` exit 0 且 stdout 有非空答案、`turn/end completed` | 一次真实模型调用完成 | 进程日志 + `$DSH_HOME/sessions/` 下新 session JSONL（zstd 解压核对 `agent/request` header） |
| `dsh --profile uniclaw-product --dump-config` | 组合树含 base+headless 行、product adapter 行、禁用的 coding tool 行（disabled） | dump 输出核对 | dump 文本 |
| profile 依赖完整性 | `pnpm --dir <home>/profiles/uniclaw-product install --frozen-lockfile` 0；`dsh plugin --profile uniclaw-product why <pkg>` 显示版本 | 无悬空/缺包 | pnpm 输出 |
| 绑定一致性 | Product 绑定文件 tier 全覆盖、provider/model 合法、共享层无泄漏 | 校验脚本 exit 0 + 泄漏 grep 无命中 | 复用/扩展 `tools/validate-model-bindings.py` |
| 隔离验证 | 以仓库内 DSH_HOME 运行时，`~/.dsh/sessions|settings` 无新增文件 | home 目录 mtime/列表不变 | 前后 diff |
| conformance 面 | Product Run id ↔ DSH session id 映射记录显式可恢复、无静默别名（C3/C4/C11 语义） | adapter 映射日志 | C# 侧 adapter 单测 + 集成 |

[推断] E2E 的 uniflow-e2e-driver（`dsh-plugins/uniflow-agent-loop/test/e2e/run-e2e.sh:12-13,21,58`
硬编码 uni-agent 工具与 `.ai/` 注册表）**不适用于产品嵌入验证**——产品验证以本表为准，
开发侧 E2E 属另一条线。

---

## 8. 推荐组合与理由 [推断/取舍]

1. **主轴**：`platforms/dsh/` 持有最小 JS 宿主（`package.json` 钉
   `@deepseek-ai/dsh@0.1.1-rc.2` + lock）→ `dsh plugin --profile uniclaw-product add
   @deepseek-ai/dsh-headless` 创建 Product profile → Kernel(C#) 每次任务 spawn
   `dsh --profile uniclaw-product "<job>"`（`DSH_HOME` 指向仓库内隔离目录、凭证 env）。
   理由：零 .NET SDK 需求、residual 面最小、每次 fresh session 天然满足
   `1 Session/1 Goal/1 Run` 与 resume 禁止、DSH_HOME 隔离一次到位。
2. **适配层**：`platforms/dsh/` 下放 Product 绑定文件（同构
   model-bindings.yaml）+ Conformance adapter（C# 侧翻译 + 映射，R1 授权项）。
3. **第二选项（常驻）**：JSON-RPC server 形态（`examples/jsonrpc-agent` 姿势 + C# client）
   在需要持续会话/事件订阅的 conformance 观测时启用；不默认选。
4. **明确不选**：web-app 进产品 profile；开发 Harness 插件（uniflow-agent-loop /
   mcp-manager 等）进产品 profile；`file:`/checkout 依赖作交付形态；
   profile-source.yaml/插件强制闭环（除非另出决议）。
5. **风险**：headless 一次性的失败语义（exit 0/1 + stdout 文本）到 Product Outcome 的
   翻译完全落在 adapter 上（C2/C7/C8 的 fail-closed 责任）；版本漂移（web 1.1.0 vs
   headless 1.0.0 已存在）会传染到产品 profile——统一钉 1.1.0；DSH 私有插件发布链路
   未解决前，产品 profile 不应依赖任何本地 tgz。

---

## 9. 未决问题与「未获实现授权」边界

**未决**：
1. Conformance adapter 的形态与宿主（C# 进程内翻译 vs Product profile 内 cordis 插件
   vs 兼有）——baseline §4 允许 adapter 在 seam 内侧存在，但具体 transport/插件切分是
   §9 Deferred ⑳（line 245），需 R1 决议；
2. Product 绑定文件与 Dev Harness `.dsh/model-bindings.yaml` 的关系（独立文件 vs
   同一文件两段）——本报告倾向独立文件（§5.2）；
3. 插件强制闭环（envelope/receipt）是否引入产品侧（MRB-001「第二套 task system」禁令
   与 baseline §2「Realization 不是 Host 平行 Authority」的交叉裁决，line 86-91）；
4. DSH_HOME 隔离目录的位置与 gitignore 策略；llm-pi-ai 多 provider 的 settings.yaml
   是否随产品 profile 交付；
5. 权限/sandbox 面（workspace-write 默认 vs 产品收紧）与 C8 approval 语义的产品化。

**边界声明（权威，非推断）**：
- `docs/architecture/uniagent-realization-baseline-v0.1.md:237-247`（§9）与
  `docs/adr/0022-codex-and-dsh-are-dual-full-uniagent-realizations.md`（Decision 5）：
  DSH Profile/plugin/package 切分、Kernel Bridge transport、Tracer Bullet、产品实现
  **未获授权**；「DSH = Product」不得推断为 production-ready（baseline line 249-250）。
- 本研究只产出可验证的选项评估与 scoping 事实，不构成对 R1 或任何实现的授权；
  进入实现需独立 change 走 uniflow 主干（AGENTS.md §2：计划/证据在 `plans/`·`evidence/`）。

---

## 附录：一手来源索引

**DSH 发布与 CLI**：`apps/cli/package.json`（bin/deps）、`README.md:20`（npx 安装）、
`apps/cli/reference/README.md`（profile boot/plugin/headless/权限/热重载）、
`apps/cli/src/{args,bin,profile-boot,plugin}.ts`。

**Profile 机制**：`packages/boot/app-boot/src/profile.ts`（bundle 双锚点
`:344-355`、模板 `:113-117`、fallback `:223-255`、initProfile `:152-168`、
loadProfile `:371-403`）、`packages/boot/app-boot/src/index.ts:278-335`（patch 解析）、
`vendor/include/src/index.ts:78-151`（patch 语法）、
`packages/util/home-paths/src/index.ts`（DSH_HOME 解析）、
`docs/user/develop/basic/{config,publish}.md`、
`packages/bundle/{base,web-app,headless}/{package.json,cordis.patch.yml,README.md}`、
`packages/settings/settings-file/src/index.ts:51-56`（settings 锚定 home）。

**嵌入式运行**：`apps/cli/reference/README.md:30`（headless 一次性）、
`packages/bundle/headless/README.md:7`、`examples/{headless-agent,jsonrpc-agent}/README.md`
与 `examples/jsonrpc-agent/{cordis.yml,minimal.cordis.yml}`、
`packages/sdk/server/src/index.ts`（sdk-jsonrpc-server）、
`packages/sdk/client/README.md`（TS client、spawn `lib/bin.js cordis.yml`）、
`docs/user/guide/python-sdk.md:17`（bundled runtime 为 Python 专属）。

**uni_claw 产品侧**：`docs/architecture/uniagent-realization-baseline-v0.1.md`（全文 264 行；
§2 Contract、§5 映射、§6 隔离、§8.4 C1-C11、§9 Deferred）、
`docs/adr/0022-codex-and-dsh-are-dual-full-uniagent-realizations.md`、
`platforms/perception/README.md`（平台 provider 进程先例）、
`changes/MRB-001/state.md`、`model-routing.yaml`、`.dsh/model-bindings.yaml`、
`.agents/skills/uniflow/SKILL.md`（B3-B5）、`schemas/work-item.schema.json`、
`tools/validate-model-bindings.py`。

**开发侧现状**：`~/.dsh/profiles/{web,headless}/package.json`、
`~/.dsh/profiles/web/cordis.patch.yml`、
`~/.dsh/profiles/web/node_modules/{.modules.yaml,@deepseek-ai,@user}`、
`~/.dsh/settings.yaml`、`~/.dsh/sessions/…`（zstd 取证）、`lsof/curl :3080` 实测。

**插件契约**：`dsh-plugins/uniflow-agent-loop/src/{index,binding,provider,gates,registry,
config-service,client}.js`、`README.md`、`CHANGELOG.md`、
`test/e2e/run-e2e.sh`、`dsh-plugins/docs/maintenance.md`。