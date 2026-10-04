# ADR-0036：Runtime Host 拥有 Run 状态、事件与查询投影

状态：accepted（2026-10-04）。PNL-004 已证明真实 DSH Host、Kernel 和 Android 执行链可以创建一次 Run；本决策冻结下一阶段的领域边界和查询形状。

## Decision

1. Runtime Host 是 `RuntimeRun` identity、生命周期、事件序列、终态和 artifact association 的 authority。
2. `TaskInstance` 是 Workspace 的发起记录，不拥有 Runtime lifecycle；`ProductSession` 是产品会话；`DshHostSessionRef` 是 DSH realization 引用。
3. RunStore 同时保存当前 snapshot 和 append-only event log。查询优先读 snapshot，审计和恢复前置数据读 event log。
4. 生命周期使用 `starting`、`running`、`completed`、`failed`、`interrupted`。没有终态证据时，用户看到“已中断”，内部 outcome 可以保持 unknown。
5. Runtime 查询返回业务 projection，包含 identity、lifecycle、consistency、artifact references 和 source-native event summary；不暴露物理路径。
6. Runtime HTTP 使用标准 HTTP 状态码承载协议结果，响应体使用稳定业务对象；错误不使用裸字符串。

## Consequences

- Workbench 可以替换为其他前端而不复制 Runtime truth。
- DSH、UniClaw、Kernel 和未来 OTel 记录可并存，source/authority 不被投影抹平。
- 文件 adapter 可以替换为 Host database 或远程 service，公共契约不变。
- 恢复策略和认证仍是后续 Change，不能在本 Change 中隐式加入。

## Rejected alternatives

- Workbench 直接扫描 runs 目录。
- DSH Session 作为 Product Run 主键。
- 只保存快照而没有不可变事件。
- 把所有来源事件强制转换成 OTel Span。
