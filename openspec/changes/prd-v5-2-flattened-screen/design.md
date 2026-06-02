# Design: PRD V5.2 两步视觉管道实现

**Change ID**: `prd-v5-2-flattened-screen`
**Created**: 2026-06-02
**Status**: Design Phase

---

## 1. 架构设计 (Architecture)

### 1.1 系统架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Uni-Claw 遍历引擎                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    VisionService (接口)                          │   │
│  ├─────────────────────────────────────────────────────────────────┤   │
│  │                                                                 │   │
│  │  ┌──────────────────────┐      ┌──────────────────────────┐    │   │
│  │  │  LegacyVisionService │      │ FlattenedVisionService  │    │   │
│  │  │  (一体化方案)         │      │ (两步管道方案)           │    │   │
│  │  │                       │      │                          │    │   │
│  │  │  ┌────────────────┐  │      │ ┌────────────────────┐  │    │   │
│  │  │  │ Multimodal AI  │  │      │ │MultimodalAnalyzer  │  │    │   │
│  │  │  │(Claude Sonnet) │  │      │ │  ↓                 │  │    │   │
│  │  │  └────────┬───────┘  │      │ │FlattenedScreen     │  │    │   │
│  │  │           │           │      │ │  ↓                 │  │    │   │
│  │  │           │           │      │ │PageAnalysis        │  │    │   │
│  │  │           │           │      │ │Assembler          │  │    │   │
│  │  │           │           │      │ │  ↓                 │  │    │   │
│  │  │           ▼           │      │ │PageAnalysis        │  │    │   │
│  │  │      PageAnalysis     │      │ └────────────────────┘  │    │   │
│  │  └──────────────────────┘      └──────────────────────────┘    │   │
│  │                                                                 │   │
│  │  配置: config.settings.VisionServiceConfig.mode                 │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              ↓                                          │
│                         PageAnalysis                                    │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 组件关系图

```
┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│  Screenshot      │      │ FlattenedScreen  │      │ PageAnalysis      │
│  (输入)          │  →   │ (中间产物)        │  →   │ (最终输出)        │
└──────────────────┘      └──────────────────┘      └──────────────────┘
         ↓                         ↓                          ↓
    bytes (PNG)            List[FlattenedElement]        层级结构
```

### 1.3 数据流图

```
┌──────────────┐
│  ADB 截图     │
└──────┬───────┘
       │ bytes
       ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ScreenCache (检查缓存)                         │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │ Cache Key: perceptual_hash(image_data)                     │   │
│  └────────────────────────────────────────────────────────────┘   │
└──────┬────────────────────────────────────────────────────────────┘
       │
       ├─ Hit ──▶ FlattenedScreen (from cache)
       │
       └─ Miss ──▶ ┌────────────────────────────────────────────────┐
                    │  MultimodalAnalyzer.analyze()                 │
                    │  ┌──────────────────────────────────────────┐ │
                    │  │ Input: image_data (bytes)                 │ │
                    │  │ Model: Claude Sonnet 3.5                  │ │
                    │  │ Output: FlattenedScreen (JSON)            │ │
                    │  └──────────────────────────────────────────┘ │
                    └────────────────┬───────────────────────────────┘
                                     │ FlattenedScreen
                                     ▼
                    ┌────────────────────────────────────────────────┐
                    │  Cache FlattenedScreen                         │
                    └────────────────┬───────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────┐
│              PageAnalysisCache (检查缓存)                        │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │ Cache Key: hash(FlattenedScreen) + hash(context)          │   │
│  └────────────────────────────────────────────────────────────┘   │
└──────┬────────────────────────────────────────────────────────────┘
       │
       ├─ Hit ──▶ PageAnalysis (from cache)
       │
       └─ Miss ──▶ ┌────────────────────────────────────────────────┐
                    │  PageAnalysisAssembler.assemble()             │
                    │  ┌──────────────────────────────────────────┐ │
                    │  │ Input: FlattenedScreen + context         │ │
                    │  │ Model: DeepSeek V4                       │ │
                    │  │ Output: PageAnalysis (JSON)               │ │
                    │  └──────────────────────────────────────────┘ │
                    └────────────────┬───────────────────────────────┘
                                     │ PageAnalysis
                                     ▼
                              ┌──────────────┐
                              │ 返回结果      │
                              └──────────────┘
```

---

## 2. 数据模型设计 (Data Models)

### 2.1 模型层次结构

```
src/models/vision/
├── __init__.py
├── bounding_box.py          # BoundingBox
├── region.py                # Region
├── type_hint.py             # TypeHint 枚举
├── selection_state.py       # SelectionState 枚举
├── flattened_element.py     # FlattenedElement
├── flattened_screen.py      # FlattenedScreen
└── screen_hints.py          # ScreenHints (可选的 screen_hints 字段)
```

### 2.2 BoundingBox (边界框)

**文件**: `src/models/vision/bounding_box.py`

