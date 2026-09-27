# PER-016 — Remove Legacy State Surface

版本：v0.1（implementation change）

```text
Leader: GPT-6 Sol
Worker: GPT-6 Luna
Base: PER-015 closed
```

## Intent（WHAT/WHY）

PER-015 已用真实 API 35 设备证明 typed checked 链路完成、无 typed→legacy rollback、无 effect-critical legacy buyer。删除生产侧 legacy state projection、egress routing、writer 与 SharedSubjects.State surface，保留历史文档、回放/仿真 fixture 语义和可追溯测试材料。

## Scope

- 删除 Product Runtime 中 `LegacyStateProjection`、`LegacyStateEgress`、`SharedSubjects.State`、`MapTargetStateClaim` 与 legacy XML/state routing。
- Host 仅消费 typed hierarchy evidence；未知/partial 继续 fail-closed。
- 历史/仿真 fixture 中的 `switch.state` 文本保留为 replay input，不再进入生产组合根。
- 增加 production surface tripwires。

## Out of Scope

- PER-011 Fusion。
- 删除历史 evidence、scenario fixtures 或回放输入。
- 修改 typed checked authority、WorldModel association 或 effect boundary。

## Acceptance

1. 生产源码不含 legacy state projection/egress/writer/routing surface。
2. typed HostLiveFull、TypedLiveChain、coordinate gate 与 deterministic tests 保持通过。
3. Unknown/Partial/Unsupported 不回退为 legacy state 或 false。
4. 历史/仿真 fixture 仍可运行；full solution、certification、coverage 通过。
5. `git diff --check` 与 production legacy tripwire 通过。
