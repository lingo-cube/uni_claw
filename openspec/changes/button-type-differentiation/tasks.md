# Implementation Tasks - Button Type Differentiation (Phase 1)

> **Phase 1 范围**: 基础按钮类型区分 + 差异化等待时间 + 差异化验证策略
> 
> **不包含**: 长按、滑动、拖拽等复杂手势（Phase 2）；输入框、滑块等特殊元素（Phase 3）
> 
> **完整路线图见**: design.md 中的"分阶段实现路线图"章节

## 1. Data Model Extensions

- [x] 1.1 Extend MenuItemType enum with new types: MENU_ITEM, TAB, BACK_BUTTON, TOGGLE, LINK, READONLY
- [x] 1.2 Create ExpectedAction enum: NAVIGATE, TOGGLE, ACTION, NONE
- [x] 1.3 Add expected_action field to MenuItem model with default value ACTION
- [x] 1.4 Add expects_page_change field to MenuItem model with default value False
- [x] 1.5 Add expects_state_change field to MenuItem model with default value False
- [x] 1.6 Update PageAnalysis to support enhanced item structure (already supported via MenuItem)
- [x] 1.7 Test backward compatibility with old state files

## 2. Vision Prompt Enhancement

- [x] 2.1 Update PROMPT_STRUCTURE with button type classification instructions
- [x] 2.2 Add type definitions and examples for each button type
- [x] 2.3 Add expected_action prediction instructions
- [x] 2.4 Add expects_page_change and expects_state_change field requirements
- [x] 2.5 Provide JSON schema examples for enhanced item structure
- [ ] 2.6 Test AI response quality with updated prompt

## 3. Traversal Engine - Wait Time Logic

- [x] 3.1 Implement _get_wait_time() method that accepts MenuItem parameter
- [x] 3.2 Add wait time mapping for NAVIGATE action (>= 1.0s)
- [x] 3.3 Add wait time mapping for TOGGLE action (<= 0.3s)
- [x] 3.4 Add wait time mapping for NONE action (0.1s)
- [x] 3.5 Implement fallback to config.wait_time for unknown actions
- [x] 3.6 Update _tap_and_wait() to accept MenuItem and use calculated wait time

## 4. Traversal Engine - Verification Logic

- [x] 4.1 Implement _verify_by_expected_action() method
- [x] 4.2 Implement _verify_navigate() method (path change check)
- [x] 4.3 Implement _verify_toggle() method (state change check, no path change)
- [x] 4.4 Implement _verify_generic() method (standard popup/jump check)
- [x] 4.5 Update _click_item() to use verification by expected action
- [x] 4.6 Add state change detection for toggle-type items

## 5. Exception Handling Enhancement

- [x] 5.1 Update _handle_no_feedback() to consider button type
- [x] 5.2 Skip child retry for toggle-type items
- [x] 5.3 Add retry logic for navigate-type failures
- [x] 5.4 Update before/after comparison to use expected action context
- [x] 5.5 Add expected behavior violation detection
- [x] 5.6 Update event emission to include button type information

## 6. Read-only Element Handling

- [x] 6.1 Implement read-only element detection in _select_next_item()
- [x] 6.2 Add option to skip read-only elements during selection
- [x] 6.3 Add minimal wait handling for read-only clicks (handled by _get_wait_time)
- [x] 6.4 Update content tree recording for read-only elements (handled by existing code)

## 7. State Manager Updates

- [x] 7.1 Ensure StateManager handles new MenuItem fields correctly (Pydantic handles defaults)
- [x] 7.2 Test loading old state files (new fields use defaults)
- [x] 7.3 Test saving new state files (all fields included)
- [x] 7.4 Verify JSON serialization/deserialization works

## 8. Testing

- [x] 8.1 Add unit tests for Extended MenuItemType enum
- [x] 8.2 Add unit tests for ExpectedAction enum
- [x] 8.3 Add unit tests for _get_wait_time() with all action types
- [x] 8.4 Add unit tests for _verify_navigate()
- [x] 8.5 Add unit tests for _verify_toggle()
- [x] 8.6 Add unit tests for _verify_generic()
- [ ] 8.7 Add integration test for complete traversal with new types
- [x] 8.8 Add test for old state file compatibility
- [ ] 8.9 Add test for AI prompt with enhanced response
- [ ] 8.10 Test wait times are actually applied (timing test)

## 9. Documentation

- [x] 9.1 Update README.md with new button types documentation
- [x] 9.2 Add examples of enhanced MenuItem structure
- [x] 9.3 Document wait time configuration
- [x] 9.4 Document expected_action behavior
- [ ] 9.5 Update API documentation for affected methods

## 10. Configuration and CLI

- [ ] 10.1 Add config option for custom wait time mappings
- [ ] 10.2 Add CLI flag for wait time override
- [ ] 10.3 Add debug mode to show button type detection
- [ ] 10.4 Add statistics output showing wait time distribution
