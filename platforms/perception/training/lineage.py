"""Lineage report (P4-T9).

Explicit partial graph of immutable identities:
  Asset → Annotation → DatasetVersion → TrainingConfig → TrainingRun
  → Checkpoint → ModelArtifact → Candidate → EvaluationRun

Missing facts remain missing. Never infer from filename/directory/mtime.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from persistence import write_once_json


@dataclass(frozen=True)
class LineageNode:
    kind: str
    identity: str
    facts: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class LineageEdge:
    source_kind: str
    source_id: str
    target_kind: str
    target_id: str


@dataclass(frozen=True)
class LineageReport:
    nodes: tuple[LineageNode, ...]
    edges: tuple[LineageEdge, ...]
    missing: tuple[str, ...] = ()   # honest gaps — e.g. "legacy ACTIVE has no TrainingRun"

    @property
    def lineage_id(self) -> str:
        return f"lineage:{canonical_hash(self.to_json())}"

    def to_json(self) -> dict[str, Any]:
        return {
            "nodes": [
                {"kind": n.kind, "identity": n.identity, "facts": n.facts}
                for n in self.nodes
            ],
            "edges": [
                {"source": f"{e.source_kind}:{e.source_id}",
                 "target": f"{e.target_kind}:{e.target_id}"}
                for e in self.edges
            ],
            "missing": list(self.missing),
        }


def save_lineage(report: LineageReport, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{report.lineage_id.replace('lineage:', '')}.json"
    return write_once_json(path, report.to_json())
