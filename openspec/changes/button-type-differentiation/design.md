# Button Type Differentiation - Design Document

## Context

### 当前状态

Uni-claw V1 实现中，所有可点击元素使用统一的 `MenuItem` 类型，包含：
- `name`: 元素名称
- `type`: 基础类型（item/switch/button/icon/text）
- `coordinate`: 坐标
- `parent`: 父元素

点击处理流程也是统一的：
1. 点击元素
2. 等待固定时间（config.wait_time，默认 0.5 秒）
3. 截图分析判断结果类型

### 问题场景

| 场景 | 当前处理 | 理想处理 |
|------|----------|----------|
| 点击菜单标签 | 等待 0.5s | 应该等待更长时间（页面切换需要时间） |
| 点击开关 | 等待 0.5s | 应该等待更短时间（状态切换很快） |
| 点击导航按钮 | 等待 0.5s 后验证 | 应该优先验证路径变化 |
| 点击只读元素 | 仍会点击 | 应该跳过或快速检测 |

### 约束条件

- 不能破坏现有数据结构（向后兼容）
- AI Prompt 不能过于复杂（影响识别准确性）
- 遍历性能不能降低

## Goals / Non-Goals

**Goals:**
- 实现按钮类型细粒度分类
- 根据按钮类型使用差异化等待时间
- 根据预期行为采用针对性验证策略
- 保持现有 API 向后兼容

**Non-Goals:**
- 修改遍历引擎的核心流程
- 实现状态机（这是独立功能）
- 图像指纹验证（这是独立功能）
- 复杂的手势支持（滑动、长按）

## Decisions

### 决策 1: 扩展而非替换类型枚举

**选择**: 在现有 `MenuItemType` 基础上扩展，不破坏现有类型

**理由**:
- 向后兼容：现有状态文件可正常加载
- 渐进迁移：新类型逐步采用
- 风险最低：不破坏现有功能

**新类型定义**:
```python
class MenuItemType(str, Enum):
    # 导航类
    MENU_ITEM = "menu_item"           # 可点击的菜单项（列表项）
    TAB = "tab"                       # 标签页按钮
    BACK_BUTTON = "back_button"      # 返回按钮

    # 操作类
    SWITCH = "switch"                # 开关（切换状态）
    TOGGLE = "toggle"                # 切换按钮（开/关状态）
    BUTTON = "button"                # 普通按钮（触发操作）

    # 其他
    ICON = "icon"                    # 图标
    LINK = "link"                    # 链接/跳转
    TEXT = "text"                    # 纯文本
    READONLY = "readonly"            # 只读元素

    # 保留旧类型以兼容
    ITEM = "item"  # 等同于 MENU_ITEM
```

**替代方案**: 完全重构类型系统
- 拒绝原因：破坏兼容性，风险高

### 决策 2: 预期行为字段设计

**选择**: 添加 `expected_action` 字段，而非使用复杂的继承结构

**理由**:
- 简单：单字段即可表达意图
- 灵活：AI 可根据实际情况判断
- 可扩展：未来可增加新的 action 类型

**expected_action 取值**:
```python
class ExpectedAction(str, Enum):
    NAVIGATE = "navigate"   # 预期页面导航（菜单、标签）
    TOGGLE = "toggle"       # 预期状态切换（开关）
    ACTION = "action"       # 预期触发操作（弹窗、跳转）
    NONE = "none"           # 预期无响应（只读）
```

**相关字段**:
```python
expects_page_change: bool   # 预期页面会变化（navigate/action）
expects_state_change: bool  # 预期状态会变化（toggle）
```

**替代方案**: 为每种类型创建子类
- 拒绝原因：过度设计，Pydantic 模型不需要继承来表达简单差异

### 决策 3: 等待时间配置策略

**选择**: 基于按钮类型的配置化等待时间

