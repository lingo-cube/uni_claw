## ADDED Requirements

### Requirement: Restricted artifact output
The Toolchain SHALL, when `--out <path>` is given on `packet-generate` or `replay-extract`, write the artifact JSON to a NEW file: the path SHALL NOT be inside the source bundle directory, SHALL NOT already exist (append-only), and the write SHALL be atomic (temp + rename). Violations SHALL fail closed with `INVALID_INPUT` (path policy / overwrite) or `SCHEMA_VIOLATION` (write failure). Without `--out`, behavior SHALL remain unchanged (artifact in envelope result).

#### Scenario: Generated artifact round-trips from disk
- **WHEN** a packet is written with `--out` and then read by `summarize` (or a fixture by `replay-run`)
- **THEN** the downstream command SHALL succeed on the written file

#### Scenario: Unsafe output rejected
- **WHEN** the output path is inside the bundle directory or already exists
- **THEN** the command SHALL return `INVALID_INPUT` without writing
