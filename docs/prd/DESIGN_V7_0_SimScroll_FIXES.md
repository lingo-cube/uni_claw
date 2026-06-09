# V7.0-SimScroll 问题修复建议

> **版本**: 修复建议 v2.0（根据多代理评审更新）
> **日期**: 2026-06-09
> **原文档**: DESIGN_V7_0_SimScroll.md
> **评审状态**: ⚠️ 有条件批准（8/10分）

---

## 评审总结

### 最终决策
**⚠️ 有条件批准** - 存在7个必须在实施前修复的关键问题

### 质量评分
**8/10** - 设计思路正确，但需修复关键问题

### 优势
- ✅ 技术方案成熟（滚动片段机制合理）
- ✅ 职责分离清晰（符合单一职责原则）
- ✅ 故障注入完备（延迟、无响应、跳跃）
- ✅ 与现有架构兼容（继承 StatefulMockVisionService）

---

## 目录

1. [关键问题修复](#关键问题修复)
2. [设计问题澄清](#设计问题澄清)
3. [架构兼容性修复](#架构兼容性修复)
4. [步长自适应策略深入设计](#步长自适应策略深入设计)
5. [实施优先级](#实施优先级)

---

## 关键问题修复

### 问题1: 路径管理与现有代码不兼容

#### 问题描述
设计文档中的 `_current_path` 属性与现有 `StatefulMockVisionService` 的实现不一致。

#### 现有代码确认
根据代码分析，`StatefulMockVisionService` 使用：
- `_current_page_id: str` - 当前页面ID
- `_navigation_history: List[str]` - 导航历史（页面ID列表）

而非设计文档假设的 `_current_path: List[str]`。

#### 修复方案

**使用现有模式进行适配**：

```python
class ScrollableMockVisionService(StatefulMockVisionService):
    """支持滚动列表模拟的视觉服务"""
    
    def __init__(
        self,
        virtual_pages: Dict[str, Any],
        scroll_data_store: Optional["ScrollDataStore"] = None,
    ):
        super().__init__(virtual_pages)
        
        # 滚动状态管理（基于 _current_page_id）
        self._scroll_states: Dict[str, ScrollState] = {}
        self._scroll_data_store = scroll_data_store or ScrollDataStore(virtual_pages)
    
    def _resolve_path_key(self) -> str:
        """解析当前路径为键（基于现有模式）
        
        使用基类的 _current_page_id 作为主键，
        滚动状态按页面ID存储。
        
        Returns:
            str: 当前页面的路径键
        """
        # 使用基类的当前页面ID
        return self._current_page_id or "home"
    
    def _get_scroll_state(self, page_id: str) -> ScrollState:
        """获取指定页面的滚动状态"""
        if page_id not in self._scroll_states:
            self._scroll_states[page_id] = ScrollState()
        return self._scroll_states[page_id]
```

**导航事件处理**：

```python
def _on_page_changed(self) -> None:
    """页面切换时的回调（可选）
    
    当基类的 _current_page_id 改变时调用，
    用于更新滚动状态的上下文。
    """
    current_key = self._resolve_path_key()
    # 可以在这里重置滚动状态或执行其他逻辑
```

#### 验证步骤
1. 确认基类 `StatefulMockVisionService` 的 `_current_page_id` 为公共属性
2. 确认页面切换时 `_current_page_id` 的更新时机
3. 测试滚动状态在页面切换时的正确性

---

### 问题2: 元素ID稳定性存在冲突风险

#### 问题描述
通过内容哈希生成ID的机制在两个不同位置的元素有相同内容时会产生冲突。

#### 修复方案（已评审确认）

**使用方案A（进度法） - 评审推荐**：

```python
def _generate_element_id(
    self,
    element: Dict[str, Any],
    progress: float,
    segment_index: int,
    element_index: int,
) -> str:
    """根据元素内容和滚动位置生成稳定的唯一ID
    
    结合内容哈希、滚动进度、片段索引、元素索引确保唯一性。
    
    Args:
        element: 元素数据
        progress: 当前滚动进度（0.0-1.0）
        segment_index: 片段索引
        element_index: 片段内的元素索引
        
    Returns:
        str: 唯一的元素ID（32位十六进制字符串）
    """
    # 内容基础哈希
    content = f"{element.get('text', '')}{element.get('type', '')}"
    content_hash = hashlib.md5(content.encode()).hexdigest()[:16]
    
    # 位置信息（保证跨位置唯一性）
    position_info = f"{progress:.3f}_{segment_index}_{element_index}"
    position_hash = hashlib.md5(position_info.encode()).hexdigest()[:16]
    
    # 组合生成唯一ID
    return f"{content_hash}_{position_hash}"

def _ensure_element_ids(
    self,
    page_key: str,
    elements: List[Dict[str, Any]],
    progress: float,
    segment_index: int,
) -> Dict[str, str]:
    """确保元素有稳定的ID映射
    
    为每个元素的原始ID分配稳定的唯一ID。
    
    Args:
        page_key: 页面键
        elements: 元素列表
        progress: 当前滚动进度
        segment_index: 片段索引
        
    Returns:
        Dict[str, str]: 原始ID到稳定ID的映射
    """
    if page_key not in self._element_ids:
        self._element_ids[page_key] = {}
    
    element_ids = self._element_ids[page_key]
    
    for idx, element in enumerate(elements):
        original_id = element.get("id")
        if original_id and original_id not in element_ids:
            # 生成稳定ID
            stable_id = self._generate_element_id(
                element, progress, segment_index, idx
            )
            element_ids[original_id] = stable_id
    
    return element_ids
```

**优点**：
- ✅ 保证跨滚动位置的唯一性
- ✅ 相同内容在不同位置有不同ID
- ✅ ID格式清晰可读（内容_位置）
- ✅ 支持去重和跟踪

---

### 问题3: 滚动片段元素收集逻辑的语义不明确

#### 修复方案

**在文档中明确累加模式语义**：

```markdown
### 3.1.1 滚动片段语义

**累加模式（Accumulative Mode）**：

滚动列表采用"累加模式"模拟真实列表行为：

1. **可见性规则**：滚动到某个进度时，显示该进度及之前所有片段的元素
   - 例如：滚动到 50% 时，显示 threshold 0.0 和 0.5 两个片段的所有元素

2. **元素去重**：通过 `id` 字段去重
   - 如果两个片段有相同 `id` 的元素，后者覆盖前者
   - 用于支持元素状态更新（例如：滚动后元素状态改变）

3. **边界处理**：
   - progress = 0.0: 只显示 threshold = 0.0 的片段
   - progress = 1.0: 显示所有片段的元素
   - threshold 边界值使用 <= 比较（包含边界）

**性能考虑**：
- 累加模式可能导致元素列表增长
- 建议单个列表的元素总数控制在 200 以内
- 超过建议使用分页或虚拟滚动
```

**实现示例**：

```python
def _collect_visible_elements(
    self,
    scroll_segments: List[ScrollSegment],
    progress: float,
) -> List[Dict[str, Any]]:
    """根据滚动进度收集可见元素（累加模式）
    
    Args:
        scroll_segments: 滚动片段列表（按 threshold 升序）
        progress: 当前滚动进度（0.0-1.0）
        
    Returns:
        List[Dict[str, Any]]: 可见元素列表（已去重）
    """
    visible_elements = {}  # 使用 dict 去重，后面的覆盖前面的
    
    for segment in sorted(scroll_segments, key=lambda s: s.threshold):
        if segment.threshold <= progress:
            for element in segment.elements:
                element_id = element.get("id")
                if element_id:
                    # 后面的元素覆盖前面的（支持状态更新）
                    visible_elements[element_id] = element
    
    return list(visible_elements.values())
```

---

## 架构兼容性修复（新增）

### 问题4: 基类接口不兼容（OperationExecutor vs ActionExecutor）

#### 问题描述
设计文档假设 `StatefulMockActionExecutor` 继承 `ActionExecutor`，但现有代码使用 `OperationExecutor` 接口。

#### 现有代码确认
```python
# src/simulation/stateful_mock_action.py
class StatefulMockActionExecutor(OperationExecutor):
    """有状态的动作执行器，实现 OperationExecutor 接口"""
    
    def execute(self, context: ExecutionContext) -> ExecutionResult:
        # ...
```

#### 修复方案

**使用正确的基类**：

```python
from src.action.executor import OperationExecutor, ExecutionResult, ExecutionContext

class ScrollableMockActionExecutor(StatefulMockActionExecutor):
    """支持滚动动作的动作执行器
    
    继承现有的 StatefulMockActionExecutor，
    扩展滚动相关功能。
    """
    
    def __init__(
        self,
        vision_service: "ScrollableMockVisionService",
    ):
        # 调用父类初始化
        super().__init__(vision_service)
        
        # 滚动动作历史
        self._scroll_actions: List[ScrollAction] = []
    
    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """执行动作，扩展滚动支持"""
        operation = context.operation
        action_type = operation.get("action", "unknown")
        
        # 先尝试父类的处理
        if action_type in ["click", "back", "input_text"]:
            return super().execute(context)
        
        # 滚动相关动作
        if action_type == "scroll_down":
            return self._execute_scroll_down(operation.get("params", {}))
        elif action_type == "scroll_up":
            return self._execute_scroll_up(operation.get("params", {}))
        
        return ExecutionResult(
            success=False,
            error=f"Unknown action type: {action_type}"
        )
    
    def _execute_scroll_down(self, params: Dict[str, Any]) -> ExecutionResult:
        """执行向下滚动"""
        # ... 实现
```

---

### 问题5: PageAnalysis 模型不兼容（items vs elements）

#### 问题描述
设计文档假设 `PageAnalysis.elements: List[UIElement]`，但现有代码使用 `PageAnalysis.items: List[MenuItem]`。

#### 现有代码确认
```python
# src/state/content_tree.py
class PageAnalysis(BaseModel):
    has_scroll: bool = False
    is_end_of_list: bool = False
    items: List[MenuItem] = []  # 注意：是 items 不是 elements
```

#### 修复方案

**适配现有模型**：

```python
from src.state.content_tree import PageAnalysis, MenuItem

def _build_page_analysis(
    self,
    page_key: str,
    elements: List[Dict[str, Any]],
    has_scroll: bool,
    is_end_of_list: bool,
) -> PageAnalysis:
    """构建 PageAnalysis（适配现有模型）
    
    将滚动片段元素转换为 MenuItem 格式。
    
    Args:
        page_key: 页面键
        elements: 元素数据列表
        has_scroll: 是否可滚动
        is_end_of_list: 是否到达列表末尾
        
    Returns:
        PageAnalysis: 页面分析对象
    """
    menu_items = []
    for element in elements:
        # 提取坐标信息（兼容现有格式）
        coord = element.get("coordinate", {"x": 0.5, "y": 0.5})
        x = coord.get("x", 0.5) if isinstance(coord, dict) else 0.5
        y = coord.get("y", 0.5) if isinstance(coord, dict) else 0.5
        
        # 创建 MenuItem
        menu_item = MenuItem(
            id=element.get("id", ""),
            name=element.get("text", ""),
            type=element.get("type", "unknown"),
            coordinate={"x": x, "y": y},
        )
        menu_items.append(menu_item)
    
    return PageAnalysis(
        page_id=page_key,
        items=menu_items,  # 使用 items 字段
        has_scroll=has_scroll,
        is_end_of_list=is_end_of_list,
        timestamp=time.time(),
    )
```

---

### 问题6: 数据格式不兼容（coordinate vs bounds）

#### 问题描述
设计文档使用 `bounds: [x, y, w, h]`，但现有代码使用 `coordinate: {x, y}`。

#### 修复方案

**在测试数据中支持两种格式**：

```json
{
  "elements": [
    {
      "id": "wifi_switch",
      "text": "WiFi",
      "type": "switch",
      "coordinate": {"x": 0.5, "y": 0.1},
      "bounds": [0, 0, 100, 60]
    }
  ]
}
```

**转换逻辑**：

```python
def _extract_coordinate(element: Dict[str, Any]) -> Dict[str, float]:
    """提取元素坐标（兼容两种格式）
    
    优先使用 coordinate，如果不存在则从 bounds 推导。
    
    Args:
        element: 元素数据
        
    Returns:
        Dict[str, float]: {x, y} 坐标
    """
    # 优先使用 coordinate 格式
    if "coordinate" in element:
        coord = element["coordinate"]
        if isinstance(coord, dict):
            return {"x": coord.get("x", 0.5), "y": coord.get("y", 0.5)}
    
    # 从 bounds 推导（如果是 [x, y, w, h] 格式）
    if "bounds" in element:
        bounds = element["bounds"]
        if isinstance(bounds, list) and len(bounds) >= 2:
            x, y = bounds[0], bounds[1]
            # 归一化到 0-1 范围（假设屏幕宽度1080）
            return {
                "x": x / 1080 if x > 1 else 0.5,
                "y": y / 1920 if y > 1 else 0.5
            }
    
    # 默认居中
    return {"x": 0.5, "y": 0.5}
```

---

## 设计问题澄清

### 问题7: `simulate_jumps` 字段未使用

#### 澄清

**决策：标记为预留字段，暂不实现**。

```python
@dataclass
class ScrollState:
    """单个页面的滚动状态"""
    current_progress: float = 0.0
    last_scroll_time: Optional[float] = None
    scroll_count: int = 0
    scroll_history: List[float] = field(default_factory=list)
    
    # 故障注入
    fail_next_scroll: bool = False
    simulate_delay_ms: int = 0
    
    # TODO: simulate_jumps 预留给 V7.x
    # 用途：模拟滚动跳跃（例如：惯性滚动导致的突然跳跃）
    # simulate_jumps: bool = False
```

**文档说明**：

```markdown
### 未来扩展：跳跃模拟

**V7.x 预留功能**：

`simulate_jumps` 字段预留用于模拟以下场景：
- 惯性滚动导致的突然跳跃
- 动态内容加载引起的自动跳转
- 焦点变化导致的页面跳转

**当前版本**：暂不实现，相关测试场景延后。
```

---

### 问题8: 运行时动态故障注入

#### 澄清

**确认支持**：当前设计支持在任意时刻调用故障注入方法。

```python
# 运行时动态注入示例
def test_runtime_fault_injection():
    """测试运行时动态故障注入"""
    vision = ScrollableMockVisionService(...)
    action = ScrollableMockActionExecutor(vision)
    engine = GraphTraversalEngine(..., vision_service=vision, action_executor=action)
    
    # 启动引擎（异步）
    import asyncio
    task = asyncio.create_task(engine.run_async())
    
    # 等待引擎开始滚动
    await asyncio.sleep(1)
    
    # 在运行时注入延迟（第1次）
    vision.set_scroll_delay("wifi_list_page", 500)
    
    # 等待更多滚动
    await asyncio.sleep(1)
    
    # 注入无响应（第2次）
    vision.enable_scroll_failure("wifi_list_page", fail_once=True)
    
    # 等待完成
    result = await task
    
    # 验证引擎正确处理了故障
    assert result.status == GlobalState.COMPLETED
    assert action.get_scroll_count() > 0
```

**线程安全说明**：

```markdown
### 故障注入线程安全性

**当前实现**：单线程模型，无并发问题。

**未来扩展**：如果引擎支持并发执行：
- 需要为 `ScrollState` 添加线程锁
- 或使用 `threading.local` 实现线程本地状态
- 或使用消息队列序列化状态更新
```

---

### 问题9: 步长自适应的职责归属

#### 澄清与实现方案

**决策：使用方案A（在 Mock Vision Service 中实现）**

**理由**：
- ✅ 引擎无需修改，保持纯净
- ✅ 滚动逻辑集中，易于测试
- ✅ 符合 V7.0-SimScroll 的 Mock 测试定位

详见下一节《步长自适应策略深入设计》。

---

## 步长自适应策略深入设计

### 问题9深入分析

步长自适应是一个智能的滚动决策逻辑，核心目标是：
1. **确保覆盖所有元素**：避免步长过大跳过元素
2. **优化滚动效率**：避免步长过小导致过多的滚动次数
3. **处理边界情况**：检测到底、跳跃、无响应等情况

### 实现方案对比

#### 方案A: 在 Mock Vision Service 中实现（✅ 已选定）

**优点**：
- ✅ 引擎无需修改，保持纯净
- ✅ 滚动逻辑集中在 Mock 服务中
- ✅ 易于测试和调试

**缺点**：
- Mock 服务承担更多职责
- 需要维护跨调用的状态

**实现设计**：

```python
class ScrollableMockVisionService(StatefulMockVisionService):
    """支持滚动列表模拟的视觉服务（含自适应步长）"""
    
    def __init__(
        self,
        virtual_pages: Dict[str, Any],
        scroll_data_store: Optional["ScrollDataStore"] = None,
        adaptive_scroll: bool = True,  # 是否启用自适应滚动
    ):
        super().__init__(virtual_pages)
        
        # 滚动状态管理
        self._scroll_states: Dict[str, ScrollState] = {}
        self._scroll_data_store = scroll_data_store or ScrollDataStore(virtual_pages)
        self._adaptive_scroll = adaptive_scroll
        
        # 自适应滚动状态
        self._last_visible_elements: Dict[str, Set[str]] = {}  # 上次的可见元素ID
        self._scroll_step_sizes: Dict[str, float] = {}  # 当前步长
        self._min_step_size: float = 0.05  # 最小步长（5%）
        self._initial_step_size: float = 0.3  # 初始步长（30%）
        self._max_step_size: float = 0.5  # 最大步长（50%）
    
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """分析当前页面，返回 PageAnalysis（含自适应步长调整）"""
        page_key = self._resolve_path_key()
        scroll_state = self._get_scroll_state(page_key)
        
        # 获取滚动片段数据
        scroll_segments = self._scroll_data_store.get_scroll_segments(page_key)
        
        # 根据当前进度收集可见元素
        visible_elements = self._collect_visible_elements(
            scroll_segments, 
            scroll_state.current_progress
        )
        
        # 自适应滚动逻辑
        if self._adaptive_scroll:
            self._adjust_scroll_step(page_key, visible_elements)
        
        # 保存当前元素集合
        current_element_ids = {e.get("id") for e in visible_elements if e.get("id")}
        self._last_visible_elements[page_key] = current_element_ids
        
        # 判断是否可滚动
        has_scroll = (
            len(scroll_segments) > 0 
            and scroll_state.current_progress < 1.0
        )
        
        # 判断是否到底
        is_end_of_list = scroll_state.current_progress >= 1.0
        
        # 构建 PageAnalysis
        return self._build_page_analysis(
            page_key=page_key,
            elements=visible_elements,
            has_scroll=has_scroll,
            is_end_of_list=is_end_of_list,
        )
    
    def _adjust_scroll_step(
        self,
        page_key: str,
        current_elements: List[Dict[str, Any]],
    ) -> None:
        """根据元素变化调整滚动步长
        
        策略：
        1. 如果有新元素 → 保持或增大步长（尝试提高效率）
        2. 如果无新元素但有重叠 → 保持步长（可能到底）
        3. 如果无新元素且无重叠 → 检测到跳跃，减小步长
        
        Args:
            page_key: 页面键
            current_elements: 当前可见元素列表
        """
        current_ids = {e.get("id") for e in current_elements if e.get("id")}
        last_ids = self._last_visible_elements.get(page_key, set())
        
        # 初始化步长
        if page_key not in self._scroll_step_sizes:
            self._scroll_step_sizes[page_key] = self._initial_step_size
        
        current_step = self._scroll_step_sizes[page_key]
        
        # 检测跳跃
        overlap = current_ids & last_ids
        if not overlap and current_ids and last_ids:
            # 跳跃检测：无重叠元素
            new_step = max(self._min_step_size, current_step * 0.5)
            self._scroll_step_sizes[page_key] = new_step
            
            # 记录跳跃事件
            scroll_state = self._get_scroll_state(page_key)
            scroll_state.jump_detected = True
        elif len(current_ids) > len(last_ids):
            # 有新元素：可以尝试增大步长（提高效率）
            if current_step < self._max_step_size:
                self._scroll_step_sizes[page_key] = min(
                    self._max_step_size,
                    current_step * 1.1
                )
        # 其他情况：保持步长
    
    def get_recommended_scroll_step(self, page_key: str) -> float:
        """获取推荐的滚动步长
        
        Args:
            page_key: 页面键
            
        Returns:
            float: 推荐的滚动步长（0.0-1.0）
        """
        return self._scroll_step_sizes.get(page_key, self._initial_step_size)
```

**配合的 ActionExecutor 调整**：

```python
class ScrollableMockActionExecutor(StatefulMockActionExecutor):
    """支持滚动动作的动作执行器（含自适应步长）"""
    
    def _execute_scroll_down(self, params: Dict[str, Any]) -> ExecutionResult:
        """执行向下滚动（使用推荐步长）"""
        page_key = self._vision._resolve_path_key()
        
        # 获取推荐的滚动步长
        step = params.get("scroll_percent")
        if step is None:
            step = self._vision.get_recommended_scroll_step(page_key)
        
        before_progress = self._vision.get_scroll_progress(page_key)
        after_progress = self._vision.simulate_scroll(page_key, step)
        
        # 记录滚动动作
        scroll_action = ScrollAction(
            action=ScrollDirection.DOWN,
            path=page_key,
            step_percent=step,
            before_progress=before_progress,
            after_progress=after_progress,
            timestamp=time.time(),
        )
        self._scroll_actions.append(scroll_action)
        
        return ExecutionResult(success=True)
```

---

## 测试数据示例（已更新）

### 文件结构

```
fixtures/scroll/
├── wifi_list.json           # WiFi列表（多屏滚动）
├── single_screen.json        # 单屏列表（不滚动）
├── large_list.json          # 大列表（性能测试）
└── nested_scroll.json       # 嵌套滚动列表
```

### wifi_list.json 示例（兼容现有格式）

```json
{
  "wifi_list_page": {
    "path": "wifi_list_page",
    "has_scroll": true,
    "scroll_segments": [
      {
        "threshold": 0.0,
        "elements": [
          {
            "id": "wifi_switch",
            "text": "WiFi",
            "type": "switch",
            "coordinate": {"x": 0.5, "y": 0.05}
          },
          {
            "id": "net1",
            "text": "Network1",
            "type": "menu_item",
            "coordinate": {"x": 0.5, "y": 0.15}
          },
          {
            "id": "net2",
            "text": "Network2",
            "type": "menu_item",
            "coordinate": {"x": 0.5, "y": 0.25}
          }
        ]
      },
      {
        "threshold": 0.5,
        "elements": [
          {
            "id": "net3",
            "text": "Network3",
            "type": "menu_item",
            "coordinate": {"x": 0.5, "y": 0.35}
          },
          {
            "id": "net4",
            "text": "Network4",
            "type": "menu_item",
            "coordinate": {"x": 0.5, "y": 0.45}
          }
        ]
      },
      {
        "threshold": 1.0,
        "elements": [
          {
            "id": "net5",
            "text": "Network5",
            "type": "menu_item",
            "coordinate": {"x": 0.5, "y": 0.55}
          }
        ]
      }
    ]
  }
}
```

### 数据格式说明

| 字段 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| `path` | string | 是 | 页面路径键 | `"wifi_list_page"` |
| `has_scroll` | boolean | 是 | 是否可滚动 | `true` |
| `scroll_segments` | array | 是 | 滚动片段数组 | - |
| `threshold` | float | 是 | 激活阈值 (0.0-1.0) | `0.5` |
| `elements` | array | 是 | 该片段的元素数组 | - |
| `id` | string | 是 | 元素唯一ID | `"net1"` |
| `text` | string | 是 | 元素显示文本 | `"Network1"` |
| `type` | string | 是 | 元素类型 | `"menu_item"` |
| `coordinate` | object | 推荐 | 元素中心坐标 | `{"x": 0.5, "y": 0.15}` |

---

## 实施优先级

### P0 - 必须在实施前解决（3小时）

| 问题 | 修复方式 | 预计工时 |
|------|----------|----------|
| #1 路径管理不兼容 | 使用 `_current_page_id` 模式 | 0.5h |
| #2 元素ID冲突 | 实现进度法+索引法 | 1h |
| #3 片段语义不明确 | 添加文档说明（累加模式） | 0.5h |
| #4 基类接口不兼容 | 使用 OperationExecutor | 0.5h |
| #5 PageAnalysis模型不兼容 | 适配 items/MenuItem | 0.5h |
| #6 数据格式不兼容 | 支持 coordinate 格式 | 0.5h |
| #8 缺少测试数据示例 | 添加完整JSON示例 | 0.5h |

**小计**: 4.5小时

### P1 - 实施过程中实现（3小时）

| 问题 | 修复方式 | 预计工时 |
|------|----------|----------|
| #7 `simulate_jumps` | 标记为预留字段 | 0.5h |
| #8 运行时故障注入 | 添加文档说明 | 0.5h |
| #9 步长自适应策略 | 实现方案A（Mock Vision） | 2h |

**小计**: 3小时

### P2 - 可选优化（1小时）

| 项目 | 内容 | 预计工时 |
|------|------|----------|
| 文档完善 | 格式调整、性能标准说明 | 1h |

**小计**: 1小时

---

## 总工时调整

| 类别 | 原计划 | 修复增加 | 新总计 |
|------|--------|----------|--------|
| 原实施计划 | 19h | - | 19h |
| P0 关键修复 | - | 4.5h | 23.5h |
| P1 设计实现 | - | 3h | 26.5h |
| P2 可选优化 | - | 1h | 27.5h |

---

## 附录：完整修复检查清单

### 文档更新

- [x] 补充 `StatefulMockVisionService` 基类实际接口说明
- [x] 明确使用 `_current_page_id` 模式而非 `_current_path`
- [x] 明确滚动片段的累加语义
- [x] 添加完整的测试数据JSON示例（coordinate格式）
- [x] 添加步长自适应策略的详细说明
- [x] 添加 OperationExecutor 接口说明
- [x] 添加 PageAnalysis 模型兼容性说明

### 代码实现

- [ ] 路径管理使用 `_current_page_id` 模式
- [ ] 元素ID生成使用进度法+索引法（四参数）
- [ ] `_ensure_element_ids` 添加 segment_index 参数
- [ ] `simulate_jumps` 标记为预留字段（TODO注释）
- [ ] 继承 `OperationExecutor` 而非 `ActionExecutor`
- [ ] 适配 `PageAnalysis.items` 和 `MenuItem`
- [ ] 支持 `coordinate` 格式的坐标提取
- [ ] 步长自适应逻辑实现（方案A）

### 测试验证

- [ ] 验证路径解析正确性（使用 _current_page_id）
- [ ] 验证元素ID唯一性（跨位置）
- [ ] 验证滚动片段累加行为
- [ ] 验证步长自适应效果
- [ ] 验证故障注入功能
- [ ] 验证与现有代码的集成

---

## 新增问题（对抗审阅发现）

### 潜在问题（需关注）

1. **性能问题**：累加模式对大列表可能效率低下
   - 缓解：限制单列表元素总数在200以内

2. **嵌套滚动**：当前设计不支持一个页面多个滚动区域
   - 缓解：V7.0 暂不支持，V7.x 预留扩展

3. **线程安全**：当前实现为单线程模型
   - 缓解：文档说明，未来扩展时添加锁

4. **状态重置**：页面切换时滚动状态的处理
   - 缓解：基于 `_current_page_id` 自动隔离

---

**文档所有者**: Uni-Claw 开发团队
**状态**: 修复建议 v2.0（已根据多代理评审更新）
**相关文档**: 
- [DESIGN_V7_0_SimScroll.md](./DESIGN_V7_0_SimScroll.md)
- [PRD_V7_0_SimScroll.md](./PRD_V7_0_SimScroll.md)
