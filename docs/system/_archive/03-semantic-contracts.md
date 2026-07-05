# Domain 语义契约

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix 后）
> **原则**: 每个类型回答一个明确的问题，拒绝回答不属于它的问题。

---

## 1. 概念分层

Domain 层有 **3 个语义层**，每层回答不同性质的问题：

| 语义层 | 回答什么 | 代表类型 | 值数量 |
|--------|----------|----------|--------|
| **视觉外观** | "这东西在屏幕上看起来像什么？" | TypeHint (8值), BoundingBox, FlattenedElement | 固定（外观有限） |
| **行为语义** | "这东西能做什么操作？属于什么交互类别？" | MenuItemType (11值), ExpectedAction (4值), OperationType (5值) | 可扩展（行为可分化） |
| **空间位置** | "这东西在哪里？占多大空间？" | Coordinate, Region, Direction | 固定（几何不变） |

**核心规则**：视觉外观层 **不做行为推断**。TypeHint 的 docstring 明确声明：

> *"These types represent visual features observable in screenshots, **without behavioral inference**."*

P0 fix 的本质就是把违反这条规则的依赖（MapAndroidClass 返回 TypeHint）拆开，让行为推断走中间字符串→MenuItemType 的独立路径。

---

## 2. 类型语义契约表

### Vision 子域

| 类型 | 一句话职责 | **不负责什么** | 辖域 |
|------|-----------|---------------|------|
| **BoundingBox** | 描述屏幕上一个矩形区域的位置和大小 | 不负责像素坐标转换、不负责子区域分割 | 空间位置 |
| **RegionRole** | 给区域分配功能角色标签 | 不负责角色之间的交互逻辑 | 视觉外观 |
| **Region** | 将空间区域和功能角色绑定成一个命名单元 | 不负责区域内的元素枚举、不负责区域间的导航 | 空间位置+外观 |
| **TypeHint** | 对 UI 元素做粗粒度视觉外观分类 | **不负责行为推断**（不是"这东西能做什么"）、不负责 Android 控件映射 | 视觉外观 |
| **SelectionState** | 描述 UI 元素的视觉激活/选择状态 | 不负责交互可用性判断（IsInteractive 只看 Disabled，不看 TypeHint） | 视觉外观 |
| **FlattenedElement** | 承载多模态模型对单个 UI 元素的完整视觉描述 | 不负责行为推断、不负责操作定义 | 视觉外观（聚合） |
| **FlattenedScreen** | 承载多模态模型对整个屏幕的完整视觉分析 | 不负责页面结构推断（PageAnalysis 回答那个问题） | 视觉外观（聚合） |
| **ScreenHints** | 承载屏幕级布局元数据和区域划分 | 不负责具体元素分类 | 视觉外观（元数据） |

### Content 子域

| 类型 | 一句话职责 | **不负责什么** | 辖域 |
|------|-----------|---------------|------|
| **Coordinate** | 描述一个归一化二维位置点 | 不负责像素坐标、不负责方向推断 | 空间位置 |
| **Direction** | 描述菜单的空间方向 | 不负责坐标计算 | 空间位置 |
| **MenuItemType** | 对 UI 元素做交互行为分类（"这东西的交互类别"） | 不负责视觉外观（Menu_item 不是外观，是行为）、不负责操作定义 | 行为语义 |
| **ExpectedAction** | 描述点击一个 UI 元素后的预期系统响应 | 不负责视觉外观、不负责具体操作参数 | 行为语义 |
| **MenuInfo** | 描述一个菜单项的名称和位置 | 不负责交互分类、不负责子菜单关系 | 空间位置+名称 |
| **MenuItem** | 描述一个可交互项的完整语义（类型+位置+预期行为） | 不负责视觉外观推断 | 行为语义（聚合） |
| **PopupInfo** | 描述弹窗的标题和关闭按钮位置 | 不负责弹窗内元素分类 | 空间位置+名称 |
| **PageAnalysis** | 描述一页的完整菜单结构和导航信息 | 不负责视觉元素分类（FlattenedScreen 回答那个问题） | 行为语义（聚合） |
| **VisitFingerprint** | 生成和还原访问路径的唯一标识 | 不负责路径语义解释 | 标识 |
| **ContentNode** | 描述内容树的节点结构和层级 | 不负责遍历逻辑、不负责 Markdown 渲染（缺 ToMarkdown） | 标识+结构 |

