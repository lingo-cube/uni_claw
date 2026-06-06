# Test Results Directory

## Purpose

This directory stores standardized test execution results in a machine-readable JSON format. It serves as:

1. **Persistent Test Record**: Historical record of test executions with timestamps and environment context
2. **CI/CD Integration**: Standardized output for CI pipelines and automated quality gates
3. **Trend Analysis**: Track test performance, coverage, and flakiness over time
4. **Evidence Artifacts**: Attachable evidence for PRs, releases, and compliance documentation

## File Format

Test result files use JSON format with the following structure:

```json
{
  "schema_version": "1.0",
  "test_id": "unique-identifier",
  "timestamp": "ISO-8601 UTC",
  "environment": {
    "os": "platform",
    "python_version": "X.Y.Z",
    "dependencies": {}
  },
  "results": {
    "total": 0,
    "passed": 0,
    "failed": 0,
    "skipped": 0,
    "errors": 0
  },
  "duration_ms": 0,
  "artifacts": [],
  "metadata": {}
}
```

Detailed schemas are defined in `schema/` subdirectory.

### Minimal Contract

For basic validation reporting, a minimal format is supported:

```json
{
  "module": "module_name",
  "total": 10,
  "passed": 9,
  "failed": 1,
  "timestamp": "2026-06-06T12:00:00Z"
}
```

## Naming Rules

Test result files follow this pattern:

```
{module}_{type}.{ext}
```

Components:
- `module`: Name of code module (lowercase + underscores only)
- `type`: `unit`, `integration`, or `e2e`
- `ext`: File extension (`json` for results, `xml` for coverage)

Examples:
```
trace_models_unit.json
trace_recorder_unit.json
graph_engine_integration.json
coverage.xml
```

**Rules**:
- No version numbers or dates in filenames (latest results only)
- Module name must match code directory structure
- Coverage files use standard `coverage.xml` naming

## Data Freshness

| Test Type | Freshness Requirement | Regeneration |
|-----------|----------------------|--------------|
| Unit tests | Results should be < 48 hours old | Re-run via module-test skill |
| Integration tests | Results should be < 7 days old | Re-run via test suite |
| Coverage reports | Should match current code | Regenerate after changes |

Results older than the freshness threshold should be regenerated for accurate validation.

## File Lifecycle

1. **Creation**: Test runner generates result file during execution
2. **Validation**: File validated against schema (see `scripts/validate_test_result.py`)
3. **Overwrite**: New runs overwrite existing files (latest results only)
4. **History**: Git history provides access to previous results
5. **Cleanup**: No manual cleanup required (files are overwritten)

## Schema Definitions

The `schema/` subdirectory contains JSON schema definitions:

- `unit_result.schema.json`: Unit test result schema (full + minimal contract)
- `integration_result.schema.json`: Integration test result schema (TODO)
- `e2e_result.schema.json`: End-to-end test result schema (TODO)

All schemas include `schema_version` field for compatibility tracking.

## Related Documentation

- [Test Guide](../docs/TEST_GUIDE.md): Overall testing documentation
- [Test Workflows](../docs/TESTING_WORKFLOWS.md): Common testing workflows
- [Validation Reports](../docs/validation/): V6 implementation validation reports

## Tools

- `scripts/validate_test_result.py`: Validate result files against schemas
- `/module-test`: Skill to run module tests and generate result files