```python
from dataclasses import dataclass
from typing import Tuple

@dataclass
class BoundingBox:
    """归一化边界框，描述元素在屏幕中的位置和大小

    所有坐标归一化到 [0, 1] 范围：
    - x, y: 左上角坐标
    - w, h: 宽度和高度

    示例：
        BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)
    """
    x: float  # 左上角 x，归一化 0~1
    y: float  # 左上角 y，归一化 0~1
    w: float  # 宽度，归一化 0~1
    h: float  # 高度，归一化 0~1

    def __post_init__(self):
        """验证坐标范围"""
        for name, value in [('x', self.x), ('y', self.y),
                            ('w', self.w), ('h', self.h)]:
            if not 0 <= value <= 1:
                raise ValueError(f"{name} must be in [0, 1], got {value}")

    def center(self) -> Tuple[float, float]:
        """返回中心点坐标"""
        return (self.x + self.w / 2, self.y + self.h / 2)

    def area(self) -> float:
        """返回面积"""
        return self.w * self.h

    def contains(self, other: 'BoundingBox') -> bool:
        """判断是否包含另一个边界框"""
        return (self.x <= other.x and
                self.y <= other.y and
                self.x + self.w >= other.x + other.w and
                self.y + self.h >= other.y + other.h)

    def overlaps(self, other: 'BoundingBox') -> bool:
        """判断是否与另一个边界框重叠"""
        return not (self.x + self.w < other.x or
                   other.x + other.w < self.x or
                   self.y + self.h < other.y or
                   other.y + other.h < self.y)
```

### 2.3 TypeHint (粗略视觉类型)

**文件**: `src/models/vision/type_hint.py`

```python
from enum import Enum

class TypeHint(str, Enum):
    """粗略视觉类型提示，仅基于视觉特征

    多模态模型输出的元素类型，不包含行为推理。
    后续由文本模型映射为更精确的 MenuItemType。
    """
    CLICKABLE_TEXT = "clickable_text"   # 可点击文本区域
    SWITCH = "switch"                   # 开关控件
    SLIDER = "slider"                   # 滑块控件
    BUTTON = "button"                   # 按钮控件
    ICON = "icon"                       # 纯图标元素
    INPUT_FIELD = "input_field"        # 输入框
    TEXT = "text"                       # 纯文本元素（不可交互）
    IMAGE = "image"                     # 图片元素

    @classmethod
    def from_string(cls, value: str) -> 'TypeHint':
        """从字符串创建 TypeHint，支持容错"""
        try:
            return cls(value.lower())
        except ValueError:
            # 容错处理
            mapping = {
                'text': cls.TEXT,
                'clickable': cls.CLICKABLE_TEXT,
                'toggle': cls.SWITCH,
                'checkbox': cls.SWITCH,
            }
            return mapping.get(value.lower(), cls.TEXT)
```

### 2.4 SelectionState (选中状态)

**文件**: `src/models/vision/selection_state.py`

```python
from enum import Enum

class SelectionState(str, Enum):
    """元素的选中/激活状态

    用于识别当前活跃的菜单项或标签页。
    """
    SELECTED = "selected"       # 当前选中/高亮
    NORMAL = "normal"           # 正常未选中
    DISABLED = "disabled"      # 禁用状态（灰显）

    @classmethod
    def from_string(cls, value: str) -> 'SelectionState':
        """从字符串创建 SelectionState，支持容错"""
        value_lower = value.lower()
        if value_lower in ('selected', 'active', 'highlighted'):
            return cls.SELECTED
        elif value_lower in ('disabled', 'gray', 'dimmed'):
            return cls.DISABLED
        return cls.NORMAL
```

### 2.5 Region (屏幕区域)

**文件**: `src/models/vision/region.py`

```python
from dataclasses import dataclass
from typing import Literal
from .bounding_box import BoundingBox

@dataclass
class Region:
    """屏幕区域划分，用于描述布局结构"""
    id: str                    # 唯一标识，如 "left_panel", "top_bar"
    bounds: BoundingBox        # 区域边界
    role: Literal["menu", "content", "tabs", "overlay", "unknown"]  # 区域角色
```

### 2.6 FlattenedElement (扁平元素)

**文件**: `src/models/vision/flattened_element.py`

```python
from dataclasses import dataclass, field
from typing import Dict, Any, Optional
from .bounding_box import BoundingBox
from .type_hint import TypeHint
from .selection_state import SelectionState

@dataclass
class FlattenedElement:
    """扁平化元素，多模态模型输出的单个元素信息"""
    id: int                                     # 元素唯一标识（在本次分析内）
    text: str = ""                              # 元素上显示的文本
    type_hint: TypeHint = TypeHint.TEXT         # 粗略视觉类型
    bbox: BoundingBox = None                    # 边界框
    region: Optional[str] = None               # 所属区域 ID
    selection_state: SelectionState = SelectionState.NORMAL  # 选中状态
    visual_state: Dict[str, Any] = field(default_factory=dict)  # 视觉状态描述
    confidence: float = 1.0                     # 识别置信度 0.0 ~ 1.0

    def __post_init__(self):
        """初始化后处理"""
        if self.bbox is None:
            self.bbox = BoundingBox(x=0, y=0, w=0, h=0)
        if not 0 <= self.confidence <= 1:
            raise ValueError(f"confidence must be in [0, 1], got {self.confidence}")
```

### 2.7 ScreenHints (屏幕提示信息)

**文件**: `src/models/vision/screen_hints.py`

```python
from dataclasses import dataclass, field
from typing import List, Dict, Any, Optional
from .region import Region

@dataclass
class ScreenHints:
    """屏幕级别提示信息"""
    top_bar_text: str = ""                        # 顶部标题栏文本
    layout_type: str = "unknown"                  # 布局类型
    regions: List[Region] = field(default_factory=list)  # 屏幕区域划分
    overlay_detected: bool = False                # 是否疑似有弹窗/覆盖层
    scroll_detected: bool = False                 # 页面是否可滚动
    extra: Dict[str, Any] = field(default_factory=dict)  # 额外信息
```

### 2.8 FlattenedScreen (扁平化屏幕)

**文件**: `src/models/vision/flattened_screen.py`

