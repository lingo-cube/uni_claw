# LIVE Demo — DSH → UniClaw Control Plane

> 一条命令把冻结的 DSH↔UniClaw 控制平面链路**真实跑起来给你看**。
> 不是截图、不是 mock：真实 pinned DSH host + 真实插件 + 按冻结协议响应的
> DriverHost 场景，五条命令全部通过真实 command registry 执行。

```bash
node demo/demo-control-plane.mjs
```

## 链路

```
DSH command registry (真实, pinned 47f94385 / 0.1.0-rc.5)
  → dsh-plugin-uniclaw (真实插件, inject: [commands])
  → UniClawAdapter (loopback TCP JSON-RPC)
  → DriverHost fixture (按冻结基线 DTO 响应, 只读 8 方法)
```

- DriverHost 端是 **wire-conformant stand-in**（因为真实 DriverHost 需要 Kernel
  运行状态；真实跨进程 DriverHost 行为由 `DriverHostPluginE2ETests` 单独保护）。
- DSH 端 100% 真实：`boot()` from `@deepseek-ai/dsh-app-boot`、真实 vendored
  cordis 4.0.1、真实 `@deepseek-ai/dsh-commands` registry、真实
  `@deepseek-ai/dsh-llm`（shadow 命令的 `ctx.llm` seam 走真实 LlmRuntime）。
- pinned DSH checkout 只读：启动前校验 HEAD + porcelain，从不写入。

## 演示内容（场景：WiFi settings run）

| 命令 | 展示 |
|---|---|
| `/uniclaw-runs-list` | 注册的 run id |
| `/uniclaw-inspect-run <runId>` | 分类只读快照（runState / semantic page / goal / bindings / beliefs） |
| `/uniclaw-inspect-trap <runId>` | 分类活动 trap（StateMismatch, expected vs observed） |
| `/uniclaw-evidence-open <locator> <runId>` | 逻辑 evidence ref（仅元数据） |
| `/uniclaw-shadow-analyze <runId> --focus trap` | Shadow Cognition：COGNITIVE_INFERENCE、kernel-fact vs shadow-inference 标签、一次 `ctx.llm` 调用 |

## 预期输出（节选）

```
$ /uniclaw-runs-list
run-wifi-settings-001
  → kind: success

$ /uniclaw-shadow-analyze run-wifi-settings-001 --focus trap
shadow analysis: shadow-run-wifi-settings-001-1
classification: COGNITIVE_INFERENCE
model call: success (demo-shadow/demo-1, 2 events, 2096 chars)
observedFacts:
  [derived-read-model] currentGoal: "WifiConnectivity.Enabled=true" ...
  [kernel-fact] TrapRaised @seq 3 (RuntimeEvent: evt-trap-1)
hypotheses:
  [shadow-inference] the first tap may have missed the switch target (uncertain)
recommendations:
  [human-investigation] inspect the switch state on the device
```

## 收尾自检（demo 结束自动打印）

- wire methods actually requested: 全部落在冻结只读 8 方法内
- model calls: 1（shadow 命令恰好一次 `ctx.llm`）
- session events written: 只有 `command/run` + `command/done`（零自定义事件）

## 环境

- pinned DSH: `/Users/fran/Documents/Code/dk-harness` @ `47f943859bef60e4160492346772ded9b24f765a`
- 可用 `DSH_PINNED_REPO=<path>` 覆盖

---

## 浏览器端可视化（可选，3081 实例已启用）

命令默认只返回纯文本；挂一个 **client 插件** 后，`uniclaw-*` 命令在 DSH Web GUI
里渲染成结构化卡片（分类色标徽章、expected/observed 对比、Shadow 分区）。

```
dsh-plugin-uniclaw/client/          ← client 插件包（dsh.client 声明 + 手写 bundle）
  package.json                      ← exports["./client"] + dsh.client.platform: web
  lib/client.js                     ← closure-factory bundle：注册 5 个 keyed commandview 卡片
  lib/index.js / index.js           ← host 侧空入口（loader 需要）
```

挂载方式：在 profile 的 `cordis.patch.yml` 里加一行 loader entry 指向
`dsh-plugin-uniclaw/client`（绝对路径），重启 web 实例后 modules 行自动扫描、
serve `/plugins/.../client.js`、注入 boot manifest。手写 bundle 的要点：

- `__ModuleLoader__.load({ id: <entry 绝对路径>, factory })` —— id 必须等于
  loader entry 的 name（绝对路径），否则报 `loaded without registering`；
- 插件导出 `{ inject: ['slots'], apply }` —— apply 里 `ctx.slots.register`
  keyed `conversation.chat.commandview`，key = 命令名；
- React 组件用 `createElement` 手写（零构建链），依赖从 client 模块表 require
  （react / dsh-client-ui-primitives 均为 shell 自带模块）。
