# PRD V6.9.2: 仿真测试增强 - 问题发现能力提升

**版本**: V6.9.2
**创建日期**: 2026-06-07
**状态**: 草案
**关联**: [PRD_V6_9_1_dynamic_matching](./PRD_V6_9_1_dynamic_matching.md)

---

## 1. 概述

### 1.1 背景

在 V6.9.1 动态匹配功能实现后，通过运行仿真测试发现了一个关键问题：**测试可能"通过"但实际行为完全不符合预期**。

**核心问题现象**：
- MockVisionService 始终返回固定的页面数据
- AUTO_ESCAPE 对同一按钮重复点击3次，但页面没有切换
- 动态生成的子节点没有被独立执行（所有动作的 node_id 都是 "root"）
- 测试显示 COMPLETED 状态，但实际行为异常

**根本原因**：
1. Mock 服务缺少状态管理能力，无法模拟页面切换
2. 验证逻辑过于宽松，只检查完成状态不检查执行质量
3. Trace 记录不完整，无法验证实际执行行为

### 1.2 目标

**核心目标**：通过仿真测试发现原有设计或代码的问题，而不是为了测试通过而通过。

**具体目标**：
1. 增强 Mock 服务的状态管理能力，能够模拟真实的页面切换
2. 实现基于期望行为的验证逻辑，验证实际执行序列
3. 增强 Trace 记录，记录页面切换和动态节点生命周期
4. 自动检测异常执行模式（无限循环、重复点击、未访问节点）

### 1.3 范围

**包含**：
- StateFixture 状态固件设计与实现
- StatefulMockVisionService 状态管理实现
- BehaviorValidator 行为验证器实现
- ProblemDetector 问题检测器实现
- 增强Trace记录（页面切换、动态节点生命周期）
- 期望行为定义框架

**不包含**：
- 真实设备测试（属于端到端测试范畴）
- 性能测试优化
- CI/CD 集成（可后续扩展）

### 1.4 设计考量与约束

#### 1.4.1 MockVisionService 兼容性策略

**现状**：现有系统已有 `MockVisionService`（基于虚拟页面JSON），它通过 `PageAnalyzer` 返回 `PageAnalysis` 对象。

**兼容性策略**：
- **共存模式**：`StatefulMockVisionService` 作为新的独立实现，用于需要页面切换的场景
- **迁移路径**：提供迁移工具将现有 virtual_pages JSON 转换为 StateFixture YAML
- **使用场景区分**：
  - `MockVisionService`：静态页面分析，无需状态变化（V6.9.2之前场景）
  - `StatefulMockVisionService`：需要页面切换和导航历史的场景（V6.9.2及之后）

**字段映射关系**：
```python
# 现有 PageAnalysis
class PageAnalysis(BaseModel):
    items: list[MenuItem]  # 注意是 items，不是 menu_items

class MenuItem(BaseModel):
    name: str  # 注意是 name，不是 text
    type: MenuItemType  # 枚举类型
    coordinate: Coordinate
    expected_action: ExpectedAction
```

#### 1.4.2 YAML 格式扩展性

**当前范围**：V6.9.2 阶段支持简单到中等复杂度场景（2-5个页面，线性或简单分支）

**复杂场景处理**（后续扩展）：
- 支持 `!include` 指令引用其他 fixture 文件
- 支持 `extends` 字段继承基础 fixture
- 支持条件跳转（通过 `condition` 字段）

**当前阶段限制**：
- 不支持循环依赖的页面切换
- 不支持运行时动态生成的页面状态

#### 1.4.3 可配置的检测阈值

**硬编码问题**：`max_action_repeats` 和 `max_loop_depth` 在 PRD 初稿中是硬编码的。

**解决方案**：
```python
class ProblemDetectorConfig(BaseModel):
    """问题检测器配置"""
    max_action_repeats: int = 3  # 最大动作重复次数
    max_loop_depth: int = 5  # 最大循环深度
    enable_infinite_loop_detection: bool = True
    enable_unvisited_node_detection: bool = True
    enable_repeated_action_detection: bool = True
    enable_state_machine_error_detection: bool = True

class ProblemDetector:
    def __init__(self, config: Optional[ProblemDetectorConfig] = None):
        self.config = config or ProblemDetectorConfig()
```

**配置来源**：
- 默认配置（代码中的默认值）
- 测试类级别的配置（fixture级别的 pytest fixture）
- 场景级别的配置（ExpectedBehavior 中的覆盖）

#### 1.4.4 动态节点匹配策略

**启发式匹配的局限性**：原设计的 `_is_dynamic_match` 通过字符串包含判断，可能误判。

**改进策略**：
1. **精确匹配优先**：优先使用 element_id 进行精确匹配
2. **降级到启发式**：当精确匹配失败时，使用启发式匹配并标记为 `fuzzy_match`
3. **误判防护**：在验证结果中区分 `exact_match` 和 `fuzzy_match`

```python
@dataclass
class MatchResult:
    """匹配结果"""
    matched: bool
    match_type: Literal["exact", "fuzzy", "none"]
    confidence: float  # 0.0 - 1.0
    reason: Optional[str] = None
```

#### 1.4.5 PageAnalysis 字段映射验证

**字段映射问题**：
- `PageAnalysis.items` → DynamicMatcher 期望的 menu_item 列表
- `MenuItem.name` → DynamicMatcher 期望的 `text` 字段
- `MenuItem.type` → DynamicMatcher 期望的 `type` 字符串

**验证要求**：在 Phase 1 实施时，必须编写映射验证测试：

```python
def test_page_analysis_to_menu_item_mapping():
    """验证 PageAnalysis 到 DynamicMatcher 输入格式的映射"""
    # 创建 StatefulMockVisionService
    fixture = StateFixture.from_yaml("...")
    vision = StatefulMockVisionService(fixture)

    # 获取 PageAnalysis
    page_analysis = vision.analyze_screenshot(b"dummy")

    # 验证可以转换为 DynamicMatcher 期望的格式
    menu_items_for_matcher = [
        {
            "type": item.type.value,  # MenuItemType -> string
            "text": item.name,  # name -> text
            "index": i,
            "coordinate": item.coordinate.dict(),
            "expected_action": item.expected_action.value
        }
        for i, item in enumerate(page_analysis.items)
    ]

    # 验证 DynamicMatcher 可以处理这个格式
    matcher = DynamicMatcher(...)
    results = matcher.match_all(menu_items_for_matcher)
    assert results is not None
```

---

## 2. 问题分析

### 2.1 当前Mock服务的限制

#### 2.1.1 MockVisionService 固定返回

**问题代码**：
```python
def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
    # 始终返回当前path对应的页面，没有状态变化
    path_key = "/".join(self._current_path) if self._current_path else "home"
    if path_key in self._virtual_pages:
        return self._build_page_analysis(path_key)
```

**影响**：
- 无法模拟页面切换
- AUTO_ESCAPE 无法验证页面是否真的变化了
- 测试不能真实反映导航行为

#### 2.1.2 缺少页面切换模拟

**问题**：
- MockActionExecutor 记录了动作执行，但没有改变 MockVisionService 的状态
- 点击按钮后，下一次 analyze_screenshot 仍然返回原来的页面
- 导致状态机陷入重试循环

### 2.2 验证逻辑过于宽松

**当前验证方式**：
```python
def test_simulation():
    result = runner.run()
    assert result.completion_reason == "COMPLETED"  # 只检查是否完成
```

**问题**：
- 无法验证执行的动作序列
- 无法验证页面切换是否正确
- 无法验证节点访问是否符合预期
- 异常执行模式无法被检测

### 2.3 Trace记录不完整

**当前Trace记录**：
- 记录了 execution span，但缺少页面切换信息
- 动态节点生命周期没有记录
- 状态机决策过程没有记录

**影响**：
- 无法通过trace验证页面切换
- 无法追踪动态节点的创建和执行
- 难以调试状态机异常行为

---

## 3. 架构设计

### 3.1 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                     仿真测试问题发现架构                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐    ┌──────────────────┐                  │
│  │  TraversalPlan  │───▶│  StateFixture    │                  │
│  │    (遍历计划)    │    │   (状态固件)      │                  │
│  └─────────────────┘    └──────────────────┘                  │
│         │                        │                              │
│         ▼                        ▼                              │
│  ┌─────────────────┐    ┌──────────────────┐                  │
│  │  GraphEngine    │───▶│ StatefulMock     │                  │
│  │  (图引擎)        │    │  VisionService   │                  │
│  └─────────────────┘    └──────────────────┘                  │
│         │                        │                              │
│         │        ┌───────────────┴───────────────┐             │
│         │        │                               │             │
│         ▼        ▼                               ▼             │
│  ┌──────────────────────────────────────────────────┐        │
│  │            Enhanced Trace Recording              │        │
│  │  (增强追踪 - 状态转换/页面切换/动态节点生命周期)    │        │
│  └──────────────────────────────────────────────────┘        │
│                              │                                 │
│                              ▼                                 │
│  ┌──────────────────────────────────────────────────┐        │
│  │         BehaviorValidator                        │        │
│  │  (行为验证器 - 期望 vs 实际动作序列分析)           │        │
│  └──────────────────────────────────────────────────┘        │
│                              │                                 │
│                              ▼                                 │
│  ┌──────────────────────────────────────────────────┐        │
│  │         ProblemDetector                          │        │
│  │  (问题检测器 - 无限循环/未访问节点/异常模式)       │        │
│  └──────────────────────────────────────────────────┘        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 核心组件关系

