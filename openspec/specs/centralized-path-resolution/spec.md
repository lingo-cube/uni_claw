# centralized-path-resolution Specification

## ADDED Requirements

### Requirement: Host resolves label-mapping.json and server.py paths once

The Host SHALL resolve absolute paths for `label-mapping.json` and `server.py` at startup. The resolved label-mapping path SHALL be set as the `UNICLAW_LABEL_MAPPING` environment variable (for Python consumption) and passed to `LocalVisionProvider` constructor. The resolved server script path SHALL be passed to `PythonVisionService` constructor.

#### Scenario: Paths resolved from env override

- **WHEN** `UNICLAW_LABEL_MAPPING` environment variable is set
- **THEN** that value is used for label-mapping path

#### Scenario: Paths resolved from project root

- **WHEN** `UNICLAW_LABEL_MAPPING` is not set
- **THEN** label-mapping path resolves to `Path.GetFullPath("tools/local_vision/label-mapping.json")`

#### Scenario: Server script path resolved

- **WHEN** `PythonVisionService` is constructed for local mode
- **THEN** `serverScriptPath` parameter contains the absolute path to `tools/local_vision/server.py`

#### Scenario: C# provider receives explicit path

- **WHEN** `LocalVisionProvider` is constructed
- **THEN** `labelMappingConfigPath` parameter is the Host-resolved absolute path, not a CWD-relative default
