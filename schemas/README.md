# schemas/ 契约导航

`schemas/` 保存跨进程、跨语言或需由多个实现共同消费的契约。仓库职责见[职责基线](../docs/architecture/repository-role-boundaries-v0.1.md)；本页只列现有入口，不复制契约字段或产品语义。

## 入口

- [`work-item.schema.json`](work-item.schema.json)：Leader 派发给 Worker 的 WorkItem 载荷契约。
- [`agt-002/`](agt-002/)：Product 决策协议 JSON Schema 与 TypeScript 类型声明。
- [`workspace/`](workspace/README.md)：Workspace query、launch、run/event、record 与错误等契约及示例；该目录有独立 README。
- [`protocol/state-machine.yaml`](protocol/state-machine.yaml)：状态机声明；生成的测试投影位于 `tests/UniClaw.Kernel.Tests/Protocol/protocol-machine.generated.json`，生成入口可从 [`tools/gen-protocol-machine.py`](../tools/gen-protocol-machine.py) 查看。

## 维护边界

Schema 定义 wire shape，不能代替产品架构或协议基线。产品语义请回到 [产品架构与协议文档](../docs/README.md)；产品可评审声明见 [`product/`](../product/AGENTS.md)，Harness 派发流程见仓库根 [AGENTS.md](../AGENTS.md)。新增契约时先确认已有契约是否可扩展，避免在导航中另造组件清单权威。

## 相关入口

- [产品代码与适配器](../src/README.md)
- [测试和 fixture 分层](../tests/README.md)
- [验证和生成工具](../tools/README.md)
