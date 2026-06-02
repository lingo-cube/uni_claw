# uni-claw 两步视觉管道 - PRD V5.2

> **文档版本**: V5.2
> **基于版本**: PRD_V5.1 (2026-05-31)
> **创建日期**: 2026-06-02
> **状态**: 设计阶段
> **变更类型**: 新增视觉分析管道

---

## 文档说明

PRD V5.2 定义了**两步视觉管道**的数据模型和架构设计。通过将视觉感知与逻辑推理解耦，实现更精准、高效、低成本的屏幕分析。

**核心创新**：
- **多模态模型**只负责"看图说话"——输出扁平化元素列表
- **文本模型**负责逻辑推理——组装层级、推断行为
- **Token 消耗减少 60%+**，**速度提升 30%~50%**，**成本减半**

---

# 1. 产品概述

## 1.1 背景与动机

当前一体化视觉方案存在三个核心问题：

| 问题 | 描述 | 影响 |
|------|------|------|
| **准确率低** | 多模态模型不擅长逻辑推理（层级判断、父子关系、行为推断） | 误判率高 |
| **耗时长** | 输出复杂结构导致响应延迟 | 用户体验差 |
| **成本高** | 大量 Token 消耗在结构化输出上 | 运营成本高 |

## 1.2 解决方案：感知与认知解耦

```
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│  多模态模型     │      │   文本模型      │      │  PageAnalysis   │
│  (感知)         │  →   │   (认知)         │  →   │  (最终输出)     │
└─────────────────┘      └─────────────────┘      └─────────────────┘
        ↓                        ↓                        ↓
   FlattenedScreen        PageAnalysisAssembler    完整层级结构
   (扁平化元素)           (逻辑组装与推断)          + 行为推断
```

**核心思想**：让每个模型做它最擅长的事
- **多模态模型**：识别视觉元素（是什么、在哪里）
- **文本模型**：推理逻辑关系（谁属于谁、点击后会怎样）

## 1.3 预期收益

| 指标 | 当前 | 目标 | 改善 |
|------|------|------|------|
| 输出 Token | 100% | 40% | -60% |
| 响应速度 | 1x | 1.3x-1.5x | +30%~50% |
| API 成本 | 100% | 50% | -50% |
| 层级准确率 | 70% | 90%+ | +20% |

---

# 2. 核心数据模型

## 2.1 模型层次关系

```
PageAnalysis (现有)
        ↑
        │ 由 PageAnalysisAssembler 组装
        │
FlattenedScreen (新增)
        ↑
        │ 由多模态模型输出
        │
Screenshot (输入)
```

## 2.2 BoundingBox（边界框）

**用途**：描述元素在屏幕中的位置和大小（归一化坐标）

```python
@dataclass
class BoundingBox:
    """归一化边界框，描述元素在屏幕中的位置和大小"""
    x: float  # 左上角 x，归一化 0~1
    y: float  # 左上角 y
    w: float  # 宽度
    h: float  # 高度
```

**设计要点**：
- 所有坐标归一化到 [0, 1] 范围
- 便于不同分辨率屏幕之间的比较
- 支持快速的空间关系计算（包含、重叠、相邻）

## 2.3 Region（屏幕区域）

**用途**：描述页面布局结构（如左侧菜单区、右侧内容区）

```python
@dataclass
class Region:
    """屏幕区域划分，用于描述布局结构"""
    id: str            # 唯一标识，如 "left_panel", "top_bar"
    bounds: BoundingBox # 区域边界
    role: str          # 区域角色：menu / content / tabs / overlay / unknown
```

**区域角色类型**：
- `menu` - 菜单区域（通常在左侧或顶部）
- `content` - 内容区域（通常在右侧或中央）
- `tabs` - 标签页区域
- `overlay` - 覆盖层/弹窗区域
- `unknown` - 未知类型

## 2.4 TypeHint 枚举（粗略视觉类型）

**用途**：多模态模型对元素的粗略视觉分类，不包含行为推理