```python
from dataclasses import dataclass, field
from typing import List, Dict, Any
from .flattened_element import FlattenedElement
from .screen_hints import ScreenHints

@dataclass
class FlattenedScreen:
    """多模态模型输出的扁平化屏幕描述"""
    elements: List[FlattenedElement] = field(default_factory=list)
    screen_hints: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        """初始化后处理：确保元素按从上到下、从左到右排序"""
        self.elements.sort(key=lambda e: (e.bbox.y, e.bbox.x))

    def element_count(self) -> int:
        """返回元素数量"""
        return len(self.elements)

    def get_elements_in_region(self, region_id: str) -> List[FlattenedElement]:
        """获取指定区域内的元素"""
        return [e for e in self.elements if e.region == region_id]

    def get_selected_elements(self) -> List[FlattenedElement]:
        """获取选中的元素"""
        return [e for e in self.elements
                if e.selection_state == SelectionState.SELECTED]

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典（用于缓存）"""
        return {
            'elements': [self._element_to_dict(e) for e in self.elements],
            'screen_hints': self.screen_hints,
        }

    @staticmethod
    def _element_to_dict(element: FlattenedElement) -> Dict[str, Any]:
        """将元素转换为字典"""
        return {
            'id': element.id,
            'text': element.text,
            'type_hint': element.type_hint.value,
            'bbox': {'x': element.bbox.x, 'y': element.bbox.y,
                    'w': element.bbox.w, 'h': element.bbox.h},
            'region': element.region,
            'selection_state': element.selection_state.value,
            'visual_state': element.visual_state,
            'confidence': element.confidence,
        }
```

---

## 3. 组件设计 (Components)

### 3.1 MultimodalAnalyzer (多模态分析器)

**文件**: `src/ai/vision/multimodal_analyzer.py`

```python
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional
import logging

from src.models.vision.flattened_screen import FlattenedScreen
from src.ai.providers import UniBrainProvider

logger = logging.getLogger(__name__)


@dataclass
class MultimodalAnalysisResult:
    """多模态分析结果"""
    flattened_screen: FlattenedScreen
    latency_ms: float
    input_tokens: int
    output_tokens: int
    cached: bool = False


class MultimodalAnalyzer(ABC):
    """多模态视觉分析器接口"""

    @abstractmethod
    def analyze(self, image_data: bytes) -> MultimodalAnalysisResult:
        """分析截图，返回 FlattenedScreen

        Args:
            image_data: PNG 格式的截图数据

        Returns:
            MultimodalAnalysisResult 包含 FlattenedScreen 和性能指标
        """
        pass


class ClaudeMultimodalAnalyzer(MultimodalAnalyzer):
    """Claude 多模态分析器实现"""

    def __init__(
        self,
        ai_provider: UniBrainProvider,
        model: str = "claude-3-5-sonnet-20241022",
        cache_enabled: bool = True,
    ):
        self.ai_provider = ai_provider
        self.model = model
        self.cache_enabled = cache_enabled
        self._prompt = self._load_prompt()

    def _load_prompt(self) -> str:
        """加载多模态分析 Prompt"""
        from src.ai.vision.prompts import MULTIMODAL_ANALYSIS_PROMPT
        return MULTIMODAL_ANALYSIS_PROMPT

    def analyze(self, image_data: bytes) -> MultimodalAnalysisResult:
        """分析截图，返回 FlattenedScreen"""
        import time

        start_time = time.time()

        # 调用 AI 进行分析
        response = self.ai_provider.complete(
            prompt=self._prompt,
            image_data=image_data,
            model=self.model,
            response_format={"type": "json_object"},
        )

        latency_ms = (time.time() - start_time) * 1000

        # 解析响应
        flattened_screen = self._parse_response(response)

        return MultimodalAnalysisResult(
            flattened_screen=flattened_screen,
            latency_ms=latency_ms,
            input_tokens=response.usage.input_tokens,
            output_tokens=response.usage.output_tokens,
            cached=False,
        )

    def _parse_response(self, response) -> FlattenedScreen:
        """解析 AI 响应为 FlattenedScreen"""
        import json
        from src.models.vision.flattened_element import FlattenedElement
        from src.models.vision.bounding_box import BoundingBox
        from src.models.vision.type_hint import TypeHint
        from src.models.vision.selection_state import SelectionState

        data = json.loads(response.content)

        elements = []
        for elem_data in data.get('elements', []):
            bbox_data = elem_data.get('bbox', {})
            bbox = BoundingBox(
                x=bbox_data.get('x', 0),
                y=bbox_data.get('y', 0),
                w=bbox_data.get('w', 0),
                h=bbox_data.get('h', 0),
            )

            element = FlattenedElement(
                id=elem_data.get('id', 0),
                text=elem_data.get('text', ''),
                type_hint=TypeHint.from_string(elem_data.get('type_hint', 'text')),
                bbox=bbox,
                region=elem_data.get('region'),
                selection_state=SelectionState.from_string(
                    elem_data.get('selection_state', 'normal')
                ),
                visual_state=elem_data.get('visual_state', {}),
                confidence=elem_data.get('confidence', 1.0),
            )
            elements.append(element)

        return FlattenedScreen(
            elements=elements,
            screen_hints=data.get('screen_hints', {}),
        )
```

### 3.2 PageAnalysisAssembler (页面组装器)

**文件**: `src/ai/vision/page_analysis_assembler.py`

