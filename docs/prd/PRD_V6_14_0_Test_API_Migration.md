# V6.14.0 Test API Migration PRD

> **修复 V6 测试套件中剩余 62 个失败/错误测试**
>
> **状态**: Draft | **创建日期**: 2026-06-09 | **优先级**: High | **版本**: 2.0

---

## 1. 问题背景

### 1.1 当前状态
- V6 测试套件: 726/807 通过 (90%)
- 剩余失败: 62 个 (51 FAILED + 11 ERROR)
- 主要集中在 2 个测试文件

### 1.2 影响范围
| 测试文件 | 失败数 | 通过数 | 总数 |
|---------|-------|-------|-----|
| `test_popup_handler.py` | 51 | 13 | 64 |
| `test_v6_9_dynamic_matching.py` | 11 | 20 | 31 |
| **总计** | **62** | **33** | **95** |

---

## 2. 根本原因分析

### 2.1 架构级 API 变更

这两个测试失败的根本原因是 **V6.11.0 引入 DynamicChildManager 架构**，导致子节点管理 API 完全重构，而非简单的参数调整。

#### V6.11.0 架构变更

**旧架构 (V6.10.x 之前)**:
- 动态子节点生成逻辑直接在 `GraphTraversalEngine` 中
- 使用 `_generate_dynamic_children()` 生成子节点
- 使用 `_get_next_unvisited_child()` 获取下一个子节点
- 使用 `invalidate_children_cache()` 失效缓存
- 使用 `_visited_nodes` 属性跟踪访问状态

**新架构 (V6.11.0+)**:
- 引入 `DynamicChildManager` 专门管理动态子节点生命周期
- 职责分离：生成、缓存、失效、去重逻辑集中在 Manager 中
- 通过 `engine._child_mgr` 访问子节点管理功能
- 使用 `context.visited_children[node.node_id]` 替代 `_visited_nodes`

#### 为什么重构？

**旧架构问题**:
1. **违反 SRP**: `GraphTraversalEngine` 承担过多职责（遍历 + 子节点管理）
2. **测试困难**: 子节点管理逻辑与遍历逻辑耦合，难以独立测试
3. **缓存管理混乱**: 缓存失效逻辑分散在多个方法中

**新架构优势**:
1. **高内聚**: 所有动态子节点相关逻辑集中在 `DynamicChildManager`
2. **低耦合**: `GraphTraversalEngine` 通过接口调用子节点管理
3. **可测试性**: `DynamicChildManager` 可独立测试

#### test_popup_handler.py 问题

**枚举值完全重构**:
```python
# 测试期望 (旧 API)
PopupType.NOTIFICATION
UrgencyLevel.DEFERABLE
BlockingType.PARTIAL_BLOCK

# 实际实现 (新 API)
PopupType  # PERMISSION, ERROR, AD, DIALOG, UNKNOWN
UrgencyLevel  # LOW, MEDIUM, HIGH, CRITICAL
BlockingType  # MODAL, NON_MODAL, TOAST
```

**类结构变化**:
```python
# 测试期望
PopupDetector(config)  # 接受配置字典
PopupInfo(title, element_id, ...)  # 多个必填字段

# 实际实现
PopupDetector()  # 无参数
PopupInfo(popup_type, confidence, ...)  # 不同的字段集
```

#### test_v6_9_dynamic_matching.py 问题

**方法被完全移除**:
```python
# 测试调用的方法 (不存在于当前实现)
_generate_dynamic_children()
_get_next_unvisited_child()
_visited_nodes (属性)
invalidate_children_cache()
```

#### API 变更影响范围

| 影响域 | 旧 API | 新 API | 破坏性 |
|-------|-------|-------|-------|
| 子节点生成 | `engine._generate_dynamic_children()` | `engine._child_mgr.generate()` | 🔴 高 |
| 获取未访问子节点 | `engine._get_next_unvisited_child()` | `engine._child_mgr.get_next_unvisited_child()` | 🔴 高 |
| 缓存失效 | `engine.invalidate_children_cache()` | 自动失效 (基于 page_fingerprint) | 🔴 高 |
| 访问状态 | `engine._visited_nodes` | `context.visited_children[node.node_id]` | 🔴 中 |
| PopupInfo 构造 | `PopupInfo(title, element_id, ...)` | `PopupInfo(popup_type, confidence, ...)` | 🔴 高 |