```python
class TypeHint(str, Enum):
    """粗略视觉类型提示，仅基于视觉特征"""
    CLICKABLE_TEXT = "clickable_text"   # 可点击文本区域
    SWITCH = "switch"                   # 开关控件
    SLIDER = "slider"                   # 滑块控件
    BUTTON = "button"                   # 按钮控件
    ICON = "icon"                       # 纯图标元素
    INPUT_FIELD = "input_field"         # 输入框
    TEXT = "text"                       # 纯文本元素（不可交互）
    IMAGE = "image"                     # 图片元素
```

**设计要点**：
- 仅描述"看起来像什么"，不描述"点击后会怎样"
- 简化的类型集合，降低多模态模型的分类负担
- 后续由文本模型将其映射为更精确的 `MenuItemType`

## 2.5 SelectionState 枚举（选中状态）

**用途**：描述元素的选中/激活状态，用于识别当前活跃的菜单项或标签页

```python
class SelectionState(str, Enum):
    """元素的选中/激活状态"""
    SELECTED = "selected"       # 当前选中/高亮
    NORMAL = "normal"           # 正常未选中
    DISABLED = "disabled"       # 禁用状态（灰显）
```

**状态说明**：

| 状态 | 视觉特征 | 用途 |
|------|----------|------|
| `SELECTED` | 高亮、加粗、有选中标记、不同背景色 | 识别当前激活的菜单项/标签 |
| `NORMAL` | 正常显示，无特殊样式 | 默认状态 |
| `DISABLED` | 灰色显示、半透明、无法交互 | 识别不可操作元素 |

**设计要点**：
- 提供结构化的状态表示，而非字典中的布尔字段
- 帮助文本模型快速定位当前路径（`current_path`）
- 与 `visual_state` 字段配合使用，提供更丰富的状态信息

## 2.6 FlattenedElement（扁平元素）

**用途**：描述单个视觉元素的所有可感知信息

```python
@dataclass
class FlattenedElement:
    """扁平化元素，多模态模型输出的单个元素信息"""
    id: int                              # 元素唯一标识（在本次分析内）
    text: str = ""                       # 元素上显示的文本
    type_hint: TypeHint = TypeHint.TEXT  # 粗略视觉类型
    bbox: BoundingBox                    # 边界框
    region: Optional[str] = None         # 所属区域 ID
    selection_state: SelectionState = SelectionState.NORMAL  # 选中状态
    visual_state: Dict[str, Any]         # 视觉状态描述
    confidence: float = 1.0              # 识别置信度 0.0 ~ 1.0
```

**字段详解**：

| 字段 | 说明 | 示例 |
|------|------|------|
| `id` | 本次分析内的唯一标识，从 0 开始递增 | 0, 1, 2, ... |
| `text` | 显示的文本内容，图标/图片可留空 | "WiFi", "设置", "" |
| `type_hint` | 基于外观的粗略类型 | `TypeHint.SWITCH` |
| `bbox` | 精确位置信息 | `BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)` |
| `region` | 所属区域 ID | "left_panel" |
| `selection_state` | 选中状态 | `SelectionState.SELECTED` |
| `visual_state` | 额外视觉状态，如加粗、指示器等 | `{"bold": true, "has_indicator": "filled_circle"}` |
| `confidence` | 识别置信度 | 0.95 |

## 2.7 FlattenedScreen（扁平化屏幕）

**用途**：多模态模型的完整输出，不包含任何层级推理

```python
@dataclass
class FlattenedScreen:
    """多模态模型输出的扁平化屏幕描述"""
    elements: List[FlattenedElement]      # 扁平化元素列表
    screen_hints: Dict[str, Any]          # 屏幕级别提示信息
```

**screen_hints 可选字段**：

| 字段 | 类型 | 说明 | 示例 |
|------|------|------|------|
| `top_bar_text` | str | 顶部标题栏文本 | "设置" |
| `layout_type` | str | 布局类型 | "split_pane", "tabbed", "single", "overlay" |
| `regions` | List[Region] | 屏幕区域划分 | `[Region(id="left_panel", ...)]` |
| `overlay_detected` | bool | 是否疑似有弹窗/覆盖层 | `true` |
| `scroll_detected` | bool | 页面是否可滚动 | `false` |