```
StateFixture ──配置──▶ StatefulMockVisionService
      │                        │
      │         ┌──────────────┴───────────────┐
      ▼         ▼                              ▼
ExpectedBehavior  GraphEngine  ──产生──▶ SimulationResult
      │         │                              │
      │         └──────────────┬───────────────┘
      ▼                        ▼
  BehaviorValidator  ◀──读取── EnhancedTrace
      │                        │
      ▼                        ▼
  ValidationResult      ProblemDetector
                               │
                               ▼
                         ProblemReport
```

---

## 4. 详细设计

### 4.1 Phase 1: StateFixture 和状态管理 (P0)

#### 4.1.1 StateFixture 设计

**文件位置**: `src/simulation/state_fixture.py`

```python
from dataclasses import dataclass
from typing import Dict, Optional, List
from pathlib import Path
import yaml


@dataclass
class PageElement:
    """页面元素定义"""
    id: str
    type: str
    text: str
    coordinate: Dict[str, float]
    action_target: Optional[str] = None  # 点击后跳转的目标页面ID


@dataclass
class PageState:
    """页面状态定义"""
    id: str
    page_name: str
    elements: List[PageElement]
    is_complete: bool = False  # 标记页面是否完成（用于状态机）


@dataclass
class PageTransition:
    """页面切换规则"""
    trigger: str  # 触发元素ID
    from_page: str  # 源页面ID
    to_page: str  # 目标页面ID
    action: str = "click"  # 触发动作


class StateFixture:
    """状态固件 - 管理页面状态和切换规则"""

    def __init__(self, pages: Dict[str, PageState], 
                 transitions: Dict[str, PageTransition]):
        self._pages = pages
        self._transitions = transitions
        self._index_by_element = self._build_element_index()

    def _build_element_index(self) -> Dict[str, PageTransition]:
        """构建元素ID到切换规则的索引"""
        index = {}
        for trans in self._transitions.values():
            index[trans.trigger] = trans
        return index

    @classmethod
    def from_yaml(cls, yaml_path: Path) -> "StateFixture":
        """从YAML文件加载fixture"""
        with open(yaml_path, 'r', encoding='utf-8') as f:
            data = yaml.safe_load(f)

        pages = {}
        for page_id, page_data in data.get('pages', {}).items():
            elements = [
                PageElement(**elem) for elem in page_data.get('elements', [])
            ]
            pages[page_id] = PageState(
                id=page_id,
                page_name=page_data.get('page_name', page_id),
                elements=elements,
                is_complete=page_data.get('is_complete', False)
            )

        transitions = {}
        for trans_id, trans_data in data.get('transitions', {}).items():
            transitions[trans_id] = PageTransition(**trans_data)

        return cls(pages, transitions)

    def get_page(self, page_id: str) -> Optional[PageState]:
        """获取页面状态"""
        return self._pages.get(page_id)

    def get_transition(self, element_id: str) -> Optional[PageTransition]:
        """获取元素触发的页面切换"""
        return self._index_by_element.get(element_id)

    def get_initial_page(self) -> str:
        """获取初始页面ID"""
        # 默认第一个页面为初始页面，或通过标记指定
        for page_id, page in self._pages.items():
            return page_id
        raise ValueError("No pages defined in fixture")

    def validate(self) -> List[str]:
        """验证fixture配置的完整性"""
        errors = []

        # 验证切换规则的目标页面是否存在
        for trans_id, trans in self._transitions.items():
            if trans.from_page not in self._pages:
                errors.append(f"Transition {trans_id} from_page '{trans.from_page}' not found")
            if trans.to_page not in self._pages:
                errors.append(f"Transition {trans_id} to_page '{trans.to_page}' not found")

        # 验证切换规则的触发元素是否存在
        for trans_id, trans in self._transitions.items():
            from_page = self._pages.get(trans.from_page)
            if from_page:
                element_ids = [elem.id for elem in from_page.elements]
                if trans.trigger not in element_ids:
                    errors.append(f"Transition {trans_id} trigger '{trans.trigger}' not found in page '{trans.from_page}'")

        return errors
```

**Fixture YAML格式**：

```yaml
# tests/v6/fixtures/simple_two_page.yaml
pages:
  home:
    page_name: HomeScreen
    elements:
      - id: btn1
        type: button
        text: Button1
        coordinate: {x: 0.3, y: 0.5}
        action_target: detail
      - id: btn2
        type: button
        text: Button2
        coordinate: {x: 0.7, y: 0.5}
        action_target: detail

  detail:
    page_name: DetailScreen
    elements:
      - id: back_btn
        type: button
        text: Back
        coordinate: {x: 0.5, y: 0.9}
        action_target: home
    is_complete: true

transitions:
  home_btn1_click:
    trigger: btn1
    from_page: home
    to_page: detail
    action: click

  home_btn2_click:
    trigger: btn2
    from_page: home
    to_page: detail
    action: click

  detail_back_click:
    trigger: back_btn
    from_page: detail
    to_page: home
    action: click
```

#### 4.1.2 StatefulMockVisionService 设计

**文件位置**: `src/simulation/stateful_mock_vision.py`

```python
from typing import Dict, Optional, List
from src.state.content_tree import (
    PageAnalysis,
    MenuItem,
    MenuItemType,
    ExpectedAction,
    Coordinate,
    Direction,
    MenuInfo,
)
from src.ai.vision_service import VisionService


class StatefulMockVisionService(VisionService):
    """具备状态管理能力的Mock视觉服务

    关键设计决策：
    - 直接返回现有的 PageAnalysis 对象，确保与 GraphEngine 兼容
    - MenuItem 字段正确映射：fixture.text -> MenuItem.name
    - 支持页面切换和导航历史跟踪
    """

    def __init__(self, fixture: StateFixture):
        self._fixture = fixture
        self._current_page = fixture.get_initial_page()
        self._navigation_history: List[str] = []
        self._history_depth = 10  # 最大历史记录深度

    @property
    def current_page_id(self) -> str:
        """获取当前页面ID"""
        return self._current_page

    @property
    def navigation_history(self) -> List[str]:
        """获取导航历史"""
        return self._navigation_history.copy()

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """返回当前页面的分析结果

        关键：返回标准 PageAnalysis 对象，确保与现有代码兼容。
        """
        page_state = self._fixture.get_page(self._current_page)
        if page_state is None:
            raise ValueError(f"Page not found: {self._current_page}")

        return self._build_page_analysis(page_state)

    def _build_page_analysis(self, page_state: PageState) -> PageAnalysis:
        """构建PageAnalysis对象

        字段映射说明：
        - fixture.text -> MenuItem.name (注意：不是 text 字段)
        - fixture.type -> MenuItem.type (MenuItemType 枚举)
        - fixture.coordinate -> Coordinate 对象
        - fixture.action_target -> MenuItem.expects_page_change 推断
        """
        items = []
        for elem in page_state.elements:
            # 字符串类型转换为 MenuItemType 枚举
            item_type = self._parse_element_type(elem.type)

            # 推断 expected_action
            expected_action = self._infer_expected_action(elem, item_type)

            menu_item = MenuItem(
                name=elem.text,  # 注意：fixture 的 text 对应 MenuItem 的 name
                type=item_type,
                coordinate=Coordinate(**elem.coordinate),
                expected_action=expected_action,
                expects_page_change=bool(elem.action_target),
                expects_state_change=(item_type == MenuItemType.SWITCH)
            )
            items.append(menu_item)

        # 构建完整的 PageAnalysis
        return PageAnalysis(
            level1_dir=Direction.BOTTOM,
            level1_menus=[],  # 简化：不模拟多级菜单
            level2_dir=Direction.RIGHT,
            level2_menus=[],
            current_path=[self._current_page],
            items=items,  # 注意：是 items 字段，不是 menu_items
            is_popup=False,
            is_end_of_list=page_state.is_complete
        )

    def _parse_element_type(self, type_str: str) -> MenuItemType:
        """将fixture中的类型字符串转换为MenuItemType枚举"""
        type_mapping = {
            "button": MenuItemType.BUTTON,
            "menu_item": MenuItemType.MENU_ITEM,
            "switch": MenuItemType.SWITCH,
            "toggle": MenuItemType.TOGGLE,
            "tab": MenuItemType.TAB,
            "back_button": MenuItemType.BACK_BUTTON,
            "icon": MenuItemType.ICON,
            "link": MenuItemType.LINK,
            "text": MenuItemType.TEXT,
            "readonly": MenuItemType.READONLY,
            "item": MenuItemType.ITEM,
        }
        return type_mapping.get(type_str.lower(), MenuItemType.ITEM)

    def _infer_expected_action(self, elem: PageElement,
                                item_type: MenuItemType) -> ExpectedAction:
        """推断期望的action类型"""
        # 如果有 action_target，说明会导航
        if elem.action_target:
            return ExpectedAction.NAVIGATE

        # 根据 type 推断
        if item_type == MenuItemType.SWITCH:
            return ExpectedAction.TOGGLE

        if item_type in (MenuItemType.BUTTON, MenuItemType.TOGGLE):
            return ExpectedAction.ACTION

        return ExpectedAction.NONE

    def simulate_action(self, element_id: str, action: str = "click") -> bool:
        """模拟动作执行并切换页面

        Args:
            element_id: 要操作的元素ID
            action: 动作类型（默认为click）

        Returns:
            是否成功执行并切换页面
        """
        transition = self._fixture.get_transition(element_id)
        if transition is None:
            return False

        if transition.action != action:
            return False

        # 检查当前页面是否匹配
        if transition.from_page != self._current_page:
            return False

        # 执行切换
        self._navigation_history.append(self._current_page)
        if len(self._navigation_history) > self._history_depth:
            self._navigation_history.pop(0)

        self._current_page = transition.to_page
        return True

    def navigate_back(self) -> bool:
        """模拟返回操作"""
        if not self._navigation_history:
            return False

        self._current_page = self._navigation_history.pop()
        return True

    def reset_to_initial(self):
        """重置到初始页面"""
        self._current_page = self._fixture.get_initial_page()
        self._navigation_history.clear()
```

