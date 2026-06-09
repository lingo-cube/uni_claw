## 1. V6.13.0 - P0: Create New Models

- [x] 1.1 Create `src/models/content_models.py` with Coordinate model
- [x] 1.2 Add Direction Enum with helper methods (values, from_value, is_valid)
- [x] 1.3 Add MenuInfo, MenuItem, MenuItemType, ExpectedAction models
- [x] 1.4 Add PageAnalysis and PopupInfo models
- [x] 1.5 Add ContentTree, ContentNode, VisitFingerprint models
- [x] 1.6 Add SimulationState model (renamed from TraversalState) with 13 fields and 8 methods
- [x] 1.7 Create `src/models/test/test_content_models.py` with tests for all 12 classes
- [x] 1.8 Verify all helper methods (from_value, is_valid, values, get_fingerprint, etc.)
- [x] 1.9 Verify serialization/deserialization for all models
- [x] 1.10 Verify ContentTree methods (add_node, add_child_node, mark_visited, get_unvisited_children, to_markdown)
- [ ] 1.11 Create fixture compatibility check script at `scripts/verify_fixture_compatibility.py`
- [ ] 1.12 Run fixture compatibility check and regenerate incompatible fixtures if needed
- [x] 1.13 Update `src/models/__init__.py` to export all content models
- [x] 1.14 Add backward compatibility alias: `TraversalState = SimulationState`
- [x] 1.15 Create backward compatibility verification script at `scripts/verify_backward_compatibility.py`
- [x] 1.16 Run backward compatibility verification and confirm alias works
- [x] 1.17 Run `pytest src/models/test/test_content_models.py -v` and ensure all tests pass

## 2. V6.13.0 - P1: Update Simulation Tests

- [x] 2.1 Update `src/simulation/mock_vision.py` imports from `src.state.content_tree` to `src.models.content_models`
- [x] 2.2 Update `src/simulation/stateful_mock_vision.py` imports
- [x] 2.3 Update `src/simulation/scroll/scrollable_mock_vision.py` imports
- [x] 2.4 Run `pytest src/simulation/ -v` and ensure all tests pass

## 3. V6.13.0 - P2: Handle TYPE_CHECKING Dependency

- [x] 3.1 Update `src/exception/context.py` TYPE_CHECKING block to use type alias
- [x] 3.2 Change import to: `from src.trace.context import TraversalRuntimeContext as TraversalState`
- [x] 3.3 Update exception tests if needed
- [x] 3.4 Run `mypy src/exception/` and ensure type checking passes (test compatibility errors resolved, remaining errors are pre-existing)
- [x] 3.5 Run `pytest src/exception/test/ -v` and ensure tests pass

## 4. V6.13.0 - P3: Update Unit Tests (Batch by Dependency)

- [ ] 4.1 Update `src/models/test/test_coordinate.py` import to `from src.models.content_models import Coordinate`
- [ ] 4.2 Update `src/models/test/test_direction.py` import and verify helper methods
- [ ] 4.3 Update `src/models/test/test_menu_info.py` import and verify Coordinate nesting
- [ ] 4.4 Search for other files using bottom layer models and update imports
- [ ] 4.5 Run batch 1 tests and verify functionality
- [ ] 4.6 Update `src/models/test/test_menu_item.py` import and verify get_fingerprint method
- [ ] 4.7 Update `src/models/test/test_page_analysis.py` import and verify nested serialization
- [ ] 4.8 Update `src/simulation/test/test_*.py` files (approx 4 files) for PageAnalysis imports
- [ ] 4.9 Update `src/ai/test/integration/test_new_architecture.py` for PageAnalysis import
- [ ] 4.10 Run batch 2 tests and verify functionality
- [ ] 4.11 Update `src/models/test/test_content_tree.py` import and verify all 6 methods
- [ ] 4.12 Update TraversalState-related exception tests (7 files) with new SimulationState/TraversalState alias
- [ ] 4.13 Search and replace remaining `from src.state` imports in test files
- [ ] 4.14 Run `grep -r "from src.state" src/ tests/ --include="*.py"` to verify no remaining imports
- [ ] 4.15 Run batch 3 tests and verify functionality

## 5. V6.13.0 - P4: Update Integration Tests

- [ ] 5.1 Update `tests/integration/test_complete_traversal.py` imports for ContentTree, VisitFingerprint
- [ ] 5.2 Update TraversalState import to SimulationState or keep as TraversalState alias
- [ ] 5.3 Update `tests/integration/test_with_real_data.py` imports
- [ ] 5.4 Verify SimulationState methods work correctly (add_level1_menu, get_level2_menus, add_items, etc.)
- [ ] 5.5 Run `grep -r "from src.state.*TraversalState\|from src.models.*TraversalState" tests/integration/ --include="*.py"`
- [ ] 5.6 Replace any remaining TraversalState imports with SimulationState or alias
- [ ] 5.7 Run `pytest tests/integration/ -v` and ensure all tests pass

## 6. V6.13.0 - P5: Add Deprecation Warnings

- [ ] 6.1 Create `src/state/__init__.py` with `__getattr__` lazy import function
- [ ] 6.2 Add DeprecationWarning with message pointing to new location
- [ ] 6.3 Add Pydantic model detection to skip warnings during serialization
- [ ] 6.4 Configure CI/CD warnings filter in setup.cfg or conftest.py
- [ ] 6.5 Verify deprecation warnings display in development environment
- [ ] 6.6 Verify CI/CD tests pass without warning failures
- [ ] 6.7 Run full test suite: `pytest tests/ -v`

## 7. V6.14.0 - P6: Delete Legacy Code

- [ ] 7.1 Verify all tests pass before deletion: `pytest tests/ -v`
- [ ] 7.2 Verify no remaining imports: `grep -r "from src.state" src/ tests/ --include="*.py"`
- [ ] 7.3 Delete `src/state/state_manager.py`
- [ ] 7.4 Delete `src/state/content_tree.py`
- [ ] 7.5 Delete `src/state/__init__.py`
- [ ] 7.6 Remove empty `src/state/` directory
- [ ] 7.7 Verify no import errors in codebase

## 8. V6.14.0 - P7: Full Verification

- [ ] 8.1 Run full test suite: `pytest tests/ -v`
- [ ] 8.2 Run simulation tests: `pytest src/simulation/ -v`
- [ ] 8.3 Run type checking: `mypy src/ --strict`
- [ ] 8.4 Verify no src.state imports remain in src/: `grep -r "from src.state" src/ --include="*.py"`
- [ ] 8.5 Verify no src.state imports remain in tests/: `grep -r "from src.state" tests/ --include="*.py"`
- [ ] 8.6 Verify no src.state imports in integration tests: `grep -r "from src.state" tests/integration/ --include="*.py"`
- [ ] 8.7 Run verification script: `python scripts/verify_state_migration.py`
- [ ] 8.8 Update `docs/INDEX.md` with new module structure
- [ ] 8.9 Update `docs/architecture/ARCHITECTURE.md` with migration notes

## 9. Verification Scripts Creation

- [ ] 9.1 Create `scripts/verify_state_migration.py` with comprehensive import checks
- [ ] 9.2 Create `scripts/verify_batch_dependencies.py` for P3 batch verification
- [ ] 9.3 Test verification scripts before running full migration
- [ ] 9.4 Add scripts to git repository