### 2.2 修复难度评估 (更新后)

| 问题类型 | 示例 | 修复方式 | 实际难度 | 风险 |
|---------|------|---------|---------|------|
| 枚举值变更 | `NOTIFICATION` → `DIALOG` | 更新测试期望值 | 低 | 低 |
| 枚举值变更 | `DEFERABLE` → `LOW` | 更新测试期望值 | 低 | 低 |
| 字段重命名 | `PARTIAL_BLOCK` → `NON_MODAL` | 更新字段引用 | 低 | 低 |
| 字段语义变更 | `title` → `popup_type` | 需要语义映射 | **高** | 中 |
| 构造函数变更 | `PopupInfo(title, ...)` → `PopupInfo(popup_type, confidence, ...)` | 重新构造调用 | **很高** | 高 |
| 方法移除 | `_generate_dynamic_children()` → `_child_mgr.generate()` | 理解新架构 + 重写 | **很高** | 高 |
| 方法移除 | `_get_next_unvisited_child()` → `_child_mgr.get_next_unvisited_child()` | 理解新架构 + 重写 | **很高** | 高 |
| 缓存机制变更 | 主动失效 → 自动失效 | 重写测试逻辑 | **很高** | 高 |
| 数据结构变更 | `_visited_nodes` → `context.visited_children[node.node_id]` | 更新访问方式 | 中 | 中 |

**难度上调原因**:
1. **架构理解成本**: 需要先理解 `DynamicChildManager` 设计和工作原理
2. **语义映射**: `PopupInfo` 字段不是简单重命名，而是语义完全不同
3. **上下文参数**: 新 API 需要正确的 `context` 参数，测试可能需要调整 fixture
4. **自动失效机制**: 新架构使用 `page_fingerprint` 自动失效，测试需要理解这个机制

---

## 3. 修复方案

### 3.1 Phase 0: 架构理解与映射 (新增)

**目标**: 理解新架构并创建完整的 API 映射表

**任务清单**:
1. **阅读 DynamicChildManager 源码**
   - 文件位置: `src/traversal/dynamic_child_manager.py`
   - 理解职责: 生成、缓存、失效、去重
   - 理解工作流程: `generate()` → `get_next_unvisited_child()` → `has_unvisited()`

2. **创建完整 API 映射表**
   - 旧 API → 新 API 映射
   - 参数变化说明
   - 返回值变化说明

3. **设计测试辅助层**
   - 确定需要封装的 API
   - 设计辅助类接口
   - 编写使用示例

**预期结果**: 完整的 API 映射表 + 测试辅助层设计

### 3.2 Phase 1: 简单修复 (预计 15 个测试)

**目标**: 修复枚举值和字段名称变更

**任务清单**:
1. **PopupType 枚举更新**
   - `NOTIFICATION` → `DIALOG`
   - 检查所有 `PopupType.*` 引用

2. **UrgencyLevel 枚举更新**
   - `DEFERABLE` → `LOW`
   - 更新默认值断言

3. **BlockingType 枚举更新**
   - `PARTIAL_BLOCK` → `NON_MODAL`
   - `FULL_BLOCK` → `MODAL`

**预期结果**: 10-15 个测试恢复通过

### 3.3 Phase 2: 中等修复 (预计 10 个测试)

**目标**: 修复字段名称和构造函数变更

**任务清单**:
1. **PopupInfo 字段适配**
   - 移除 `title`, `element_id` 断言
   - 适配新字段 `popup_type`, `confidence`, `target_element`

2. **构造函数适配**
   - `PopupDetector()` 改为无参数构造
   - 移除配置相关测试或适配新配置方式

**预期结果**: 10 个测试恢复通过

### 3.4 Phase 3: 复杂修复/重构 (预计 20-37 个测试)

**目标**: 处理已移除的类和方法

#### 3.4.1 API 映射表

