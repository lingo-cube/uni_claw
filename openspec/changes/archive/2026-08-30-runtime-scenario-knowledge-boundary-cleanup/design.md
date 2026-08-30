## Context

See `proposal.md` for motivation. Three generic Semantic Perception production
types contain `DeveloperOptions` only in XML documentation examples. The
existing Runtime source guard scans comments as well as executable text and
correctly treats those literals as scenario-knowledge leakage.

The affected types are otherwise generic and already preserve the required
evidence boundary: semantic output is advisory evidence, Runtime owns its
validation and reconciliation, and Agent owns execution and completion.

## Goals / Non-Goals

**Goals:**

- Restore scenario-neutral Runtime production source.
- Preserve the exact public and behavioral shape of `SemanticEvidence`,
  `SemanticCandidate`, and `SemanticCorpus`.
- Classify all matching Runtime literals and stop on executable scenario logic.

**Non-Goals:**

- Move scenario knowledge to another Runtime namespace or Environment adapter.
- Rename or alias the concrete scenario.
- Move generic Corpus types, redesign Semantic Perception, or change evidence
  ownership.
- Modify Agent, Traversal, FSM, GoalEvidence, Recovery, or Strategy behavior.

## Decisions

### 1. Remove the examples instead of replacing or relocating them

The three concrete examples add no contract information. Their documentation
will describe only structural semantics: an opaque candidate identity or corpus
identity. No substitute screen, application, route, or scenario label will be
introduced.

Alternative considered: move the examples to a Runtime capability binding or
adapter. Rejected because this launders scenario knowledge without changing its
ownership.

### 2. Preserve every model and API shape

Only XML documentation lines are edited. Constructors, members, namespaces,
assemblies, and runtime behavior remain byte-for-byte equivalent at the C# API
level. Test fixtures may continue to use concrete scenarios under `tests/`.

Alternative considered: move Corpus infrastructure out of Runtime in this
change. Rejected because that is a separate dependency/API decision and is not
required to repair the known leakage.

### 3. Use the existing guard as the mechanical authority proof

The existing `RuntimeSource_HasNoScenarioSpecificScrollLogic` test already
scans all `src/UniClaw.Runtime/**/*.cs`, including documentation. The audit also
classifies every discovered scenario literal before editing; any executable
dependency triggers the user-defined stop condition.

## Risks / Trade-offs

- [A concrete example is replaced by a disguised scenario] → Remove it without
  replacement and audit the resulting source.
- [A real executable dependency is mistaken for documentation leakage] → Stop
  rather than edit when a literal participates in runtime data or branching.
- [Unrelated dirty-worktree changes contaminate validation] → Change only the
  three approved files and report unrelated failures separately.

## Migration Plan

1. Audit Runtime production literals and classify them.
2. Remove only the three approved XML examples.
3. Run the scenario guard and the approved regression matrix.
4. Roll back by restoring only those documentation lines; no data or API
   migration exists.
