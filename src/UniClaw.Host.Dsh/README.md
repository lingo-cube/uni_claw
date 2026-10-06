# UniClaw.Host.Dsh 代码索引 — DSH Realization Adapter

UniClaw 的 **DSH realization**（ADR-0022：codex 与 DSH 是 dual full
UniAgent/Host realizations）：程序入口、runtime-http 暴露面、DSH 模型
binding 注入。语义权威在 Kernel/Agent，本目录只做宿主映射——替换成
其他宿主 realization 时本目录整体可换，核心零改动。

## 代码归属

| 文件 | 职责 |
|---|---|
| `Program.cs` | DSH Host 入口：配置装载、ModelManagement 组合（经 `UniClaw.Agent.Dsh` 的 DshModelManagement/UniagentProdYaml）、HostRunner 装配 |
| `RuntimeHttpServer.cs` | runtime-http 组合根（RuntimeRunStore 投影消费；模型 resolve 同经产品缝） |

`evidence/`、`runs/` 为运行产物目录，非源码。

## Tool 的两层概念（ADR-0039）

- **Harness 层 Tool**：`tool-registry.yaml`（仓库根）是 Development
  Harness 信任域的工具暴露清单（harness 消费者：AI Coder / Workbench /
  前端模型调用）；调用形态（CLI/MCP/HTTP/skill procedure）是
  implementation detail；
- **产品层调用词汇**：UniAgent 自己定义的 tools（见
  `../UniClaw.Agent/README.md`）属产品语义，不经本清单注册；
- **单向映射**：Tool 可由 Capability 映射而来（`backing: capability` +
  `capabilityRef`），只持暴露元数据，语义真相在 CapabilityHub；
- **skill 单源双路径**：研发态经 skill catalog 直载；产品态经
  `skillRef` 由本 adapter 在调用时解析注入——**产品代码不得硬编码
  harness skill 路径**；
- 校验器：`python3 tools/validate-tool-registry.py`。

## 变更规则

DSH 特有装配/transport/模型解析进本目录（或 `../UniClaw.Agent.Dsh/`）；
能力语义变化回 Kernel；新增 harness Tool 暴露 = 改 `tool-registry.yaml`
+ 过校验器，不动产品代码。

## 指向

- ADR-0022（dual realizations）、ADR-0031（host-neutral core）、
  ADR-0039（Tool/Capability 边界）；
- 本地联调环境：`docs/agents/test-emulator.md`；
- 组合根：`../UniClaw.Host/README.md`。