**元素排序**：按从上到下、从左到右的顺序排列，便于文本模型分析空间关系。

---

# 3. PageAnalysisAssembler（文本模型组装器）

## 3.1 职责

接收 `FlattenedScreen` 和可选的遍历上下文，输出标准的 `PageAnalysis`。

## 3.2 核心任务

### 3.2.1 控件精确分类

将 `TypeHint` 映射为 `MenuItemType`，需结合上下文判断：

| TypeHint | 可能的 MenuItemType | 判断依据 |
|----------|-------------------|----------|
| `CLICKABLE_TEXT` | `MENU_ITEM` 或 `BUTTON` | 位置、上下文、历史信息 |
| `SWITCH` | `SWITCH` | 直接映射 |
| `BUTTON` | `BUTTON` 或 `TOGGLE` | 是否有状态指示 |
| `ICON` | `ICON` 或 `BUTTON` | 是否可点击、周围元素 |

### 3.2.2 行为推断

为每个 `MenuItem` 指定 `expected_action`：

| 预期行为 | 判断依据 |
|----------|----------|
| `navigate` | 位于菜单区域、有子元素 |
| `toggle` | 类型为 SWITCH/TOGGLE |
| `action` | 按钮类、不在菜单区域 |
| `back` | 位于顶部/左上角、文本为"返回" |
| `input` | 类型为 INPUT_FIELD |

### 3.2.3 层级构建

利用以下信息推断元素层级关系：

- **坐标关系**：包含、相邻、重叠
- **区域归属**：同一区域内的元素可能属于同一层级
- **视觉状态**：高亮、加粗等指示激活状态
- **文本缩进**：左右位置关系暗示层级

### 3.2.4 菜单结构提取

识别并填充：
- `level1_menus`：一级菜单列表
- `level2_menus`：二级标签列表
- `current_path`：当前激活路径（基于 `visual_state.highlighted`）

### 3.2.5 弹窗识别

判断是否为弹窗及弹窗类型：

- **位置中心化**：元素集中在屏幕中央
- **背景遮罩**：检测半透明背景层
- **元素数量**：弹窗通常元素较少
- **关闭按钮**：检测 X 或关闭按钮

---

# 4. 实现架构

## 4.1 组件划分

```
┌─────────────────────────────────────────────────────────────┐
│                    VisionPipeline                           │
│  ┌──────────────────────┐      ┌──────────────────────┐    │
│  │  MultimodalAnalyzer  │      │ PageAnalysisAssembler│    │
│  │  (多模态分析器)       │  →   │  (页面组装器)         │    │
│  └──────────────────────┘      └──────────────────────┘    │
│            ↓                              ↓                   │
│    FlattenedScreen                  PageAnalysis             │
└─────────────────────────────────────────────────────────────┘
```

## 4.2 组件职责

| 组件 | 输入 | 输出 | 模型 |
|------|------|------|------|
| `MultimodalAnalyzer` | `bytes` (截图) | `FlattenedScreen` | 多模态模型 |
| `PageAnalysisAssembler` | `FlattenedScreen` + 上下文 | `PageAnalysis` | 文本模型 |

## 4.3 缓存策略

### 4.3.1 FlattenedScreen 缓存

```python
@dataclass
class ScreenCache:
    """屏幕分析缓存"""
    screen_hash: str      # 截图指纹
    flattened_screen: FlattenedScreen
    created_at: datetime
```

**缓存键**：截图内容哈希（如 perceptual hash）

**缓存策略**：
- 优先从缓存读取 `FlattenedScreen`
- 若命中缓存，跳过多模态模型调用
- 仅当缓存未命中时调用多模态模型

### 4.3.2 PageAnalysis 缓存

