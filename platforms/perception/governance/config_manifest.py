"""PerceptionConfigManifest + canonical configId (P4-D2).

Resolved EFFECTIVE evidence-affecting configuration only.
Identity is behavior-oriented, not source-oriented: env override of 720
that matches the default of 720 yields the SAME identity as the default.
Operational-only settings (socket, restarts, timeouts, workers, OMP)
never enter the manifest.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from evaluation.identity import canonical_hash
from persistence import write_once_json

MANIFEST_SCHEMA = "uniclaw.perceptionConfig.v1"


class ConfigCompleteness(str, Enum):
    COMPLETE = "COMPLETE"
    PARTIAL = "PARTIAL"
    UNRESOLVED = "UNRESOLVED"


@dataclass(frozen=True)
class PerceptionConfigManifest:
    preprocessing: dict[str, Any]        # maxWidth, cropTopRatio, cropBottomRatio
    yolo: dict[str, Any]                # confidence
    ocr: dict[str, Any]                 # backend, mode, textScore, language, roiPadding
    scroll: dict[str, Any]              # edgeThreshold
    referenced_artifacts: dict[str, Any] = field(default_factory=dict)
    # referenced_artifacts: {labelMapping: {contentHash, evidenceRelevant}}
    # — owned transitively by ConfigId, never a second identity axis (IDR-03)
    completeness: ConfigCompleteness = ConfigCompleteness.COMPLETE
    unresolved: tuple[str, ...] = ()    # names of unresolved material settings
    #: Rule-set content axis (S1C): {contentHash, evidenceRelevant} — the
    #: content hash of the ACTIVE rule set carried by the config (the stable
    #: default-root marker when absent).  Owned by ConfigId (IDR-03 pattern,
    #: never a second deployment identity axis): a rule-set change changes
    #: configId → deploymentId → receipt.  Empty dict = a manifest whose
    #: serialized form predates the axis (parses as before).
    ruleset: dict[str, Any] = field(default_factory=dict)

    @property
    def config_id(self) -> str:
        """canonical configId = SHA-256(canonical identity content).

        Display metadata excluded; sorted serialization; path-independent.
        """
        return f"config:{canonical_hash(self._identity_content())}"

    def _identity_content(self) -> dict[str, Any]:
        return {
            "schema": MANIFEST_SCHEMA,
            "preprocessing": self.preprocessing,
            "yolo": self.yolo,
            "ocr": self.ocr,
            "scroll": self.scroll,
            "referencedArtifacts": self.referenced_artifacts,
            "completeness": self.completeness.value,
            "ruleset": self.ruleset,
        }
        # NOTE: `unresolved` is diagnostics, NOT identity content — naming a
        # gap does not change effective behavior.

    def to_json(self) -> dict[str, Any]:
        d = self._identity_content()
        d["configId"] = self.config_id
        d["unresolved"] = list(self.unresolved)
        return d

    @classmethod
    def from_json(cls, d: dict[str, Any]) -> "PerceptionConfigManifest":
        return cls(
            preprocessing=dict(d.get("preprocessing", {})),
            yolo=dict(d.get("yolo", {})),
            ocr=dict(d.get("ocr", {})),
            scroll=dict(d.get("scroll", {})),
            referenced_artifacts=dict(d.get("referencedArtifacts", {})),
            completeness=ConfigCompleteness(d.get("completeness", "COMPLETE")),
            unresolved=tuple(d.get("unresolved", [])),
            ruleset=dict(d.get("ruleset", {})),
        )


def ruleset_content_hash(ruleset_content: str | None) -> str:
    """Stable content identity of the ACTIVE rule-set bytes.

    Present → ``sha256:<hex>`` of the serialized rule-set text; absent → the
    stable default-root marker (the same sentinel the config-hash axis binds
    for the default semantics).  A serialized rule-set document can never
    equal the marker, so absent and present never collide.
    """
    from evaluation.identity import sha256_bytes
    from uniclaw_perception.config import DEFAULT_RULESET_MARKER
    if ruleset_content is None:
        return DEFAULT_RULESET_MARKER
    return f"sha256:{sha256_bytes(ruleset_content.encode('utf-8'))}"


def build_from_perception_config(cfg: Any,
                                 label_mapping_path: str | Path | None = None,
                                 label_mapping_content_hash: str | None = None,
                                 unresolved: tuple[str, ...] = ()) -> PerceptionConfigManifest:
    """Build the manifest from the resolved in-memory PerceptionConfig.

    cfg: uniclaw_perception.config.PerceptionConfig (post-load snapshot).
    Resolved effective values only — env overrides already applied.

    label_mapping_content_hash: the hash of the EXACT bytes loaded into the
    process (cfg.config_hash). Prefer passing it — the manifest must never
    re-read the file and risk describing bytes the process did not load
    (G10 closure). Only fall back to reading the path for dev tooling.
    """
    from evaluation.identity import sha256_file
    refs: dict[str, Any] = {}
    if label_mapping_content_hash is not None:
        refs["labelMapping"] = {
            "contentHash": label_mapping_content_hash,
            "evidenceRelevant": ["detection.confidence", "spatial.roiPadding",
                                 "spatial.edgeThreshold", "spatial.preprocessing"],
        }
    elif label_mapping_path is not None and Path(label_mapping_path).exists():
        refs["labelMapping"] = {
            "contentHash": f"sha256:{sha256_file(label_mapping_path)}",
            "evidenceRelevant": ["detection.confidence", "spatial.roiPadding",
                                 "spatial.edgeThreshold", "spatial.preprocessing"],
        }
    completeness = (ConfigCompleteness.COMPLETE if not unresolved
                    else ConfigCompleteness.PARTIAL)
    return PerceptionConfigManifest(
        preprocessing={
            "maxWidth": cfg.max_width,
            "cropTopRatio": round(float(cfg.crop_top), 6),
            "cropBottomRatio": round(float(cfg.crop_bottom), 6),
        },
        yolo={"confidence": round(float(cfg.detection_confidence), 6)},
        ocr={
            "backend": cfg.ocr_backend,
            "mode": cfg.ocr_mode,
            "textScore": round(float(cfg.ocr_text_score), 6),
            "language": cfg.ocr_lang,
            "roiPadding": dict(cfg.spatial.get("roiPadding", {})),
        },
        scroll={"edgeThreshold": round(float(cfg.spatial.get("edgeThreshold", 0.92)), 6)},
        referenced_artifacts=refs,
        # S1C: the ACTIVE rule-set axis — content hash of the config-carried
        # rule set, or the stable default-root marker when absent.  Present
        # AND absent are both identity content (any byte change, or presence
        # vs absence, ⇒ a different configId).
        ruleset={
            "contentHash": ruleset_content_hash(
                getattr(cfg, "ruleset_content", None)),
            "evidenceRelevant": True,
        },
        completeness=completeness,
        unresolved=unresolved,
    )


def save_manifest(manifest: PerceptionConfigManifest, out_dir: str | Path) -> Path:
    out = Path(out_dir)
    path = out / f"{manifest.config_id.replace('config:', '')}.json"
    return write_once_json(path, manifest.to_json())