#### 4.1.3 StatefulMockActionExecutor 设计

**文件位置**: `src/simulation/stateful_mock_action.py`

```python
from typing import Dict, Any, Optional, List
from src.traversal.executor import ActionExecutor, ExecutionContext, ExecutionResult


class StatefulMockActionExecutor(ActionExecutor):
    """与StatefulMockVisionService配合的Mock执行器"""

    def __init__(self, vision_service: StatefulMockVisionService):
        self._vision = vision_service
        self._history: List[Dict[str, Any]] = []

    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """执行动作并更新视觉服务状态"""
        operation = context.operation
        action_type = operation.get("action", "unknown")
        target = operation.get("target")

        result = ExecutionResult(success=False, error=None)
        trace_data = {
            "action_type": action_type,
            "target": str(target) if target else None,
            "node_id": context.node_id,
            "success": False
        }

        if action_type == "click" and target:
            # 尝试通过element_id或text查找元素
            element_id = self._extract_element_id(target)
            if element_id:
                success = self._vision.simulate_action(element_id, "click")
                result = ExecutionResult(success=success, error=None)
                trace_data["success"] = success
                trace_data["element_id"] = element_id

        elif action_type == "back":
            success = self._vision.navigate_back()
            result = ExecutionResult(success=success, error=None)
            trace_data["success"] = success

        elif action_type == "no_action":
            result = ExecutionResult(success=True, error=None)
            trace_data["success"] = True

        else:
            result = ExecutionResult(success=False, error=f"Unknown action: {action_type}")
            trace_data["error"] = str(result.error)

        self._history.append(trace_data)
        return result

    def _extract_element_id(self, target: Any) -> Optional[str]:
        """从target对象中提取element_id"""
        if hasattr(target, 'element_id'):
            return target.element_id
        if isinstance(target, str):
            return target
        if isinstance(target, dict):
            return target.get('element_id') or target.get('id')
        return None

    def get_history(self) -> List[Dict[str, Any]]:
        """获取执行历史"""
        return self._history.copy()

    def clear_history(self):
        """清空历史"""
        self._history.clear()
```

#### 4.1.4 集成到SimulationRunner

**修改**: `src/simulation/runner.py`

```python
class SimulationRunner:
    """仿真运行器 - 支持状态管理"""

    def __init__(self, fixture: StateFixture, plan: TraversalPlan):
        self.fixture = fixture
        self.plan = plan

        # 使用状态管理的Mock服务
        self.vision = StatefulMockVisionService(fixture)
        self.action = StatefulMockActionExecutor(self.vision)

        # 其他初始化...
        self.storage = FileStorage(trace_dir)
        self.recorder = TraceRecorder(self.storage)

    def run(self) -> SimulationResult:
        """运行仿真测试"""
        # 初始化引擎（使用状态管理的Mock服务）
        engine = GraphTraversalEngine(
            plan=self.plan,
            vision_service=self.vision,
            action_executor=self.action,
            trace_recorder=self.recorder
        )

        # 运行遍历
        start_time = time.time()
        final_state = engine.run_traversal()
        elapsed = time.time() - start_time

        # 获取trace
        trace_id = self.recorder.current_trace_id
        trace_nodes = self.storage.read(trace_id)

        return SimulationResult(
            trace_id=trace_id,
            completion_reason=final_state.value,
            elapsed_seconds=elapsed,
            trace_nodes=trace_nodes,
            final_state=final_state,
            navigation_history=self.vision.navigation_history,
            action_history=self.action.get_history()
        )
```

---

### 4.2 Phase 2: 增强Trace记录 (P0)

#### 4.2.1 新增Trace节点类型

**文件位置**: `src/trace/models.py` (扩展现有模型)

```python
@dataclass
class PageTransitionSpan(SpanNode):
    """页面切换Trace记录"""
    span_type: str = "page_transition"
    from_page: str = ""
    to_page: str = ""
    trigger_element: str = ""
    action: str = "click"

    def __post_init__(self):
        if self.span_type != "page_transition":
            raise ValueError(f"Expected span_type='page_transition', got '{self.span_type}'")


@dataclass
class DynamicNodeLifecycle(SpanNode):
    """动态节点生命周期Trace记录"""
    span_type: str = "dynamic_lifecycle"
    event: str = ""  # created/matched/pushed/executed/popped
    node_id: str = ""
    parent_id: str = ""
    match_rule_id: str = ""
    element_id: str = ""

    def __post_init__(self):
        if self.span_type != "dynamic_lifecycle":
            raise ValueError(f"Expected span_type='dynamic_lifecycle', got '{self.span_type}'")


@dataclass
class StateDecisionSpan(SpanNode):
    """状态机决策Trace记录"""
    span_type: str = "state_decision"
    current_state: str = ""
    decision: str = ""  # retry/escape/complete/wait
    reason: str = ""
    context: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        if self.span_type != "state_decision":
            raise ValueError(f"Expected span_type='state_decision', got '{self.span_type}'")
```

#### 4.2.2 在GraphTraversalEngine中记录页面切换

**修改**: `src/traversal/graph_engine.py`

```python
class GraphTraversalEngine:
    def _execute_node_action(self, node: TraversalNode) -> ExecutionResult:
        """执行节点动作并记录页面切换"""

        # 记录执行前页面
        if isinstance(self.vision_service, StatefulMockVisionService):
            before_page = self.vision_service.current_page_id

        # 执行动作
        result = self.action_executor.execute(exec_ctx)

        # 检查页面是否切换
        if isinstance(self.vision_service, StatefulMockVisionService):
            after_page = self.vision_service.current_page_id

            if before_page != after_page:
                # 记录页面切换
                self.trace_recorder.record_span(PageTransitionSpan(
                    from_page=before_page,
                    to_page=after_page,
                    trigger_element=element_id,
                    action=operation.get("action", "click"),
                    status="success",
                    timestamp=time.time()
                ))

        return result
```

#### 4.2.3 记录动态节点生命周期

**修改**: `src/traversal/graph_engine.py`

```python
class GraphTraversalEngine:
    def _generate_dynamic_children(self, node: TraversalNode,
                                   page_analysis: PageAnalysis) -> List[TraversalNode]:
        """生成动态子节点并记录生命周期"""

        # 原有逻辑：生成动态子节点
        children = super()._generate_dynamic_children(node, page_analysis)

        # 记录每个动态子节点的创建
        for child in children:
            self.trace_recorder.record_span(DynamicNodeLifecycle(
                event="created",
                node_id=child.node_id,
                parent_id=node.node_id,
                match_rule_id=child.match_rule_id,
                element_id=child.bound_element_id,
                timestamp=time.time()
            ))

        return children

    def _push_node(self, node: TraversalNode):
        """推送节点到栈并记录"""
        super()._push_node(node)

        # 记录push事件（针对动态节点）
        if node.is_dynamic:
            self.trace_recorder.record_span(DynamicNodeLifecycle(
                event="pushed",
                node_id=node.node_id,
                parent_id=node.parent_id or "",
                timestamp=time.time()
            ))
```

---

### 4.3 Phase 3: 行为验证器 (P0)

#### 4.3.1 ExpectedBehavior 设计

**文件位置**: `src/simulation/expected_behavior.py`

