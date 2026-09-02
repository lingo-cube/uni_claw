#!/usr/bin/env python3
"""Embedding separation / margin / magnet analysis for the frozen BGE-small
pipeline over ContainerIdentity-heldout-v1.

READ-ONLY analysis of the committed BGE report (which records the full
per-identity cosine vector per case). No tuning, no profile change; it only
produces evidence for the Semantic Perception Pipeline boundary & safety review.

Run: python validation/semantic/bge-held-out/analyze_separation.py
"""
from __future__ import annotations

import json
from collections import Counter
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
REPORT_PATH = (
    REPO_ROOT
    / "semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v1.json"
)
CORPUS_PATH = REPO_ROOT / "semantic-assets/heldout/ContainerIdentity-heldout-v1.json"
OUT_PATH = REPO_ROOT / "semantic-assets/heldout/reports/similarity-separation-analysis.json"

IDENTITIES = ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]


def percentile(sorted_vals: list[float], p: float) -> float:
    if not sorted_vals:
        return 0.0
    pos = (len(sorted_vals) - 1) * p
    lo = int(pos)
    hi = min(len(sorted_vals) - 1, lo + 1)
    if lo == hi:
        return sorted_vals[lo]
    w = pos - lo
    return sorted_vals[lo] * (1 - w) + sorted_vals[hi] * w


def stats(values: list[float]) -> dict:
    if not values:
        return {"n": 0, "min": None, "median": None, "p95": None, "max": None}
    s = sorted(values)
    return {
        "n": len(s),
        "min": round(s[0], 4),
        "median": round(percentile(s, 0.50), 4),
        "p95": round(percentile(s, 0.95), 4),
        "max": round(s[-1], 4),
    }


