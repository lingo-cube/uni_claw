"""Performance result pipeline (R19/I29-I31).

Same evaluation identity model as quality evaluation — no disconnected
benchmark architecture. Percentiles guarded by sample count (B13/B14):
  • raw samples always recorded
  • p50/p95 reported when n >= 10
  • p99 reported when n >= 100
"""
from __future__ import annotations

import statistics
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .deployment import DeploymentSnapshot
from .identity import canonical_hash


MIN_SAMPLES_P50_P95 = 10
MIN_SAMPLES_P99 = 100


@dataclass(frozen=True)
class PerformanceResult:
    result_id: str
    run_id: str
    deployment: DeploymentSnapshot
    asset_id: str | None
    environment: dict[str, Any]
    evaluator_revision: str
    samples_ms: tuple[float, ...]
    warm: bool
    input_resolution: str = ""

    @property
    def sample_count(self) -> int:
        return len(self.samples_ms)

    def _percentile(self, pct: float) -> float:
        data = sorted(self.samples_ms)
        k = (len(data) - 1) * pct
        lo = int(k)
        hi = min(lo + 1, len(data) - 1)
        return data[lo] + (data[hi] - data[lo]) * (k - lo)

    def to_json(self) -> dict[str, Any]:
        out: dict[str, Any] = {
            "resultId": self.result_id,
            "runId": self.run_id,
            "deployment": self.deployment.to_json(),
            "assetId": self.asset_id,
            "environment": self.environment,
            "evaluatorRevision": self.evaluator_revision,
            "samplesMs": [round(s, 2) for s in self.samples_ms],
            "sampleCount": self.sample_count,
            "warm": self.warm,
            "inputResolution": self.input_resolution,
            "summary": {
                "medianMs": round(statistics.median(self.samples_ms), 2),
                "meanMs": round(statistics.mean(self.samples_ms), 2),
                "minMs": round(min(self.samples_ms), 2),
                "maxMs": round(max(self.samples_ms), 2),
            },
        }
        # B14: percentiles only with sufficient samples
        if self.sample_count >= MIN_SAMPLES_P50_P95:
            out["summary"]["p50Ms"] = round(self._percentile(0.50), 2)
            out["summary"]["p95Ms"] = round(self._percentile(0.95), 2)
        if self.sample_count >= MIN_SAMPLES_P99:
            out["summary"]["p99Ms"] = round(self._percentile(0.99), 2)
        return out

    @classmethod
    def create(cls, samples_ms: list[float], *, run_id: str,
               deployment: DeploymentSnapshot, asset_id: str | None,
               environment: dict[str, Any], evaluator_revision: str,
               warm: bool, input_resolution: str = "") -> "PerformanceResult":
        identity_inputs = {
            "runId": run_id,
            "deployment": deployment.to_json(),
            "assetId": asset_id,
            "environment": environment,
            "evaluatorRevision": evaluator_revision,
            "samples": [round(s, 3) for s in samples_ms],
        }
        return cls(
            result_id=f"perf:{canonical_hash(identity_inputs)}",
            run_id=run_id,
            deployment=deployment,
            asset_id=asset_id,
            environment=environment,
            evaluator_revision=evaluator_revision,
            samples_ms=tuple(samples_ms),
            warm=warm,
            input_resolution=input_resolution,
        )


def capture_environment(platform_module=None, python_version: str = "",
                        worker_topology: str = "single-process/single-worker",
                        model_id: str = "",
                        input_resolution: str = "",
                        warm: bool = True) -> dict[str, Any]:
    """Truthfully available environment profile (I31)."""
    import platform
    import sys
    env: dict[str, Any] = {
        "os": platform.system(),
        "cpuArch": platform.machine(),
        "pythonVersion": python_version or platform.python_version(),
        "workerTopology": worker_topology,
        "modelId": model_id,
        "inputResolution": input_resolution,
        "warm": warm,
    }
    # dependency versions, truthfully recorded where importable
    try:
        from importlib.metadata import version as _v
        env["ultralyticsVersion"] = _v("ultralytics")
    except Exception:
        env["ultralyticsVersion"] = "unknown"
    try:
        from importlib.metadata import version as _v
        env["rapidocrVersion"] = _v("rapidocr-onnxruntime")
    except Exception:
        env["rapidocrVersion"] = "unknown"
    return env
