## Context

**背景**：Uni-Claw 核心业务模型文档 (docs/core_business_models.md) 已完成对已实现 59+ 数据模型的系统性梳理，涵盖 8 个领域：页面分析、图节点、内容树、状态机、运行时上下文、异常处理、AI 能力和 Trace。

**当前状态**：
- 模型定义分散在 11 个代码模块中
- 部分模型使用 Pydantic BaseModel（有内置验证），部分使用 dataclass（无验证）
- 枚举类型继承自 `str` 和 `Enum`，但缺少便捷方法
- 缺少统一的模型测试规范
- 测试文件结构混乱，部分测试已过时

**约束条件**：
- 不能破坏现有 API 兼容性
- 需要兼容 Pydantic 和 dataclass 两种定义方式
- 测试不应引入新的外部依赖
- 需要清理旧的无用测试文件

**利益相关者**：
- 开发工程师 - 使用模型进行业务逻辑实现
- AI 提示工程师 - 需要准确的模型定义用于提示词
- 测试工程师 - 需要测试规范验证实现正确性

## Goals / Non-Goals

**Goals**：
1. 为所有枚举类型添加统一的辅助方法（`values()`, `from_value()`, `is_valid()`）
2. 建立核心业务模型的测试规范和测试用例模板
3. 补充 dataclass 模型的字段验证逻辑
4. 提供模型序列化/反序列化的测试标准
5. **清理旧的无用测试文件**
6. **将测试代码归类为测试资产**

**Non-Goals**：
- 不修改模型的核心结构或字段定义
- 不改变现有 API 行为
- 不引入新的模型类型或框架
- 不实现性能优化（如缓存、延迟加载）

## Decisions

### 1. 枚举辅助方法设计

**决策**：为所有枚举类型添加三个类方法

```python
class MyEnum(str, Enum):
    VALUE1 = "value1"
    VALUE2 = "value2"

    @classmethod
    def values(cls) -> List[str]:
        """获取所有枚举值的列表"""
        return [e.value for e in cls]

    @classmethod
    def from_value(cls, value: str) -> "MyEnum":
        """从字符串值创建枚举实例"""
        try:
            return cls(value)
        except ValueError:
            raise ValueError(f"Invalid {cls.__name__} value: {value}. Valid values: {cls.values()}")

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """验证字符串值是否为有效枚举值"""
        return value in cls.values()
```

**理由**：
- 统一的接口便于使用和记忆
- `values()` 返回字符串列表便于前端集成
- `from_value()` 提供友好的错误消息
- `is_valid()` 用于前置条件验证

---

### 2. 测试规范组织方式

**决策**：按模型模块组织测试，每个模型模块对应一个测试文件

```
tests/
├── assets/                     # 测试资产（新增）
│   ├── fixtures/               # 测试固件
│   │   ├── page_analysis.json  # 页面分析样本数据
│   │   ├── graph_nodes.json    # 图节点样本数据
│   │   └── trace_data.json     # Trace 样本数据
│   └── utils/                  # 测试工具类
│       ├── model_helpers.py    # 模型测试辅助函数
│       └── assertions.py        # 自定义断言
│
├── models/                      # 模型测试（新增）
│   ├── __init__.py
│   ├── test_content_tree.py    # 页面分析模型测试
│   ├── test_graph_nodes.py     # 图节点模型测试
│   ├── test_state_machine.py   # 状态机模型测试
│   ├── test_context.py          # 运行时上下文测试
│   ├── test_exception.py        # 异常处理测试
│   ├── test_ai_types.py         # AI 类型测试
│   └── test_trace.py            # Trace 模型测试
│
├── integration/                 # 集成测试（保留）
├── unit/                        # 单元测试（保留，非模型类）
└── conftest.py                  # pytest 配置
```

**理由**：
- 将模型测试集中到 `tests/models/` 目录，便于统一管理
- 创建 `tests/assets/` 存放测试固件和工具类，作为可复用的测试资产
- 保留现有的 `integration/` 和 `unit/` 目录结构
- 测试资产可在多个测试文件中复用

---

### 3. 测试文件清理策略

**决策**：识别并移除以下类型的测试文件

| 文件类型 | 处理方式 | 示例 |
|----------|----------|------|
| 与已移除模块相关的测试 | 删除 | 与旧 API 相关的测试 |
| 重复功能的测试 | 合并 | 多个文件测试同一功能 |
| 过时的集成测试 | 归档 | 移动到 `tests/archive/` |
| 空测试文件 | 删除 | 无测试用例的文件 |

