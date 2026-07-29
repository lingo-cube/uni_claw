## ADDED Requirements

### Requirement: Every real device action passes through one deterministic safety gate

Every candidate click, scroll, back, launch, text input, toggle, long press, or other real device action SHALL be evaluated by the deterministic safety gate immediately before the device action executor. No Host, traversal, recovery, popup, or entry path may send a real action that bypasses this gate.

#### Scenario: Traversal generates a click
- **WHEN** traversal generates a click candidate for a Settings row
- **THEN** the action executor receives it only after an allow decision linked to the same run, step, page fingerprint, and target

#### Scenario: Recovery generates an action
- **WHEN** error recovery or popup handling proposes a back or click action
- **THEN** that candidate is evaluated by the same gate before execution

### Requirement: Deny precedence and default-deny semantics are fixed

The safety gate SHALL evaluate rules in this order: boundary/budget denial, dangerous semantic/text denial, scenario allowlist denial, missing-or-untrusted-target denial, explicit safe-navigation allowance, and default denial. A deny decision MUST NOT be overridden by AI output or by a later allow rule. Unknown actions, targets, pages, or rule results SHALL be denied.

#### Scenario: Target matches both allow and deny information
- **WHEN** a target is a clickable Settings row but also matches a reset or erase deny rule
- **THEN** the deny rule wins and the click is not sent

#### Scenario: Action is unknown
- **WHEN** the candidate action type or target identity is not recognized
- **THEN** the gate returns a default-deny decision with a stable rule ID

### Requirement: V1 Settings policy permits only bounded read-navigation actions

For V1 Settings scenarios, the policy SHALL allow only scenario-listed navigation-row clicks, back navigation, bounded scrolling, and any explicitly required Settings launch/home preparation action. It SHALL deny toggle/state changes, text entry, long press, installation, uninstallation, disablement, clear-data, account or credential removal, reset, factory reset, erase, format, purchase/payment, permission grant, unknown-app installation, and destructive developer options.

#### Scenario: Safe row navigation is allowed
- **WHEN** a known first-level row is classified as navigation, is inside the Settings home boundary, is not dangerous, and click is allowed by the scenario
- **THEN** the gate may allow exactly that click with its target and expected page change

#### Scenario: Toggle is denied
- **WHEN** analysis identifies a switch/toggle even if its label has no dangerous keyword
- **THEN** the gate denies the action because state-changing controls are outside the V1 allowlist

#### Scenario: Destructive text is denied
- **WHEN** the target text or normalized alias matches reset, erase, delete, remove account, uninstall, disable, format, purchase, payment, credential removal, or an equivalent configured dangerous term
- **THEN** the gate denies the action and records the matched rule and normalized target

### Requirement: Safety decisions are explicit and auditable

Every safety evaluation SHALL produce a serializable decision containing policy version, disposition, stable rule ID, human-readable reason, candidate action, normalized target, page/path identity, input confidence/evidence, and run/step correlation. Secrets MUST NOT appear in the decision.

#### Scenario: Dangerous entry is skipped
- **WHEN** the enumerate scenario encounters a denied first-level entry
- **THEN** its safety decision identifies the policy version and deny rule and the scenario result references that decision as skipped evidence

### Requirement: Denied actions have zero device side effects

When an action is denied, the runner SHALL NOT invoke the device action executor for that candidate. It SHALL record a skip/block trace event, capture or reference unchanged page evidence, update scenario accounting, and either continue with another safe item or stop according to scenario policy.

#### Scenario: Denied reset row
- **WHEN** the candidate target is a reset row
- **THEN** no tap is sent at its coordinate, a safety skip is traced, and the entry is counted as discovered-but-skipped

### Requirement: Safety policy and corpus are versioned inputs

The deterministic rule set, action allowlist, dangerous vocabulary, aliases, confidence thresholds, and boundary rules SHALL have an explicit policy version and content hash. Each run SHALL record the exact version/hash used so that a historical decision can be reproduced.

#### Scenario: Policy changes between iterations
- **WHEN** a safety rule file changes after iteration 1 and iteration 2 starts
- **THEN** each iteration records its own policy hash and aggregate reporting identifies the differing policy inputs