```python
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional, Dict, Any
import logging

from src.models.vision.flattened_screen import FlattenedScreen
from src.models.page_analysis import PageAnalysis
from src.ai.providers import UniBrainProvider

logger = logging.getLogger(__name__)


@dataclass
class AssemblyResult:
    """组装结果"""
    page_analysis: PageAnalysis
    latency_ms: float
    input_tokens: int
    output_tokens: int
    cached: bool = False


class PageAnalysisAssembler(ABC):
    """页面组装器接口"""

    @abstractmethod
    def assemble(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> AssemblyResult:
        """将 FlattenedScreen 组装为 PageAnalysis

        Args:
            flattened_screen: 扁平化屏幕描述
            context: 可选的遍历上下文（当前路径、历史页面等）

        Returns:
            AssemblyResult 包含 PageAnalysis 和性能指标
        """
        pass


class DeepSeekPageAnalysisAssembler(PageAnalysisAssembler):
    """DeepSeek 文本模型组装器实现"""

    def __init__(
        self,
        ai_provider: UniBrainProvider,
        model: str = "deepseek-v4-flash",
        cache_enabled: bool = True,
    ):
        self.ai_provider = ai_provider
        self.model = model
        self.cache_enabled = cache_enabled
        self._prompt_template = self._load_prompt_template()

    def _load_prompt_template(self) -> str:
        """加载组装器 Prompt 模板"""
        from src.ai.vision.prompts import ASSEMBLER_PROMPT_TEMPLATE
        return ASSEMBLER_PROMPT_TEMPLATE

    def assemble(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> AssemblyResult:
        """将 FlattenedScreen 组装为 PageAnalysis"""
        import time
        import json

        start_time = time.time()

        # 构建 Prompt
        prompt = self._build_prompt(flattened_screen, context)

        # 调用 AI 进行组装
        response = self.ai_provider.complete(
            prompt=prompt,
            model=self.model,
            response_format={"type": "json_object"},
        )

        latency_ms = (time.time() - start_time) * 1000

        # 解析响应
        page_analysis = self._parse_response(response)

        return AssemblyResult(
            page_analysis=page_analysis,
            latency_ms=latency_ms,
            input_tokens=response.usage.input_tokens,
            output_tokens=response.usage.output_tokens,
            cached=False,
        )

    def _build_prompt(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> str:
        """构建 Prompt"""
        import json

        flattened_json = json.dumps(
            flattened_screen.to_dict(),
            ensure_ascii=False,
            indent=2,
        )

        context_json = json.dumps(context or {}, ensure_ascii=False, indent=2)

        return self._prompt_template.format(
            flattened_screen=flattened_json,
            context=context_json,
        )

    def _parse_response(self, response) -> PageAnalysis:
        """解析 AI 响应为 PageAnalysis"""
        import json
        from src.models.page_analysis import PageAnalysis

        data = json.loads(response.content)
        return PageAnalysis.from_dict(data)
```

### 3.3 FlattenedVisionService (两步管道视觉服务)

**文件**: `src/ai/vision/flattened_vision_service.py`

```python
from dataclasses import dataclass
from typing import Optional, Dict, Any
import logging

from src.models.page_analysis import PageAnalysis
from src.ai.vision.multimodal_analyzer import (
    MultimodalAnalyzer,
    MultimodalAnalysisResult,
)
from src.ai.vision.page_analysis_assembler import (
    PageAnalysisAssembler,
    AssemblyResult,
)
from src.ai.vision.cache import ScreenCache, PageAnalysisCache

logger = logging.getLogger(__name__)


@dataclass
class VisionAnalysisResult:
    """视觉分析结果"""
    page_analysis: PageAnalysis
    total_latency_ms: float
    multimodal_latency_ms: float
    assembler_latency_ms: float
    total_tokens: int
    multimodal_cached: bool
    assembler_cached: bool


class FlattenedVisionService:
    """两步管道视觉服务

    流程：
    1. 多模态模型分析截图 → FlattenedScreen
    2. 文本模型组装 → PageAnalysis
    """

    def __init__(
        self,
        multimodal_analyzer: MultimodalAnalyzer,
        assembler: PageAnalysisAssembler,
        screen_cache: Optional[ScreenCache] = None,
        page_analysis_cache: Optional[PageAnalysisCache] = None,
        legacy_service: Optional['LegacyVisionService'] = None,
    ):
        self.multimodal_analyzer = multimodal_analyzer
        self.assembler = assembler
        self.screen_cache = screen_cache
        self.page_analysis_cache = page_analysis_cache
        self.legacy_service = legacy_service

    def analyze_screenshot(
        self,
        image_data: bytes,
        context: Optional[Dict[str, Any]] = None,
    ) -> VisionAnalysisResult:
        """分析截图，返回 PageAnalysis

        Args:
            image_data: PNG 格式的截图数据
            context: 可选的遍历上下文

        Returns:
            VisionAnalysisResult 包含 PageAnalysis 和性能指标
        """
        try:
            # Step 1: 多模态视觉感知
            multimodal_result = self._analyze_multimodal(image_data)

            # Step 2: 文本模型逻辑组装
            assembly_result = self._assemble_page(
                multimodal_result.flattened_screen,
                context,
            )

            return VisionAnalysisResult(
                page_analysis=assembly_result.page_analysis,
                total_latency_ms=multimodal_result.latency_ms +
                                 assembly_result.latency_ms,
                multimodal_latency_ms=multimodal_result.latency_ms,
                assembler_latency_ms=assembly_result.latency_ms,
                total_tokens=multimodal_result.output_tokens +
                            assembly_result.output_tokens,
                multimodal_cached=multimodal_result.cached,
                assembler_cached=assembly_result.cached,
            )

        except Exception as e:
            logger.warning(
                f"Flattened pipeline failed: {e}, "
                f"falling back to legacy service"
            )
            if self.legacy_service:
                return self._fallback_to_legacy(image_data, context)
            raise

    def _analyze_multimodal(
        self,
        image_data: bytes,
    ) -> MultimodalAnalysisResult:
        """多模态视觉分析（带缓存）"""
        # 检查缓存
        if self.screen_cache:
            cached = self.screen_cache.get(image_data)
            if cached:
                logger.debug("Screen cache hit")
                return MultimodalAnalysisResult(
                    flattened_screen=cached,
                    latency_ms=0,
                    input_tokens=0,
                    output_tokens=0,
                    cached=True,
                )

        # 调用分析器
        result = self.multimodal_analyzer.analyze(image_data)

        # 缓存结果
        if self.screen_cache:
            self.screen_cache.set(image_data, result.flattened_screen)

        return result

    def _assemble_page(
        self,
        flattened_screen,
        context: Optional[Dict[str, Any]] = None,
    ) -> AssemblyResult:
        """页面组装（带缓存）"""
        # 生成缓存键
        cache_key = self._generate_assembly_cache_key(flattened_screen, context)

        # 检查缓存
        if self.page_analysis_cache:
            cached = self.page_analysis_cache.get(cache_key)
            if cached:
                logger.debug("Page analysis cache hit")
                return AssemblyResult(
                    page_analysis=cached,
                    latency_ms=0,
                    input_tokens=0,
                    output_tokens=0,
                    cached=True,
                )

        # 调用组装器
        result = self.assembler.assemble(flattened_screen, context)

        # 缓存结果
        if self.page_analysis_cache:
            self.page_analysis_cache.set(cache_key, result.page_analysis)

        return result

    def _generate_assembly_cache_key(
        self,
        flattened_screen,
        context: Optional[Dict[str, Any]] = None,
    ) -> str:
        """生成组装缓存键"""
        import hashlib
        import json

        screen_hash = hashlib.md5(
            json.dumps(flattened_screen.to_dict(), sort_keys=True).encode()
        ).hexdigest()

        context_hash = hashlib.md5(
            json.dumps(context or {}, sort_keys=True).encode()
        ).hexdigest()

        return f"{screen_hash}:{context_hash}"

    def _fallback_to_legacy(
        self,
        image_data: bytes,
        context: Optional[Dict[str, Any]] = None,
    ) -> VisionAnalysisResult:
        """降级到旧方案"""
        from src.ai.vision.legacy_vision_service import LegacyVisionService

        if not self.legacy_service:
            raise RuntimeError("No legacy service available for fallback")

        # 调用旧方案
        page_analysis = self.legacy_service.analyze_screenshot(image_data, context)

        return VisionAnalysisResult(
            page_analysis=page_analysis,
            total_latency_ms=0,
            multimodal_latency_ms=0,
            assembler_latency_ms=0,
            total_tokens=0,
            multimodal_cached=False,
            assembler_cached=False,
        )
```

