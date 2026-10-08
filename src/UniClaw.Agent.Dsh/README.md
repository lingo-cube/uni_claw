# UniClaw.Agent.Dsh 代码索引 — UniAgent 的 DSH Realization Adapter

UniAgent（L1 · 系统的大脑）的 **DSH realization**（ADR-0022：codex 与
DSH 是 dual full realizations）：模型 binding 注入、决策通道传输、
协议编解码。语义权威在 `../UniClaw.Agent/`（产品语义层）与 Kernel；
本目录只做宿主映射——**替换 realization 整体可换，产品层零改动**
（AGT-018 定义的维度边界：L3 不进 UniAgent 归属表）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `DshModelManagement.cs` | Model Management 缺省（借用 DSH）realization：产品声明+DSH 绑定双输入 → ModelManagement 快照注入（CAP-006；PRF-002） |
| `UniagentDshBindings.cs` | DSH realization 绑定加载结果（CAP-007 modelSelection+service；PRF-002） |
| `UniagentDshBindingsYaml.cs` | DSH 绑定 loader（`.dsh/product/uniagent-prod-bindings.yaml`；PRF-002 接替 UniagentProdYaml 的绑定半部，产品半部在 `../UniClaw.Agent/Profile/`） |
| `DecisionChannel.cs` | 决策通道的 DSH 产品语义缝（不含宿主协议细节） |
| `DshAgentAdapter.cs` | DSH realization 的产品侧 supervisor（只拥有 realization 生命周期） |
| `DshSlowConsult.cs` | 同步 Consult 与异步流水（UniPerception）共用的 slow 咨询 realization |
| `Handshake.cs` | 握手词汇（profileId / profileVersion / capabilityManifestHash） |
| `Observer.cs` | 观察投影（F3：产品真值是 canonical owner 记录） |
| `DshOpenedHttpPeer.cs` | 正式认证的本地 web peer（AGT-002 E2E 切片单一入口） |
| `DshWebCredential.cs` | 本地 DSH web 服务传输凭证 |
| `ProtocolModels.cs` | DSH 会话状态与协议模型 |
| `ProtocolSchema.cs` | 协议 schema 校验（runtime build 时记录；JSON Schema 副本不作权威） |
| `DecisionJsonConverters.cs` | Kernel record ↔ 传输 JSON 转换（Kernel records 是权威） |

## 变更规则

DSH 特有装配/transport/模型解析进本目录；goal/评估/决策语义回
`../UniClaw.Agent/`；产品语义变化回 Kernel。能力注册经 Host 组合根
（`../UniClaw.Host/Capability/`），本目录不自建注册面。

## 指向

- ADR-0022（dual realizations）、ADR-0031（host-neutral core）；
- 产品层：`../UniClaw.Agent/README.md`；组合根：`../UniClaw.Host/README.md`；
- 本地联调：`docs/agents/test-emulator.md`。