| 旧 API (测试中) | 新 API (实际实现) | 迁移路径 | 示例代码 |
|----------------|------------------|----------|---------|
| `engine._generate_dynamic_children(node, page_analysis)` | `engine._child_mgr.generate(node, context)` | 替换调用 + 调整参数 | 见下方示例 |
| `engine._get_next_unvisited_child(node)` | `engine._child_mgr.get_next_unvisited_child(node, context)` | 替换调用 + 添加 context 参数 | 见下方示例 |
| `engine.invalidate_children_cache(node_id)` | 无需调用 (自动失效) | 删除调用或模拟 page 变化 | 见下方示例 |
| `engine._visited_nodes` 属性 | `context.visited_children[node.node_id]` | 替换访问方式 | `context.visited_children.get(node.node_id, set())` |
| `PopupInfo(title, element_id, urgency, blocking, ...)` | `PopupInfo(popup_type, confidence, target_element, ...)` | 重构构造 | 见下方示例 |
| `PopupType.NOTIFICATION` | `PopupType.DIALOG` | 替换枚举值 | `PopupType.DIALOG` |
| `UrgencyLevel.DEFERABLE` | `UrgencyLevel.LOW` | 替换枚举值 | `UrgencyLevel.LOW` |
| `BlockingType.PARTIAL_BLOCK` | `BlockingType.NON_MODAL` | 替换枚举值 | `BlockingType.NON_MODAL` |
| `PopupType.PERMISSION` | `PopupType.PERMISSION` | 无需更改 | ✅ 兼容 |
| `PopupType.ERROR` | `PopupType.ERROR` | 无需更改 | ✅ 兼容 |

#### 3.4.2 迁移示例

**示例 1: 动态子节点生成**

```python
# 旧代码 (测试中)
mock_page_analysis = {...}
children = engine._generate_dynamic_children(node, mock_page_analysis)
assert len(children) == 2

# 新代码
# 注意：新 API 不需要 page_analysis 参数，使用 context.current_page_analysis
children = engine._child_mgr.generate(node, engine.context)
assert len(children) == 2
```

**示例 2: 获取下一个未访问子节点**

```python
# 旧代码 (测试中)
child_id = engine._get_next_unvisited_child(node)
assert child_id == "child1"

# 新代码
child_id = engine._child_mgr.get_next_unvisited_child(node, engine.context)
assert child_id == "child1"
```

**示例 3: 缓存失效**

```python
# 旧代码 (测试中)
engine.invalidate_children_cache("parent_node")

# 新代码
# 新架构使用 page_fingerprint 自动失效
# 如需测试失效逻辑，模拟 page 变化：
engine.context.current_page_analysis = {"new_page": True}
```

**示例 4: PopupInfo 构造**

```python
# 旧代码 (测试中)
popup = PopupInfo(
    title="Location Permission",
    element_id="permission_dialog_123",
    urgency=UrgencyLevel.HIGH,
    blocking=BlockingType.FULL_BLOCK,
    ...
)

# 新代码 (需要语义映射)
popup = PopupInfo(
    popup_type=PopupType.PERMISSION,  # 从 title 推断
    confidence=0.8,  # 新增必需字段
    target_element={"text": "Location Permission"},  # 从 title 转换
    urgency_level=UrgencyLevel.HIGH,  # 字段名变更
    blocking_type=BlockingType.MODAL,  # 枚举值变更
)
```

#### 3.4.3 修复策略

**方案 A - 迁移到新 API** (推荐用于有价值的测试):
1. 使用 API 映射表定位新 API
2. 更新测试代码调用新 API
3. 调整 fixture 以提供正确的 `context` 参数
4. 验证测试逻辑在新 API 下仍然有效

**方案 B - 使用测试辅助层** (推荐用于复杂测试):
1. 创建测试辅助函数封装 API 差异
2. 测试代码调用辅助函数而非直接调用 API
3. 辅助函数内部处理新旧 API 兼容性
4. 提高未来 API 变更时的测试稳定性

**方案 C - 标记废弃** (用于过时测试):
1. 使用 `@pytest.mark.skip` 标记测试
2. 添加 skip reason 说明废弃原因
3. 记录是否有其他测试覆盖相同功能
4. 在测试文档中更新测试覆盖矩阵

**预期结果**: 20-37 个测试恢复通过或标记废弃

### 3.5 Phase 4: 验证与文档

