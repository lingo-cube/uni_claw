# Button Type Differentiation - Phase 1

## Why

当前 uni-claw 遍历引擎对所有可点击元素采用统一的处理策略，无法区分不同类型按钮的行为差异。这导致：
- **菜单/标签按钮**和**设置开关**使用相同的等待时间
- 无法根据按钮预期行为采用不同的处理策略
- 点击效果验证不够精确

通过引入按钮类型区分，可以：
- 根据按钮类型使用不同的等待时间
- 采用针对性的验证策略
- 提高遍历效率和准确性

**这是第一阶段实现**，专注于基础按钮类型和差异化等待/验证策略。复杂手势（长按、滑动）和特殊元素（输入框、滑块）将在后续阶段实现。

## What Changes

### 核心变更

1. **扩展 MenuItem 类型定义**
   - 增加更细粒度的按钮类型：MENU_ITEM, TAB, BACK_BUTTON, TOGGLE, LINK, READONLY
   - 增加 `expected_action` 字段：标识按钮预期行为（navigate/toggle/action/none）
   - 增加 `expects_page_change` 字段：标识是否期望页面变化
   - 增加 `expects_state_change` 字段：标识是否期望状态变化

2. **增强 Vision Prompt**
   - 在 PROMPT_STRUCTURE 中加入按钮类型判断指令
   - 要求 AI 返回按钮的预期行为类型

3. **差异化点击处理**
   - 根据按钮类型使用不同的等待时间
   - 根据预期行为采用不同的验证逻辑
   - 针对 navigate 类型检查 current_path 变化
   - 针对 toggle 类型检查状态变化而非页面变化

4. **扩展 ClickResult 枚举**
   - 增加更细粒度的结果类型
   - 区分"成功切换"和"成功触发"等场景

### 数据流变化

```
当前流程：
点击元素 → 等待固定时间 → 分析结果 → 判断 ClickResult

新流程：
识别按钮类型 → 根据类型设置等待时间 → 点击 → 
根据预期行为验证 → 返回细化的 ClickResult
```

## Capabilities

### New Capabilities

- **button-type-classification**: 按钮类型分类和预期行为标识

### Modified Capabilities

- **vision-service**: 扩展 PROMPT_STRUCTURE，增加按钮类型判断要求
- **traversal-engine**: 根据按钮类型采用不同的点击和验证策略
- **exception-handling**: 增加基于按钮类型的异常处理逻辑

## Impact

### 代码影响

- `src/state/content_tree.py`: 扩展 MenuItemType 枚举和 MenuItem 模型
- `src/vision/vision_service.py`: 更新 PROMPT_STRUCTURE 模板
- `src/traversal/traversal_engine.py`: 实现差异化点击处理逻辑
- `src/vision/base_vision.py`: 可能需要更新解析逻辑

### 数据影响

- TraversalState 中的 items_cache 将包含更丰富的按钮信息
- 现有状态文件向后兼容（新增字段有默认值）

### API 影响

- VisionService.analyze_screenshot() 返回的 PageAnalysis.items 包含新字段
- 不破坏现有 API，向后兼容

### 性能影响

- AI 调用次数不变，但单次 prompt 略长
- 总体遍历时间可能减少（优化等待时间）
