#!/usr/bin/env python3
"""Safety-first margin scan for Profile V2 (SEMANTIC_SAFETY_HARDENING_APPLY).

Scans MinimumTop1Top2Margin over the tuning + former-heldout-v1 (now a
regression/adversarial corpus). Selection principle (gate §7): minimize False
Recovery first; among margins with equal safety, prefer higher recall.

The scan is a pure function of committed artifacts (the v1 BGE report already
records the frozen per-case cosine similarity vectors), so it does not
re-embed. It writes reports/margin-scan-profile-v2.json and prints the table.

Run: python validation/semantic/bge-held-out/scan_margin.py
"""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np

from run_held_out import (  # noqa: F401
    decide,
    evidence_sufficiency,
    load_hardening,
    INSUFFICIENT_EVIDENCE_IDS,
    CORPUS_PATH,
    PROFILE_V1_PATH,
    PROFILE_V2_PATH,
    REPORT_V1,
)

import run_held_out as rho

REPO_ROOT = Path(__file__).resolve().parents[3]
SCAN_OUT = REPO_ROOT / "semantic-assets/heldout/reports/margin-scan-profile-v2.json"
GENERATED = "2026-08-30"


def main() -> None:
    v1_report = json.loads(REPORT_V1.read_text(encoding="utf-8"))
    profile_v1 = json.loads(PROFILE_V1_PATH.read_text(encoding="utf-8"))
    profile_v2 = json.loads(PROFILE_V2_PATH.read_text(encoding="utf-8"))
    corpus = json.loads(CORPUS_PATH.read_text(encoding="utf-8"))

    thresholds = profile_v1["per_identity_thresholds"] if "per_identity_thresholds" in profile_v1 else profile_v1["perIdentityThresholds"]
    label = profile_v1["identity_prototypes"] if "identity_prototypes" in profile_v1 else profile_v1["identityPrototypes"]
    identity_order = ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]
    prototype_types = {
        identity: set(rho.build_feature_texts(label[identity]["elements"])["types"])
        for identity in identity_order
    }

    hardening = load_hardening(profile_v2)
    baseline_margin = hardening["margin"]

    by_case = {c["caseId"]: c for c in corpus["cases"]}
    rows = v1_report["cases"]
    assert len(rows) == len(by_case) == 48

    print(f"{'margin':>7} {'FR':>6} {'FPR':>6} {'HNR':>6} {'IEadm':>5} {'CorrRec':>7} {'Top1':>6} {'Abs%':>6} {'accept':>6}")
    table = []
    margins = [0.0] + [round(m, 3) for m in np.arange(0.040, 0.106, 0.005)]
    for margin in sorted(set(margins)):
        hardening["margin"] = margin
        accepted_negative = 0
        negative_abstained = 0
        correct_recovery = 0
        ie_admitted = 0
        hits = 0
        negatives = 0
        positives = 0
        abstain = 0
        total = len(rows)
        for row in rows:
            case = by_case[row["caseId"]]
            if case["expectedCandidate"] == "None":
                negatives += 1
            else:
                positives += 1
            features = rho.build_feature_texts(case["elements"])
            decision = rho.decide(
                features=features,
                similarities=row["similarities"],
                prototype_types=prototype_types,
                thresholds=thresholds,
                prev=case["previousVerifiedIdentity"],
                hardening=hardening,
            )
            pred = decision["predicted"]
            if pred == case["expectedCandidate"]:
                hits += 1
            if case["expectedCandidate"] == "None":
                if decision["accepted"]:
                    accepted_negative += 1
                if pred == "None":
                    negative_abstained += 1
                if row["caseId"] in INSUFFICIENT_EVIDENCE_IDS and decision["accepted"]:
                    ie_admitted += 1
            else:
                if pred == case["expectedCandidate"]:
                    correct_recovery += 1
            if pred == "None":
                abstain += 1
        fr = accepted_negative / negatives if negatives else 0.0
        fpr = accepted_negative / negatives if negatives else 0.0
        hnr = negative_abstained / negatives if negatives else 0.0
        corr_rec = correct_recovery / positives if positives else 0.0
        top1 = hits / total
        abs_rate = abstain / total
        table.append({
            "margin": margin, "falseRecovery": fr, "falsePositive": fpr, "hardNegativeRejection": hnr,
            "insufficientEvidenceAdmitted": ie_admitted, "correctRecovery": corr_rec,
            "top1Accuracy": top1, "abstentionRate": abs_rate,
            "acceptedOnNegative": accepted_negative,
        })
        print(f"{margin:7.3f} {fr:6.3f} {fpr:6.3f} {hnr:6.3f} {ie_admitted:5d} {corr_rec:7.3f} {top1:6.3f} {abs_rate:6.3f} {accepted_negative:6d}")

    # Selection: safety-first — among margins with FR=0 AND IE-admission=0,
    # prefer the highest correct recovery; ties → lowest abstention.
    safe = [t for t in table if t["falseRecovery"] == 0.0 and t["insufficientEvidenceAdmitted"] == 0]
    selected = max(safe, key=lambda t: (t["correctRecovery"], -t["abstentionRate"])) if safe else None

    scan = {
        "schema": "uniclaw.semantic.marginScan.v1",
        "corpusRole": "former-heldout-v1: regression/adversarial corpus",
        "generated": GENERATED,
        "baselineProfileMargin": baseline_margin,
        "selectionPrinciple": "safety-first: minimize false recovery, then insufficient-evidence admission, then maximize correct recovery (never Top1)",
        "table": table,
        "selectedMargin": selected["margin"] if selected else None,
    }
    SCAN_OUT.parent.mkdir(parents=True, exist_ok=True)
    SCAN_OUT.write_text(json.dumps(scan, indent=2), encoding="utf-8")
    print(f"\nselected margin: {scan['selectedMargin']} (baseline 0.06)")
    print(f"wrote {SCAN_OUT}")


if __name__ == "__main__":
    main()