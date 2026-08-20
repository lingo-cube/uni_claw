# Knowledge Maintenance Policy

DocumentType: KNOWLEDGE_MAINTENANCE_GUIDE  
Authority: NONE  
Scope: Knowledge-routing guidance only. This guide neither authorizes nor prohibits Architecture Decisions; existing authority and process remain unchanged.

## Purpose

Keep current work understandable with a minimal default context while preserving complete historical traceability.

## When to Create a Decision

Create a Decision only when the project's existing authorized process has made a durable choice requiring long-term traceability and the choice concerns Architecture, Authority, Ownership, Contract, Lifecycle, or a Gate conclusion.

This guide does not make, approve, or classify Decisions. Follow the existing authority and process for every Decision.

## When to Use an Ordinary Record

Use an ordinary record for observations, investigations, audits, implementation notes, temporary status, and similar material that does not establish authority or change lifecycle.

## When to Use a Snapshot

Use a Snapshot only for source-referenced current project state, blockers, and next actions. A Snapshot must not carry historical narrative or create new authority.

## When to Use a Projection

Use a Projection only to express a source-linked current view. A Projection must declare `Authority: NONE` and must not create independent facts, `SHALL` statements, or lifecycle conclusions.

## When to Use a Skill

Use a Skill for a stable, reusable, cross-project process that contains no project state, architecture conclusion, lifecycle determination, or authority.

## When to Route to an Existing Architecture Gate

If proposed knowledge maintenance would establish or change Architecture Authority, ownership, a normative component boundary or contract, or a gate or lifecycle conclusion, do not perform it as ordinary documentation maintenance. Stop and route the matter to the project's existing authorized Architecture Gate or process.

This guide does not determine any Gate result or expand who may make the decision.

## Safety and Maintenance Rules

- Preserve historical material: do not delete, merge, or rewrite it.
- Apply `Current State > Historical Process` only as a context-loading and routing principle; it does not change facts or authority.
- Maintain traceability from current projections to their source evidence.
- Keep the default context minimal; retrieve historical evidence only when the task requires it.
- Ensure each maintenance change has a practical rollback path.
- If the correct placement is unclear, or the work could affect authority or lifecycle, stop and output `ARCHITECTURE_DECISION_REQUIRED`.
