# runtime-debugging-casebook/ — Debugging Experience Casebook

> 本目录保存 Runtime debugging 经验案例，用于沉淀真实问题分析过程和工程
> 经验。**不是架构规范，不产生架构约束**。

## 用途

帮助 AI Coding Agent 理解：

- 用户看到的问题（Human Symptom）
- 真实世界状态（Expected Reality / Observed Reality）
- Evidence 定位过程（Evidence Reference / First Divergence Point）
- Owner 判断（责任组件）
- 最小修复（Minimal Change / Rejected Alternatives）
- 可复用规则（Engineering Lesson）

## 固定案例格式

每个案例文件必须包含以下 10 个字段：

```
Human Symptom
Expected Reality
Observed Reality
Reality Gap
Evidence Reference
First Divergence Point
Owner
Minimal Change
Rejected Alternatives
Engineering Lesson
```

完整格式定义见根 `docs/decisions/AGENTS.md` §Document Types — Casebook。

## 当前案例

| 文件 | 案例 |
|---|---|
| `01-scroll-stability-confirmation.md` | Scroll Stability Confirmation |
| `02-adaptive-revisit-coverage.md` | Adaptive Revisit Coverage |
| `03-stale-observation-wrong-tap.md` | Stale Observation / Wrong Tap |
| `04-scroll-execution-profile.md` | Scroll Execution Profile |
| `05-external-transition-settle.md` | External Transition Settle |
| `06-external-foreground-detection.md` | External Foreground Detection |

## Usage Rules

### 案例用于

- **类似问题识别**：当新问题与某个案例的 Human Symptom 或 Reality Gap 匹配时，案例的 First Divergence Point 和 Evidence Reference 可作为初始排查方向
- **Debugging 思路参考**：案例的 Engineering Lesson 提供了可复用的分析模式
- **历史证据链**：案例引用的 Evidence Reference（决策文档、trace、test）可作为当前问题的证据起点

### 案例不是

- 架构规范（不替代 `docs/system/constitution/` 或 `docs/architecture/` 的权威）
- Runtime contract（不替代 `docs/system/constitution/runtime-architecture-contract.md`）
- Authority 定义（不替代 `AGENTS.md §2 Authority Order`）
- 自动修改规则（不能直接说"以前这样修，所以现在也修改同一个模块"）

### 引用案例时必须

重新确认当前问题**是否具有相同 Reality Gap**。如果 Reality Gap 不同，即使 Human Symptom 看起来相似，First Divergence Point 和 Owner 也可能不同。

### 禁止

- "以前这样修，所以现在也修改同一个模块"——每个问题必须独立分析 Reality Gap
- 将案例的 Engineering Lesson 当作架构规则（lesson 是经验，不是不变量）
- 用案例替代 Evidence Collection（案例只提供排查方向，不替代当前问题的证据收集）