**需要审查的文件**：
- `tests/test_state.py` - 可能与新的模型测试重复
- `tests/test_traversal_context.py` - 需要迁移到 `tests/models/test_context.py`
- `tests/test_ai_types.py` - 需要迁移到 `tests/models/test_ai_types.py`

---

### 4. dataclass 字段验证策略

**决策**：为 dataclass 模型添加 `__post_init__` 方法进行字段验证

```python
from dataclasses import dataclass

@dataclass
class MyModel:
    field1: str
    field2: int

    def __post_init__(self):
        if not self.field1:
            raise ValueError("field1 cannot be empty")
        if self.field2 < 0:
            raise ValueError("field2 must be non-negative")
```

**理由**：
- 兼容现有代码，无需迁移到 Pydantic
- 在实例化时立即验证，符合fail-fast原则
- 可以提供自定义错误消息

---

### 5. 测试用例覆盖标准

**决策**：每个模型需要覆盖以下测试场景

| 测试类型 | 覆盖内容 |
|----------|----------|
| **字段验证** | 必填字段、类型检查、值范围、默认值 |
| **序列化** | to_dict() / to_json()（如有） |
| **反序列化** | from_dict() / from_json()（如有） |
| **边界条件** | 空值、极端值、无效值 |
| **枚举专属** | values()、from_value()、is_valid() |

---

## Risks / Trade-offs

**风险 1**：枚举方法命名与未来 Python 版本冲突
- **缓解**：使用常见且稳定的命名约定（values、from_value、is_valid）

**风险 2**：dataclass 验证逻辑可能影响性能
- **缓解**：仅在实例化时验证一次，不影响访问性能

**风险 3**：测试覆盖率目标过高可能延长期限
- **缓解**：先建立测试规范，再逐步补充测试用例，不追求一次性 100% 覆盖

**风险 4**：删除测试文件可能影响 CI/CD 流程
- **缓解**：先在开发分支验证，确保所有有效测试通过后再删除

**风险 5**：测试资产复用可能导致测试耦合
- **缓解**：测试资产只包含数据，不包含断言逻辑

**权衡**：
- 选择在 dataclass 中添加验证而非迁移到 Pydantic：牺牲了一些高级功能，但保持了兼容性和简洁性
- 选择按模块组织测试而非按类型：增加了测试文件数量，但提高了可维护性
- 选择创建测试资产目录：增加了结构复杂度，但提高了复用性

## Migration Plan

**部署步骤**：

1. **阶段 0：测试清理与准备**
   - 审查现有测试文件，识别待删除/合并的文件
   - 创建 `tests/assets/` 目录结构
   - 创建 `tests/models/` 目录

2. **阶段 1：枚举辅助方法**
   - 为每个枚举类型添加三个辅助方法
   - 运行现有测试确保兼容性
   - 添加枚举方法的单元测试

3. **阶段 2：dataclass 字段验证**
   - 识别缺少验证的 dataclass 模型
   - 逐个添加 `__post_init__` 验证
   - 添加验证失败的测试用例

4. **阶段 3：测试规范与资产**
   - 创建测试文件骨架
   - 创建测试资产（fixtures、utils）
   - 为每个模型编写基础测试用例
   - 建立测试覆盖率报告

5. **阶段 4：清理与迁移**
   - 删除无用的测试文件
   - 迁移现有模型测试到新结构
   - 更新 CI/CD 配置（如有）

**回滚策略**：
- 枚举方法是新增功能，回滚只需删除方法定义
- dataclass 验证逻辑可通过条件开关禁用
- 测试资产和文件结构变更可通过 git revert 回滚

## Open Questions

1. **是否需要为枚举添加 `description()` 方法获取枚举值的中文描述？**
   - 待定：取决于国际化需求

2. **是否需要为模型添加 `copy_with()` 方法创建修改后的副本？**
   - 待定：取决于实际使用场景

3. **测试覆盖率目标是多少？**
   - 建议：核心模型 80%+，辅助模型 60%+

4. **测试资产是否需要版本控制？**
   - 建议：测试固件使用 git 版本控制，动态生成的数据不需要

5. **是否需要为测试资产添加验证机制？**
   - 建议：添加固件格式验证，确保测试数据正确性