---

## 4. 缓存设计 (Caching)

### 4.1 缓存架构

```
┌─────────────────────────────────────────────────────────────┐
│                      双层缓存系统                             │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────┐      ┌─────────────────────────┐   │
│  │   ScreenCache       │      │ PageAnalysisCache       │   │
│  │                     │      │                         │   │
│  │  Key: perceptual_   │      │ Key: screen_hash +     │   │
│  │       hash(image)   │      │      context_hash       │   │
│  │                     │      │                         │   │
│  │  Value:             │      │ Value:                 │   │
│  │  FlattenedScreen    │      │ PageAnalysis           │   │
│  │                     │      │                         │   │
│  │  TTL: 5 分钟        │      │ TTL: 10 分钟           │   │
│  └─────────────────────┘      └─────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 ScreenCache 实现

**文件**: `src/ai/vision/cache/screen_cache.py`

```python
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional
import hashlib
import time

from src.models.vision.flattened_screen import FlattenedScreen


@dataclass
class CacheEntry:
    """缓存条目"""
    value: FlattenedScreen
    created_at: float


class ScreenCache(ABC):
    """屏幕分析缓存接口"""

    @abstractmethod
    def get(self, image_data: bytes) -> Optional[FlattenedScreen]:
        """从缓存获取 FlattenedScreen"""
        pass

    @abstractmethod
    def set(self, image_data: bytes, value: FlattenedScreen) -> None:
        """设置缓存"""
        pass

    @abstractmethod
    def clear(self) -> None:
        """清空缓存"""
        pass


class InMemoryScreenCache(ScreenCache):
    """内存实现"""

    def __init__(self, ttl: int = 300, max_size: int = 1000):
        self.ttl = ttl  # 秒
        self.max_size = max_size
        self._cache: dict[str, CacheEntry] = {}

    def _generate_key(self, image_data: bytes) -> str:
        """生成缓存键（感知哈希）"""
        # 简化实现：使用 MD5
        # 生产环境建议使用 perceptual hash
        return hashlib.md5(image_data).hexdigest()

    def get(self, image_data: bytes) -> Optional[FlattenedScreen]:
        """从缓存获取"""
        key = self._generate_key(image_data)
        entry = self._cache.get(key)

        if entry is None:
            return None

        # 检查 TTL
        if time.time() - entry.created_at > self.ttl:
            del self._cache[key]
            return None

        return entry.value

    def set(self, image_data: bytes, value: FlattenedScreen) -> None:
        """设置缓存"""
        key = self._generate_key(image_data)

        # LRU 淘汰
        if len(self._cache) >= self.max_size:
            oldest_key = min(self._cache.items(),
                            key=lambda x: x[1].created_at)[0]
            del self._cache[oldest_key]

        self._cache[key] = CacheEntry(value=value, created_at=time.time())

    def clear(self) -> None:
        """清空缓存"""
        self._cache.clear()
```

### 4.3 PageAnalysisCache 实现

**文件**: `src/ai/vision/cache/page_analysis_cache.py`

```python
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional
import time

