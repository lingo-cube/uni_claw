# ARCH-DOC-019 — Reconcile RUN-004 historical review and current fact

lifecycle_state: closed · disposition: none · depth: standard · base: 0c6f6673

## Intent（WHAT/WHY）

`changes/RUN-004/spec.md` 的 §11 仍把 2026-09-23、旧 HEAD `b4b6865c` 的评审快照
写成当前事实；同一文件 §12 与 `changes/RUN-004/state.md` 已记录后续 Gate 5-7
收口。两层事实没有明确分界，维护者会同时看到已闭合与仍 OPEN 的冲突结论。

## Scope / Out of Scope

- Scope：标明 §11 的历史基线；更新 RUN-004 规格头部的事实基线指向；补充当前
  代码/测试复核记录；保持 §12 的 current-fact 内容可追溯。
- Out of Scope：不改变 RUN-004 协议、产品代码、测试行为、G4 DEFERRED 决定或
  任何仍需独立 Change 的实现范围。

## Decisions

1. §11 保留为不可删除的历史审阅证据，但不得再使用“当前事实”措辞。
2. §12 作为 RUN-004 当前事实唯一入口；当前代码基线记录为 `0c6f6673`。
3. 当前复核只引用可重跑的全量 solution 命令和 RUN-004 专项验收，不把历史
   评审表中的 OPEN 标记重新解释为当前开放 Change。

## Acceptance

1. RUN-004 spec 明确区分历史 §11 与 current-fact §12。
2. spec 头部不再把旧 HEAD `17fc7c16` 当作未标注的当前基线。
3. RUN-004 state 记录本次文档复核与当前验收证据。
4. 只修改文档和 Change State；产品源码、测试代码和运行时行为不变。

## Verification

```yaml
level: DETERMINISTIC
method: >-
  dotnet test UniClaw.Kernel.slnx --no-restore --nologo --verbosity minimal;
  dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-build
  --no-restore --nologo --verbosity minimal
  --filter FullyQualifiedName~KernelRunDriverAcceptanceV03RedTests;
  rg -n "历史评审|current-fact|0c6f6673" changes/RUN-004/spec.md
  changes/RUN-004/state.md; git diff --check
expected: >-
  全量 solution 退出码 0；RUN-004 专项测试 7/7；规格中的历史/当前事实边界
  清晰可检索；差异检查通过。
actual: >-
  全量 solution 退出码 0；RUN-004 专项测试 7/7 通过；§11 已标为历史快照，§12
  保持 current-fact，当前代码基线记录为 0c6f6673；git diff --check 通过。
evidence: 命令输出；changes/RUN-004/spec.md；changes/RUN-004/state.md
```

## Status log

- 2026-10-04 · UNDERSTAND → RESOLVE · 复核发现 RUN-004 state 已闭合而 spec §11 仍引用旧 HEAD 并保留 OPEN 结论；当前 §12 与代码/测试显示后续收口已完成。
- 2026-10-04 · RESOLVE → PERSIST → IMPLEMENT · 建立 ARCH-DOC-019，划分历史评审快照与 current-fact 入口，未修改产品或测试代码。
- 2026-10-04 · IMPLEMENT → VERIFY → CLOSED · 全量 solution 退出码 0，RUN-004 专项验收 7/7，文档边界和差异检查通过。
