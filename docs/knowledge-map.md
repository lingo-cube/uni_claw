# Knowledge Map

> DocumentType: `KNOWLEDGE_ROUTING_INDEX`
> Authority: `NONE`
> Scope: retrieval only

This index routes retrieval to existing sources. It does not replace a source,
establish authority, classify lifecycle, or state project facts.

## Knowledge Layer Map

| Knowledge Type | Location | Authority | Loading Level | Retrieval Method |
|---|---|---|---|---|
| Architecture Authority | Architecture Constitution (logical authority layer; no file), [AGENTS.md](../AGENTS.md) repository map/entry, [Architecture index](architecture/README.md) architecture entry, [Architecture v1](architecture/uniagent-architecture-v1-core-development-guide.md) baseline authority | Source-defined; this index remains `NONE` | L0–L1 | Default authority entry; load the baseline relevant to the task. |
| Current State | [Current architecture state](architecture/current-architecture-state.md), [latest snapshot](snapshots/latest.md) | `NONE` | L3–L4 | Default projection retrieval; follow cited sources when evidence is needed. |
| Active Work | [Current gates](work/active/current-gates.md) and task-relevant active OpenSpec artifacts | Projection: `NONE`; source authority remains at source | L4 | Load the current-gates projection, then only the active OpenSpec relevant to the task. |
| Historical Evidence | [Decision registry](decisions/index.md), [failure index](failures/index.md), [OpenSpec archive](../openspec/changes/archive/) | `NONE`; evidence only | L5 | Retrieve on demand by decision, capability, scenario, gate, failure, or successor. |
| Reusable Capability | [Process skills](../.ai/skills/) | `NONE` | Task-triggered | Load only the process skill that matches the task; skills do not establish authority. |
| Context Routing | [Context Loading Guide](context-loading-guide.md) | `NONE` | L0–L5 | Start here to select the minimum task-relevant sources. |

## Retrieval Boundary

Default loading uses the applicable current authority, projection, and active
work sources. Historical evidence is retrieved only when the task requires
traceability, evidence, a predecessor, or a failure record.