**任务清单**:
1. 运行完整测试套件确认无回归
2. 更新测试文档说明 API 变更
3. 归档本次修复变更

**预期结果**: 完整的测试文档 + 修复归档

### 3.6 测试辅助层设计 (新增)

为降低未来 API 变更对测试的影响，设计测试辅助层来封装 API 差异。

#### 3.6.1 设计原则

1. **隔离变化**: 辅助层封装实现细节，测试代码通过稳定接口调用
2. **单向依赖**: 测试依赖辅助层，辅助层依赖实现，避免循环依赖
3. **版本化**: 辅助层提供版本化接口，便于未来扩展
4. **可测试**: 辅助层本身易于测试和维护

#### 3.6.2 辅助类设计

**文件位置**: `tests/v6/helpers/api_migration_helper.py`

```python
"""V6 测试 API 迁移辅助层

封装 V6.10.x → V6.14.0 API 差异，提供稳定的测试接口。
"""

from typing import Optional, Dict, Any, List
from src.state_machine.popup_handler import (
    PopupInfo,
    PopupType,
    UrgencyLevel,
    BlockingType
)
from src.traversal.graph_engine import GraphTraversalEngine
from src.trace.context import TraversalRuntimeContext


class PopupTestHelper:
    """Popup 测试辅助类 - 封装 PopupInfo API 差异"""

    # 旧 API 枚举值 → 新 API 枚举值映射
    _POPUP_TYPE_MAP = {
        "NOTIFICATION": PopupType.DIALOG,
    }

    _URGENCY_MAP = {
        "DEFERABLE": UrgencyLevel.LOW,
    }

    _BLOCKING_MAP = {
        "PARTIAL_BLOCK": BlockingType.NON_MODAL,
        "FULL_BLOCK": BlockingType.MODAL,
    }

    @classmethod
    def create_from_old_style(
        cls,
        popup_type: str,
        title: str,
        element_id: str,
        urgency: str,
        blocking: str,
        **kwargs
    ) -> PopupInfo:
        """
        从旧 API 风格创建 PopupInfo

        Args:
            popup_type: 旧 API 的 PopupType (如 "NOTIFICATION")
            title: 弹窗标题
            element_id: 元素 ID
            urgency: 旧 API 的 UrgencyLevel (如 "DEFERABLE")
            blocking: 旧 API 的 BlockingType (如 "PARTIAL_BLOCK")
            **kwargs: 其他旧 API 参数

        Returns:
            符合新 API 的 PopupInfo 实例
        """
        # 映射枚举值
        mapped_type = cls._POPUP_TYPE_MAP.get(popup_type, PopupType.DIALOG)
        mapped_urgency = cls._URGENCY_MAP.get(urgency, UrgencyLevel.MEDIUM)
        mapped_blocking = cls._BLOCKING_MAP.get(blocking, BlockingType.MODAL)

        return PopupInfo(
            popup_type=mapped_type,
            confidence=kwargs.get('confidence', 0.8),
            target_element={"text": title, "element_id": element_id},
            urgency_level=mapped_urgency,
            blocking_type=mapped_blocking,
        )


class DynamicChildTestHelper:
    """动态子节点测试辅助类 - 封装 DynamicChildManager API 差异"""

    @staticmethod
    def generate_children(
        engine: GraphTraversalEngine,
        node,
        page_analysis: Optional[Dict[str, Any]] = None
    ) -> List:
        """
        兼容旧的 _generate_dynamic_children 调用

        Args:
            engine: GraphTraversalEngine 实例
            node: 父节点
            page_analysis: 页面分析 (可选，新 API 使用 context.current_page_analysis)

        Returns:
            生成的子节点列表
        """
        if page_analysis:
            # 如果提供了 page_analysis，更新 context
            engine.context.current_page_analysis = page_analysis

        engine._child_mgr.generate(node, engine.context)
        return engine._child_mgr._dynamic_children.get(node.node_id, [])

    @staticmethod
    def get_next_unvisited_child(
        engine: GraphTraversalEngine,
        node
    ) -> Optional[str]:
        """
        兼容旧的 _get_next_unvisited_child 调用

        Args:
            engine: GraphTraversalEngine 实例
            node: 父节点

        Returns:
            下一个未访问子节点的 ID，或 None
        """
        return engine._child_mgr.get_next_unvisited_child(node, engine.context)

    @staticmethod
    def invalidate_cache(
        engine: GraphTraversalEngine,
        node_id: str
    ) -> None:
        """
        兼容旧的 invalidate_children_cache 调用

        注意：新架构使用 page_fingerprint 自动失效，
        此方法通过模拟 page 变化来实现类似效果。

        Args:
            engine: GraphTraversalEngine 实例
            node_id: 节点 ID
        """
        # 通过更新 page_fingerprint 触发自动失效
        engine.context.current_page_analysis = {"_invalidate": True}

    @staticmethod
    def get_visited_children(
        engine: GraphTraversalEngine,
        node_id: str
    ) -> set:
        """
        兼容旧的 _visited_nodes 属性访问

        Args:
            engine: GraphTraversalEngine 实例
            node_id: 节点 ID

        Returns:
            已访问子节点集合
        """
        return engine.context.visited_children.get(node_id, set())
```