```python
@dataclass
class PageAnalysisCache:
    """页面分析缓存"""
    screen_hash: str           # FlattenedScreen 指纹
    context_hash: str          # 上下文指纹
    page_analysis: PageAnalysis
    created_at: datetime
```

**缓存键**：`FlattenedScreen` 内容哈希 + 上下文哈希

**缓存策略**：
- 相同屏幕 + 相同上下文 → 复用 `PageAnalysis`
- 相同屏幕 + 不同上下文 → 仅调用文本模型

## 4.4 指纹生成

### 4.4.1 截图指纹

```python
def generate_screen_hash(image_data: bytes) -> str:
    """生成截图指纹（perceptual hash）"""
    # 使用感知哈希算法，对微小变化不敏感
    return perceptual_hash(image_data)
```

### 4.4.2 FlattenedScreen 指纹

```python
def generate_flattened_hash(screen: FlattenedScreen) -> str:
    """生成 FlattenedScreen 指纹"""
    # 基于元素数量、类型分布、空间布局生成
    return structural_hash(screen.elements, screen.screen_hints)
```

---

# 5. Prompt 设计

## 5.1 多模态模型 Prompt

**核心原则**：只要求视觉感知，禁止逻辑推理

```
你是一个车机 UI 视觉分析专家。请分析提供的截图，输出屏幕上所有可见元素的信息。

对于每个元素，请提供：
1. text: 元素上显示的文本（如无文本则留空）
2. type_hint: 元素类型（只能是：clickable_text, switch, slider, button, icon, input_field, text, image）
3. bbox: 边界框坐标（归一化 0-1，格式：x,y,w,h）
4. region: 所属区域（如 left_panel, content_area, top_bar, tabs, null）
5. selection_state: 选中状态（selected = 高亮/选中, normal = 正常, disabled = 禁用/灰显）
6. visual_state: 额外视觉状态（如 bold, dimmed, has_indicator）
7. confidence: 识别置信度（0-1）

额外信息：
- top_bar_text: 顶部标题栏文本
- layout_type: 布局类型（split_pane, tabbed, single, overlay, unknown）
- overlay_detected: 是否有弹窗/覆盖层（true/false）
- scroll_detected: 是否可滚动（true/false）

重要：
- 仅描述视觉特征，不要推断元素行为或功能
- 不要推断父子关系或层级结构
- 元素按从上到下、从左到右顺序排列
```

## 5.2 文本模型 Prompt

**核心原则**：利用思维链进行逻辑推理

```
你是一个车机 UI 逻辑分析专家。基于提供的扁平化元素列表和上下文信息，推断出完整的页面结构。

输入：
1. flattened_screen: 扁平化元素列表
2. context: 当前遍历上下文（当前路径、历史页面等）

任务：
1. 分析布局结构（分栏、标签页、单页等）
2. 确定区域角色和边界
3. 为每个元素分类（menu_item, tab, switch, button 等）
4. 推断元素行为（navigate, toggle, action, back 等）
5. 构建层级关系（父子关系）
6. 识别当前激活路径
7. 检测弹窗（如有）

请按以下格式输出 PageAnalysis JSON...
```

---

# 6. 实施策略

## 6.1 双模式运行（新旧共存）

为确保平滑过渡和系统稳定性，采用双模式运行策略：

### 6.1.1 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                    VisionService (接口)                      │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐      ┌──────────────────────┐    │
│  │  LegacyVisionService │      │  FlattenedVisionService│  │
│  │  (当前一体化方案)     │      │  (新两步管道方案)      │    │
│  │  └─ 兜底保障          │      │  └─ 默认启用           │    │
│  └──────────────────────┘      └──────────────────────┘    │
│              ↓                           ↓                   │
│       PageAnalysis (旧)           PageAnalysis (新)          │
└─────────────────────────────────────────────────────────────┘
```

### 6.1.2 切换机制

通过配置控制使用哪种方案：

```python
# config/settings.py
class VisionServiceConfig(BaseSettings):
    """视觉服务配置"""
    mode: Literal["legacy", "flattened", "dual"] = "flattened"
    
    # flattened 模式配置
    multimodal_model: str = "claude-3-5-sonnet-20241022"
    text_model: str = "deepseek-v4-flash"
    
    # 缓存配置
    enable_cache: bool = True
    cache_ttl: int = 300  # 5分钟
