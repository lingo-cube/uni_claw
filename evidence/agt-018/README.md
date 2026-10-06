# AGT-018 证据 — UniAgent README + Memory System 声明

## A1 — UniAgent README

`src/UniClaw.Agent/README.md`：定位=**系统的大脑**（含基线 §3.1 措辞
偏差注记）、tools（ADR-0038 挂靠）、realization 可替换（ADR-0022 挂靠，
Agent.Dsh 不进归属表）、8 文件归属表、四不边界、与 Kernel 两条缝。

## A2 — Memory System 声明

`docs/design/memory-system-component-declaration-v0.1.md`（CANDIDATE /
Authority: NONE）：给 UniAgent 提供记忆、管理常用规则/知识；DSH 组件
映射路径；零落地状态与升格条件。已入 `docs/design/README.md` 索引。

## A3 — design 索引孤儿清理

capability 协议指南行状态 `DRAFT / GUIDE` → `FROZEN / PROTOCOL GUIDE
v0.1（CAP-010 升格）`（CAP-010 升格时未同步索引的遗留）。

## A4 — 验证

```sh
# 覆盖核对
Agent .cs: 8, 未提及: [] → coverage: 8/8（见 coverage-check.txt）
# DocsMetadata（design/ 新文档体例 + 索引同步）
已通过! 失败: 0，通过: 4（见 docsmetadata.txt）
```

## 文件

- `coverage-check.txt` — Agent 8/8 覆盖。
- `docsmetadata.txt` — DocsMetadataTests 4/4。
