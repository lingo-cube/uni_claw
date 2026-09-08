# docs/ 目录放置规则

| 目录 | 语义 | 允许 | 不允许 |
|---|---|---|---|
| `analysis/` | 非权威分析：draft、grill 材料、候选、迁移/映射分析、inventory | Authority: NONE / DRAFT 的候选文档 | 任何 FROZEN / CLOSED 基线或协议 |
| `architecture/` | 冻结架构 authority 目录 | 已冻结的产品/组件基线与协议（跨组件协议在 `protocols/`） | 未冻结的候选稿 |
| `adr/` | 架构决策记录 | ADR | 其他内容 |
| `agents/` | Development Harness 对 docs 的消费规则 | agent 指南 | 产品域内容 |

规则：

1. 文档在 `analysis/` 只能处于候选态。升级为 FROZEN / CLOSED 时必须迁入
   `architecture/`（或对应权威目录），并同步更新跨文档引用与指向。
2. 判据是文档状态，不是作者意图：只要声明 Freeze / CLOSED / canonical
   落点，就不得继续驻留 `analysis/`。

（本规则由 UWM-009 冻结迁移事件补立，product-architecture-baseline 的
迁移由 ARCH-DOC-013 收口执行（2026-09-09）；此前仅有协议基线头注中的
意图陈述，未形成可执行规则。ADR 目录的物理迁移仍按协议基线头注的
deferred decision，另立 change 再做。）