```

**模式说明**：
- `legacy`: 强制使用旧方案（兜底）
- `flattened`: 强制使用新两步管道（默认）
- `dual`: 同时运行两种方案，结果对比并记录

### 6.1.3 降级策略

```python
class FlattenedVisionService:
    """新两步管道视觉服务"""
    
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        try:
            # Step 1: 多模态视觉感知
            flattened = self.multimodal_analyzer.analyze(image_data)
            
            # Step 2: 文本模型逻辑组装
            page_analysis = self.assembler.assemble(
                flattened, 
                context=self.context
            )
            
            # 记录性能数据
            self.metrics.record_success()
            return page_analysis
            
        except Exception as e:
            # 降级到旧方案
            logger.warning(f"Flattened pipeline failed: {e}, falling back to legacy")
            return self.legacy_service.analyze_screenshot(image_data)
```

---

# 7. 测试策略

## 7.1 测试数据准备

### 7.1.1 测试截图集

创建标准测试数据集用于性能对比：

```
tests/assets/screenshots/
├── settings_main.png          # 设置主页（分栏布局）
├── settings_display.png       # 显示设置（含开关）
├── settings_network.png       # 网络设置（含列表）
├── dialog_confirm.png         # 确认弹窗
├── dialog_input.png           # 输入弹窗
├── tabbed_view.png            # 标签页视图
├── single_page.png            # 单页视图
└── overlay_popup.png          # 覆盖层弹窗
```

### 7.1.2 标注数据

人工标注的标准答案（ground truth）：

```
tests/assets/ground_truth/
├── settings_main.json         # 标准 PageAnalysis
├── settings_display.json
├── settings_network.json
└── ...
```

## 7.2 对比测试框架

### 7.2.1 性能测试

```python
# tests/vision/performance_comparison.py

@dataclass
class PerformanceMetrics:
    """性能指标记录"""
    screenshot: str
    mode: str  # "legacy" or "flattened"
    
    # 延迟指标
    multimodal_latency_ms: float
    text_latency_ms: float
    total_latency_ms: float
    
    # Token 消耗
    input_tokens: int
    multimodal_output_tokens: int
    text_output_tokens: int
    total_tokens: int
    
    # 准确率指标
    hierarchy_accuracy: float      # 层级结构准确率
    behavior_accuracy: float        # 行为推断准确率
    popup_detection_accuracy: float # 弹窗检测准确率
    
    # 缓存指标
    cache_hit: bool
    
    # 时间戳
    timestamp: datetime

class PerformanceComparison:
    """性能对比测试"""
    
    def test_both_modes(self, screenshot_path: str) -> tuple:
        """同时运行新旧两种方案"""
        
        # 旧方案
        legacy_result = self.test_legacy_mode(screenshot_path)
        
        # 新方案
        flattened_result = self.test_flattened_mode(screenshot_path)
        
        # 对比
        return legacy_result, flattened_result
    
    def generate_report(self) -> dict:
        """生成性能对比报告"""
        return {
            "token_reduction": self.calculate_token_reduction(),
            "speed_improvement": self.calculate_speed_improvement(),
            "accuracy_comparison": self.calculate_accuracy_comparison(),
            "cache_effectiveness": self.calculate_cache_hit_rate(),
        }
```

### 7.2.2 准确率测试

```python
# tests/vision/accuracy_test.py

def test_hierarchy_accuracy():
    """测试层级结构准确率"""
    ground_truth = load_ground_truth("settings_main.json")
    result = vision_service.analyze_screenshot(load_screenshot("settings_main.png"))
    
    # 对比 level1_menus, level2_menus, current_path
    score = compare_hierarchy(ground_truth, result)
    assert score >= 0.90, f"Hierarchy accuracy {score} below 90%"

