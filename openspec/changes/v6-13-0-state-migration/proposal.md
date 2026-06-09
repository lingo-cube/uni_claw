## Why

The `/src/state` directory contains legacy state management code that mixes simulation data models, persistence logic, and content tree models, violating single responsibility principle. Since V6 architecture refactoring introduced `TraversalRuntimeContext` for runtime state management, the old state module has become redundant but remains in use by 33 files across simulation and integration tests. This technical debt creates unclear architecture, potential naming conflicts, and maintenance burden. Migrating these models to a proper location (`src/models/content_models.py`) will clarify their purpose as simulation/testing utilities and eliminate confusion.

## What Changes

- **Move 11-12 model classes** from `src/state/content_tree.py` to new `src/models/content_models.py`:
  - `Coordinate`, `Direction`, `MenuInfo`, `MenuItem`, `MenuItemType`, `ExpectedAction`
  - `PageAnalysis`, `PopupInfo`
  - `ContentTree`, `ContentNode`, `VisitFingerprint` (integration test support)
  - `SimulationState` (renamed from `TraversalState` BaseModel)

- **BREAKING: Rename `TraversalState` BaseModel → `SimulationState`**
  - Eliminates naming conflict with `TraversalState` Enum in `src/state_machine/traversal_fsm.py`
  - Provides backward compatibility alias via `src/models/__init__.py`
  - Marked as "simulation and integration test only"

- **Update 33 import statements** across:
  - Simulation mock files (3 files)
  - Exception tests (3 files) 
  - Integration tests (2 files)
  - Model tests (12 files)
  - Other tests (12 files)
  - AI integration tests (1 file)

- **Add deprecation warnings** via `__getattr__` in `src/state/__init__.py` (V6.13.0)

- **Delete `/src/state` directory** entirely (V6.14.0)

## Capabilities

### New Capabilities

- `content-models`: Centralized content models for simulation and testing, including coordinate systems, menu structures, page analysis, and content tree representations. Replaces scattered models from legacy state module.

### Modified Capabilities

None (this is a migration/refactoring, no functional requirements changes)

## Impact

- **Code affected**: 33 files requiring import path updates (16 src + 17 tests + 2 integration tests)
- **Simulation tests**: Mock vision files use `PageAnalysis`, `PopupInfo`, `MenuItem` models
- **Integration tests**: Use `ContentTree`, `ContentNode`, `VisitFingerprint` for traversal tracking
- **Exception handling**: TYPE_CHECKING import of `TraversalState` for type annotations
- **Dependencies**: None added (pure refactoring)
- **API changes**: Import paths change from `src.state.content_tree` to `src.models.content_models`
- **Risk profile**: Medium - affects test infrastructure but production code uses `TraversalRuntimeContext`
