"""UniClaw Perception Evaluation Foundation (Phase 4, first vertical slice).

Establishes the reusable evaluation workflow:

  Evidence → Asset Manifest → Classification → GroundTruth → EvaluationSuite
  → EvaluationRun → Fresh Prediction → Matching → Metrics → Scorecard
  → Coverage / Evidence Sufficiency → Immutable Baseline Report

Constraints (frozen by admission):
  • Evaluation produces evidence only — no promotion, no ACTIVE mutation,
    no Runtime semantic authority.
  • Numeric thresholds NOT_FROZEN; config identity is
    LEGACY_PARTIAL_CONFIG_IDENTITY until Phase 4 canonical configId exists.
  • No weights, no training, no L3/L4 execution in this slice.
"""
__version__ = "1.0.0"

EVALUATION_SCHEMA_VERSION = "1.0"
