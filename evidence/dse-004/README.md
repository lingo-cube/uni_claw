# DSE-004 证据 — Effect Driver R5 可替换性执法

## A1/A2/A3 — 互换测试

`tests/UniClaw.Kernel.Tests/Effects/EffectDriverReplaceabilityTests.cs`（3 例）：

- 消费闭包只依赖 EffectBoundary 公开面（Dispatch/ReceiptLog）；
- 两个结构不同的 driver double：无状态脚本型 vs 有状态首败后成型；
- 可互换律：gate 不受 driver 身份影响 / receipt 忠实三态映射 /
  每次 Dispatch 恰一次 Deliver（无代驾 retry，不变量 27）/ 留痕唯一；
- 互换测试：同一闭包委托依次消费两 driver，代码零改动。

```sh
dotnet test tests/UniClaw.Kernel.Tests --filter EffectDriverReplaceability
# actual: 已通过! 3/3（targeted.txt）
```

## A4 — 回归

```sh
dotnet test tests/UniClaw.Kernel.Tests
# actual: 已通过! 849/849（kernel-tests.txt）
```

期间插曲：全量首跑 SourceReadmeOwnership RED——并行会话新落
`RuntimeToolHost` 后又新增 `RuntimeToolConfig.cs`（PNL-010）未登记；
**ARCH-DOC-022 执法上线后首次实战拦截**，已按头注释登记后复绿。

## 文件

- `targeted.txt` — 3/3。
- `kernel-tests.txt` — 全量 849/849。
