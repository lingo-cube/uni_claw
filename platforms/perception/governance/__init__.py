"""UniClaw Perception Deployment Identity Governance (Phase 4).

Canonical identity axes:

  SchemaVersion + ModelId + ConfigId + PipelineRevision
  = PerceptionDeploymentIdentity

No material perception behavior may change while all four canonical
identity axes remain unchanged.

Frozen semantics:
  • serviceVersion = metadata only, NO behavior authority (IDR-01)
  • RELEASE UNIT = PerceptionDeploymentIdentity (never ModelArtifact)
  • ModelArtifact carries immutable facts only — no ACTIVE/VALIDATED state
  • configHash = legacy compatibility identity only
  • ModelVersion remains DEFERRED
  • No ReleasePolicy / promotion / activation in this foundation
"""
__version__ = "1.0.0"
GOVERNANCE_SCHEMA_VERSION = "1.0"
