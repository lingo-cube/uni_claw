# ARCH-DOC-021 证据 — 基线 §3.1 定位措辞修订（Narrow Amendment v0.1.2）

## diff 范围（A1/A2/A3）

仅三处：

1. §3.1 Definition 首句：「面向用户的完整智能主体」→「整个系统的大脑
   （cognition core）——系统级认知与决策中枢」；职责句原文不变；
2. 尾部新增 §25 Narrow Amendment v0.1.2（依据：所有者裁决 2026-10-07
   + AGT-018；性质：非结构性，Owner/Authority/Boundary 零变化，不重开
   §23）；
3. `src/UniClaw.Agent/README.md` 已知偏差节 → 已收口（指向基线 §25）。

## A4 — 验证

```sh
dotnet test tests/UniClaw.Kernel.Tests --filter DocsMetadata
# actual: 已通过! 4/4（见 docsmetadata.txt）
python3 tools/check-invariant-matrix.py
# actual: INVARIANT-MATRIX CHECK: PASS (47 rows = 47 invariants)
```

## 文件

- `docsmetadata.txt` — DocsMetadataTests 4/4。
