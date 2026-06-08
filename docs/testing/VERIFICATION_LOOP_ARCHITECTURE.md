# 验证闭环架构

> **Verification Loop Architecture**
> **创建**: 2026-06-08
> **更新**: 2026-06-08

---

## 核心改进

在原有Battle机制基础上，增加了**验证闭环**，确保改进真正有效。

```
之前: Battle发现问题 → 修正 → 完成（不知道修正是否有效）
现在: Battle发现问题 → 修正 → 验证 → 如有问题再修正（最多2次）
```

---

## 架构流程

```
┌─────────────────────────────────────────────────────────────┐
│                   Phase 1: Plan                             │
│                  架构师规划 (Opus)                           │
└─────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────▼──────────┐
                    │  Phase 2: Execute   │
                    │ 子代理执行 (Haiku)   │
                    └─────────┬──────────┘
                              │
                    ┌─────────▼──────────┐
                    │   Phase 3: Battle  │
                    │ 对抗验证 (Haiku/Sonnet) │
                    └─────────┬──────────┘
                              │
                    发现问题列表
                              │
                    ┌─────────▼──────────┐
                    │  Phase 4: Extract   │
                    │ 场景提取 (Haiku+Opus) │
                    └─────────┬──────────┘
                              │
                    ┌─────────▼──────────┐
                    │  Phase 5: Generate  │
                    │ 代码生成 (Haiku+Opus) │
                    └─────────┬──────────┘
                              │
                    ┌─────────▼──────────┐
                    │  Phase 6: Verify    │
                    │ 综合验证 (Haiku+Opus) │
                    └─────────┬──────────┘
                              │
                    关键问题列表
                              │
┌─────────────────────────────────────────────────────────────┐
│              Phase 7: Precision Refinement                   │
│                   精准改进 (Opus)                             │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  iteration = 0                                       │   │
│  │  while (iteration < 2 && issues.length > 0):         │   │
│  │    iteration += 1                                    │   │
│  │    Opus分析并修正                                    │   │
│  │    Haiku快速验证                                     │   │
│  │    if 无问题: break                                  │   │
│  │    else: 继续循环                                    │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                    修正后代码
                              │
┌─────────────────────────────────────────────────────────────┐
│              Phase 8: Final Verification                     │
│                   快速验证闭环 (Haiku)                       │
│                                                              │
│  验证项:                                                     │
│  1. 原问题是否已解决？                                       │
│  2. 是否引入新问题？                                         │
│  3. 代码质量是否提升？                                       │
│                                                              │
│  输出:                                                       │
│  • overall_score: 0-100                                     │
│  • can_proceed: boolean                                    │
└─────────────────────────────────────────────────────────────┘
                              │
                    验证结果
                              │
                    ┌─────────▼──────────┐
                    │  Phase 9: Report    │
                    │ 生成报告 (Opus)      │
                    └─────────────────────┘
```

---

## 关键改进点

### 1. 精准改进 (Phase 7)

**之前**: 单次修正，不知道是否有效

```javascript
const refined = await agent('改进代码', {model: 'opus'});
// 不知道改进是否真的解决了问题
```

**现在**: 最多2次迭代，确保改进有效

```javascript
let iteration = 0;
while (iteration < 2 && issues.length > 0) {
  iteration += 1;
  const refined = await agent('改进代码', {model: 'opus'});
  const check = await agent('快速验证', {model: 'haiku'});
  if (check.resolved) break;
  issues = check.remaining;
}
```

**优势**:
- ✅ 验证改进效果
- ✅ 如未解决，继续改进
- ✅ 最多2次，避免无限循环
- ✅ 成本可控

### 2. 快速验证闭环 (Phase 8)

**新增**: 专门验证改进是否有效

```javascript
const finalCheck = await agent(`
  验证修正后的代码:
  • 原问题是否解决？
  • 是否引入新问题？
  • 质量是否提升？
`, {model: 'haiku'});
```

**输出**:
```json
{
  "original_resolved": true,
  "new_issues": [],
  "quality_improved": true,
  "overall_score": 85,
  "can_proceed": true
}
```

---

## 迭代示例

### 示例1: 一次改进成功

```
发现3个问题:
  1. 缺少mock
  2. 断言不足
  3. 无fixture

第1次改进:
  Opus: 添加mock，完善断言，添加fixture
  Haiku验证: 所有问题已解决 ✓

最终验证:
  原问题已解决 ✓
  无新问题 ✓
  质量提升 ✓
  can_proceed: true ✓

结果: 迭代1次，成功
```

### 示例2: 两次改进成功