def test_behavior_inference():
    """测试行为推断准确率"""
    for item in result.items:
        # 验证 expected_action 与 ground_truth 一致
        assert item.expected_action == truth_item.expected_action

def test_popup_detection():
    """测试弹窗检测准确率"""
    result = vision_service.analyze_screenshot(load_screenshot("dialog_confirm.png"))
    assert result.is_popup == True
    assert result.popup_info is not None
```

## 7.3 持续监控

### 7.3.1 运行时数据收集

```python
# src/vision/metrics.py

class VisionMetricsCollector:
    """视觉服务指标收集器"""
    
    def record_call(self, mode: str, metrics: PerformanceMetrics):
        """记录每次调用的性能数据"""
        self.data.append({
            "timestamp": datetime.now(),
            "mode": mode,
            "screenshot_hash": metrics.screenshot_hash,
            "latency_ms": metrics.total_latency_ms,
            "tokens": metrics.total_tokens,
            "accuracy_score": metrics.calculate_accuracy(),
        })
    
    def get_summary(self, days: int = 7) -> dict:
        """获取指定时间段的汇总数据"""
        return {
            "avg_latency": calculate_average_latency(self.data, days),
            "avg_tokens": calculate_average_tokens(self.data, days),
            "avg_accuracy": calculate_average_accuracy(self.data, days),
            "cache_hit_rate": calculate_cache_hit_rate(self.data, days),
        }
