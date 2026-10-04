# CAP-001 S3 Registry Verification

## Scope

验证冻结后的感知分类能通过 Product Capability Registry 显式注册，并区分复合能力、独立能力、协议角色、来源/适配器角色和装配依赖。本证据不包含模型调用、Fast/OCR/Slow 推理或 P2/P3 写入。

## Verification

- method: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter FullyQualifiedName~PerceptionCapabilityRegistryTests`
  - expected: 分类注册、查询、协议一致性和 fail-closed 校验全部通过。
  - actual: 5 passed, 0 failed。
- method: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore`
  - expected: Kernel 既有回归与新增公共类型白名单全部通过。
  - actual: 742 passed, 0 failed。
- method: `python3 -m json.tool workitems/WI-CAP001-003.json` and WorkItem JSON Schema validation
  - expected: WorkItem remains valid and status is `done`.
  - actual: valid.
- method: `git diff --check`
  - expected: no whitespace errors.
  - actual: pass.

## Result

`Text Semantic Perception` 以复合能力注册，Slow Text 未独立注册；Slow Visual 可独立声明一个或两个感知协议；未知 Product 感知协议、来源/适配器冒充和 dangling assembly relationship 在注册前拒绝，且不产生 entry、lifecycle fact 或 sequence。