```python
from dataclasses import dataclass, field
from typing import List, Dict, Set, Optional
from enum import Enum
from pathlib import Path
import yaml


class CompletionMode(Enum):
    """完成模式"""
    NORMAL = "normal"  # 正常完成
    EXCEPTION = "exception"  # 异常退出
    CANCELLED = "cancelled"  # 被取消
    TIMEOUT = "timeout"  # 超时


@dataclass
class ExpectedAction:
    """期望的动作定义"""
    action: str  # click/back/no_action/swipe
    node_id: str  # 执行节点ID
    target: Optional[str] = None  # 目标元素描述
    order: int = -1  # 顺序（用于验证）

    @classmethod
    def from_dict(cls, data: Dict) -> "ExpectedAction":
        return cls(
            action=data["action"],
            node_id=data["node"],
            target=data.get("target"),
            order=data.get("order", -1)
        )


@dataclass
class ExpectedPageTransition:
    """期望的页面切换"""
    from_page: str
    to_page: str
    trigger: str  # 触发元素ID

    @classmethod
    def from_dict(cls, data: Dict) -> "ExpectedPageTransition":
        return cls(
            from_page=data["from"],
            to_page=data["to"],
            trigger=data["trigger"]
        )


@dataclass
class ExpectedBehavior:
    """期望行为定义"""
    scenario: str  # 场景名称
    description: str  # 场景描述

    # 期望的动作序列
    actions: List[ExpectedAction] = field(default_factory=list)

    # 期望的页面切换
    page_transitions: List[ExpectedPageTransition] = field(default_factory=list)

    # 期望访问的节点集合
    visited_nodes: Set[str] = field(default_factory=set)

    # 期望的最终状态
    final_state: str = "COMPLETED"

    # 完成模式
    completion_mode: CompletionMode = CompletionMode.NORMAL

    # 期望的异常（如果completion_mode为EXCEPTION）
    expected_exception: Optional[str] = None

    @classmethod
    def from_yaml(cls, yaml_path: Path) -> "ExpectedBehavior":
        """从YAML文件加载期望行为"""
        with open(yaml_path, 'r', encoding='utf-8') as f:
            data = yaml.safe_load(f)

        actions = [ExpectedAction.from_dict(a) for a in data.get("actions", [])]

        transitions = [
            ExpectedPageTransition.from_dict(t)
            for t in data.get("page_transitions", [])
        ]

        visited = set(data.get("visited_nodes", []))

        completion_mode = CompletionMode.NORMAL
        if data.get("completion_mode"):
            completion_mode = CompletionMode(data["completion_mode"])

        return cls(
            scenario=data["scenario"],
            description=data.get("description", ""),
            actions=actions,
            page_transitions=transitions,
            visited_nodes=visited,
            final_state=data.get("final_state", "COMPLETED"),
            completion_mode=completion_mode,
            expected_exception=data.get("expected_exception")
        )

    def validate(self) -> List[str]:
        """验证期望行为定义的完整性"""
        errors = []

        # 验证动作序列
        for i, action in enumerate(self.actions):
            if action.order != -1 and action.order != i:
                errors.append(f"Action {i} order mismatch: expected {action.order}, got {i}")

        # 验证节点访问
        if not self.visited_nodes:
            errors.append("No visited_nodes defined")

        # 验证异常模式
        if self.completion_mode == CompletionMode.EXCEPTION and not self.expected_exception:
            errors.append("EXCEPTION mode requires expected_exception")

        return errors
```

**期望行为YAML格式**：

```yaml
# tests/v6/fixtures/expected/simple_two_page_expected.yaml
scenario: simple_two_page
description: "简单两页面遍历：点击按钮进入详情页，返回首页"

actions:
  - {action: no_action, node: root}
  - {action: click, node: btn1, target: Button1}
  - {action: back, node: btn1}
  - {action: no_action, node: root}

page_transitions:
  - {from: home, to: detail, trigger: btn1}
  - {from: detail, to: home, trigger: back_btn}

visited_nodes: [root, btn1]
final_state: COMPLETED
completion_mode: normal
```

#### 4.3.2 BehaviorValidator 设计

**文件位置**: `src/simulation/behavior_validator.py`

