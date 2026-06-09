## Context

The `/src/state` module was originally created for V5 state management but became legacy after V6 architecture refactoring introduced `TraversalRuntimeContext` in `src/trace/context.py`. The old module persists with 550 lines mixing data models, persistence logic, and content tree structures. Current analysis shows 33 files still import from it (16 src + 17 tests + 2 integration tests), primarily for simulation mocks and integration test utilities.

The module serves two distinct purposes:
1. **Simulation models**: `Coordinate`, `Direction`, `MenuInfo`, `MenuItem`, `PageAnalysis`, `PopupInfo` - used by mock vision systems
2. **Integration test models**: `ContentTree`, `ContentNode`, `VisitFingerprint` - used by traversal integration tests
3. **Legacy state container**: `TraversalState` BaseModel (being renamed to `SimulationState`) - runtime state for simulation/integration tests

A critical naming conflict exists: `TraversalState` is defined twice - as an Enum in `src/state_machine/traversal_fsm.py` and as a BaseModel in `src/state/content_tree.py`. This creates ambiguity when both are imported.

**Constraints:**
- Cannot delete `TraversalState` BaseModel - integration tests use its methods (`add_level1_menu`, `get_level2_menus`, etc.)
- Must maintain backward compatibility for test infrastructure
- Production code already uses `TraversalRuntimeContext` - no production impact
- V6.13.0 for migration, V6.14.0 for cleanup

## Goals / Non-Goals

**Goals:**
- Move all 11-12 model classes to `src/models/content_models.py` with clear purpose
- Rename `TraversalState` BaseModel → `SimulationState` to eliminate naming conflict
- Update all 33 import statements across the codebase
- Add deprecation warnings for V6.13.0 transition period
- Delete `/src/state` directory in V6.14.0
- Maintain 100% test compatibility (no test failures)

**Non-Goals:**
- Rewriting simulation tests (test behavior unchanged)
- Migrating `SimulationState` functionality to `TraversalRuntimeContext` (deferred to V7.0)
- Changing model APIs or data structures (only moving location)
- Affecting production traversal code (uses `TraversalRuntimeContext`)

## Decisions

### Decision 1: Single-file model organization

**Choice**: Consolidate all models into `src/models/content_models.py` (~410 lines)

**Alternatives considered:**
- Multi-file structure like `src/models/vision/` (7 files, 788 lines)
- Keep existing scattered structure

**Rationale:**
- Models are tightly coupled (Coordinate → MenuItem → PageAnalysis → ContentTree)
- Single file easier to verify and test during migration
- 410 lines is manageable for one file
- Content models used primarily for testing (vs vision models for production AI)
- Can split in V7.0 if grows beyond 600 lines

### Decision 2: Rename `TraversalState` → `SimulationState`

**Choice**: Rename BaseModel to `SimulationState` with backward compatibility alias

**Alternatives considered:**
- Delete `TraversalState` entirely and rewrite integration tests
- Use only `TraversalRuntimeContext` everywhere

**Rationale:**
- Naming conflict with `TraversalState` Enum causes genuine ambiguity
- `SimulationState` better reflects actual usage (simulation/integration tests only)
- Backward compatibility alias via `src/models/__init__.py` smooths transition
- Deleting would require rewriting 7 integration test files (5-8h extra work)
- Defer full migration to V7.0 when more time available

### Decision 3: TYPE_CHECKING handling via type alias

**Choice**: Use `from src.trace.context import TraversalRuntimeContext as TraversalState` in TYPE_CHECKING block

**Alternatives considered:**
- Replace with `Any` type
- Create Protocol interface
- Direct import of new `SimulationState`

**Rationale:**
- Maintains type safety for static analysis
- Aligns with V6 architecture (production uses `TraversalRuntimeContext`)
- Zero runtime impact (TYPE_CHECKING only)
- Simplest solution for type annotations

### Decision 4: Two-phase migration (V6.13.0 → V6.14.0)

**Choice**: Add deprecation warnings in V6.13.0, delete in V6.14.0

**Alternatives considered:**
- Big bang delete in V6.13.0
- Keep legacy module indefinitely

**Rationale:**
- Allows gradual verification and testing
- Deprecation warnings alert developers to update imports
- Separates migration from cleanup for safer rollout
- V6.14.0 deletion confirms no hidden dependencies