from src.models.page_analysis import PageAnalysis


@dataclass
class CacheEntry:
    """缓存条目"""
    value: PageAnalysis
    created_at: float


class PageAnalysisCache(ABC):
    """页面分析缓存接口"""

    @abstractmethod
    def get(self, cache_key: str) -> Optional[PageAnalysis]:
        """从缓存获取 PageAnalysis"""
        pass

    @abstractmethod
    def set(self, cache_key: str, value: PageAnalysis) -> None:
        """设置缓存"""
        pass

    @abstractmethod
    def clear(self) -> None:
        """清空缓存"""
        pass


class InMemoryPageAnalysisCache(PageAnalysisCache):
    """内存实现"""

    def __init__(self, ttl: int = 600, max_size: int = 1000):
        self.ttl = ttl  # 秒
        self.max_size = max_size
        self._cache: dict[str, CacheEntry] = {}

    def get(self, cache_key: str) -> Optional[PageAnalysis]:
        """从缓存获取"""
        entry = self._cache.get(cache_key)

        if entry is None:
            return None

        # 检查 TTL
        if time.time() - entry.created_at > self.ttl:
            del self._cache[cache_key]
            return None

        return entry.value

    def set(self, cache_key: str, value: PageAnalysis) -> None:
        """设置缓存"""
        # LRU 淘汰
        if len(self._cache) >= self.max_size:
            oldest_key = min(self._cache.items(),
                            key=lambda x: x[1].created_at)[0]
            del self._cache[oldest_key]

        self._cache[cache_key] = CacheEntry(value=value, created_at=time.time())

    def clear(self) -> None:
        """清空缓存"""
        self._cache.clear()
```

---

## 5. 配置设计 (Configuration)

### 5.1 配置结构

**文件**: `config/settings.py` (更新)

```python
from pydantic import BaseModel
from typing import Literal, Optional


class VisionServiceConfig(BaseModel):
    """视觉服务配置"""

    # 模式选择
    mode: Literal["legacy", "flattened", "dual"] = "flattened"

    # 多模态模型配置
    multimodal_model: str = "claude-3-5-sonnet-20241022"
    multimodal_max_tokens: int = 4096

    # 文本模型配置
    text_model: str = "deepseek-v4-flash"
    text_max_tokens: int = 2048

    # 缓存配置
    enable_cache: bool = True
    screen_cache_ttl: int = 300  # 5 分钟
    page_analysis_cache_ttl: int = 600  # 10 分钟
    cache_max_size: int = 1000

    # 降级配置
    enable_fallback: bool = True
    fallback_on_error: bool = True
    fallback_timeout_ms: float = 5000

    # 性能监控
    enable_metrics: bool = True
    metrics_sample_rate: float = 0.1  # 10% 采样率


class Settings(BaseModel):
    """全局设置"""

    # ... 现有配置 ...

    vision: VisionServiceConfig = VisionServiceConfig()
```

### 5.2 工厂模式

**文件**: `src/ai/vision/vision_service_factory.py`

```python
from typing import Optional
from src.ai.vision.flattened_vision_service import FlattenedVisionService
from src.ai.vision.legacy_vision_service import LegacyVisionService
from src.ai.vision.multimodal_analyzer import ClaudeMultimodalAnalyzer
from src.ai.vision.page_analysis_assembler import DeepSeekPageAnalysisAssembler
from src.ai.vision.cache.screen_cache import InMemoryScreenCache
from src.ai.vision.cache.page_analysis_cache import InMemoryPageAnalysisCache
from src.ai.providers import UniBrainProvider
from config.settings import Settings


class VisionServiceFactory:
    """视觉服务工厂"""

    @staticmethod
    def create(settings: Settings) -> Optional['VisionService']:
        """根据配置创建视觉服务"""
        mode = settings.vision.mode

        if mode == "legacy":
            return VisionServiceFactory._create_legacy(settings)
        elif mode == "flattened":
            return VisionServiceFactory._create_flattened(settings)
        elif mode == "dual":
            return VisionServiceFactory._create_dual(settings)
        else:
            raise ValueError(f"Unknown mode: {mode}")

    @staticmethod
    def _create_legacy(settings: Settings) -> LegacyVisionService:
        """创建旧方案服务"""
        # ... 现有实现 ...
        pass

    @staticmethod
    def _create_flattened(settings: Settings) -> FlattenedVisionService:
        """创建新两步管道服务"""
        ai_provider = UniBrainProvider(settings.ai)

        # 多模态分析器
        multimodal_analyzer = ClaudeMultimodalAnalyzer(
            ai_provider=ai_provider,
            model=settings.vision.multimodal_model,
        )

        # 页面组装器
        assembler = DeepSeekPageAnalysisAssembler(
            ai_provider=ai_provider,
            model=settings.vision.text_model,
        )

        # 缓存
        screen_cache = None
        page_analysis_cache = None
        if settings.vision.enable_cache:
            screen_cache = InMemoryScreenCache(
                ttl=settings.vision.screen_cache_ttl,
                max_size=settings.vision.cache_max_size,
            )
            page_analysis_cache = InMemoryPageAnalysisCache(
                ttl=settings.vision.page_analysis_cache_ttl,
                max_size=settings.vision.cache_max_size,
            )

        # 降级服务
        legacy_service = None
        if settings.vision.enable_fallback:
            legacy_service = VisionServiceFactory._create_legacy(settings)

        return FlattenedVisionService(
            multimodal_analyzer=multimodal_analyzer,
            assembler=assembler,
            screen_cache=screen_cache,
            page_analysis_cache=page_analysis_cache,
            legacy_service=legacy_service,
        )

    @staticmethod
    def _create_dual(settings: Settings) -> 'DualVisionService':
        """创建双模式服务（同时运行新旧方案，用于对比）"""
        # ... 实现 ...
        pass