**实现**:
```python
ACTION_WAIT_TIMES = {
    ExpectedAction.NAVIGATE: 1.0,   # 页面切换需要更长等待
    ExpectedAction.TOGGLE: 0.3,     # 开关切换很快
    ExpectedAction.ACTION: 0.5,     # 默认等待
    ExpectedAction.NONE: 0,         # 不等待
}

def get_wait_time(self, item: MenuItem) -> float:
    return ACTION_WAIT_TIMES.get(
        item.expected_action,
        self.config.wait_time  # 回退到默认配置
    )
```

**替代方案**: 让 AI 返回建议的等待时间
- 拒绝原因：增加 AI 负担，可能不准确

### 决策 4: 验证策略差异化

**选择**: 根据 expected_action 选择不同的验证逻辑

**实现**:
```python
def _verify_click_result(self, item: MenuItem, before: PageAnalysis, after: PageAnalysis) -> ClickResult:
    if item.expected_action == ExpectedAction.NAVIGATE:
        # 验证路径变化
        if after.current_path != before.current_path:
            return ClickResult.PAGE_JUMP
        return ClickResult.NO_CHANGE
    
    elif item.expected_action == ExpectedAction.TOGGLE:
        # 验证状态变化（items 变化或开关状态变化）
        if len(after.items) != len(before.items):
            return ClickResult.NORMAL
        # 检查特定开关的状态变化...
        return ClickResult.NO_CHANGE
    
    else:  # ACTION or NONE
        # 通用验证
        return self._generic_verify(before, after)
```

**替代方案**: 统一验证逻辑
- 拒绝原因：无法发挥按钮类型区分的价值

### 决策 5: AI Prompt 增强方式

**选择**: 在 PROMPT_STRUCTURE 中增加按钮类型判断指令

**Prompt 增加内容**:
```
对于每个元素，请判断其类型和预期行为：
- type: menu_item/tab/switch/toggle/button/readonly 等
- expected_action: navigate/toggle/action/none
- expects_page_change: true/false
- expects_state_change: true/false

示例：
{
  "name": "互联",
  "type": "tab",
  "expected_action": "navigate",
  "expects_page_change": true,
  "expects_state_change": false,
  "x": 0.28, "y": 0.06
}
```

**替代方案**: 创建单独的类型识别 Prompt
- 拒绝原因：增加 AI 调用次数，影响性能

### 决策 6: 向后兼容策略

**选择**: 新增字段使用默认值，现有状态文件无需修改

**实现**:
```python
class MenuItem(BaseModel):
    # 现有字段（保持不变）
    name: str
    type: MenuItemType = MenuItemType.ITEM
    coordinate: Coordinate
    parent: Optional[str] = None
    
    # 新增字段（有默认值）
    expected_action: ExpectedAction = ExpectedAction.ACTION
    expects_page_change: bool = False
    expects_state_change: bool = False
```

**迁移路径**:
- 旧状态文件加载时，新字段自动使用默认值
- 新遍历生成的状态文件包含完整字段
- 无需手动迁移

## 数据流设计

### 增强后的点击流程

```
┌─────────────────────────────────────────────────────────┐
│                    选择下一个元素                         │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
        ┌──────────────────────────────┐
        │   读取按钮类型和预期行为       │
        │   expected_action,            │
        │   expects_page_change         │
        └──────────────┬───────────────┘
                       │
                       ▼
        ┌──────────────────────────────┐
        │   根据类型设置等待时间         │
        │   navigate: 1.0s              │
        │   toggle: 0.3s                │
        │   action: 0.5s                 │
        └──────────────┬───────────────┘
                       │
                       ▼
        ┌──────────────────────────────┐
        │         点击元素               │
        └──────────────┬───────────────┘
                       │
                       ▼
        ┌──────────────────────────────┐
        │       等待（差异化时间）        │
        └──────────────┬───────────────┘
                       │
                       ▼
        ┌──────────────────────────────┐
        │         截图分析               │
        └──────────────┬───────────────┘
                       │
                       ▼
        ┌──────────────────────────────┐
        │   根据预期行为验证结果         │
        │   navigate → 检查路径变化       │
        │   toggle → 检查状态变化        │
        │   action → 检查弹窗/跳转       │
        └──────────────┬───────────────┘
                       │
                       ▼
              返回细化的 ClickResult
```

