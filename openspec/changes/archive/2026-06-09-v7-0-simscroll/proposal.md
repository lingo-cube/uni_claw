## Why

V6.11 GraphTraversalEngine lacks scrollable list simulation support for testing. The current `StatefulMockVisionService` has `has_scroll` hardcoded to `False`, returns fixed element sets per page, provides no scroll state tracking (progress, position, history), and cannot simulate fault scenarios like scroll stuttering or unresponsiveness. This limits test coverage for apps with scrollable lists.

## What Changes

- **New Services**: Add `ScrollableMockVisionService` and `ScrollableMockActionExecutor` extending existing mock services with scroll simulation
- **Scroll State Management**: Track scroll progress (0.0-1.0), scroll count, and scroll history per page
- **Accumulation Mode**: Support progressive element visibility based on scroll threshold (elements appear when `threshold <= progress`)
- **Fault Injection**: Add methods to simulate scroll delays (`set_scroll_delay`) and unresponsiveness (`enable_scroll_failure`)
- **Element Deduplication**: Automatic ID-based deduplication when elements appear across multiple scroll segments
- **Test Infrastructure**: Complete test suite with 52 tests covering 10 PRD scenarios (basic, edge, fault, performance)

## Capabilities

### New Capabilities

- `scrollable-mock-vision`: Mock vision service with scroll list simulation support, including scroll state tracking, accumulation mode element visibility, fault injection, and element deduplication
- `scrollable-mock-action`: Mock action executor with scroll_down/scroll_up actions and scroll history tracking
- `scroll-data-models`: Data models for scroll segments (ScrollSegment), scroll state (ScrollState), and scroll actions (ScrollAction)
- `scroll-test-scenarios`: Test suite covering 10 scenarios including multi-screen scrolling, end-of-list detection, jump detection, empty lists, single-screen lists, stutter simulation, unresponsiveness, element deduplication, large lists (100 elements), and nested lists

### Modified Capabilities

- None (existing mock services are extended, not modified)

## Impact

- **Affected Code**:
  - New module: `src/simulation/scroll/` for scroll-related models and services
  - New test package: `tests/simulation/scroll/` with 52 tests
  - New fixtures: `fixtures/scroll/` with test data (WiFi lists, empty lists, nested lists)

- **API Changes**:
  - `ScrollableMockVisionService.analyze_screenshot()` - returns progressive elements based on scroll progress
  - `ScrollableMockVisionService.simulate_scroll()` - updates scroll state with delta
  - `ScrollableMockVisionService.set_scroll_delay()` - inject scroll delay
  - `ScrollableMockVisionService.enable_scroll_failure()` - inject scroll failure
  - `ScrollableMockActionExecutor.scroll_down/scroll_up` - new scroll actions

- **Dependencies**:
  - V6.11 GraphTraversalEngine (no modifications needed)
  - StatefulMockVisionService (base class)
  - StatefulMockActionExecutor (base class)
  - Existing PageAnalysis.items and MenuItem models (P0 compatibility fixes applied)

- **Systems**:
  - Simulation/test infrastructure
  - No impact on production runtime or real device execution