```

### 7.3.2 日志格式

```json
{
  "timestamp": "2026-06-02T10:30:00Z",
  "screenshot_hash": "a1b2c3d4",
  "mode": "flattened",
  "step1_multimodal": {
    "latency_ms": 1234,
    "input_tokens": 500,
    "output_tokens": 380
  },
  "step2_text": {
    "latency_ms": 456,
    "input_tokens": 800,
    "output_tokens": 420
  },
  "total": {
    "latency_ms": 1690,
    "tokens": 1600
  },
  "accuracy": {
    "hierarchy": 0.95,
    "behavior": 0.88,
    "popup": 1.0
  },
  "cache": {
    "flattened_hit": true,
    "page_analysis_hit": false
  }
}
```

---

# 8. 性能目标与验收标准

## 8.1 性能对比指标

| 指标 | 旧方案 (baseline) | 新方案 (target) | 改善幅度 |
|------|-------------------|-----------------|----------|
| **Token 消耗** | 100% | ≤40% | -60% |
| **响应延迟** | 1x | ≤0.7x | +30% |
| **多模态输出 Token** | ~800 | ~350 | -56% |
| **文本模型输出 Token** | 0 | ~400 | 新增 |
| **总输出 Token** | ~800 | ~750 | -6% |
| **层级准确率** | 70% | ≥90% | +20% |
| **行为推断准确率** | 65% | ≥85% | +20% |
| **弹窗检测准确率** | 80% | ≥95% | +15% |

## 8.2 验收标准

### 8.2.1 功能验收

- [ ] 多模态模型能正确输出 `FlattenedScreen`
- [ ] 文本模型能正确组装 `PageAnalysis`
- [ ] 新旧方案切换功能正常
- [ ] 降级机制工作正常
- [ ] 层级推断准确率 ≥ 90%
- [ ] 行为推断准确率 ≥ 85%
- [ ] 弹窗检测准确率 ≥ 95%

### 8.2.2 性能验收

- [ ] Token 消耗减少 ≥ 60%
- [ ] 响应延迟减少 ≥ 30%
- [ ] 缓存命中率 ≥ 70%（重复页面）
- [ ] 降级延迟增加 ≤ 20%

### 8.2.3 质量验收

- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 集成测试通过率 100%
- [ ] 性能对比测试通过
- [ ] 代码审查通过

---

# 9. 实施计划

## 9.1 阶段划分

| 阶段 | 任务 | 交付物 | 估时 |
|------|------|--------|------|
| **P1 - 数据模型** | 定义 FlattenedScreen 相关模型 | `src/vision/flattened_screen.py` | 1天 |
| **P2 - 多模态分析器** | 实现 MultimodalAnalyzer | `src/vision/multimodal_analyzer.py` | 2天 |
| **P3 - 页面组装器** | 实现 PageAnalysisAssembler | `src/vision/page_assembler.py` | 3天 |
| **P4 - 双模式集成** | 实现新旧方案共存 | `src/vision/vision_service_v2.py` | 2天 |
| **P5 - 缓存系统** | 实现双层缓存 | `src/vision/cache.py` | 1天 |
| **P6 - 测试框架** | 性能对比和准确率测试 | `tests/vision/performance_comparison.py` | 2天 |
| **P7 - 验证与优化** | 端到端测试和 Prompt 优化 | 测试报告、优化建议 | 2天 |

## 9.2 验收标准

### 9.2.1 功能验收

- [ ] 多模态模型能正确输出 `FlattenedScreen`
- [ ] 文本模型能正确组装 `PageAnalysis`
- [ ] 新旧方案切换功能正常
- [ ] 降级机制工作正常（新方案失败时自动切换到旧方案）
- [ ] 缓存系统工作正常
- [ ] 层级推断准确率 ≥ 90%
- [ ] 行为推断准确率 ≥ 85%
- [ ] 弹窗检测准确率 ≥ 95%

### 9.2.2 性能验收

- [ ] Token 消耗减少 ≥ 60%
- [ ] 响应延迟减少 ≥ 30%
- [ ] 缓存命中率 ≥ 70%（重复页面）
- [ ] 降级延迟增加 ≤ 20%
- [ ] 性能对比测试通过

### 9.2.3 质量验收

- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 集成测试通过率 100%
- [ ] 性能对比测试通过
- [ ] 代码审查通过
- [ ] 测试数据集完整（≥10张标准截图）

---

# 10. 风险与缓解

## 10.1 技术风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 多模态模型类型识别不准 | 中 | 中 | 优化 Prompt，增加示例，使用 Few-shot |
| 文本模型层级推断错误 | 高 | 低 | 提供清晰规则，思维链引导 |
| 两步调用延迟增加 | 中 | 低 | 缓存 FlattenedScreen，并行优化 |
| 缓存指纹冲突 | 低 | 低 | 使用鲁棒哈希算法，设置 TTL |
| 降级机制复杂度 | 中 | 低 | 统一接口，透明切换 |
| 新旧方案结果不一致 | 中 | 中 | 对比记录，以新方案为准，记录差异 |

## 10.2 业务风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 两步调用总成本增加 | 中 | 低 | Token 消耗监控，设置预算告警 |
| 多模态 + 文本双重成本 | 中 | 中 | 成本对比分析，必要时切换到旧方案 |
| 模型可用性 | 高 | 低 | 多模型备选，降级到旧方案 |
| 成本超预期 | 低 | 低 | 设置 Token 预算告警 |

---

# 8. 附录

## 8.1 与现有模型的映射

| FlattenedScreen 字段 | PageAnalysis 字段 | 映射逻辑 |
|---------------------|-------------------|----------|
| `text` + `type_hint` | `MenuItem.name` + `type` | 文本模型推断 |
| `bbox` | `Coordinate` | 坐标转换 |
| `region` + `visual_state` | `parent` + 状态 | 上下文推理 |
| `screen_hints.layout_type` | `PageAnalysis` 结构 | 结构推断 |

## 8.2 术语表

| 术语 | 定义 |
|------|------|
| 扁平化 | 将嵌套结构展平为线性列表 |
| 感知与认知解耦 | 视觉感知（是什么）与逻辑推理（意味着什么）分离 |
| 归一化坐标 | 将像素坐标映射到 [0, 1] 范围 |
| 感知哈希 | 对内容微小变化不敏感的哈希算法 |

---

**文档版本**: V5.2
**最后更新**: 2026-06-02
**维护者**: Uni-Clow 开发团队
