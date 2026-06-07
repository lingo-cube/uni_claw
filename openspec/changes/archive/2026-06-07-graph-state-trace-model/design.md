## Context

uni-claw V3.0 当前架构：
- `TraversalEngine`: 线性遍历逻辑，基于 `level1_menus`/`level2_menus` 缓存
- `StateManager`: 维护 `current_path` 和 `visited_pages`
- `ExceptionChain`: 责任链处理异常

V3.0 的局限性：
1. 硬编码的菜单层级结构，难以适配不同车机系统
2. 遍历逻辑与位置管理耦合，异常时难以回退
3. 缺乏遍历过程记录，难以复现和调试问题

PRD V4.0 通过图模型、状态机和 Trace 系统解决上述问题。

## Goals / Non-Goals

**Goals:**
- 建立统一的 `TraversalNode` 抽象，支持静态图和动态图
- 实现三层状态机，规范化遍历生命周期管理
- 实现 Trace 系统，支持遍历过程记录和回放
- 保持 V3.0 兼容性，通过开关控制新旧模式

**Non-Goals:**
- 不引入 AI 决策（留待 V5.0）
- 不修改现有规则引擎逻辑
- 不重构视觉分析服务

## Decisions

### 1. TraversalNode 设计

**决策**: 使用数据类（dataclass）定义统一节点结构

```python
@dataclass
class TraversalNode:
    node_id: str
    name: str
    node_type: NodeType
    operation: Operation
    precondition: Optional[Precondition]
    children_strategy: ChildrenStrategy
    error_policy: Optional[ErrorPolicy]
    meta: Dict[str, Any]
```

**理由**:
- dataclass 提供清晰的字段定义和默认值
- `node_type` 区分容器节点（可展开）和叶子节点（终端操作）
- `children_strategy` 支持静态、动态、无子节点三种模式
- `meta` 字段存储运行时状态，避免修改核心结构

**替代方案**:
- 使用继承体系（ContainerNode、LeafNode 等）: 优点类型安全，缺点新增节点类型需要修改代码
- 使用字典: 优点灵活，缺点缺少类型检查和 IDE 支持

### 2. 模板注册表设计

**决策**: JSON 文件 + 动态实例化

```json
{
  "templates": {
    "menu_container": {
      "node_type": "container",
      "operation": {"action": "click", "target": {"by": "text", "value": "{{item_text}}"}},
      "children_strategy": {
        "type": "dynamic_match",
        "dynamic_rules": {
          "menu_rule": {"match_condition": {"type": "menu_item"}, "child_template": "menu_container"}
        }
      }
    }
  }
}
```

**理由**:
- JSON 格式可读性强，易于手动编辑
- `{{item_text}}` 占位符支持运行时填充
- 新增控件类型只需更新 JSON，无需修改代码
- 支持版本管理和 A/B 测试

**替代方案**:
- Python 配置文件: 优点支持复杂逻辑，缺点非技术人员难以维护
- 数据库存储: 优点支持动态更新，缺点增加系统复杂度

### 3. 状态机分层设计

**决策**: 三层状态机（全局、遍历、节点栈）

| 层级 | 职责 |
|------|------|
| 全局状态机 | 管理遍历任务生命周期（IDLE → INITIALIZING → TRAVERSING → COMPLETED） |
| 遍历状态机 | 处理单个节点（NODE_SELECT → PRECONDITION_CHECK → EXECUTE → RESULT_VERIFY → BRANCH） |
| 节点栈 | 维护深度优先遍历上下文 |

**理由**:
- 分层清晰，职责单一
- 全局状态机支持暂停、恢复、错误恢复
- 遍历状态机封装节点执行流程
- 节点栈天然支持深度优先遍历的回溯

**替代方案**:
- 单一状态机: 过于复杂，难以维护
- 状态机库（如 transitions）: 增加依赖，当前需求简单无需引入

### 4. Trace 存储格式

**决策**: JSON Lines + 文件夹组织

```
trace_session/
├── trace.jsonl        # 步骤记录
├── snapshots.jsonl    # 状态快照
├── screenshots/       # 截图
└── summary.json      # 统计摘要
```

**理由**:
- JSON Lines 支持流式写入，避免内存占用
- 每行一个 JSON 对象，易于解析和过滤
- 截图独立存储，避免 JSON 文件过大
- 支持压缩和历史清理

**替代方案**:
- SQLite 数据库: 优点支持复杂查询，缺点增加依赖和文件复杂度
- 单一大 JSON 文件: 优点简单，缺点无法流式写入，大文件难以解析

### 5. 目录结构

**决策**:

```
src/
├── graph/
│   ├── __init__.py
│   ├── node.py              # TraversalNode 相关数据类
│   ├── template.py           # 模板注册表
│   └── matcher.py           # 动态匹配逻辑
├── state_machine/
│   ├── __init__.py
│   ├── global_fsm.py        # 全局状态机
│   ├── traversal_fsm.py     # 遍历状态机
│   └── node_stack.py        # 节点栈
├── trace/
│   ├── __init__.py
│   ├── recorder.py          # Trace 记录器
│   ├── replay.py            # 回放引擎
│   └── models.py            # Trace 数据模型
└── traversal/
    └── traversal_engine.py  # 主引擎（集成点）
```

**理由**:
- 按功能模块组织目录
- `graph/`、`state_machine/`、`trace/` 独立模块便于测试和维护
- 主引擎作为集成点，通过开关选择新旧模式

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| 状态机复杂度增加调试难度 | 详细的状态转换日志和 Trace 记录 |
| 动态匹配规则配置错误导致遍历中断 | 提供规则验证工具和调试模式 |
| Trace 文件占用存储空间 | 支持配置保留数量和自动压缩 |
| 节点栈内存占用（深层遍历） | 设置栈深度上限和定期快照 |
| 新旧模式切换可能导致状态不一致 | 明确的初始化流程和状态清理 |

## Migration Plan

1. **Phase 1 (2 周)**: 实现图模型基础
2. **Phase 2 (2 周)**: 实现状态机引擎
3. **Phase 3 (1.5 周)**: 实现 Trace 系统
4. **Phase 4 (1.5 周)**: 系统集成与测试
5. **灰度**: 配置 `use_graph_mode=false`，逐步开放给测试环境
6. **验证**: 运行现有测试套件，确保 V3.0 模式无破坏性变更

## Open Questions

1. **Q**: 模板注册表是否需要版本管理？
   - **A**: 初期不支持版本，后续根据需求添加

2. **Q**: Trace 回放是否需要支持跨设备？
   - **A**: 不支持，回放仅用于同设备回归验证

3. **Q**: 节点栈深度上限设为多少？
   - **A**: 初始设为 10，根据实际遍历深度调整
