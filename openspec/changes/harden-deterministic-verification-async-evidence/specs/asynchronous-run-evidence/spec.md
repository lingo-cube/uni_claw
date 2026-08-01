## ADDED Requirements

### Requirement: Eligible run evidence uses a bounded ordered asynchronous pipeline

The Host SHALL provide one run-scoped bounded asynchronous submission pipeline for eligible screenshots, UI XML, normalized analysis, verification assets, issues, and durable trace records. Each accepted envelope SHALL receive a monotonic run sequence and a single consumer SHALL persist accepted envelopes in sequence order.

#### Scenario: Slow durable writer does not block an unsaturated producer
- **WHEN** queue capacity is available and the durable writer is deliberately delayed
- **THEN** traversal-side submission completes after immutable queue acceptance while `FlushAsync` remains incomplete until persistence finishes

#### Scenario: Queue reaches capacity
- **WHEN** producers fill the bounded queue faster than the writer can persist it
- **THEN** subsequent submission applies backpressure until capacity is available and no accepted envelope is dropped or reordered

### Requirement: Submitted payloads are immutable and redacted

The pipeline SHALL copy or take exclusive ownership of submitted bytes and SHALL prevent later producer mutation from changing persisted evidence. Persisted text, JSON, trace metadata, exceptions, and issues SHALL pass through the existing redaction policy.

#### Scenario: Producer mutates a screenshot buffer after submission
- **WHEN** a producer changes its original byte array after the evidence envelope is accepted
- **THEN** the persisted screenshot equals the immutable submitted payload and not the later mutation

#### Scenario: Queued metadata contains a configured secret
- **WHEN** trace or issue metadata contains a configured secret substring
- **THEN** the durable asset contains the redaction marker and not the secret

### Requirement: Trace remains immediately queryable and becomes durable asynchronously

For every accepted trace record, the Host SHALL update the run's in-memory trace read model before asynchronously submitting the corresponding durable JSONL record. `VerificationAnalyzer` SHALL observe the immediate read model, while terminal success SHALL require all durable trace records and session metadata to be flushed.

#### Scenario: Analyzer runs while durable trace is queued
- **WHEN** a trace record has updated memory but its JSONL write is still pending
- **THEN** `VerificationAnalyzer` can query the record and final success remains blocked until durable flush completes

#### Scenario: Run completes normally
- **WHEN** engine execution and verification finish without writer failure
- **THEN** `session.json` and ordered `trace.jsonl` exist at the result's referenced path before `result.json` reports success

### Requirement: Safety and terminal result retain durability barriers

The scenario plan and safety allow/deny evidence required before a real device action SHALL be durably written before the action executes. Run creation inputs SHALL be durable before traversal starts. The authoritative terminal `result.json` SHALL be written synchronously only after accepted asynchronous evidence drains successfully.

#### Scenario: Safe action is ready while its decision is not durable
- **WHEN** a click has an allow decision but the required safety evidence write has not completed
- **THEN** the underlying ADB action executor is not invoked

#### Scenario: Terminal success waits for evidence
- **WHEN** device verification succeeds while queued screenshot or trace writes remain
- **THEN** `result.json` is not finalized as success until the queue drains and referenced evidence is verified durable

### Requirement: Writer failure is sticky and prevents successful completion

The first asynchronous writer failure SHALL fault the run evidence pipeline. Later submissions and flush SHALL surface the same classified failure. The Host MUST NOT report success after any accepted evidence fails to persist, and SHALL attempt a redacted synchronous fallback result describing a trace/reporting failure.

#### Scenario: Trace append fails after target navigation succeeds
- **WHEN** durable JSONL append fails after the device reaches the requested page
- **THEN** the run reports trace/reporting failure rather than success and retains all evidence written before the failure

#### Scenario: Failure occurs with queued work remaining
- **WHEN** the writer faults while later envelopes are queued
- **THEN** flush completes with the sticky fault, no queued evidence is silently reported durable, and the worker terminates

### Requirement: Cancellation drains accepted evidence and leaves no orphan writer

On cancellation, the Host SHALL stop accepting new scenario evidence, drain already accepted envelopes using a bounded shutdown token, await the writer task, and then publish a cancelled or reporting-failure result. A run MUST NOT leave a background evidence writer active after command completion.

#### Scenario: Cancellation occurs with queued screenshots
- **WHEN** cancellation is requested while accepted screenshots remain queued and the writer is healthy
- **THEN** the Host drains them within the shutdown budget, awaits the worker, and writes a cancelled result referencing only durable evidence

#### Scenario: Cancellation drain exceeds its budget
- **WHEN** accepted evidence cannot drain before the shutdown deadline
- **THEN** the result records incomplete/reporting evidence and the run MUST NOT claim a clean successful flush

### Requirement: Pipeline telemetry exposes pressure and flush cost

The Host SHALL record accepted envelope count, maximum queue depth, backpressure count, writer failure status, and terminal flush duration in run diagnostics without changing locked Core trace enums.

#### Scenario: Emulator run uses asynchronous evidence
- **WHEN** `scenario-locate` completes through the asynchronous pipeline
- **THEN** its retained diagnostics show queue and flush telemetry, zero lost envelopes, and durable asset counts consistent with the result and trace
