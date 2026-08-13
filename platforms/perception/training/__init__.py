"""UniClaw Perception Training/Dataset Reproducibility Foundation (Phase 4).

Provides reproducible provenance for future model artifacts:

  Asset → Annotation → DatasetVersion → TrainingConfig → TrainingRun
  → Checkpoint → ModelArtifact → Candidate → (existing Evaluation workflow)

Constraints:
  • REPRODUCIBLE_PROVENANCE target — not bitwise reproducibility.
  • No release authority: training metrics never authorize promotion.
  • Production inference never imports this package.
  • ModelPrediction != AcceptedAnnotation.
"""
__version__ = "1.0.0"
TRAINING_SCHEMA_VERSION = "1.0"