```python
from dataclasses import dataclass
from typing import List, Optional, Literal
from enum import Enum


class ValidationResultStatus(Enum):
    """验证结果状态"""
    OK = "ok"
    FAIL = "fail"
    PARTIAL = "partial"


@dataclass
class MatchResult:
    """节点匹配结果"""
    matched: bool
    match_type: Literal["exact", "fuzzy", "none"]
    confidence: float  # 0.0 - 1.0
    reason: Optional[str] = None

    @classmethod
    def exact(cls) -> "MatchResult":
        return cls(matched=True, match_type="exact", confidence=1.0)

    @classmethod
    def fuzzy(cls, confidence: float, reason: str) -> "MatchResult":
        return cls(matched=True, match_type="fuzzy", confidence=confidence, reason=reason)

    @classmethod
    def none(cls) -> "MatchResult":
        return cls(matched=False, match_type="none", confidence=0.0)


@dataclass
class ValidationIssue:
    """验证问题"""
    category: str  # action_sequence / page_transition / node_visitation / state
    description: str
    expected: str
    actual: str
    severity: str = "error"  # error/warning/info
    match_result: Optional[MatchResult] = None  # 用于动态节点匹配


@dataclass
class ValidationResult:
    """验证结果"""
    status: ValidationResultStatus
    issues: List[ValidationIssue] = field(default_factory=list)
    fuzzy_match_count: int = 0  # 模糊匹配计数
    exact_match_count: int = 0  # 精确匹配计数

    def is_ok(self) -> bool:
        return self.status == ValidationResultStatus.OK

    def add_issue(self, issue: ValidationIssue):
        self.issues.append(issue)
        if issue.severity == "error":
            self.status = ValidationResultStatus.FAIL
        elif self.status == ValidationResultStatus.OK:
            self.status = ValidationResultStatus.PARTIAL

    def get_errors(self) -> List[ValidationIssue]:
        return [i for i in self.issues if i.severity == "error"]

    def get_warnings(self) -> List[ValidationIssue]:
        return [i for i in self.issues if i.severity == "warning"]

    def get_fuzzy_matches(self) -> List[ValidationIssue]:
        """获取所有模糊匹配的问题"""
        return [i for i in self.issues if i.match_result and i.match_result.match_type == "fuzzy"]


class BehaviorValidator:
    """行为验证器 - 验证实际执行是否符合期望

    匹配策略：
    1. 优先尝试精确匹配（element_id 或 node_id 完全一致）
    2. 精确匹配失败时，尝试启发式匹配（target 文本、位置等）
    3. 记录匹配类型和置信度，便于后续分析
    """

    def __init__(self, strict_fuzzy_match: bool = False):
        """
        初始化验证器

        Args:
            strict_fuzzy_match: 严格模式，模糊匹配视为 error；否则视为 warning
        """
        self.strict_fuzzy_match = strict_fuzzy_match

    def validate(self,
                 result: SimulationResult,
                 expected: ExpectedBehavior) -> ValidationResult:
        """验证仿真结果是否符合期望行为"""

        validation_result = ValidationResult(status=ValidationResultStatus.OK)

        # 1. 验证最终状态
        self._validate_final_state(result, expected, validation_result)

        # 2. 验证动作序列
        self._validate_action_sequence(result, expected, validation_result)

        # 3. 验证页面切换
        self._validate_page_transitions(result, expected, validation_result)

        # 4. 验证节点访问
        self._validate_node_visitation(result, expected, validation_result)

        # 5. 验证完成模式
        self._validate_completion_mode(result, expected, validation_result)

        # 统计匹配结果
        validation_result.exact_match_count = sum(
            1 for i in validation_result.issues
            if i.match_result and i.match_result.match_type == "exact"
        )
        validation_result.fuzzy_match_count = sum(
            1 for i in validation_result.issues
            if i.match_result and i.match_result.match_type == "fuzzy"
        )

        return validation_result

    def _validate_final_state(self, result: SimulationResult,
                              expected: ExpectedBehavior,
                              vr: ValidationResult):
        """验证最终状态"""
        if result.final_state.value != expected.final_state:
            vr.add_issue(ValidationIssue(
                category="state",
                description="最终状态不匹配",
                expected=expected.final_state,
                actual=result.final_state.value,
                severity="error"
            ))

    def _validate_action_sequence(self, result: SimulationResult,
                                   expected: ExpectedBehavior,
                                   vr: ValidationResult):
        """验证动作序列"""
        actual_actions = self._extract_actions(result)

        # 比较动作序列
        if len(actual_actions) != len(expected.actions):
            vr.add_issue(ValidationIssue(
                category="action_sequence",
                description=f"动作数量不匹配",
                expected=f"{len(expected.actions)} actions",
                actual=f"{len(actual_actions)} actions",
                severity="error"
            ))
            return

        for i, (actual, expected_action) in enumerate(zip(actual_actions, expected.actions)):
            # 验证动作类型
            if actual["action"] != expected_action.action:
                vr.add_issue(ValidationIssue(
                    category="action_sequence",
                    description=f"第{i+1}个动作类型不匹配",
                    expected=expected_action.action,
                    actual=actual["action"],
                    severity="error"
                ))
                continue

            # 验证节点ID
            if expected_action.node_id:
                match_result = self._match_node(actual, expected_action)
                if not match_result.matched:
                    severity = "error" if self.strict_fuzzy_match else "warning"
                    vr.add_issue(ValidationIssue(
                        category="action_sequence",
                        description=f"第{i+1}个动作的节点不匹配",
                        expected=f"node={expected_action.node_id}",
                        actual=f"node={actual.get('node_id')}",
                        severity=severity,
                        match_result=match_result
                    ))

    def _match_node(self, actual: Dict, expected: ExpectedAction) -> MatchResult:
        """匹配节点ID

        策略：
        1. 精确匹配：node_id 完全一致
        2. ID 包含匹配：期望ID包含在实际ID中（或反之）
        3. Target 匹配：通过 target 文本匹配
        4. 位置匹配：通过在序列中的位置匹配
        """
        actual_node = actual.get('node_id', '')

        # 1. 精确匹配
        if actual_node == expected.node_id:
            return MatchResult.exact()

        # 2. ID 包含匹配（处理动态生成的ID）
        if expected.node_id in actual_node or actual_node in expected.node_id:
            return MatchResult.fuzzy(
                confidence=0.9,
                reason=f"ID包含匹配: '{expected.node_id}' in '{actual_node}'"
            )

        # 3. Target 匹配
        if expected.target:
            actual_target = actual.get('target', '')
            if expected.target in actual_target or actual_target in expected.target:
                return MatchResult.fuzzy(
                    confidence=0.7,
                    reason=f"Target文本匹配: '{expected.target}' in '{actual_target}'"
                )

        # 4. 无匹配
        return MatchResult.none()

    def _extract_actions(self, result: SimulationResult) -> List[Dict]:
        """从trace中提取动作序列"""
        actions = []

        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'execution':
                action = {
                    "action": getattr(node, 'action', 'unknown'),
                    "target": str(getattr(node, 'target', '')),
                    "node_id": getattr(node, 'node_id', ''),
                    "status": getattr(node, 'status', '')
                }
                actions.append(action)

        return actions

    def _validate_page_transitions(self, result: SimulationResult,
                                   expected: ExpectedBehavior,
                                   vr: ValidationResult):
        """验证页面切换"""
        actual_transitions = self._extract_page_transitions(result)

        if len(actual_transitions) != len(expected.page_transitions):
            vr.add_issue(ValidationIssue(
                category="page_transition",
                description=f"页面切换数量不匹配",
                expected=f"{len(expected.page_transitions)} transitions",
                actual=f"{len(actual_transitions)} transitions",
                severity="error"
            ))
            return

        for i, (actual, expected_trans) in enumerate(zip(actual_transitions, expected.page_transitions)):
            if actual['from'] != expected_trans.from_page or \
               actual['to'] != expected_trans.to_page:
                vr.add_issue(ValidationIssue(
                    category="page_transition",
                    description=f"第{i+1}个页面切换不匹配",
                    expected=f"{expected_trans.from_page} -> {expected_trans.to_page}",
                    actual=f"{actual['from']} -> {actual['to']}",
                    severity="error"
                ))

    def _extract_page_transitions(self, result: SimulationResult) -> List[Dict]:
        """从trace中提取页面切换"""
        transitions = []

        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'page_transition':
                transition = {
                    'from': getattr(node, 'from_page', ''),
                    'to': getattr(node, 'to_page', ''),
                    'trigger': getattr(node, 'trigger_element', ''),
                    'action': getattr(node, 'action', '')
                }
                transitions.append(transition)

        return transitions

    def _validate_node_visitation(self, result: SimulationResult,
                                 expected: ExpectedBehavior,
                                 vr: ValidationResult):
        """验证节点访问"""
        actual_nodes = self._extract_visited_nodes(result)

        missing_nodes = expected.visited_nodes - actual_nodes
        extra_nodes = actual_nodes - expected.visited_nodes

        if missing_nodes:
            vr.add_issue(ValidationIssue(
                category="node_visitation",
                description="期望访问的节点未被访问",
                expected=str(sorted(missing_nodes)),
                actual="",
                severity="error"
            ))

        if extra_nodes:
            vr.add_issue(ValidationIssue(
                category="node_visitation",
                description="访问了未期望的节点",
                expected="",
                actual=str(sorted(extra_nodes)),
                severity="warning"
            ))

    def _extract_visited_nodes(self, result: SimulationResult) -> Set[str]:
        """从trace中提取访问过的节点"""
        nodes = set()

        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'step':
                node_id = getattr(node, 'node_id', '')
                if node_id:
                    nodes.add(node_id)

        return nodes

    def _validate_completion_mode(self, result: SimulationResult,
                                 expected: ExpectedBehavior,
                                 vr: ValidationResult):
        """验证完成模式"""
        if expected.completion_mode == CompletionMode.EXCEPTION:
            # 检查是否有期望的异常
            has_exception = False
            for node in result.trace_nodes:
                if getattr(node, 'node_type', '') == 'span' and \
                   getattr(node, 'span_type', '') == 'error':
                    error_msg = getattr(node, 'error_message', '')
                    if expected.expected_exception and \
                       expected.expected_exception in error_msg:
                        has_exception = True
                        break

            if not has_exception:
                vr.add_issue(ValidationIssue(
                    category="completion_mode",
                    description="期望发生异常但实际未发生",
                    expected=f"exception: {expected.expected_exception}",
                    actual="completed normally",
                    severity="error"
                ))
```

---

### 4.4 Phase 4: 问题检测器 (P1)

#### 4.4.1 ProblemDetector 设计

**文件位置**: `src/simulation/problem_detector.py`

