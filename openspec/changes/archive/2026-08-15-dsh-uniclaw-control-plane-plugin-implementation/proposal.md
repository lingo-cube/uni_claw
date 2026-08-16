# Proposal: dsh-uniclaw-control-plane-plugin-implementation

## Problem

The control-plane protocol baseline between DeepSeek Harness (DSH) and UniClaw is
frozen (`openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-protocol-baseline/`,
graduation record `docs/decisions/dsh-uniclaw-control-plane-protocol-baseline-graduation.md`).
The baseline deliberately deferred the concrete plugin, transport, and command
surface (TRANSPORT_DEFERRED, plugin module shape deferred). There is no running
DSH plugin that can reach the DriverHost read-only observability surface, so the
control plane exists only on paper.

## What this change does

Implements the minimum real chain, one bounded vertical slice:

1. **A DSH plugin module** (`dsh-plugin-uniclaw/`, Node, Cordis plugin) that mounts
   into the pinned DSH baseline, registers a read-only `uniclaw` service, registers
   deterministic zero-model commands, and subscribes to the DSH-native `session/event`
   fanout.
2. **A concrete DriverHost connection boundary** — exactly ONE local transport:
   loopback TCP, newline-delimited JSON-RPC, server owned by the DriverHost process,
   client owned by the plugin (Node `node:net` ↔ .NET `System.Net.Sockets`).
3. **Read-only consumption of the frozen Kernel surfaces**: `RunSnapshot`
   (classification-preserving), `RuntimeEvent` pages (run-scoped cursors, stable
   `EventId`, `Sequence` ≠ observation progress), and `EvidenceRef` inspection
   (logical locator only).
4. **A deterministic human control seam**: `uniclaw-inspect-run`, `uniclaw-inspect-trap`,
   `uniclaw-evidence-open`, `uniclaw-runs-list` implemented as zero-model commands; `start`,
   `pause`, `resume`, `stop`, `abort` explicitly
   `DEFERRED_NO_KERNEL_CONTROL_BUYER` because the Kernel public surface has no
   truthful control buyer for them.
5. **Failure/reconnect semantics**: typed protocol errors, bounded reconnect with
   fresh state fetches, no state fabrication.
6. **Architecture guards + e2e deterministic integration tests** on both planes.

No cognition, no shadow/advisory/blocking layers, no new Runtime semantic
emitters, no Runtime agent refactor, no semantic model change, no parallel
protocol, no generic transport/provider framework. The pinned DSH checkout
(commit `47f943859bef60e4160492346772ded9b24f765a`, `0.1.0-rc.5`) is read-only.

## Non-goals (explicitly out of scope)

- DSH↔UniClaw shadow cognition / advisory / blocking cognition.
- New Runtime semantic emitters or Runtime semantic model changes.
- Generic transport abstractions, multi-transport frameworks, remote-first transport.
- Persisting custom durable session events (F18–F21 are
  `NOT_APPLICABLE_NO_CUSTOM_DURABLE_EVENTS`).
- Persistent (lazy, disk-backed) evidence resolution — stays DEFERRED.
- Control commands with no truthful Kernel buyer (`start`/`pause`/`resume`/`stop`/`abort`).

## Required output

`PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PLUGIN_IMPLEMENTATION_RESULT` with
Status `IMPLEMENTED_READY_FOR_GRADUATION_REVIEW`, claiming only
`DSH_UNICLAW_CONTROL_PLANE_PLUGIN_IMPLEMENTED` (integration maturity requires the
separate graduation review gate). OpenSpec change is NOT archived during Apply.

## Authority

`PROJECT_LEADER_CREATE_AND_APPLY_DSH_UNICLAW_CONTROL_PLANE_PLUGIN_IMPLEMENTATION`,
MODE `BOUNDED_VERTICAL_SLICE_IMPLEMENTATION`, IMPLEMENTATION
`ALLOWED_WITHIN_FROZEN_PROTOCOL_BASELINE`.
