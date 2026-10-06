# PER-020 证据 — Perception README + 冻结基线 v0.2

## A1/A2 — README 与基线图

- 新增 `src/UniClaw.Kernel/Perception/README.md`（双协议为纲：§1 身份
  ISemanticPerception/IUiElementPerception → §2 UniPerception union →
  §3 快慢降级为异步策略（保留概念）→ §4-§5 归属 → §6 方向 → §7 指向）。
- 基线 §1 组成图 +14 行（10 根文件 + Fusion/UiHierarchy 子目录行），
  覆盖核对见 coverage-check.txt（31/31）。

## A3 — 契约零变化

`git diff docs/architecture/perception-provider-baseline-v0.1.md` 仅 4 个
hunk：头部 v0.2 / 修订注记 / §1 图 / 概念澄清段；§2 数据流、§3 契约及
之后零 diff（hunk 清单见 docsmetadata.txt 运行前后 diff 记录）。

## A4 — 执法测试

```sh
dotnet test tests/UniClaw.Kernel.Tests --filter "FullyQualifiedName~DocsMetadata"
# expected: 通过
# actual:   已通过! 4/4（2026-10-07）
```

## 文件

- `coverage-check.txt` — README↔目录 31/31 一致性核对输出。
- `docsmetadata.txt` — DocsMetadataTests 4/4 输出。