```python
from dataclasses import dataclass
from typing import List, Dict, Set, Optional
from enum import Enum
from pydantic import BaseModel


class ProblemType(Enum):
    """问题类型"""
    INFINITE_LOOP = "infinite_loop"  # 无限循环
    UNVISITED_NODE = "unvisited_node"  # 未访问节点
    REPEATED_ACTION = "repeated_action"  # 重复动作
    STATE_MACHINE_ERROR = "state_machine_error"  # 状态机错误
    PAGE_MISMATCH = "page_mismatch"  # 页面不匹配
    ORPHAN_NODE = "orphan_node"  # 孤立节点


@dataclass
class Problem:
    """检测到的问题"""
    problem_type: ProblemType
    description: str
    severity: str  # critical/warning/info
    location: str  # 问题位置（节点/页面）
    evidence: Dict  # 证据数据

    def to_dict(self) -> Dict:
        return {
            "type": self.problem_type.value,
            "description": self.description,
            "severity": self.severity,
            "location": self.location,
            "evidence": self.evidence
        }


class ProblemDetectorConfig(BaseModel):
    """问题检测器配置"""
    # 阈值配置
    max_action_repeats: int = 3  # 最大动作重复次数
    max_loop_depth: int = 5  # 最大循环深度
    max_state_errors: int = 1  # 最大状态错误次数

    # 检测开关
    enable_infinite_loop_detection: bool = True
    enable_unvisited_node_detection: bool = True
    enable_repeated_action_detection: bool = True
    enable_state_machine_error_detection: bool = True
    enable_page_mismatch_detection: bool = True
    enable_orphan_node_detection: bool = True

    # 灵敏度配置
    loop_detection_sensitivity: str = "medium"  # low/medium/high
    node_visitation_strictness: str = "warning"  # error/warning/info


class ProblemDetector:
    """问题检测器 - 自动检测异常执行模式"""

    def __init__(self, config: Optional[ProblemDetectorConfig] = None):
        """
        初始化问题检测器

        Args:
            config: 检测器配置，如果为 None 则使用默认配置
        """
        self.config = config or ProblemDetectorConfig()

        # 根据灵敏度配置调整阈值
        self._adjust_thresholds()

    def _adjust_thresholds(self):
        """根据灵敏度配置调整检测阈值"""
        sensitivity = self.config.loop_detection_sensitivity
        if sensitivity == "low":
            self._effective_max_repeats = self.config.max_action_repeats * 2
            self._effective_max_loop_depth = self.config.max_loop_depth * 2
        elif sensitivity == "high":
            self._effective_max_repeats = max(1, self.config.max_action_repeats // 2)
            self._effective_max_loop_depth = max(2, self.config.max_loop_depth // 2)
        else:  # medium
            self._effective_max_repeats = self.config.max_action_repeats
            self._effective_max_loop_depth = self.config.max_loop_depth

    def detect(self, result: SimulationResult,
               expected: Optional[ExpectedBehavior] = None) -> List[Problem]:
        """检测执行中的问题"""

        problems = []

        # 1. 检测无限循环
        problems.extend(self._detect_infinite_loop(result))

        # 2. 检测未访问节点
        if expected:
            problems.extend(self._detect_unvisited_nodes(result, expected))

        # 3. 检测异常重复动作
        problems.extend(self._detect_repeated_actions(result))

        # 4. 检测状态机异常
        problems.extend(self._detect_state_machine_error(result))

        # 5. 检测页面不匹配
        problems.extend(self._detect_page_mismatch(result))

        # 6. 检测孤立节点
        problems.extend(self._detect_orphan_nodes(result))

        return problems

    def _detect_infinite_loop(self, result: SimulationResult) -> List[Problem]:
        """检测无限循环"""
        problems = []

        # 分析动作序列，检测循环模式
        actions = self._extract_actions(result)

        # 检测简单循环：同一动作重复多次
        action_counts: Dict[str, int] = {}
        for action in actions:
            key = f"{action['action']}_{action.get('target', '')}"
            action_counts[key] = action_counts.get(key, 0) + 1

            if action_counts[key] > self.max_action_repeats:
                problems.append(Problem(
                    problem_type=ProblemType.INFINITE_LOOP,
                    description=f"动作重复超过{self.max_action_repeats}次: {action['action']} on {action.get('target', '')}",
                    severity="critical",
                    location=action.get('node_id', ''),
                    evidence={"action": action, "count": action_counts[key]}
                ))

        # 检测复杂循环：状态序列重复
        state_sequence = self._extract_state_sequence(result)
        loops = self._find_repeating_patterns(state_sequence, min_length=2)

        for loop in loops:
            problems.append(Problem(
                problem_type=ProblemType.INFINITE_LOOP,
                description=f"检测到循环模式: {loop}",
                severity="warning",
                location="state_machine",
                evidence={"loop_pattern": loop}
            ))

        return problems

    def _detect_unvisited_nodes(self, result: SimulationResult,
                               expected: ExpectedBehavior) -> List[Problem]:
        """检测未访问节点"""
        problems = []

        actual_nodes = self._extract_visited_nodes(result)
        missing_nodes = expected.visited_nodes - actual_nodes

        for node in missing_nodes:
            problems.append(Problem(
                problem_type=ProblemType.UNVISITED_NODE,
                description=f"期望访问的节点未被访问: {node}",
                severity="warning",
                location=node,
                evidence={"expected": list(expected.visited_nodes), "actual": list(actual_nodes)}
            ))

        return problems

    def _detect_repeated_actions(self, result: SimulationResult) -> List[Problem]:
        """检测异常重复动作"""
        problems = []

        actions = self._extract_actions(result)

        # 检测同一动作在同一节点上的重复
        node_actions: Dict[str, List[str]] = {}
        for i, action in enumerate(actions):
            node_id = action.get('node_id', '')
            action_type = action['action']

            if node_id not in node_actions:
                node_actions[node_id] = []

            node_actions[node_id].append(action_type)

            # 检测同一节点上同一动作连续重复
            if len(node_actions[node_id]) >= 2:
                last_actions = node_actions[node_id][-2:]
                if len(set(last_actions)) == 1 and len(last_actions) >= self.max_action_repeats:
                    problems.append(Problem(
                        problem_type=ProblemType.REPEATED_ACTION,
                        description=f"节点 {node_id} 上动作 {action_type} 连续重复 {len(last_actions)} 次",
                        severity="warning",
                        location=node_id,
                        evidence={"action": action_type, "count": len(last_actions)}
                    ))

        return problems

    def _detect_state_machine_error(self, result: SimulationResult) -> List[Problem]:
        """检测状态机异常"""
        problems = []

        # 检测最终状态是否为ERROR
        if result.final_state.value == "ERROR":
            problems.append(Problem(
                problem_type=ProblemType.STATE_MACHINE_ERROR,
                description="状态机进入错误状态",
                severity="critical",
                location="state_machine",
                evidence={"final_state": "ERROR"}
            ))

        # 检测异常状态转换
        state_transitions = self._extract_state_transitions(result)
        for i, trans in enumerate(state_transitions):
            from_state = trans.get('from', '')
            to_state = trans.get('to', '')

            # 检测无效转换
            if not self._is_valid_transition(from_state, to_state):
                problems.append(Problem(
                    problem_type=ProblemType.STATE_MACHINE_ERROR,
                    description=f"无效的状态转换: {from_state} -> {to_state}",
                    severity="error",
                    location=f"transition_{i}",
                    evidence={"transition": trans}
                ))

        return problems

    def _detect_page_mismatch(self, result: SimulationResult) -> List[Problem]:
        """检测页面不匹配"""
        problems = []

        # 检测页面切换失败
        transitions = self._extract_page_transitions(result)

        for trans in transitions:
            # 如果from_page等于to_page，可能表示切换失败
            if trans.get('from') == trans.get('to'):
                problems.append(Problem(
                    problem_type=ProblemType.PAGE_MISMATCH,
                    description=f"页面切换可能失败: {trans.get('from')} -> {trans.get('to')}",
                    severity="warning",
                    location=trans.get('trigger', ''),
                    evidence={"transition": trans}
                ))

        return problems

    def _detect_orphan_nodes(self, result: SimulationResult) -> List[Problem]:
        """检测孤立节点（动态创建但从未执行）"""
        problems = []

        # 收集动态节点生命周期事件
        lifecycle_events: Dict[str, List[str]] = {}
        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'dynamic_lifecycle':
                node_id = getattr(node, 'node_id', '')
                event = getattr(node, 'event', '')

                if node_id not in lifecycle_events:
                    lifecycle_events[node_id] = []
                lifecycle_events[node_id].append(event)

        # 检查有created但没有executed的节点
        for node_id, events in lifecycle_events.items():
            if 'created' in events and 'executed' not in events:
                problems.append(Problem(
                    problem_type=ProblemType.ORPHAN_NODE,
                    description=f"动态节点被创建但从未执行: {node_id}",
                    severity="warning",
                    location=node_id,
                    evidence={"events": events}
                ))

        return problems

    def _extract_actions(self, result: SimulationResult) -> List[Dict]:
        """提取动作序列"""
        actions = []
        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'execution':
                actions.append({
                    'action': getattr(node, 'action', ''),
                    'target': str(getattr(node, 'target', '')),
                    'node_id': getattr(node, 'node_id', '')
                })
        return actions

    def _extract_state_sequence(self, result: SimulationResult) -> List[str]:
        """提取状态序列"""
        states = []
        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'state_transition':
                states.append(getattr(node, 'to_state', ''))
        return states

    def _extract_state_transitions(self, result: SimulationResult) -> List[Dict]:
        """提取状态转换"""
        transitions = []
        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'state_transition':
                transitions.append({
                    'from': getattr(node, 'from_state', ''),
                    'to': getattr(node, 'to_state', '')
                })
        return transitions

    def _extract_visited_nodes(self, result: SimulationResult) -> Set[str]:
        """提取访问过的节点"""
        nodes = set()
        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'step':
                node_id = getattr(node, 'node_id', '')
                if node_id:
                    nodes.add(node_id)
        return nodes

    def _extract_page_transitions(self, result: SimulationResult) -> List[Dict]:
        """提取页面切换"""
        transitions = []
        for node in result.trace_nodes:
            if getattr(node, 'node_type', '') == 'span' and \
               getattr(node, 'span_type', '') == 'page_transition':
                transitions.append({
                    'from': getattr(node, 'from_page', ''),
                    'to': getattr(node, 'to_page', ''),
                    'trigger': getattr(node, 'trigger_element', '')
                })
        return transitions

    def _is_valid_transition(self, from_state: str, to_state: str) -> bool:
        """检查状态转换是否有效"""
        # 定义有效的状态转换
        valid_transitions = {
            'IDLE': ['BINDING', 'EXECUTING'],
            'BINDING': ['EXECUTING', 'ERROR'],
            'EXECUTING': ['AUTO_ESCAPE', 'FRAME_COMPLETE', 'BRANCH', 'COMPLETED'],
            'AUTO_ESCAPE': ['EXECUTING', 'FRAME_COMPLETE', 'ERROR'],
            'FRAME_COMPLETE': ['BINDING', 'COMPLETED'],
            'BRANCH': ['EXECUTING', 'COMPLETED'],
            'ERROR': [],
            'COMPLETED': []
        }

        return to_state in valid_transitions.get(from_state, [])

    def _find_repeating_patterns(self, sequence: List[str],
                                  min_length: int = 2) -> List[str]:
        """查找重复模式"""
        patterns = []

        for length in range(min_length, len(sequence) // 2):
            for i in range(len(sequence) - length * 2):
                pattern = sequence[i:i + length]
                next_segment = sequence[i + length:i + length * 2]

                if pattern == next_segment:
                    patterns.append(' -> '.join(pattern))
                    break

        return patterns
```

---

## 5. 测试策略

### 5.1 单元测试

#### 5.1.1 StateFixture 测试