```
发现5个问题:
  1. 缺少engine mock
  2. 缺少trace_recorder
  3. 断言只验证返回值
  4. 无副作用验证
  5. 无边界测试

第1次改进:
  Opus: 添加mock，完善断言
  Haiku验证: 还有2个问题
    • 副作用验证不足
    • 边界测试缺失

第2次改进:
  Opus: 补充副作用验证，添加边界测试
  Haiku验证: 所有问题已解决 ✓

最终验证:
  原问题已解决 ✓
  无新问题 ✓
  质量提升 ✓
  can_proceed: true ✓

结果: 迭代2次，成功
```

### 示例3: 达到上限

```
发现8个问题:

第1次改进:
  解决了5个，还剩3个

第2次改进:
  解决了2个，还剩1个

达到最大迭代次数(2)，停止改进
标记: 警告，但有改进

最终验证:
  原问题大部分解决 ✓
  剩余1个小问题 ⚠
  质量明显提升 ✓
  overall_score: 75/100
  can_proceed: true (有小问题但可接受)

结果: 迭代2次，部分成功
```

---

## 质量保证机制

### 1. 职责分离

| 阶段 | 角色 | 职责 | 模型 |
|------|------|------|------|
| Battle | 挑刺者 | 发现问题 | Haiku/Sonnet |
| Refine | 解决者 | 修正问题 | Opus |
| Verify | 验证者 | 确认效果 | Haiku |

### 2. 迭代控制

```javascript
maxIterations = 2  // 最多2次，避免无限循环
stopCondition = issues.length === 0  // 问题全部解决时停止
costControl = 每次迭代用1次Opus + 1次Haiku  // 成本可控
```

### 3. 验证标准

```javascript
验证项 = {
  原问题解决: boolean,
  无新问题: boolean,
  质量提升: boolean,
  综合评分: 0-100
}

can_proceed = 原问题解决 && 无新问题 && 质量提升
```

---

## 与自我修正Battle的对比

| 特性 | 自我修正Battle | 验证闭环模式 |
|------|---------------|-------------|
| **修正者** | Battle中的Defender | 专门阶段(Phase 7) |
| **验证者** | Battle中的Critic | 专门阶段(Phase 8) |
| **迭代次数** | 不确定，可能很多 | 最多2次 |
| **职责分离** | ❌ 混乱 | ✅ 清晰 |
| **成本可控** | ❌ 难控制 | ✅ 可控 |
| **避免争论** | ❌ 可能争论 | ✅ 无争论 |
| **质量保证** | ⚠️ 依赖Battle | ✅ 独立验证 |

---

## 成本分析

### 单次改进（无验证闭环）

```
1 Opus调用 = ~$0.15
总计: $0.15
质量: 不确定
```

### 验证闭环（2次迭代）

```
1次迭代:
  - 1 Opus = $0.15
  - 1 Haiku = $0.015
  - 小计: $0.165

2次迭代:
  - 2 Opus = $0.30
  - 2 Haiku = $0.030
  - 小计: $0.330

最终验证:
  - 1 Haiku = $0.015

总计: $0.345
质量: 验证通过
```

**成本增加**: $0.345 vs $0.15 = +130%
**质量提升**: 从不确定到验证通过 = **无价**

---

## 使用指南

### 运行workflow

```bash
/Workflow multi-agent-test-validation-tiered state_machine
```

### 输出示例

```
Phase 7: Precision Refinement (Opus, max 2 iterations)...
🔧 第 1 次改进: 3 个问题...
✓ 第 1 次改进后，所有问题已解决
✓ 改进完成
  迭代次数: 1
  剩余问题: 0

Phase 8: Final Verification Loop (Haiku)...
🔍 执行最终验证闭环...
✓ 验证通过: 评分 85/100
✓ 验证完成
  验证评分: 85/100
  可继续: true

📊 验证闭环统计:
  精准改进迭代: 1 次
  剩余问题: 0 个
  最终验证评分: 85/100
  验证通过: ✅
```

---

## 总结

### 验证闭环的价值

1. **确保改进有效**: 不再盲目修正
2. **成本可控**: 最多2次迭代
3. **质量可见**: 明确的评分和通过/不通过
4. **职责清晰**: Battle找茬，Refine修正，Verify确认

### 最佳实践

1. **设置合理上限**: maxIterations = 2
2. **使用Haiku验证**: 快速、便宜
3. **记录迭代过程**: 每次改进做了什么
4. **最终验证不可省**: 确认整体质量

---

**Workflow文件**: [`.claude/workflows/multi-agent-test-validation-tiered-models.js`](.claude/workflows/multi-agent-test-validation-tiered-models.js)

**维护者**: Uni-Claw Development Team
**核心理念**: 改进不是目的，有效的改进才是目的