### Decision 5: Batched test updates by dependency layer

**Choice**: Update tests in three batches (bottom → middle → top layer models)

**Alternatives considered:**
- Update all tests at once
- Update file-by-file alphabetically

**Rationale:**
- Models have dependency hierarchy (Coordinate → MenuItem → PageAnalysis)
- Batched updates allow incremental verification
- Failures isolated to specific layer
- Can merge batches if dependencies prove complex

## Risks / Trade-offs

### Risk 1: Fixture deserialization failure

**Risk**: Pickle fixtures contain old `__module__` references (`src.state.content_tree`) that fail to deserialize

**Mitigation:**
- T1 includes fixture compatibility check script
- Regenerate incompatible fixtures using new models
- Or provide migration script to update `__module__` in pickle files

### Risk 2: Batch dependency conflicts

**Risk**: P3 batches may have hidden dependencies causing failures

**Mitigation:**
- Automated verification script between batches
- If conflicts found, merge batches and update together
- Dependency check: `python -c "from src.models.content_models import <models>"`

### Risk 3: Deprecation warning noise

**Risk**: 33 files importing from deprecated module produce excessive warnings

**Mitigation:**
- Configure CI/CD warnings filter: `ignore::DeprecationWarning:src.state.*`
- Add filter to `setup.cfg` or `conftest.py`
- Allows CI/CD to pass while warnings visible in development

### Risk 4: Integration test method incompatibility

**Risk**: `SimulationState` methods may have different signatures than expected

**Mitigation:**
- T4 includes dedicated method verification (2-3h allocated)
- Test all 8 methods: `add_level1_menu`, `get_level2_menus`, `add_items`, etc.
- Fix any signature mismatches during migration

### Risk 5: Hidden imports missed by grep

**Risk**: Complex import patterns not caught by standard grep

**Mitigation:**
- Comprehensive import patterns including relative imports
- Verify script checks 6 patterns (absolute, relative, module-level)
- Final verification runs all tests before V6.14.0 deletion

## Migration Plan

### V6.13.0 Migration Phase

**P0 - Create new models (6h)**
- Create `src/models/content_models.py` with 12 classes
- Add unit tests for all models
- Verify fixture compatibility
- Add backward compatibility alias

**P1 - Update simulation tests (1.5h)**
- Update 3 mock vision files
- Verify simulation functionality unchanged

**P2 - Handle TYPE_CHECKING (1h)**
- Update `src/exception/context.py` type alias
- Verify exception tests pass

**P3 - Update unit tests in batches (11h)**
- Batch 1: Bottom layer models (3h)
- Batch 2: Middle layer models (4h)
- Batch 3: Top layer + TraversalState (4h)

**P4 - Update integration tests (5h)**
- Update 2 integration test files
- Verify SimulationState method compatibility
- Search and replace all TraversalState imports

**P5 - Add deprecation warnings (1h)**
- Add `__getattr__` to `src/state/__init__.py`
- Configure CI/CD warnings filter
- Verify warnings display correctly

### V6.14.0 Cleanup Phase

**P6 - Delete legacy code (0.5h)**
- Delete `src/state/state_manager.py`
- Delete `src/state/content_tree.py`
- Delete `src/state/__init__.py`
- Remove empty `src/state/` directory

**P7 - Full verification (3h)**
- Run all tests: `pytest tests/ -v`
- Run type checking: `mypy src/ --strict`
- Verify no remaining imports
- Update documentation

### Rollback Strategy

| Stage | Rollback Method |
|-------|----------------|
| P0-P1 failure | `git reset --hard origin/main` |
| P2 failure | `git checkout HEAD~1 -- src/exception/` |
| P3 failure | `git revert HEAD` (per commit) |
| P4 failure | `git checkout HEAD~1 -- tests/integration/` |
| Complete failure | Delete branch and return to main |

### Verification Commands

```bash
# Check no remaining src.state imports
grep -r "from src.state" src/ tests/ --include="*.py"

# Verify all tests pass
pytest tests/ -v

# Verify type checking
mypy src/ --strict

# Verify fixture compatibility
python scripts/verify_fixture_compatibility.py

# Verify batch dependencies
python scripts/verify_batch_dependencies.py
```

## Open Questions

None - all technical decisions settled through PRD review process.