#### 3.6.3 使用示例

```python
# 旧测试代码
def test_popup_detection():
    popup = PopupInfo(
        popup_type=PopupType.NOTIFICATION,
        title="New Message",
        ...
    )

# 新测试代码 (使用辅助层)
def test_popup_detection():
    popup = PopupTestHelper.create_from_old_style(
        popup_type="NOTIFICATION",
        title="New Message",
        element_id="msg_123",
        urgency="LOW",
        blocking="NON_BLOCKING",
    )
```

---

## 4. 优先级与依赖

### 4.1 优先级矩阵

| Phase | 价值 | 复杂度 | 优先级 |
|-------|------|-------|-------|
| Phase 0 (新增) | 高 | 中 | **P0** |
| Phase 1 | 高 | 低 | P1 |
| Phase 2 | 中 | 中 | P2 |
| Phase 3 | 中-低 | 高 | P2 |

### 4.2 依赖关系

```
Phase 0 (架构理解与映射)
    ↓
Phase 1 (枚举修复)
    ↓
Phase 2 (字段适配)
    ↓
Phase 3 (API 重构)
    ↓
Phase 4 (验证)
```

---

## 5. 风险与缓解

### 5.1 风险

1. **实现不存在**: 某些测试的功能可能已被移除，无法简单修复
2. **级联失败**: 修复一个测试可能暴露其他 API 不匹配
3. **测试过时**: 测试描述的功能可能已废弃
4. **自动失效机制冲突**: DynamicChildManager 的自动失效可能与测试假设冲突

### 5.2 缓解措施

1. **分类处理**: 按难度分阶段修复
2. **保留选项**: 对无法修复的测试标记为废弃而非删除
3. **增量验证**: 每个阶段后运行完整测试套件
4. **辅助层隔离**: 使用测试辅助层降低未来 API 变更影响

---

## 6. 成功标准

### 6.1 定量目标
- [ ] Phase 0: 完成架构理解 + API 映射表
- [ ] Phase 1: 15+ 测试恢复
- [ ] Phase 2: 10+ 测试恢复
- [ ] Phase 3: 20+ 测试恢复或标记废弃
- [ ] **最终**: V6 测试通过率 ≥ 95% (768/807)

### 6.2 定性目标
- [ ] 所有修复的测试有明确的 API 迁移说明
- [ ] 废弃的测试有清晰的废弃原因记录
- [ ] 测试文档更新以反映当前 API
- [ ] 创建测试辅助层以降低未来维护成本

---

## 7. 实施计划

### 7.1 建议顺序

1. **先 Phase 0** - 理解新架构，创建 API 映射表
2. **然后 Phase 1** - 低成本快速提升通过率
3. **评估 Phase 3** - 确定哪些功能仍有存在价值
4. **执行 Phase 2** - 对有价值的测试进行适配
5. **最后 Phase 4** - 验证和文档

### 7.2 时间估算 (更新后)