## 类图更新

```python
# 扩展后的 MenuItem
class MenuItem(BaseModel):
    # 基础信息
    name: str
    type: MenuItemType
    coordinate: Coordinate
    parent: Optional[str] = None
    description: Optional[str] = None
    
    # 新增：预期行为
    expected_action: ExpectedAction = ExpectedAction.ACTION
    expects_page_change: bool = False
    expects_state_change: bool = False

# 新增枚举
class ExpectedAction(str, Enum):
    NAVIGATE = "navigate"
    TOGGLE = "toggle"
    ACTION = "action"
    NONE = "none"

# 扩展后的 MenuItemType
class MenuItemType(str, Enum):
    # 现有类型（保留兼容）
    ITEM = "item"
    SWITCH = "switch"
    BUTTON = "button"
    ICON = "icon"
    TEXT = "text"
    
    # 新增类型
    MENU_ITEM = "menu_item"
    TAB = "tab"
    BACK_BUTTON = "back_button"
    TOGGLE = "toggle"
    LINK = "link"
    READONLY = "readonly"
```

## TraversalEngine 方法更新

```python
class TraversalEngine:
    # 新增方法
    def _get_wait_time(self, item: MenuItem) -> float:
        """根据按钮类型获取等待时间"""
        base_time = self.config.wait_time
        
        if item.expected_action == ExpectedAction.NAVIGATE:
            return max(base_time, 1.0)  # 页面切换至少 1s
        elif item.expected_action == ExpectedAction.TOGGLE:
            return min(base_time, 0.3)  # 开关切换最多 0.3s
        elif item.expected_action == ExpectedAction.NONE:
            return 0.1  # 只读元素快速检测
        return base_time
    
    def _verify_by_expected_action(
        self, 
        item: MenuItem,
        before: PageAnalysis, 
        after: PageAnalysis
    ) -> ClickResult:
        """根据预期行为验证点击结果"""
        if item.expected_action == ExpectedAction.NAVIGATE:
            return self._verify_navigate(before, after)
        elif item.expected_action == ExpectedAction.TOGGLE:
            return self._verify_toggle(before, after)
        else:
            return self._verify_generic(before, after)
    
    def _verify_navigate(self, before: PageAnalysis, after: PageAnalysis) -> ClickResult:
        """验证导航类点击"""
        if after.current_path != before.current_path:
            return ClickResult.PAGE_JUMP
        if after.is_popup:
            return ClickResult.POPUP
        return ClickResult.NO_CHANGE
    
    def _verify_toggle(self, before: PageAnalysis, after: PageAnalysis) -> ClickResult:
        """验证开关类点击"""
        # 开关不期望页面跳转
        if after.current_path != before.current_path:
            # 意外跳转，可能是副作用
            return ClickResult.PAGE_JUMP
        
        # 检查状态变化
        if self._has_toggle_state_changed(before, after):
            return ClickResult.NORMAL
        
        return ClickResult.NO_CHANGE
    
    # 更新现有方法
    def _tap_and_wait(self, coord: Coordinate, item: MenuItem) -> None:
        """点击元素并等待（差异化等待时间）"""
        self.adb.tap(coord.x, coord.y)
        wait_time = self._get_wait_time(item)
        time.sleep(wait_time)
```

## Risks / Trade-offs

### 风险 1: AI 类型判断不准确

**风险**: AI 可能误判按钮类型，导致错误的等待时间和验证策略

**缓解措施**:
- Prompt 中提供清晰的类型定义和示例
- 使用默认回退策略：当不确定时使用 ACTION 类型
- 验证逻辑保持容错性：即使类型错误也能正常处理

### 风险 2: 向后兼容性问题

