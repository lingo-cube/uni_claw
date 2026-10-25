#!/usr/bin/env python3
"""Verify the sealed slice candidate/evidence; never run devices, models or mutate source."""
import argparse
import hashlib
import json
import tarfile
from pathlib import Path
from xml.etree import ElementTree

HERE = Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def counters(relative):
    root = ElementTree.parse(HERE / relative).getroot()
    node = root.find("{*}ResultSummary/{*}Counters")
    return {key: int(value) for key, value in node.attrib.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-result", action="store_true")
    args = parser.parse_args()
    candidate = json.loads((HERE / "candidate-v6.json").read_text())
    binding = json.loads((HERE / "build-binding-v6.json").read_text())
    checks = {}
    checks["candidate_files"] = all(
        digest(HERE / "candidate-v6" / path) == value
        for path, value in candidate["files_sha256"].items())
    checks["base_archive"] = digest(HERE / binding["base_source_archive"]) == binding["base_source_archive_sha256"]
    mismatches = []
    with tarfile.open(HERE / binding["base_source_archive"]) as archive:
        for path, expected in binding["inputs_sha256"].items():
            if path in binding["overlay_paths"]:
                actual = digest(HERE / binding["overlay_source_root"] / path)
            else:
                actual = hashlib.sha256(archive.extractfile(path).read()).hexdigest()
            if actual != expected:
                mismatches.append(path)
    checks["complete_build_inputs"] = not mismatches
    kernel = counters("pure-verification/kernel-pure-v6.trx")
    host = counters("pure-verification/host-pure-20261025-01.trx")
    checks["kernel25"] = kernel["total"] == kernel["passed"] == 25 and kernel["failed"] == 0
    checks["host12"] = host["total"] == host["passed"] == 12 and host["failed"] == 0
    original = counters("luna-red/baseline-red-v2.trx")
    checks["original_behavior_red"] = original["total"] == original["failed"] == 1
    checks["original_host_red"] = counters("host-red/host-red-20261025-01.trx")["failed"] == 1
    checks["entry_kernel_failures_reproduced"] = counters("baseline-regression/kernel-entry-failures-v1.trx")["failed"] == 3
    checks["entry_overlay_failure_reproduced"] = counters("baseline-regression/overlay-entry-v1.trx")["failed"] == 1
    checks["final_reviews_present"] = all((HERE / ("review-" + axis + "-v6.md")).is_file()
                                          for axis in ["spec", "standards"])
    # The stronger congestion assertion is test-only; reuse v4's independently reviewed production.
    v4 = json.loads((HERE / "candidate-v4.json").read_text())
    checks["production_unchanged_since_v4_review"] = all(
        value == v4["files_sha256"][path]
        for path, value in candidate["files_sha256"].items() if path.startswith("src/"))
    manifest = HERE / "final-artifacts-v6.json"
    if manifest.exists():
        sealed = json.loads(manifest.read_text())
        checks["sealed_artifacts_unchanged"] = all(digest(HERE / path) == value for path, value in sealed.items())
    result = {
        "level": "DETERMINISTIC+SCENARIO", "candidate": "candidate-v6.json",
        "method": "Pinned pure input archive/overlay and public Driver/Host TRX; independent review records",
        "expected": "Kernel25/Host12 pass; original behavioral failures reproduced; immutable input binding matches",
        "actual": checks, "passed": all(checks.values()), "mismatches": mismatches,
        "kernel": kernel, "host": host,
        "limitations": "Slice1 only. No parent completion, device/model, auto-resume, power-loss or full pure-suite claim. Mixed workspace business changes are outside ownership.",
    }
    if args.write_result:
        with (HERE / "verification-v6.json").open("x") as output:
            json.dump(result, output, ensure_ascii=False, indent=2)
            output.write("\n")
    print(json.dumps({"passed": result["passed"], "checks": checks}, ensure_ascii=False))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
