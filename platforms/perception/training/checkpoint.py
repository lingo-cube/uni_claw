"""Checkpoint + ModelArtifact foundation (P4-T5).

Terminology: a checkpoint is MATERIALIZED into a ModelArtifact — the word
PROMOTE is reserved for the release lifecycle.

checkpointName = training role ("best"/"last"/"epoch_N") — never identity.
checkpointId  = SHA-256 of exact checkpoint bytes.
modelId       = full SHA-256 of exact artifact bytes (FROZEN).
modelName     = stable family identity (FROZEN) — never derived from
                filename / checkpoint role / hash.
"""
from __future__ import annotations

import json
import os
import shutil
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from evaluation.identity import sha256_file
from persistence import write_once_json
from . import TRAINING_SCHEMA_VERSION


@dataclass(frozen=True)
class Checkpoint:
    checkpoint_name: str          # "best" | "last" | "epoch_N" — role only
    source_path: str              # where the bytes currently live
    selection_metric: str | None = None   # e.g. "ultralytics best-val mAP50-95"
    note: str = ""

    @property
    def checkpoint_id(self) -> str:
        return f"sha256:{sha256_file(self.source_path)}"


@dataclass(frozen=True)
class ModelArtifact:
    """Immutable materialized model artifact.

    model_id = full SHA-256 of exact bytes (frozen). Copying/renaming the
    artifact never changes model_id; changing bytes does.
    """
    model_name: str               # stable family identity
    model_id: str                 # full 64-hex SHA-256
    source_training_run_id: str
    source_checkpoint_id: str
    source_checkpoint_name: str
    artifact_path: str            # canonical storage location (reference)
    note: str = ""

    def to_json(self) -> dict[str, Any]:
        return {
            "schema": TRAINING_SCHEMA_VERSION,
            "modelName": self.model_name,
            "modelId": self.model_id,
            "sourceTrainingRunId": self.source_training_run_id,
            "sourceCheckpointId": self.source_checkpoint_id,
            "sourceCheckpointName": self.source_checkpoint_name,
            "artifactPath": self.artifact_path,
            "note": self.note,
        }


def materialize_model_artifact(
    checkpoint: Checkpoint,
    training_run_id: str,
    model_name: str,
    target_dir: str | Path,
) -> ModelArtifact:
    """MATERIALIZE (not promote): copy checkpoint bytes into canonical model
    storage, identity from bytes."""
    target = Path(target_dir)
    target.mkdir(parents=True, exist_ok=True)
    model_id = sha256_file(checkpoint.source_path)
    dest = target / f"{model_id}.pt"
    if dest.exists():
        if sha256_file(dest) != model_id:
            raise ValueError(
                f"model content collision at {dest}: existing bytes do not match {model_id}")
    else:
        # Copy to a private sibling then link atomically, never replacing a
        # concurrent materialization of the same content identity.
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{model_id}.", suffix=".tmp", dir=target)
        temporary = Path(temporary_name)
        try:
            with os.fdopen(descriptor, "wb") as stream:
                with Path(checkpoint.source_path).open("rb") as source:
                    shutil.copyfileobj(source, stream)
                stream.flush()
                os.fsync(stream.fileno())
            if sha256_file(temporary) != model_id:
                raise ValueError("checkpoint bytes changed during materialization")
            try:
                os.link(temporary, dest)
            except FileExistsError:
                if sha256_file(dest) != model_id:
                    raise ValueError(
                        f"model content collision at {dest}: existing bytes do not match {model_id}")
        finally:
            temporary.unlink(missing_ok=True)
    return ModelArtifact(
        model_name=model_name,
        model_id=model_id,
        source_training_run_id=training_run_id,
        source_checkpoint_id=checkpoint.checkpoint_id,
        source_checkpoint_name=checkpoint.checkpoint_name,
        artifact_path=str(dest),
        note=checkpoint.note,
    )


def save_model_artifact(artifact: ModelArtifact, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{artifact.model_id}.json"
    return write_once_json(path, artifact.to_json())