**风险**: 旧状态文件缺少新字段，可能导致加载异常

**缓解措施**:
- 所有新增字段使用默认值
- Pydantic 自动填充默认值
- 测试旧状态文件加载

### 风险 3: 性能影响

**风险**: 增加的字段和逻辑可能影响遍历速度

**缓解措施**:
- 等待时间优化总体可能减少遍历时间
- 验证逻辑分支不影响性能
- 不增加 AI 调用次数

### 权衡 1: Prompt 复杂度 vs 准确性

**选择**: 适度增加 Prompt 复杂度

**权衡**:
- 更复杂的 Prompt → 更准确的类型判断
- 但可能增加 AI 错误率

**平衡点**: 提供清晰示例，避免过度复杂

### 权衡 2: 类型数量 vs 维护成本

**选择**: 保持适度数量的类型（10 种以内）

**权衡**:
- 更多类型 → 更精细的控制
- 但增加维护和 AI 理解成本

**平衡点**: 覆盖主要场景，不过度细分

## Migration Plan

### 阶段 1: 数据模型扩展
1. 更新 `MenuItemType` 枚举
2. 添加 `ExpectedAction` 枚举
3. 扩展 `MenuItem` 模型（带默认值）

### 阶段 2: Vision Prompt 更新
1. 更新 `PROMPT_STRUCTURE` 模板
2. 添加类型判断指令和示例
3. 测试 AI 返回质量

### 阶段 3: 遍历逻辑更新
1. 实现 `_get_wait_time()` 方法
2. 实现差异化验证方法
3. 更新 `_click_item()` 调用新方法

### 阶段 4: 测试和验证
1. 单元测试：各类型按钮的处理
2. 集成测试：完整遍历流程
3. 性能测试：遍历时间对比

### 回滚策略

如果新功能出现问题：
1. Prompt 可回退到旧版本（通过配置开关）
2. 代码中保留旧逻辑作为回退路径
3. 新字段默认值确保兼容性

## Open Questions

1. **等待时间最佳值**: 当前设定的 1.0s/0.3s 是否合适？可能需要实际测试调整
2. **AI 判断准确率**: AI 对按钮类型的判断准确率如何？需要收集数据
3. **异常处理**: 当预期行为与实际不符时，应该如何处理？
4. **只读元素检测**: 只读元素是否应该完全跳过，还是快速检测一次？

这些问题将在实现过程中通过测试和数据收集来解决。

---

## 分阶段实现路线图

### 总体策略

采用渐进式实现，每个阶段建立在前一阶段基础上，降低风险并便于验证。

### 第一阶段：基础按钮类型区分（当前实现）

**目标**: 实现基本的按钮类型分类和差异化处理

**范围**:
- ✅ 扩展按钮类型（MENU_ITEM, TAB, BACK_BUTTON, TOGGLE, LINK, READONLY）
- ✅ 添加预期行为字段（expected_action, expects_page_change, expects_state_change）
- ✅ 实现差异化等待时间（navigate 1.0s / toggle 0.3s / action 0.5s）
- ✅ 实现差异化验证策略

**支持的UI元素**:
| 元素 | 类型 | 点击方式 | 预期行为 |
|------|------|----------|----------|
| 菜单项 | MENU_ITEM | Tap | NAVIGATE |
| 标签页 | TAB | Tap | NAVIGATE |
| 开关 | SWITCH/TOGGLE | Tap | TOGGLE |
| 普通按钮 | BUTTON | Tap | ACTION |
| 返回按钮 | BACK_BUTTON | Tap | NAVIGATE |
| 链接 | LINK | Tap | ACTION |
| 只读元素 | READONLY | - | NONE |

**不支持（后续阶段）**:
- ❌ 输入框（需要输入操作）
- ❌ 滑块（需要拖拽操作）
- ❌ 可滑动列表（需要滑动操作）
- ❌ 长按菜单（需要长按操作）
- ❌ 下拉刷新（需要滑动操作）

