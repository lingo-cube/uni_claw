"""Versioned EvaluationSuite — membership references AssetId, never copies bytes.

Frozen by Phase 4 gate:
  • Suite membership = asset identity references + role binding.
  • Changing membership creates a NEW suite version (content-addressed id).
  • Historical membership is never mutated in place (PF2).
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Any

from .asset import CorpusRole, PerceptionTask
from .identity import canonical_hash, canonical_json
from persistence import write_once_json


@dataclass(frozen=True)
class SuiteMembership:
    asset_id: str                  # content-addressed reference
    roles: tuple[CorpusRole, ...]


@dataclass(frozen=True)
class EvaluationSuite:
    suite_schema_version: str
    backend: str = "L2_RECORDED_IMAGE_INFERENCE"
    evaluator_revision: str = "evaluator-v1"
    required_tasks: tuple[PerceptionTask, ...] = ()
    members: tuple[SuiteMembership, ...] = ()
    description: str = ""

    @property
    def suite_id(self) -> str:
        """Content-addressed suite identity: any membership change → new id."""
        return f"suite:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "suiteSchemaVersion": self.suite_schema_version,
            "backend": self.backend,
            "evaluatorRevision": self.evaluator_revision,
            "requiredTasks": sorted(t.value for t in self.required_tasks),
            "members": sorted(
                (
                    {
                        "assetId": m.asset_id,
                        "roles": sorted(r.value for r in m.roles),
                    }
                    for m in self.members
                ),
                key=lambda x: x["assetId"],
            ),
            "description": self.description,
        }

    def with_members(self, members: tuple[SuiteMembership, ...],
                     description: str | None = None) -> "EvaluationSuite":
        """PF2: new membership → new suite version (never mutation)."""
        return EvaluationSuite(
            suite_schema_version=self.suite_schema_version,
            backend=self.backend,
            evaluator_revision=self.evaluator_revision,
            required_tasks=self.required_tasks,
            members=members,
            description=description if description is not None else self.description,
        )

    def to_json(self) -> dict[str, Any]:
        return {
            "suiteSchemaVersion": self.suite_schema_version,
            "suiteId": self.suite_id,
            "backend": self.backend,
            "evaluatorRevision": self.evaluator_revision,
            "requiredTasks": [t.value for t in self.required_tasks],
            "members": [
                {"assetId": m.asset_id, "roles": [r.value for r in m.roles]}
                for m in self.members
            ],
            "description": self.description,
        }

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "EvaluationSuite":
        return cls(
            suite_schema_version=d["suiteSchemaVersion"],
            backend=d.get("backend", "L2_RECORDED_IMAGE_INFERENCE"),
            evaluator_revision=d.get("evaluatorRevision", "evaluator-v1"),
            required_tasks=tuple(PerceptionTask(t) for t in d.get("requiredTasks", [])),
            members=tuple(
                SuiteMembership(
                    asset_id=m["assetId"],
                    roles=tuple(CorpusRole(r) for r in m.get("roles", [])),
                )
                for m in d.get("members", [])
            ),
            description=d.get("description", ""),
        )


def save_suite(suite: EvaluationSuite, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{suite.suite_id.replace('suite:', '')}.json"
    return write_once_json(path, suite.to_json())


def load_suite(path: str | Path) -> EvaluationSuite:
    return EvaluationSuite.from_json(json.loads(Path(path).read_text(encoding="utf-8")))