| Phase | 原估算 | 修正估算 | 变更原因 |
|-------|-------|---------|---------|
| **Phase 0** (新增) | - | **1-2h** | 需要先理解 DynamicChildManager 架构 |
| Phase 1 | 30-60min | **1h** | 枚举映射可能需要语义分析 |
| Phase 2 | 1-2h | **3-4h** | PopupInfo 字段完全不同，需要重构 |
| Phase 3 | 2-4h | **4-6h** | 需要理解新架构 + 重写测试逻辑 |
| Phase 4 | 30min | **1h** | 需要更新架构文档和覆盖矩阵 |
| **总计** | **4-8h** | **10-14h** | **增加 60-100% 缓冲** |

**工时上调原因**:
1. **架构学习成本**: 需要阅读和理解 `DynamicChildManager` 源码
2. **语义映射**: `PopupInfo` 字段变化不是简单重命名，需要语义理解
3. **上下文调整**: 新 API 需要正确的 `context` 参数，fixture 需要调整
4. **不确定性缓冲**: 首次处理此类架构迁移，保留缓冲以应对意外情况

**风险提示**: 如果 `DynamicChildManager` 的自动失效机制与测试假设冲突，可能需要额外 2-4 小时调整测试策略。

---

## 8. 附录

### 8.1 相关文件
- 测试文件:
  - `tests/v6/unit/test_popup_handler.py`
  - `tests/v6/test_v6_9_dynamic_matching.py`
- 实现文件:
  - `src/state_machine/popup_handler.py`
  - `src/traversal/graph_engine.py`
  - `src/traversal/dynamic_child_manager.py`
- 辅助层:
  - `tests/v6/helpers/api_migration_helper.py` (新建)

### 8.2 参考资料
- V6.11 引擎重构文档
- V6.12 节点执行上下文 PRD
- 已修复的 test_compiler.py (参考模式)
- DynamicChildManager 源码

---

## 9. 测试 API 稳定性策略 (新增)

为防止类似的 API 变更导致大量测试失败，提出以下预防策略。

### 9.1 测试 API 版本化

**原则**: 测试代码应通过稳定的辅助层访问实现，而非直接调用内部 API。

**实施**:
1. 为常用测试操作创建版本化辅助函数
2. 辅助函数提供稳定接口，内部适配实现变化
3. 使用语义化版本管理测试 API

**示例**:
```python
# tests/v6/helpers/v1/__init__.py
"""V6 测试 API v1.0 - 稳定测试接口"""

from .popup_helper import PopupTestHelper
from .dynamic_child_helper import DynamicChildTestHelper

__all__ = ['PopupTestHelper', 'DynamicChildTestHelper']
```

### 9.2 架构评审检查项

在 API 变更时，评估对测试的影响：

| 检查项 | 说明 | 负责人 |
|-------|------|-------|
| 测试影响评估 | 评估 API 变更会影响多少测试 | API 修改者 |
| 迁移指南提供 | 为测试维护者提供迁移指南 | API 修改者 |
| 辅助层更新 | 更新测试辅助层以支持新 API | 测试维护者 |
| CHANGELOG 标记 | 在 CHANGELOG 中标记破坏性变更 | API 修改者 |

### 9.3 测试 API 变更通知机制

**内部通知**:
1. API 变更 PR 需要标记 "tests-impact" 标签
2. 测试维护者订阅相关通知
3. 在架构评审会上讨论测试影响

**文档更新**:
1. CHANGELOG 中添加 "Tests Migration" 章节
2. 提供 API 变更前后对比
3. 提供迁移示例代码

### 9.4 测试覆盖矩阵维护

**目的**: 确保核心功能有测试覆盖，避免因测试废弃导致功能失去保障。

**实施**:
1. 维护"功能 → 测试"映射表
2. 删除测试前验证是否有替代测试
3. 定期审计测试覆盖矩阵

**示例**:
```markdown
| 功能 | 测试文件 | 测试用例 | 状态 |
|------|---------|---------|------|
| 弹窗检测 | test_popup_handler.py | test_permission_popup | ✅ 通过 |
| 弹窗分类 | test_popup_handler.py | test_ad_popup_classification | ⚠️ 已废弃，test_popup_detection 覆盖 |
| 动态子节点生成 | test_v6_9_dynamic_matching.py | test_generate_children | ❌ 失败，待修复 |
```

---

**文档版本**: 2.0
**最后更新**: 2026-06-09
**审阅状态**: 已审阅，有条件批准