**文件**: `tests/v6/unit/test_state_fixture.py`

```python
def test_state_fixture_loading():
    """测试fixture加载"""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")

    assert fixture.get_page("home") is not None
    assert fixture.get_page("detail") is not None


def test_state_fixture_transitions():
    """测试页面切换规则"""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")

    transition = fixture.get_transition("btn1")
    assert transition is not None
    assert transition.from_page == "home"
    assert transition.to_page == "detail"


def test_state_fixture_validation():
    """测试fixture验证"""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    errors = fixture.validate()

    assert len(errors) == 0
```

#### 5.1.2 StatefulMockVisionService 测试

**文件**: `tests/v6/unit/test_stateful_mock_vision.py`

```python
def test_initial_page():
    """测试初始页面"""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    vision = StatefulMockVisionService(fixture)

    assert vision.current_page_id == "home"


def test_page_transition():
    """测试页面切换"""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    vision = StatefulMockVisionService(fixture)

    # 执行切换
    success = vision.simulate_action("btn1", "click")

    assert success is True
    assert vision.current_page_id == "detail"
    assert len(vision.navigation_history) == 1


def test_navigation_back():
    """测试返回导航"""
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    vision = StatefulMockVisionService(fixture)

    vision.simulate_action("btn1", "click")
    assert vision.current_page_id == "detail"

    vision.navigate_back()
    assert vision.current_page_id == "home"


def test_page_analysis_field_mapping():
    """验证 PageAnalysis 字段映射正确性

    关键验证点：
    - fixture.text -> MenuItem.name（不是 text 字段）
    - fixture.type -> MenuItem.type（MenuItemType 枚举）
    - PageAnalysis.items（不是 menu_items）
    """
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    vision = StatefulMockVisionService(fixture)

    # 获取 PageAnalysis
    page_analysis = vision.analyze_screenshot(b"dummy")

    # 验证基本结构
    assert hasattr(page_analysis, 'items')  # 不是 menu_items
    assert len(page_analysis.items) > 0

    # 验证 MenuItem 字段
    first_item = page_analysis.items[0]
    assert hasattr(first_item, 'name')  # 不是 text
    assert hasattr(first_item, 'type')  # MenuItemType 枚举
    assert hasattr(first_item, 'coordinate')

    # 验证枚举类型
    from src.state.content_tree import MenuItemType
    assert isinstance(first_item.type, MenuItemType)


def test_menu_item_compatible_with_dynamic_matcher():
    """验证 MenuItem 可以被 DynamicMatcher 正确处理"""
    from src.graph.matcher import DynamicMatcher, MatchCondition

    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
    vision = StatefulMockVisionService(fixture)

    # 获取 PageAnalysis
    page_analysis = vision.analyze_screenshot(b"dummy")

    # 转换为 DynamicMatcher 期望的格式
    menu_items_for_matcher = [
        {
            "type": item.type.value,  # MenuItemType -> string
            "text": item.name,  # name -> text
            "index": i,
            "coordinate": item.coordinate.dict(),
            "expected_action": item.expected_action.value
        }
        for i, item in enumerate(page_analysis.items)
    ]

    # 验证 DynamicMatcher 可以处理这个格式
    condition = MatchCondition({"type": "button"})
    for item in menu_items_for_matcher:
        result = condition.matches(item)
        # 如果类型匹配，应该返回 True
        if item["type"] == "button":
            assert result is True
```

#### 5.1.3 BehaviorValidator 测试

**文件**: `tests/v6/unit/test_behavior_validator.py`

```python
def test_action_sequence_validation():
    """测试动作序列验证"""
    # 创建期望行为
    expected = ExpectedBehavior(
        scenario="test",
        description="Test scenario",
        actions=[
            ExpectedAction(action="no_action", node_id="root"),
            ExpectedAction(action="click", node_id="btn1", target="Button1")
        ]
    )

    # 创建仿真结果（匹配期望）
    result = create_mock_result(actions=[
        {"action": "no_action", "node_id": "root"},
        {"action": "click", "node_id": "btn1", "target": "Button1"}
    ])

    validator = BehaviorValidator()
    validation = validator.validate(result, expected)

    assert validation.is_ok()


def test_action_sequence_mismatch():
    """测试动作序列不匹配"""
    expected = ExpectedBehavior(
        scenario="test",
        description="Test scenario",
        actions=[
            ExpectedAction(action="click", node_id="btn1", target="Button1")
        ]
    )

    # 创建不匹配的结果
    result = create_mock_result(actions=[
        {"action": "back", "node_id": "root"}
    ])

    validator = BehaviorValidator()
    validation = validator.validate(result, expected)

    assert not validation.is_ok()
    assert len(validation.get_errors()) > 0
```

#### 5.1.4 ProblemDetector 测试

**文件**: `tests/v6/unit/test_problem_detector.py`

```python
def test_infinite_loop_detection():
    """测试无限循环检测"""
    # 创建有重复动作的trace
    result = create_mock_result(actions=[
        {"action": "click", "node_id": "root", "target": "Button1"},
        {"action": "click", "node_id": "root", "target": "Button1"},
        {"action": "click", "node_id": "root", "target": "Button1"},
        {"action": "click", "node_id": "root", "target": "Button1"}
    ])

    detector = ProblemDetector()
    problems = detector.detect(result)

    assert any(p.problem_type == ProblemType.INFINITE_LOOP for p in problems)


def test_unvisited_node_detection():
    """测试未访问节点检测"""
    expected = ExpectedBehavior(
        scenario="test",
        description="Test",
        visited_nodes={"root", "btn1", "btn2"}
    )

    # 创建只访问了root和btn1的结果
    result = create_mock_result(visited_nodes={"root", "btn1"})

    detector = ProblemDetector()
    problems = detector.detect(result, expected)

    assert any(p.problem_type == ProblemType.UNVISITED_NODE for p in problems)
```

### 5.2 集成测试

#### 5.2.1 端到端仿真测试

**文件**: `tests/v6/integration/test_simulation_e2e.py`

```python
def test_simple_two_page_traversal():
    """测试简单两页面遍历"""
    # 加载fixture
    fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")

    # 创建遍历计划
    root = TraversalNode(
        node_id="root",
        name="Root",
        node_type=NodeType.CONTAINER,
        operation=Operation(action="no_action"),
        children_strategy=ChildrenStrategy(
            type=ChildrenStrategyType.DYNAMIC_MATCH,
            dynamic_rules={
                "button": DynamicRule(
                    rule_id="button_rule",
                    match_condition={"type": "button"},
                    child_template="button_template",
                    action="generate_child"
                )
            }
        )
    )

    plan = TraversalPlan(
        entry_app="TestApp",
        entry_policy=EntryPolicy(strategy=EntryStrategy.BIND_CURRENT_SCREEN),
        root_node=root
    )

    # 运行仿真
    runner = SimulationRunner(fixture, plan)
    result = runner.run()

    # 加载期望行为
    expected = ExpectedBehavior.from_yaml(
        "tests/v6/fixtures/expected/simple_two_page_expected.yaml"
    )

    # 验证结果
    validator = BehaviorValidator()
    validation = validator.validate(result, expected)

    assert validation.is_ok(), f"Validation failed: {validation.get_errors()}"

    # 检测问题
    detector = ProblemDetector()
    problems = detector.detect(result, expected)

    critical_problems = [p for p in problems if p.severity == "critical"]
    assert len(critical_problems) == 0, f"Critical problems found: {critical_problems}"


def test_dynamic_buttons_with_state_change():
    """测试动态按钮与页面切换"""
    # 这个测试验证：点击按钮后页面真正切换
    fixture = StateFixture.from_yaml("tests/v6/fixtures/dynamic_buttons.yaml")
    plan = create_dynamic_plan()

    runner = SimulationRunner(fixture, plan)
    result = runner.run()

    # 验证页面切换确实发生
    transitions = extract_page_transitions(result.trace_nodes)
    assert len(transitions) > 0, "No page transitions detected"

    # 验证没有无限循环（这是之前的问题）
    detector = ProblemDetector()
    problems = detector.detect(result)

    loop_problems = [p for p in problems if p.problem_type == ProblemType.INFINITE_LOOP]
    assert len(loop_problems) == 0, f"Infinite loop detected: {loop_problems}"
```

### 5.3 问题发现测试

**专门设计用于发现问题的测试用例**：

#### 5.3.1 测试原有Bug

**文件**: `tests/v6/integration/test_bug_detection.py`