```

---

## 6. Prompt 设计 (Prompts)

### 6.1 多模态分析 Prompt

**文件**: `src/ai/vision/prompts/multimodal_prompt.py`

```python
MULTIMODAL_ANALYSIS_PROMPT = """你是一个车机 UI 视觉分析专家。请分析提供的截图，输出屏幕上所有可见元素的信息。

对于每个元素，请提供：
1. id: 元素唯一标识（从 0 开始递增）
2. text: 元素上显示的文本（如无文本则留空）
3. type_hint: 元素类型（只能是：clickable_text, switch, slider, button, icon, input_field, text, image）
4. bbox: 边界框坐标（归一化 0-1，格式：{"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.05}）
5. region: 所属区域（如 left_panel, content_area, top_bar, tabs, null）
6. selection_state: 选中状态（selected = 高亮/选中, normal = 正常, disabled = 禁用/灰显）
7. visual_state: 额外视觉状态（如 {"bold": true, "dimmed": false, "has_indicator": "filled_circle"}）
8. confidence: 识别置信度（0-1）

额外信息（screen_hints）：
- top_bar_text: 顶部标题栏文本
- layout_type: 布局类型（split_pane, tabbed, single, overlay, unknown）
- overlay_detected: 是否有弹窗/覆盖层（true/false）
- scroll_detected: 是否可滚动（true/false）

重要：
- 仅描述视觉特征，不要推断元素行为或功能
- 不要推断父子关系或层级结构
- 元素按从上到下、从左到右顺序排列
- 坐标使用归一化值，范围 0-1

输出格式（JSON）：
{
  "elements": [
    {
      "id": 0,
      "text": "WiFi",
      "type_hint": "clickable_text",
      "bbox": {"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.05},
      "region": "left_panel",
      "selection_state": "selected",
      "visual_state": {"bold": true, "has_indicator": "filled_circle"},
      "confidence": 0.95
    }
  ],
  "screen_hints": {
    "top_bar_text": "设置",
    "layout_type": "split_pane",
    "overlay_detected": false,
    "scroll_detected": false
  }
}
"""
```

### 6.2 页面组装 Prompt

**文件**: `src/ai/vision/prompts/assembler_prompt.py`

```python
ASSEMBLER_PROMPT_TEMPLATE = """你是一个车机 UI 逻辑分析专家。基于提供的扁平化元素列表和上下文信息，推断出完整的页面结构。

输入数据：

## 扁平化屏幕（flattened_screen）
```json
{flattened_screen}
```

## 上下文（context）
```json
{context}
```

任务：
1. 分析布局结构（分栏、标签页、单页等）
2. 确定区域角色和边界
3. 为每个元素分类（menu_item, tab, switch, button 等）
4. 推断元素行为（navigate, toggle, action, back 等）
5. 构建层级关系（父子关系）
6. 识别当前激活路径（current_path）
7. 检测弹窗（如有）

推理过程：
1. 首先分析 screen_hints.layout_type 判断布局类型
2. 根据 region 字段确定元素所属区域
3. 对于每个元素：
   - 根据 type_hint + region + context 推断精确的 MenuItemType
   - 根据位置、上下文推断 expected_action
   - 根据坐标关系推断父子关系
4. 根据 selection_state 推断当前激活路径
5. 根据 overlay_detected 判断是否为弹窗

输出格式（PageAnalysis JSON）：
```json
{{
  "layout_type": "split_pane",
  "level1_menus": [
    {{
      "id": "wifi",
      "name": "WiFi",
      "type": "menu_item",
      "expected_action": "navigate",
      "coordinate": {{"x": 0.1, "y": 0.2}},
      "parent": null
    }}
  ],
  "level2_menus": [],
  "current_path": ["wifi"],
  "is_popup": false
}}
```
"""
```

---

## 7. 测试设计 (Testing)

### 7.1 测试目录结构

```
tests/
├── vision/
│   ├── __init__.py
│   ├── models/
│   │   ├── __init__.py
│   │   ├── test_bounding_box.py
│   │   ├── test_type_hint.py
│   │   ├── test_selection_state.py
│   │   ├── test_flattened_element.py
│   │   ├── test_flattened_screen.py
│   │   └── test_region.py
│   ├── analyzers/
│   │   ├── __init__.py
│   │   ├── test_multimodal_analyzer.py
│   │   └── test_page_assembler.py
│   ├── cache/
│   │   ├── __init__.py
│   │   ├── test_screen_cache.py
│   │   └── test_page_analysis_cache.py
│   ├── service/
│   │   ├── __init__.py
│   │   ├── test_flattened_vision_service.py
│   │   └── test_vision_service_factory.py
│   ├── performance/
│   │   ├── __init__.py
│   │   ├── performance_comparison.py
│   │   └── benchmark.py
│   ├── accuracy/
│   │   ├── __init__.py
│   │   ├── test_hierarchy_accuracy.py
│   │   ├── test_behavior_accuracy.py
│   │   └── test_popup_detection.py
│   └── assets/
│       ├── screenshots/
│       │   ├── settings_main.png
│       │   ├── settings_display.png
│       │   ├── settings_network.png
│       │   ├── dialog_confirm.png
│       │   ├── dialog_input.png
│       │   ├── tabbed_view.png
│       │   ├── single_page.png
│       │   └── overlay_popup.png
│       └── ground_truth/
│           ├── settings_main.json
│           ├── settings_display.json
│           ├── settings_network.json
│           ├── dialog_confirm.json
│           ├── dialog_input.json
│           ├── tabbed_view.json
│           ├── single_page.json
│           └── overlay_popup.json
```

### 7.2 单元测试示例

**文件**: `tests/vision/models/test_bounding_box.py`

```python
import pytest
from src.models.vision.bounding_box import BoundingBox


