# 设计规范：跨帧行身份稳定化（Row Identity Stabilization）

> 状态：已批准（Human 授权 2026-08-29："方案可靠就把刚刚敲定的几个都做吧"）
> 范围：Phase 2.6 感知/归一化管线
> 目的：形成稳定的项目规范，防止"每次唤醒遗漏"

## 1. 问题定义

C# 归一化用 `Text|PerceptionType` 做行身份键。OCR 在不同帧对同一行可能产出不同文本 → 键不稳定 → 归一化断链。

## 2. 核心设计决策

### D1：C# 拥有记忆，Python 只做匹配

```
C# Runtime（Run 生命周期内维护已知行）
  → 每次观测时将 known_rows 上下文发送给 Python
  → Python 用上下文做匹配，返回 row_id
  → Python 无状态，重启无影响

NOT: Python 维护跨帧缓存（有状态，重启丢失，Run 边界不明确）
```

### D2：RowId 是行的稳定身份

```
签名：RowId|PerceptionType（替代 Text|PerceptionType）

RowId 规则：
  - 由 C# 分配，格式 "row_NNN"（NNN 为 Run 内递增序号）
  - 一旦分配，Run 内不变
  - 新 Run 重置计数器
  - Run 结束后 RowId 不再有意义（不跨 Run）

Text 字段保留（人类可读），但不参与签名匹配。
```

### D3：Python 稳定化器无状态化

```
输入：当前帧候选行 + C# 提供的 known_rows
输出：匹配到的行带 row_id；未匹配的行标 "new"

Python 不维护缓存。每次调用都是独立的。
```

### D4：上下文传递协议

```
C# → Python 请求：
  POST /v1/analyze
  Content-Type: image/png
  X-Known-Rows: [
    {"id":"row_001","text":"Network & internet"},
    {"id":"row_002","text":"Connected devices"},
    ...
  ]

Python → C# 响应：
  candidates: [
    {"text":"Network&internet", "row_id":"row_001", ...},  // 匹配到已知行
    {"text":"New Item", "row_id":null, ...},                 // 新行
  ]
```

### D5：匹配算法（Python 端）

```
对每个候选行：
  1. 三元组规范化（去空白、小写）
  2. 与 known_rows 精确匹配 → 直接返回 row_id
  3. 三元组 Jaccard ≥ 0.75 + 邻居上下文确认 → 返回匹配的 row_id
  4. 三元组 Jaccard ≥ 0.90 → 直接返回 row_id
  5. 不匹配 → row_id = null（新行）

歧义（两个候选得分相同）→ 返回 null（让 C# 决定，不猜）
```

### D6：C# 端签名变更

```csharp
// 之前：
BuildSignature(element) => $"{element.Text}|{element.PerceptionType}||";

// 之后：
BuildSignature(element) => $"{element.StableKey ?? element.Text}|{element.PerceptionType}||";
// StableKey = Python 返回的 row_id；缺失时回退到 Text（新行首帧）
```

## 3. 交互流程

```
┌────────┐    截图 + known_rows     ┌────────┐
│  C#    │ ─────────────────────→  │ Python │
│ Runtime│                          │ 感知   │
│        │  ←─────────────────────  │ 服务   │
│        │  candidates + row_id     │        │
└────┬───┘                          └────────┘
     │
     ▼
┌────────────────────────────────────────┐
│  C# 处理                               │
│  1. 有 row_id → 签名 = row_id|type    │
│  2. 无 row_id → 分配新 row_id         │
│     → 签名 = 新row_id|type            │
│     → 加入 known_rows                 │
│  3. 归一化用签名匹配（精确 Ordinal）    │
└────────────────────────────────────────┘
```

## 4. 不变量

- RowId 在 Run 内不变、不重用
- Python 服务无状态（可重启）
- C# 归一化仍然精确匹配（不做模糊）
- Text 字段始终保留（人类可读）
- 新 Run 重置 RowId 计数器和 known_rows

## 5. 与已有修复的关系

| 已有修复 | 本规范的影响 |
|---|---|
| 边界容忍（跳过首行） | 不受影响（签名仍精确匹配）|
| 锚定合并 | 不受影响（锚点匹配用 row_id，更稳定）|
| 稳定化器 | 重构为无状态（接受外部上下文）|
| 列提升/去重 | 不受影响（发生在稳定化之前）|