def main() -> None:
    report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
    corpus = json.loads(CORPUS_PATH.read_text(encoding="utf-8"))
    by_case = {c["caseId"]: c for c in corpus["cases"]}
    rows = report["cases"]
    assert len(rows) == 48
    for row in rows:
        if row["similarities"]:
            assert set(row["similarities"].keys()) == set(IDENTITIES), row["caseId"]
    # R4-abstained cases (e.g. ho-root-F3, zero elements) never embed and have
    # an empty similarity vector — excluded from embedding/margin statistics.
    embedded = [r for r in rows if r["similarities"]]

    # ── buckets ──────────────────────────────────────────────────────────────
    positives = [r for r in rows if r["expected"] != "None"]
    negatives = [r for r in rows if r["expected"] == "None"]
    hard_negatives = [r for r in negatives if by_case[r["caseId"]]["difficulty"] == "Hard"]
    title_visible_pos = [
        r for r in positives if by_case[r["caseId"]]["viewportState"] == "TitleVisible"
    ]
    offscreen_pos = [
        r for r in positives if by_case[r["caseId"]]["viewportState"] == "TitleOffscreen"
    ]
    partial_pos = [r for r in positives if by_case[r["caseId"]]["viewportState"] == "Partial"]
    for label, bucket in [
        ("all", rows), ("positives", positives), ("negatives", negatives),
        ("hard_negatives", hard_negatives),
        ("pos_title_visible", title_visible_pos), ("pos_title_offscreen", offscreen_pos),
        ("pos_partial", partial_pos),
    ]:
        emb = [r for r in bucket if r["similarities"]]
        max_sim = [max(r["similarities"].values()) for r in emb]
        margin = [margin_of(r) for r in emb]
        low_margin = sum(1 for r in emb if margin_of(r) < 0.05)
        embedded_note = f" (embedded {len(emb)}/{len(bucket)})" if len(emb) != len(bucket) else ""
        print(f"{label:24s} n={len(bucket):2d}{embedded_note} maxsim={stats(max_sim)} margin={stats(margin)} share<0.05={low_margin}/{len(emb)}")

    # ── raw ranking correctness (pre-policy) ─────────────────────────────────
    def raw_top1_identity(row: dict) -> str | None:
        sims = row["similarities"]
        if not sims:
            return None
        return max(sims, key=sims.get)

    raw_correct_pos = sum(1 for r in positives if raw_top1_identity(r) == r["expected"])
    print(f"raw-top1 correct positives all: {raw_correct_pos}/{len(positives)}")
    for label, bucket in [
        ("title_visible", title_visible_pos), ("title_offscreen", offscreen_pos),
        ("partial", partial_pos),
    ]:
        ok = sum(1 for r in bucket if raw_top1_identity(r) == r["expected"])
        print(f"raw-top1 correct positives {label:14s}: {ok}/{len(bucket)}")

    # ── magnet / confusion matrix: negative cases' raw top1 ──────────────────
    neg_top1 = Counter(raw_top1_identity(r) for r in negatives if r["similarities"])
    print("negative raw-top1 attraction:", dict(neg_top1))

    # expected -> raw_top1 confusion for all cases (excluding correct)
    confusion: dict[str, Counter] = {}
    for r in rows:
        t1 = raw_top1_identity(r)
        if t1 is None:
            continue
        key = r["expected"] if r["expected"] != "None" else "ABSTAIN"
        if t1 != key:
            confusion.setdefault(key, Counter())[t1] += 1
    print("confusion (expected -> raw top1):", {k: dict(v) for k, v in sorted(confusion.items())})

    def emb(bucket: list[dict]) -> list[dict]:
        return [r for r in bucket if r["similarities"]]

    # ── separation verdict inputs ────────────────────────────────────────────
    verdict = {
        "embeddings_recorded": True,
        "buckets": {
            "positives": {
                "max_similarity": stats([max(r["similarities"].values()) for r in emb(positives)]),
                "margin": stats([margin_of(r) for r in emb(positives)]),
            },
            "negatives": {
                "max_similarity": stats([max(r["similarities"].values()) for r in emb(negatives)]),
                "margin": stats([margin_of(r) for r in emb(negatives)]),
            },
            "hard_negatives": {
                "max_similarity": stats([max(r["similarities"].values()) for r in emb(hard_negatives)]),
                "margin": stats([margin_of(r) for r in emb(hard_negatives)]),
            },
            "pos_title_visible": {
                "max_similarity": stats([max(r["similarities"].values()) for r in emb(title_visible_pos)]),
                "margin": stats([margin_of(r) for r in emb(title_visible_pos)]),
            },
            "pos_title_offscreen": {
                "max_similarity": stats([max(r["similarities"].values()) for r in emb(offscreen_pos)]),
                "margin": stats([margin_of(r) for r in emb(offscreen_pos)]),
            },
            "pos_partial": {
                "max_similarity": stats([max(r["similarities"].values()) for r in partial_pos]),
                "margin": stats([margin_of(r) for r in partial_pos]),
            },
        },
        "raw_top1_correct_positives": {
            "all": sum(1 for r in positives if raw_top1_identity(r) == r["expected"]),
            "positives_total": len(positives),
            "title_visible": sum(1 for r in title_visible_pos if raw_top1_identity(r) == r["expected"]),
            "title_offscreen": sum(1 for r in offscreen_pos if raw_top1_identity(r) == r["expected"]),
            "partial": sum(1 for r in partial_pos if raw_top1_identity(r) == r["expected"]),
        },
        "negative_raw_top1_attraction": dict(neg_top1),
        "confusion": {k: dict(v) for k, v in sorted(confusion.items())},
        "margin_threshold_share": {
            "positives_margin_lt_0_05": sum(1 for r in positives if margin_of(r) < 0.05),
            "negatives_margin_lt_0_05": sum(1 for r in negatives if margin_of(r) < 0.05),
            "hard_negatives_margin_lt_0_05": sum(1 for r in hard_negatives if margin_of(r) < 0.05),
            "positives_margin_lt_0_10": sum(1 for r in positives if margin_of(r) < 0.10),
            "negatives_margin_lt_0_10": sum(1 for r in negatives if margin_of(r) < 0.10),
        },
    }
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(json.dumps(verdict, indent=2), encoding="utf-8")
    print(f"wrote {OUT_PATH}")


def margin_of(row: dict) -> float:
    sims = row["similarities"]
    ordered = sorted(sims.values(), reverse=True)
    if len(ordered) < 2:
        return 0.0
    return ordered[0] - ordered[1]


if __name__ == "__main__":
    main()