class TestBoundingBox:
    """BoundingBox 单元测试"""

    def test_creation(self):
        """测试创建"""
        bbox = BoundingBox(x=0.1, y=0.2, w=0.3, h=0.05)
        assert bbox.x == 0.1
        assert bbox.y == 0.2
        assert bbox.w == 0.3
        assert bbox.h == 0.05

    def test_validation(self):
        """测试坐标验证"""
        with pytest.raises(ValueError):
            BoundingBox(x=1.5, y=0.2, w=0.3, h=0.05)

    def test_center(self):
        """测试中心点计算"""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        center = bbox.center()
        assert center == (0.25, 0.25)

    def test_area(self):
        """测试面积计算"""
        bbox = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        assert bbox.area() == 0.25

    def test_contains(self):
        """测试包含判断"""
        outer = BoundingBox(x=0.0, y=0.0, w=1.0, h=1.0)
        inner = BoundingBox(x=0.1, y=0.1, w=0.2, h=0.2)
        assert outer.contains(inner)
        assert not inner.contains(outer)

    def test_overlaps(self):
        """测试重叠判断"""
        a = BoundingBox(x=0.0, y=0.0, w=0.5, h=0.5)
        b = BoundingBox(x=0.3, y=0.3, w=0.5, h=0.5)
        assert a.overlaps(b)

        c = BoundingBox(x=0.6, y=0.6, w=0.4, h=0.4)
        assert not a.overlaps(c)
```

### 7.3 性能对比测试

**文件**: `tests/vision/performance/performance_comparison.py`

```python
from dataclasses import dataclass
from typing import List
from src.ai.vision.flattened_vision_service import FlattenedVisionService
from src.ai.vision.legacy_vision_service import LegacyVisionService


@dataclass
class PerformanceMetrics:
    """性能指标记录"""
    screenshot: str
    mode: str  # "legacy" or "flattened"

    multimodal_latency_ms: float = 0
    text_latency_ms: float = 0
    total_latency_ms: float = 0

    input_tokens: int = 0
    multimodal_output_tokens: int = 0
    text_output_tokens: int = 0
    total_tokens: int = 0

    hierarchy_accuracy: float = 0
    behavior_accuracy: float = 0
    popup_detection_accuracy: float = 0

    cache_hit: bool = False


class PerformanceComparison:
    """性能对比测试"""

    def __init__(
        self,
        legacy_service: LegacyVisionService,
        flattened_service: FlattenedVisionService,
    ):
        self.legacy_service = legacy_service
        self.flattened_service = flattened_service
        self.results: List[PerformanceMetrics] = []

    def test_screenshot(self, screenshot_path: str) -> tuple:
        """测试单个截图"""
        # ... 实现 ...
        pass

    def generate_report(self) -> dict:
        """生成性能对比报告"""
        return {
            "token_reduction": self._calculate_token_reduction(),
            "speed_improvement": self._calculate_speed_improvement(),
            "accuracy_comparison": self._calculate_accuracy_comparison(),
            "cache_hit_rate": self._calculate_cache_hit_rate(),
        }

    def _calculate_token_reduction(self) -> float:
        """计算 Token 消耗减少百分比"""
        # ... 实现 ...
        pass

    def _calculate_speed_improvement(self) -> float:
        """计算速度提升百分比"""
        # ... 实现 ...
        pass
```

---

## 8. 部署设计 (Deployment)

### 8.1 配置变更

**环境变量**：

```bash
# 视觉服务模式
VISION_MODE=flattened  # legacy | flattened | dual

# 多模态模型配置
MULTIMODAL_MODEL=claude-3-5-sonnet-20241022

# 文本模型配置
TEXT_MODEL=deepseek-v4-flash

# 缓存配置
VISION_CACHE_ENABLED=true
VISION_CACHE_TTL=300
```

### 8.2 灰度发布策略

1. **阶段 1**: 内部测试
   - 模式设置为 `dual`
   - 收集性能对比数据
   - 优化 Prompt

2. **阶段 2**: 小流量灰度
   - 模式设置为 `flattened`
   - 10% 流量使用新方案
   - 监控错误率和性能指标

3. **阶段 3**: 全量发布
   - 模式设置为 `flattened`
   - 100% 流量使用新方案
   - 保留降级能力

---

## 9. 监控设计 (Monitoring)

### 9.1 指标收集

**文件**: `src/ai/vision/metrics.py`

```python
from dataclasses import dataclass
from typing import List
from datetime import datetime


@dataclass
class VisionMetrics:
    """视觉服务指标"""
    timestamp: datetime
    screenshot_hash: str
    mode: str
    multimodal_latency_ms: float
    text_latency_ms: float
    total_latency_ms: float
    multimodal_output_tokens: int
    text_output_tokens: int
    total_tokens: int
    multimodal_cached: bool
    assembler_cached: bool
    hierarchy_accuracy: float
    behavior_accuracy: float
    popup_detection_accuracy: float


class VisionMetricsCollector:
    """视觉服务指标收集器"""

    def __init__(self):
        self.metrics: List[VisionMetrics] = []

    def record(self, metrics: VisionMetrics) -> None:
        """记录指标"""
        self.metrics.append(metrics)

    def get_summary(self, days: int = 7) -> dict:
        """获取汇总数据"""
        # ... 实现 ...
        pass
```

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
