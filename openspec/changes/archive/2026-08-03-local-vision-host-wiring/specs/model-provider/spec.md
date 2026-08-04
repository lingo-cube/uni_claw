# model-provider delta Specification

## MODIFIED Requirements

### Requirement: LocalVisionProvider requires explicit labelMappingConfigPath

LocalVisionProvider constructor SHALL NOT fall back to a CWD-relative default path. The `labelMappingConfigPath` parameter SHALL be required — the Host is responsible for resolving and passing the absolute path. The existing constructor overload without the path parameter SHALL be removed.

#### Scenario: Constructor requires path parameter

- **WHEN** LocalVisionProvider is constructed in Host assembly
- **THEN** `labelMappingConfigPath` is an absolute path resolved by Host

#### Scenario: No CWD fallback

- **WHEN** `labelMappingConfigPath` is null or empty
- **THEN** constructor throws ArgumentNullException or DomainValidationException
