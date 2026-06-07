## 1. Template Registry Loading

- [x] 1.1 Implement `_load_template_registry()` to create `TemplateRegistry` with 3 built-in templates
- [x] 1.2 Add custom template file loading when `plan.template_registry` path exists
- [x] 1.3 Create `DynamicMatcher` instance and assign to `self.dynamic_matcher`
- [x] 1.4 Add missing template file warning (continue with built-ins)

## 2. Path Concatenation

- [x] 2.1 Add `parent_path` parameter to `TemplateInstantiator.instantiate()`
- [x] 2.2 Implement path concatenation logic: `precondition.path = parent_path + [name]`
- [x] 2.3 Add `parent_path` parameter to `TemplateRegistry.instantiate()`
- [x] 2.4 Forward `parent_path` from `TemplateRegistry.instantiate()` to `TemplateInstantiator.instantiate()`

## 3. Dynamic Child Generation

- [x] 3.1 Add engine fields: `template_registry`, `dynamic_matcher`, `_dynamic_children`, `_last_known_path`
- [x] 3.2 Implement `_generate_dynamic_children()` method
- [x] 3.3 Add DynamicRule to dict conversion for `load_rules()`
- [x] 3.4 Implement MenuItem to dict field mapping (type, text, index, coordinate_x/y)
- [x] 3.5 Call `DynamicMatcher.match_all()` and instantiate matched children
- [x] 3.6 Cache generated children in `_dynamic_children[node_id]`
- [x] 3.7 Add `_record_skip_span()` for unmatched items
- [x] 3.8 Extend `_get_next_unvisited_child()` to handle DYNAMIC_MATCH strategy
- [x] 3.9 Return cached unvisited children, mark as visited

## 4. FRAME_COMPLETE Interception

- [x] 4.1 Add FRAME_COMPLETE interception logic in `_step_once()`
- [x] 4.2 Check for remaining unvisited dynamic children when FRAME_COMPLETE occurs
- [x] 4.3 Push next unvisited child and override to NODE_SELECT if children remain
- [x] 4.4 Allow FRAME_COMPLETE to proceed normally when no children remain

## 5. Cache Invalidation

- [x] 5.1 Implement `invalidate_children_cache(node_id)` method
- [x] 5.2 Add path change detection at end of `_step_once()`
- [x] 5.3 Compare `current_path` with `last_known_path`
- [x] 5.4 Call `invalidate_children_cache()` when path changes
- [x] 5.5 Update `last_known_path` after each step

## 6. Plan Compiler

- [x] 6.1 Create `src/graph/compiler.py` with `CompilerError` exception
- [x] 6.2 Implement `PlanCompiler` class skeleton
- [x] 6.3 Implement `_validate_slots()` with all validation rules
- [x] 6.4 Implement `compile()` main method
- [x] 6.5 Implement scope → completion_policy mapping (full/partial/target_only/target_path)
- [x] 6.6 Implement element_handling → dynamic_rules template set mapping
- [x] 6.7 Implement navigation → exit_condition.fallback mapping
- [x] 6.8 Implement completion → completion_policy override mapping
- [x] 6.9 Implement static path generation for target_path scope
- [x] 6.10 Implement depth, restore, and target_app direct mappings
- [x] 6.11 Add completion override warning log

## 7. Task Parser

- [x] 7.1 Create `src/ai/task_parser.py`
- [x] 7.2 Implement `parse_task_to_slots()` function skeleton
- [x] 7.3 Add Chinese app keyword extraction (设置, 显示, 声音, 网络, 存储, 应用, 微信, 相册)
- [x] 7.4 Add English app keyword extraction (settings, display, sound, network, storage, apps, wechat, gallery)
- [x] 7.5 Implement scope extraction from search/partial keywords
- [x] 7.6 Implement target extraction from "找到/查找/搜索/查看" keywords
- [x] 7.7 Add punctuation stripping for extracted target
- [x] 7.8 Return `IntentSlots` object with extracted fields

## 8. Testing

- [x] 8.1 Add unit tests for `_load_template_registry()` (3 built-ins + custom file)
- [x] 8.2 Add unit tests for path concatenation in template instantiation
- [x] 8.3 Add unit tests for `_generate_dynamic_children()` (mock page items)
- [x] 8.4 Add unit tests for MenuItem → dict field mapping
- [x] 8.5 Add unit tests for `_get_next_unvisited_child()` DYNAMIC_MATCH branch
- [x] 8.6 Add unit tests for FRAME_COMPLETE interception
- [x] 8.7 Add unit tests for cache invalidation on path change
- [x] 8.8 Add unit tests for `PlanCompiler` all mapping scenarios
- [x] 8.9 Add unit tests for `_validate_slots()` all validation rules
- [x] 8.10 Add unit tests for `parse_task_to_slots()` heuristic extraction
- [x] 8.11 Add simulation test for full traversal with dynamic children
- [x] 8.12 Add simulation test for compilation → execution pipeline

**Note**: Created comprehensive test suites:
- `tests/v6/test_v6_9_plan_compilation.py` - 30 tests for PlanCompiler and TaskParser
- `tests/v6/test_v6_9_dynamic_matching.py` - 10 tests for dynamic matching features
- All 40 tests passing ✓

## 9. Documentation

- [x] 9.1 Update `src/graph/README.md` with plan compiler documentation
- [x] 9.2 Update `src/traversal/README.md` with dynamic matching documentation
- [x] 9.3 Add examples of using `PlanCompiler` in docs
- [x] 9.4 Add examples of heuristic `parse_task_to_slots()` outputs

**Note**: Created comprehensive documentation:
- `src/graph/README.md` - Plan compiler, node models, template system, dynamic matcher
- `src/traversal/README.md` - GraphTraversalEngine, dynamic matching, cache invalidation, trace integration
- Both include usage examples and V6.9 feature documentation
