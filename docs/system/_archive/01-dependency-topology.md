# Domain 依赖拓扑

> **日期**: 2026-07-02
> **分支**: `feature/refactor`（P0 fix phase1-1-domain-corrections 后）
> **范围**: Domain 层内部 + Domain→上层连接点

---

## 1. 总览

Domain 层分为 4 个子域 + 1 个跨切面 + 1 个映射桥：

```
Domain (root namespace)
  ├── DomainValidationException    ← 跨切面：所有校验 record 共用
  ├── DomainJsonOptions            ← 跨切面：JSON 序列化策略
  │
  ├── Models.Vision (8 类型)       ← 独立岛，零 Content/Common import
  ├── Models.Content (10 类型)     ← 独立岛，零 Vision/Common import
  ├── Models.Common (5 类型)       ← 独立岛，零 Vision/Content import
  │
  └── Mappings (2 类型)            ← 桥：连接 Vision + Content + Common
      ElementTypeMapper             ← 核心映射器
      AndroidWidgetClass            ← 孤立 enum（无引用者）
```

**关键特征**：
- Vision ↔ Content ↔ Common 之间 **零直接 import**
- Mappings 是唯一跨子域的显式桥
- DomainValidationException 是唯一跨全部子域的隐式桥（但方向单一：所有子域→root）

---

## 2. 子域内部依赖 DAG

### 2.1 Vision 岛 (8 类型)

```
RegionRole ──→ Region ──→ ScreenHints
BoundingBox ──→ Region
TypeHint ──→ FlattenedElement ──→ FlattenedScreen
SelectionState ──→ FlattenedElement ──→ FlattenedScreen
BoundingBox ──→ FlattenedElement ──→ FlattenedScreen
              ScreenHints ──→ FlattenedScreen
```

**Hub**: BoundingBox (3 直依赖: Region, FlattenedElement, FlattenedScreen)

**孤立**: RegionRole 仅被 Region 引用，无其他依赖者

### 2.2 Content 岛 (10 类型)

```
Coordinate ──→ MenuInfo
Coordinate ──→ MenuItem
Coordinate ──→ PopupInfo
Coordinate ──→ PageAnalysis
Coordinate ──→ ContentNode
Coordinate ──→ VisitFingerprint (间接：ContentNode 构造器)

Direction ──→ PageAnalysis
MenuItemType ──→ MenuItem
ExpectedAction ──→ MenuItem
MenuItem ──→ PageAnalysis
MenuInfo ──→ PageAnalysis
PopupInfo ──→ PageAnalysis
VisitFingerprint ──→ ContentNode (无直接 import，但同文件)
```

**Hub**: Coordinate (5 直依赖: MenuInfo, MenuItem, PopupInfo, PageAnalysis, ContentNode)

### 2.3 Common 岛 (5 类型)

```
OperationType ──→ Operation
OperationType ──→ RestoreAction
TargetType ──→ Target
Target ──→ Operation
Target ──→ RestoreAction
```

**Hub**: OperationType (2 直依赖), Target (2 直依赖)

---

## 3. 跨域桥

### 3.1 Mappings 桥 — ElementTypeMapper

```
ElementTypeMapper
  ├──→ MenuItemType(Content)     ← 核心依赖：TYPE_TO_MENU_ITEM 字典
  ├──→ ExpectedAction(Content)   ← 核心依赖：TYPE_TO_EXPECTED_ACTION 字典
  ├──→ TypeHint(Vision)          ← 可选依赖：仅 ToTypeHint(string) 便利方法
  ├──→ DomainValidationException ← null 防御：MapAndroidClass 入口
  └──→ DomainJsonOptions         ← 间接：序列化时
```

**桥强度分级**：

