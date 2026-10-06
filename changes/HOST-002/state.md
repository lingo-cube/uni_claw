# HOST-002 — Host 组合根与 Host.Dsh adapter 的维度 README + Tool 两层概念关系说明
lifecycle_state: closed · disposition: none · depth: standard · base: ca0274c3

## Intent（WHAT/WHY）

ARCH-DOC-020/AGT-018 补齐 Kernel 与 Agent 维度后，`src/UniClaw.Host/`
（组合根）与 `src/UniClaw.Host.Dsh/`（DSH realization adapter）仍无
README。同时 AGENTS.md 新入 `tool-registry.yaml`（ADR-0039，Harness 工具
暴露清单），与 UniAgent「自己定义的 tools」（AGT-018）是两层概念，
需要在文档里说清关系，避免混淆。所有者指令「直接干」。

## Scope / Out of Scope

In：`src/UniClaw.Host/README.md`、`src/UniClaw.Host.Dsh/README.md`（均新
增）；`src/UniClaw.Agent/README.md` tools 节补两层概念澄清段。
Out：不改代码；不改 tool-registry.yaml / ADR-0039 / 基线；不改其他
维度 README。

## Decisions

- D1：Host README 定位 = **组合根（Composition Root）**：装配 Kernel 六
  L2 与 Capability Plane、注册能力实例、接线观察 feed——不含业务语义
  （HOST-001 spec 先例；ADR-0035 三域组合根分立）。
- D2：Host.Dsh README 定位 = **DSH realization adapter**（ADR-0022 dual
  full realizations 之一）：程序入口、runtime-http 面、DSH 模型经
  Agent.Dsh 注入；为 ADR-0039 产品态 skillRef 解析注入的 adapter 落点。
- D3：Tool 两层概念关系按 ADR-0039 权威表述：产品层调用词汇（UniAgent
  自定义 tools）≠ Harness 层 Tool（tool-registry.yaml，harness 消费面
  暴露清单）；capability-backed 经 capabilityRef 单向映射，语义真相在
  CapabilityHub；产品代码不硬编码 harness skill 路径。

## Acceptance

| # | 判据 |
|---|---|
| A1 | Host README：组合根定位 + 根 10 文件 + 3 子目录归属表 + 变更规则 |
| A2 | Host.Dsh README：adapter 定位 + 2 文件归属 + skillRef/两层概念节 |
| A3 | UniAgent README tools 节含两层概念澄清（指向 ADR-0039） |
| A4 | 覆盖核对：Host 16 文件 + Host.Dsh 2 文件全点名 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "覆盖核对脚本（Host/Host.Dsh/Agent ↔ README 点名）"
  expected: "18/18 + Agent 8/8 全覆盖"
  actual: "覆盖 22/22（Host 16 + Host.Dsh 2 + Agent 8）"
  evidence: "evidence/host-002/"
```
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者「直接干」授权；
  HOST-002 开立。
- 2026-10-07 · PERSIST → IMPLEMENT → VERIFY → CLOSED · 两个 README +
  UniAgent tools 节澄清落地；覆盖 22/22；INDEX 已再生。
