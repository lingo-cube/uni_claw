# HOST-002 证据 — Host/Host.Dsh README + Tool 两层概念关系

## A1/A2 — 两个 README

- `src/UniClaw.Host/README.md`：组合根定位（HOST-001 先例）；根 10 文件
  + Capability/（2）+ Runtime/（1）+ SettingsCoverage/（6）归属表；变更
  规则（业务语义回 Kernel、宿主特有进 Host.Dsh）。
- `src/UniClaw.Host.Dsh/README.md`：DSH realization adapter 定位
  （ADR-0022）；Program/RuntimeHttpServer 归属；**Tool 两层概念节**
  （ADR-0039 权威表述：Harness Tool ≠ 产品 tools；capabilityRef 单向
  映射；skillRef 产品态解析注入落点；产品代码不硬编码 harness skill
  路径）。

## A3 — UniAgent README tools 节

补两层概念澄清子弹（指向 ADR-0039，不复制其权威内容）。

## A4 — 覆盖核对

```sh
# Host(16) + Host.Dsh(2) + Agent(8) ↔ 对应 README 点名
# actual: coverage: 22/22（见 coverage-check.txt；Agent 8/8 含于其中口径）
```

## 文件

- `coverage-check.txt` — 22/22 覆盖输出。
