## ADDED Requirements

### Requirement: DependencyDirectionGuardTests enforce no Simulation reference in engine layer (C-5)

`ArchitectureGuardTests.cs` SHALL contain a guard test that scans all production `.cs` files under `StateMachine/`, `Traversal/`, and `Domain/` and asserts none reference the `UniClaw.Core.Simulation` namespace — no `using UniClaw.Core.Simulation*` directive and no `Simulation.*` type token. This strengthens C-5 (dependency direction) by forbidding the engine layer from depending on the Simulation/test-double layer, eliminating the concrete-mock downcasts (`is ScrollableMockVisionService` / `is ScrollableMockActionExecutor`) that previously coupled engine scroll handling to mocks. Test files (`tests/`) and the `Simulation/` layer itself are out of scope.

#### Scenario: Engine production file does not import Simulation
- **WHEN** a production `.cs` file under `StateMachine/`, `Traversal/`, or `Domain/` is scanned
- **THEN** it MUST NOT contain `using UniClaw.Core.Simulation` (any sub-namespace) or any `Simulation.` type reference

#### Scenario: Guard fails on a concrete-mock downcast regression
- **WHEN** a production engine file reintroduces `is ScrollableMockVisionService` or `using UniClaw.Core.Simulation.Scroll`
- **THEN** the guard test fails (CI-blocking)

#### Scenario: Test files are not flagged
- **WHEN** a file under `tests/` references `UniClaw.Core.Simulation`
- **THEN** it is NOT flagged (test layer may construct mocks)