```python
def test_detect_mock_service_limitation():
    """测试检测Mock服务限制（原Bug场景）"""
    # 使用原始的MockVisionService（无状态管理）
    # 这个测试应该FAIL，暴露Mock服务的问题

    fixture = StateFixture.from_yaml("tests/v6/fixtures/original_bug.yaml")
    plan = create_plan_with_dynamic_matching()

    # 使用原始Mock服务
    vision = MockVisionService(fixture.pages)  # 旧版本，无状态管理
    runner = SimulationRunner(fixture, plan, vision=vision)
    result = runner.run()

    # 检测问题
    detector = ProblemDetector()
    problems = detector.detect(result)

    # 应该检测到无限循环
    loop_problems = [p for p in problems if p.problem_type == ProblemType.INFINITE_LOOP]
    assert len(loop_problems) > 0, "Should detect infinite loop with old mock service"

    # 应该检测到重复动作
    repeated_problems = [p for p in problems if p.problem_type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) > 0, "Should detect repeated actions with old mock service"


def test_verify_stateful_mock_fixes_bug():
    """测试StatefulMock修复了原有Bug"""
    # 使用新的StatefulMockVisionService
    fixture = StateFixture.from_yaml("tests/v6/fixtures/original_bug.yaml")
    plan = create_plan_with_dynamic_matching()

    # 使用状态管理的Mock服务
    vision = StatefulMockVisionService(fixture)
    runner = SimulationRunner(fixture, plan, vision=vision)
    result = runner.run()

    # 检测问题
    detector = ProblemDetector()
    problems = detector.detect(result)

    # 不应该有无限循环
    loop_problems = [p for p in problems if p.problem_type == ProblemType.INFINITE_LOOP]
    assert len(loop_problems) == 0, "Stateful mock should prevent infinite loop"

    # 不应该有重复动作
    repeated_problems = [p for p in problems if p.problem_type == ProblemType.REPEATED_ACTION]
    assert len(repeated_problems) == 0, "Stateful mock should prevent repeated actions"
```

---

## 6. 实施计划

### 6.1 分阶段实施

#### Phase 1: StateFixture 和状态管理 (P0) - 3-4天

**目标**：实现基础的状态管理能力

**任务**：
1. 实现 `StateFixture` 类
2. 实现 `StatefulMockVisionService` 类
3. 实现 `StatefulMockActionExecutor` 类
4. 修改 `SimulationRunner` 集成状态管理
5. 编写单元测试

**验收标准**：
- StateFixture 可以从 YAML 加载并验证
- StatefulMockVisionService 可以正确模拟页面切换
- 导航历史被正确记录
- 单元测试覆盖率 > 90%

#### Phase 2: 增强Trace记录 (P0) - 2-3天

**目标**：记录页面切换和动态节点生命周期

**任务**：
1. 扩展 `src/trace/models.py` 添加新的Trace节点类型
2. 在 `GraphTraversalEngine` 中记录页面切换
3. 记录动态节点生命周期事件
4. 记录状态机决策
5. 编写集成测试验证Trace记录

**验收标准**：
- 页面切换被正确记录到trace
- 动态节点创建/执行/销毁被记录
- Trace可以通过可视化工具查看

#### Phase 3: 行为验证器 (P0) - 3-4天

**目标**：基于期望行为验证测试结果

**任务**：
1. 实现 `ExpectedBehavior` 类
2. 实现 `BehaviorValidator` 类
3. 实现动作序列验证
4. 实现页面切换验证
5. 实现节点访问验证
6. 编写验证器测试

**验收标准**：
- 可以从 YAML 加载期望行为
- 验证器可以检测动作序列不匹配
- 验证器可以检测页面切换不匹配
- 验证器可以检测未访问节点

#### Phase 4: 问题检测器 (P1) - 2-3天

**目标**：自动检测异常执行模式

**任务**：
1. 实现 `ProblemDetector` 类
2. 实现无限循环检测
3. 实现重复动作检测
4. 实现未访问节点检测
5. 实现状态机异常检测
6. 编写问题检测测试

**验收标准**：
- 可以检测无限循环
- 可以检测重复动作
- 可以检测未访问节点
- 可以检测状态机异常

#### Phase 5: 集成与文档 (P1) - 2-3天

**目标**：完整集成并编写文档

**任务**：
1. 更新仿真测试套件使用新组件
2. 编写使用文档
3. 编写示例fixture和期望行为
4. 验证原有问题已解决
5. 编写迁移指南

**验收标准**：
- 所有现有测试通过
- 新增测试覆盖新功能
- 文档完整可用
- 原有Bug被验证已修复

### 6.2 时间估算

| 阶段 | 工作量 | 依赖 |
|------|--------|------|
| Phase 1 | 3-4天 | 无 |
| Phase 2 | 2-3天 | Phase 1 |
| Phase 3 | 3-4天 | Phase 1, 2 |
| Phase 4 | 2-3天 | Phase 1, 2, 3 |
| Phase 5 | 2-3天 | Phase 1, 2, 3, 4 |
| **总计** | **12-17天** | |

### 6.3 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| Fixture格式兼容性 | 现有测试fixture需要迁移 | 提供迁移工具和指南 |
| Trace性能影响 | 额外的记录可能影响性能 | 可配置的记录级别 |
| 验证逻辑复杂性 | 验证规则可能过于复杂 | 支持灵活的验证配置 |
| 状态管理bug | 状态切换可能有bug | 完整的单元测试和集成测试 |

---

## 7. 验收标准

### 7.1 功能验收

- [ ] StateFixture 可以从 YAML 加载并验证配置
- [ ] StatefulMockVisionService 可以正确模拟页面切换
- [ ] 页面切换被记录到 trace
- [ ] 动态节点生命周期被记录到 trace
- [ ] BehaviorValidator 可以验证动作序列
- [ ] BehaviorValidator 可以验证页面切换
- [ ] ProblemDetector 可以检测无限循环
- [ ] ProblemDetector 可以检测重复动作
- [ ] 原有的 AUTO_ESCAPE 重复点击问题被修复

### 7.2 质量验收

- [ ] 单元测试覆盖率 > 90%
- [ ] 所有集成测试通过
- [ ] 无 critical 级别的问题
- [ ] 文档完整可用

### 7.3 问题发现能力验收

**核心验收标准**：能够通过仿真测试发现原有的设计或代码问题

- [ ] 原有的 Mock 服务限制问题可以被检测出来
- [ ] AUTO_ESCAPE 无限循环可以被检测出来
- [ ] 动态节点未被正确执行可以被检测出来
- [ ] 页面切换失败可以被检测出来

---

## 8. 附录

### 8.1 示例Fixture文件

#### 8.1.1 简单两页面

```yaml
# tests/v6/fixtures/simple_two_page.yaml
pages:
  home:
    page_name: HomeScreen
    elements:
      - id: btn1
        type: button
        text: Button1
        coordinate: {x: 0.3, y: 0.5}
        action_target: detail
      - id: btn2
        type: button
        text: Button2
        coordinate: {x: 0.7, y: 0.5}
        action_target: detail

  detail:
    page_name: DetailScreen
    elements:
      - id: back_btn
        type: button
        text: Back
        coordinate: {x: 0.5, y: 0.9}
        action_target: home
    is_complete: true

transitions:
  home_btn1_click:
    trigger: btn1
    from_page: home
    to_page: detail
    action: click

  home_btn2_click:
    trigger: btn2
    from_page: home
    to_page: detail
    action: click

  detail_back_click:
    trigger: back_btn
    from_page: detail
    to_page: home
    action: click
```

#### 8.1.2 期望行为

```yaml
# tests/v6/fixtures/expected/simple_two_page_expected.yaml
scenario: simple_two_page
description: "简单两页面遍历：点击按钮进入详情页，返回首页"

actions:
  - {action: no_action, node: root}
  - {action: click, node: btn1, target: Button1}
  - {action: back, node: btn1}
  - {action: no_action, node: root}

page_transitions:
  - {from: home, to: detail, trigger: btn1}
  - {from: detail, to: home, trigger: back_btn}

visited_nodes: [root, btn1]
final_state: COMPLETED
completion_mode: normal
```

### 8.2 术语表

| 术语 | 定义 |
|------|------|
| StateFixture | 状态固件，定义页面状态和切换规则 |
| StatefulMockVisionService | 具备状态管理能力的Mock视觉服务 |
| ExpectedBehavior | 期望行为定义，用于验证测试结果 |
| BehaviorValidator | 行为验证器，验证实际行为是否符合期望 |
| ProblemDetector | 问题检测器，自动检测异常执行模式 |
| PageTransition | 页面切换，从源页面到目标页面 |
| DynamicNodeLifecycle | 动态节点生命周期，记录创建/执行/销毁 |

### 8.3 参考资料

- [PRD_V6_9_1_dynamic_matching.md](./PRD_V6_9_1_dynamic_matching.md) - 动态匹配功能PRD
- [ARCHITECTURE_V6.md](./architecture/ARCHITECTURE_V6.md) - V6架构文档
- [SIMULATION_TESTING_GUIDE.md](./SIMULATION_TESTING_GUIDE.md) - 仿真测试指南
- [代码审查报告](../../reports/code_review_findings.md) - 原有问题的代码审查报告

---

**文档历史**：
- 2026-06-07: 初始版本创建
- 2026-06-07: 完成详细设计章节

**维护者**: Uni-Claw 开发团队
