# Domain 系统设计文档索引

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix phase1-1-domain-corrections 后）

---

## 文档列表

| # | 文档 | 核心产出 | 优先级 |
|---|------|----------|--------|
| 01 | [依赖拓扑](01-dependency-topology.md) | DAG 图 + 核心/可选依赖标注 | ✅ 完成 |
| 02 | [数据流路径](02-data-flow-paths.md) | 端到端链路图 + ToggleButton 映射链 | ✅ 完成 |
| 03 | [语义契约](03-semantic-contracts.md) | 每个类型的职责声明 + 不负责什么 | ✅ 完成 |
| 04 | [跨域桥](04-cross-domain-bridges.md) | 桥清单 + 必要性论证 + Phase 2 预测 | ✅ 完成 |
| 05 | [变更稳定性](05-change-stability.md) | 稳定性评级 + 波及面 + 变更策略矩阵 | ✅ 完成 |
| 06 | [验证边界](06-validation-boundaries.md) | 校验矩阵 + 风险评级 + 校验密度图 | ✅ 完成 |
| 07 | [序列化契约](07-serialization-contracts.md) | 序列化行为表 + Python 兼容差距 | ✅ 完成 |

## 关键发现

1. **Domain 层核心类型迁移完成**（24 类型，229 测试全绿）
2. **P0 fix 后依赖拓扑已更新** — ElementTypeMapper→TypeHint 从核心依赖降为可选依赖
3. **唯一真正的校验漏洞** — Region.id 不校验非空（P3）
4. **唯一序列化不一致** — TypeHint 无 `[JsonPropertyName]`，其他 3 个 Domain enum 都有（P3）
5. **Phase 2 关键缺失** — Template dict→TraversalNode record 转换器

## 与其他文档的关系

- `docs/refactor/04-phase1-python-csharp-comparison.md` — Python↔C# 全量对比（P0/P2/P3 问题清单）
- `docs/refactor/05-model-relationship-map.md` — 旧版依赖图（需更新：§3.1 已修，§2/§6 箭头需更新）
- `openspec/specs/` — Delta spec 已同步到 main spec