### Common 子域

| 类型 | 一句话职责 | **不负责什么** | 辖域 |
|------|-----------|---------------|------|
| **OperationType** | 定义受限的动作集合（Click/Swipe/Back/InputText/NoAction） | 不负责动作参数、不负责动作验证结果 | 行为语义 |
| **Operation** | 定义在节点上执行的一个完整操作（动作+目标+参数+恢复） | 不负责操作的执行引擎、不负责操作序列编排 | 行为语义 |
| **TargetType** | 定义定位 UI 元素的受限方式集合 | 不负责定位逻辑实现 | 行为语义 |
| **Target** | 描述如何定位一个 UI 元素（方式+值+元数据） | 不负责定位算法、不负责坐标转换 | 行为语义 |
| **RestoreAction** | 定义操作后的状态恢复动作 | 不负责恢复逻辑执行 | 行为语义 |

### Mappings 子域

| 类型 | 一句话职责 | **不负责什么** | 辖域 |
|------|-----------|---------------|------|
| **ElementTypeMapper** | 将 Android 控件类名桥接到行为语义（中间字符串→MenuItemType→ExpectedAction） | **不负责视觉外观推断**（ToTypeHint 是可选便利，非核心职责）、不负责 AI 输出解析 | 桥接 |
| **AndroidWidgetClass** | 定义 Android 控件类名枚举 | **当前无引用者**（孤立 enum） | 标识（预留） |

### 跨切面

| 类型 | 一句话职责 | **不负责什么** | 辖域 |
|------|-----------|---------------|------|
| **DomainValidationException** | 报告领域对象构造期的校验失败（字段名+非法值） | 不负责上层异常转换、不负责日志记录 | 跨切面 |
| **DomainJsonOptions** | 定义 Domain 层 JSON 序列化策略（camelCase + enum as string） | 不负责反序列化校验、不负责 snake_case 兼容 | 跨切面 |

---

## 3. 语义冲突记录

| # | 冲突描述 | 涉及类型 | 严重度 | 当前状态 |
|---|----------|----------|--------|----------|
| C1 | TypeHint 同时承载视觉外观和 Android 行为映射 | TypeHint + ElementTypeMapper | P0 | **已修** — MapAndroidClass 不再返回 TypeHint |
| C2 | FlattenedElement.IsInteractive 混合视觉+行为判断 | FlattenedElement | P3 | **接受** — IsInteractive = TypeHint.IsInteractive() && SelectionState.IsInteractive()，这是"综合视觉线索推断交互性"，不违反 TypeHint 单独的职责 |
| C3 | "menu_item" 既是中间字符串又是行为语义 | ElementTypeMapper + MenuItemType | 设计正确 | **接受** — menu_item 是行为语义桥接词汇，TypeHint.ClickableText 是视觉外观，两者不重叠 |
| C4 | Operation.Action 是 OperationType enum 但 Template 用 dict | Operation + Template | P2 | **Phase 2 待修** — Template dict→Operation record 转换器缺失 |

---

## 4. 辖域互斥规则

| 规则 | 说明 | 违反后果 |
|------|------|----------|
| **视觉外观 ≠ 行为语义** | TypeHint 不含 "toggle" / "menu_item" / "input"；这些属于中间字符串层 | 8 值不够 14 值，P0 问题复现 |
| **空间位置 ≠ 交互分类** | Coordinate 不含交互类型信息；MenuInfo 不含视觉外观 | 职责膨胀，类型难以独立演进 |
| **桥接 ≠ 分类** | ElementTypeMapper 是桥（转换器），不是分类器（它不定义新类别） | 如果 ElementTypeMapper 返回 TypeHint，桥就变成了分类器 |
| **跨切面 ≠ 业务逻辑** | DomainValidationException 只报告错误，不做业务判断 | 如果 DVE 开始做范围检查，它就从跨切面变成了校验逻辑层 |

这些规则是 Domain 层演进的护栏——任何新增类型或字段修改都应该先检查是否违反辖域互斥规则。