| 箭头 | 类型 | 依赖方法 | 可消除？ |
|------|------|----------|----------|
| → MenuItemType | 核心 | TypeToMenuItemTypeMap 字典值 | ❌ 映射器核心功能 |
| → ExpectedAction | 核心 | TypeToExpectedActionMap 字典值 | ❌ 映射器核心功能 |
| → TypeHint | 可选 | ToTypeHint(string) 便利方法 | ✅ 删除 ToTypeHint 即可消除 |
| → DomainValidationException | 跨切面 | MapAndroidClass null 检查 | ❌ 防御性编程 |

### 3.2 DomainValidationException 跨切面桥

```
DomainValidationException
  ←── BoundingBox(Vision)       4 次调用 (X, Y, Width, Height 校验)
  ←── FlattenedElement(Vision)  1 次调用 (Confidence 校验)
  ←── FlattenedScreen(Vision)   1 次调用 (Elements.IsDefault 检查)
  ←── Coordinate(Content)       2 次调用 (X, Y 范围校验)
  ←── Target(Common)            1 次调用 (By enum 校验)
  ←── Operation(Common)         1 次调用 (Action enum 校验)
  ←── RestoreAction(Common)     1 次调用 (Action enum 校验)
  ←── VisitFingerprint(Content) 1 次调用 (FromString 格式校验)
  ←── DirectionExtensions(Content)   1 次调用 (FromValue 校验)
  ←── MenuItemTypeExtensions(Content) 1 次调用 (FromValue 校验)
  ←── ExpectedActionExtensions(Content) 1 次调用 (FromValue 校验)
  ←── ElementTypeMapper(Mappings)     1 次调用 (MapAndroidClass null 防御)
```

**12 个引用点**，方向单一（子域→root），无反向依赖。Python 无此角色（Python 各模块用 ValueError/TypeError）。

### 3.3 DomainJsonOptions 跨切面桥

```
DomainJsonOptions
  ←── 所有需要 JSON 序列化的类型（Coordinate, Direction, PageAnalysis 等）
```

方向单一（子域→root），无反向依赖。Python 无此角色（Python 用 Pydantic/dataclass 自带序列化）。

---

## 4. Domain→上层连接点

| 连接 | 方向 | 依赖类型 | Phase |
|------|------|----------|-------|
| FlattenedScreen → AI 层 | AI ← Vision | FlattenedScreen 引用 | Phase 2+ |
| Operation → TraversalNode | Graph ← Common | Operation record 引用 | Phase 2 |
| ITraversalContext → IAIStrategyAdvisor | AI ← StateMachine | 接口参数 | Phase 2+ |
| ElementTypeMapper → 上层遍历逻辑 | 上层 ← Mappings | MapAndroidClass + ToMenuItemType + ToExpectedAction | Phase 2 |

**注意**: StateMachine 和 Observability 当前 **零 Domain 依赖**——完全独立。

---

## 5. 与 P0 fix 前的差异

| 项目 | fix 前 | fix 后 |
|------|--------|--------|
| ElementTypeMapper → TypeHint | **核心依赖**（MapAndroidClass 返回 TypeHint） | **可选依赖**（仅 ToTypeHint 便利方法） |
| ElementTypeMapper → DomainValidationException | 无 | 新增（MapAndroidClass null 防御） |
| Mappings 桥宽度 | 3 核心（TypeHint + MenuItemType + ExpectedAction） | 2 核心 + 1 可选 + 1 跨切面 |
| Vision ↔ Content 隐式耦合 | Mappings 是显式 TypeHint 依赖 | 隐式字符串词汇表耦合（同 Python） |

---

## 6. 无循环依赖确认

```
扫描方向：所有箭头从子域指向 root 或从桥指向子域
反向依赖检查：无。无任何 root→子域、子域→子域的 import。
循环依赖检查：无。DAG 是严格的单向图。
```

**3 岛 + 1 桥 + 2 跨切面**的结构是干净的 DAG，与 Python 3 岛 + 1 隐式桥的架构对齐度显著提升（fix 后）。