### 第二阶段：点击方式扩展（规划中）

**目标**: 支持多种点击方式和手势操作

**新增内容**:
```python
class ClickType(str, Enum):
    TAP = "tap"                # 单击
    LONG_PRESS = "long_press"  # 长按
    DOUBLE_TAP = "double_tap"  # 双击
    SWIPE = "swipe"            # 滑动
    DRAG = "drag"              # 拖拽
```

**新增UI元素类型**:
| 元素 | 类型 | 点击方式 | 参数 |
|------|------|----------|------|
| 输入框 | INPUT | TAP + 输入 | 输入内容 |
| 长按菜单项 | CONTEXT_MENU | LONG_PRESS | duration: 2s |
| 可滑动列表 | SCROLLABLE_LIST | SWIPE | direction: up/down |
| 下拉刷新 | PULL_REFRESH | SWIPE | direction: down, distance: long |
| 可展开项 | EXPANDABLE | TAP/DOUBLE_TAP | - |

**新增ADB方法**:
- `long_press(x, y, duration)` - 长按操作
- `swipe(start_x, start_y, end_x, end_y, duration)` - 滑动操作
- `drag(start_x, start_y, end_x, end_y, duration)` - 拖拽操作

**新增MenuItem字段**:
```python
click_type: ClickType = ClickType.TAP
click_duration: float = 0.0        # 长按持续时间
drag_target: Optional[Coordinate] = None  # 拖拽目标
swipe_direction: Optional[str] = None      # 滑动方向
```

### 第三阶段：复杂交互支持（规划中）

**目标**: 支持复杂的多步骤交互和输入操作

**新增内容**:
- 文本输入支持（输入框填写）
- 滑块拖拽支持（数值调整）
- 列表滚动支持（内容加载）
- 多点触控支持（缩放、旋转）
- 复合手势支持（长按+拖拽）

**新增UI元素类型**:
| 元素 | 类型 | 交互方式 | 验证策略 |
|------|------|----------|----------|
| 滑块 | SLIDER | DRAG | 数值变化检测 |
| 进度条 | PROGRESS_BAR | TAP | 跳转到位置 |
| 可缩放区域 | PINCH_AREA | PINCH | 缩放比例检测 |
| 搜索框 | SEARCH_BOX | TAP + INPUT + ENTER | 搜索结果检测 |
| 选择器 | PICKER | TAP + SELECT | 选择值检测 |

**新增验证策略**:
- 数值变化验证（滑块、进度条）
- 文本内容验证（输入框、搜索框）
- 列表长度验证（滚动加载）
- 位置变化验证（拖拽、缩放）

### 阶段间依赖关系

```
第一阶段（基础类型）
    ↓ 建立类型区分基础
第二阶段（点击方式）
    ↓ 扩展交互能力
第三阶段（复杂交互）
    ↓ 完整覆盖所有UI元素
```

### 各阶段优先级

| 阶段 | 优先级 | 预估工作量 | 依赖 |
|------|--------|-----------|------|
| 第一阶段 | P0（高） | 2-3天 | 现有代码 |
| 第二阶段 | P1（中） | 3-5天 | 第一阶段 |
| 第三阶段 | P2（低） | 5-7天 | 第二阶段 |

### 验收标准

**第一阶段验收**:
- [ ] 所有基础按钮类型可正确识别
- [ ] 等待时间根据类型正确应用
- [ ] 验证策略根据预期行为正确执行
- [ ] 旧状态文件可正常加载
- [ ] 遍历性能不降低

**第二阶段验收**:
- [ ] 长按操作可正确执行
- [ ] 滑动操作可正确执行
- [ ] 输入框可识别和输入
- [ ] 所有新操作有对应的验证策略

**第三阶段验收**:
- [ ] 滑块拖拽可执行和验证
- [ ] 列表滚动可执行和验证
- [ ] 复杂手势可正确处理
- [ ] 多步骤交互可正确执行
