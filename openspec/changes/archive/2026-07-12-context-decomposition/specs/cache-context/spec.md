# Cache Context Specification

## ADDED Requirements

### Requirement: Cache context tracks cache and configuration state
The system SHALL provide a `CacheContext` class that encapsulates cache state including page cache, cache validity, and Phase 3 reserved fields.

#### Scenario: Cache context initialization
- **WHEN** a `CacheContext` is created
- **THEN** the context initializes with empty PageCache dictionary and CacheValid set to false

### Requirement: Page cache storage
The system SHALL maintain a dictionary of cached page data keyed by cache key.

#### Scenario: Access page cache
- **WHEN** consumer accesses `Cache.PageCache`
- **THEN** system returns `IReadOnlyDictionary<string, object>` for read access

#### Scenario: Modify page cache via concrete class
- **WHEN** consumer needs to modify cache
- **THEN** they access the internal Dictionary via concrete class methods (not specified in this spec, implementation detail)

### Requirement: Cache validity flag
The system SHALL maintain a boolean flag indicating whether the current cache is valid.

#### Scenario: Set cache valid
- **WHEN** cache validity changes via `CacheContext.SetCacheValid(value)`
- **THEN** CacheValid is updated to the new boolean value

#### Scenario: Read-only cache validity access
- **WHEN** consumer accesses `Cache.CacheValid`
- **THEN** system returns the current boolean value

### Requirement: Phase 3 reserved fields
The system SHALL reserve positions for scroll handler and page snapshot functionality to be implemented in Phase 3.

#### Scenario: Scroll handler reserved
- **WHEN** Phase 3 is implemented
- **THEN** CacheContext will have ScrollHandler field for scroll state management

#### Scenario: Current snapshot reserved
- **WHEN** Phase 3 is implemented
- **THEN** CacheContext will have CurrentSnapshot field for page snapshot management

#### Scenario: Reserved fields are object
- **WHEN** reserved fields are accessed
- **THEN** they return object? (nullable object) until Phase 3 implements concrete types

### Requirement: Read-only interface isolation
The system SHALL provide `ICacheContext` interface with only read-only property getters.

#### Scenario: Interface exposes no mutation methods
- **WHEN** consumer holds `ICacheContext` reference
- **THEN** only read-only properties are accessible (PageCache, CacheValid)

#### Scenario: Mutation methods only on concrete class
- **WHEN** consumer needs to mutate cache state
- **THEN** they must cast to or hold `CacheContext` concrete class reference
