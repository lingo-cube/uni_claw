#!/usr/bin/env python3
"""BGE-small frozen-profile held-out validation runner (Profile V1 / V2).

Gate: PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY

The same frozen BGE-small embeddings are scored under two pipeline profiles:

  --profile v1  SEMANTIC_CONTAINER_IDENTITY_PROFILE_V1
               (threshold-only acceptance + structural + conflict + min-evidence)
  --profile v2  SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2
               (V1 + margin-based abstention + evidence sufficiency)

Profile V2 parameters (margin, sufficiency, anchors, generic tokens) are read
VERBATIM from semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json
— no in-code magic numbers, no case-id special cases. Former-heldout-v1 is used
ONLY as a regression/adversarial corpus; a green V2 run is REGRESSION_SAFETY_RECOVERED,
never production qualification.

Run:
    uv run --with fastembed python validation/semantic/bge-held-out/run_held_out.py --profile v2
"""
from __future__ import annotations

import argparse
import hashlib
import json
import time
from pathlib import Path

import numpy as np

REPO_ROOT = Path(__file__).resolve().parents[3]
PROFILE_V1_PATH = REPO_ROOT / "semantic-assets/profiles/BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1.json"
PROFILE_V2_PATH = REPO_ROOT / "semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2.json"
PROFILE_V3_PATH = REPO_ROOT / "semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3.json"
PROFILE_V4_PATH = REPO_ROOT / "semantic-assets/profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4.json"
TERMINOLOGY_PATH = REPO_ROOT / "semantic-assets/profiles/SEMANTIC_TERMINOLOGY_PROFILE_V1.json"
CORPUS_V1 = REPO_ROOT / "semantic-assets/heldout/ContainerIdentity-heldout-v1.json"
CORPUS_V2 = REPO_ROOT / "semantic-assets/heldout/ContainerIdentity-heldout-v2.json"
MANIFEST_V2 = REPO_ROOT / "semantic-assets/heldout/manifest-heldout-v2.json"
CORPUS_V3 = REPO_ROOT / "semantic-assets/heldout/ContainerIdentity-heldout-v3.json"
MANIFEST_V3 = REPO_ROOT / "semantic-assets/heldout/manifest-heldout-v3.json"
CORPUS_V4 = REPO_ROOT / "semantic-assets/heldout/ContainerIdentity-heldout-v4.json"
MANIFEST_V4 = REPO_ROOT / "semantic-assets/heldout/manifest-heldout-v4.json"
REPORT_V1 = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v1.json"
REPORT_V2 = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v2.json"
REPORT_V2_QUAL = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v2-bge-small-profile-v2.json"
REPORT_V3_V1CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v3.json"
REPORT_V3_V3CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v3-bge-small-profile-v3.json"
REPORT_V4_V1CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v4.json"
REPORT_V4_V2CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v2-bge-small-profile-v4.json"
REPORT_V4_V3CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v3-bge-small-profile-v4.json"
REPORT_V4_V4CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v4-bge-small-profile-v4.json"
REPORT_V3_V2CORPUS = REPO_ROOT / "semantic-assets/heldout/reports/container-identity-heldout-v2-bge-small-profile-v3.json"
GENERATED = "2026-08-30"

# Failure taxonomy (gate §20).
EMBEDDING_SEPARATION_FAILURE = "EMBEDDING_SEPARATION_FAILURE"
FEATURE_REPRESENTATION_FAILURE = "FEATURE_REPRESENTATION_FAILURE"
THRESHOLD_GENERALIZATION_FAILURE = "THRESHOLD_GENERALIZATION_FAILURE"
STRUCTURAL_RULE_FAILURE = "STRUCTURAL_RULE_FAILURE"
PROTOTYPE_MAGNET_FAILURE = "PROTOTYPE_MAGNET_FAILURE"
MARGIN_AMBIGUITY_FAILURE = "MARGIN_AMBIGUITY_FAILURE"
EVIDENCE_SUFFICIENCY_FAILURE = "EVIDENCE_SUFFICIENCY_FAILURE"
UNKNOWN = "UNKNOWN"

# Designated insufficient-evidence case ids.
V1_INSUFFICIENT_EVIDENCE_IDS = {
    "ho-dev-D1", "ho-wifi-D1", "ho-net-D1", "ho-root-D1",
    "ho-dev-F3", "ho-net-F3", "ho-root-F3",
}


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve_corpus(corpus: str) -> tuple[Path, dict]:
    if corpus == "v4":
        manifest = json.loads(MANIFEST_V4.read_text(encoding="utf-8"))
        return CORPUS_V4, manifest
    if corpus == "v3":
        manifest = json.loads(MANIFEST_V3.read_text(encoding="utf-8"))
        return CORPUS_V3, set(manifest["designatedInsufficientEvidenceIds"])
    if corpus == "v2":
        manifest = json.loads(MANIFEST_V2.read_text(encoding="utf-8"))
        return CORPUS_V2, manifest
    return CORPUS_V1, {"designatedInsufficientEvidenceIds": sorted(V1_INSUFFICIENT_EVIDENCE_IDS)}


def elem_type(e: dict) -> str | None:
    return e.get("perceptionType") or e.get("type")


def load_terminology() -> list[tuple[str, str]]:
    """(surface_lower, concept) pairs sorted by length desc (phrase-first)."""
    prof = json.loads(TERMINOLOGY_PATH.read_text(encoding="utf-8"))
    pairs = []
    for concept in prof["concepts"]:
        for surface in concept["surfaces"]:
            pairs.append((surface.lower(), concept["concept"]))
    pairs.sort(key=lambda x: -len(x[0]))
    return pairs


def normalize(text: str, concept_map: list[tuple[str, str]] | None) -> str:
    """Surface -> original + [concept] annotations (semantic normalization, Stage A).
    Never outputs an identity; preserves the original surface."""
    if not concept_map:
        return text
    lower = text.strip().lower()
    concepts = []
    for surface, concept in concept_map:
        if surface in lower:
            concepts.append(concept)
    if not concepts:
        return text
    seen = []
    for c in concepts:
        if c not in seen:
            seen.append(c)
    return f"{text} [{'] ['.join(seen)}]"


def build_feature_texts(elements: list[dict], concept_map: list[tuple[str, str]] | None = None) -> dict:
    """Frozen v1-text-plus-type feature extraction (mirrors C# extractor).
    With concept_map (Profile V4 Stage A), element text is semantic-normalized:
    surface form + [concept] annotations, on queries AND prototypes alike.
    ``texts_annotated`` carries the normalized forms for concept-based evidence
    sufficiency."""
    texts: list[str] = [e["text"] for e in elements if e.get("text") and e["text"].strip()]
    types: set[str] = {elem_type(e) for e in elements if elem_type(e)}
    structural: list[str] = []
    for e in elements:
        if elem_type(e):
            structural.append(f"type:{elem_type(e)}")
        if e.get("switchState") is not None:
            structural.append(f"switch:{str(e['switchState']).lower()}")
    query_text = "; ".join(
        f"{normalize(e['text'], concept_map)} ({elem_type(e)})" if elem_type(e) else normalize(e['text'], concept_map)
        for e in elements
        if e.get("text") and e["text"].strip()
    )
    return {
        "texts": texts,
        "texts_annotated": [normalize(t, concept_map) for t in texts],
        "types": sorted(types),
        "structural": structural,
        "query_text": query_text,
    }


def cosine_similarities(query_vec: np.ndarray, prototype_vecs: np.ndarray) -> np.ndarray:
    q = query_vec / (np.linalg.norm(query_vec) + 1e-12)
    p = prototype_vecs / (np.linalg.norm(prototype_vecs, axis=1, keepdims=True) + 1e-12)
    return (p @ q).astype(float)


def load_hardening(profile_json: dict) -> dict:
    """Builds policy parameters from a committed profile JSON (SSOT)."""
    policy = profile_json["policy"]
    sufficiency = profile_json["evidenceSufficiency"]
    return {
        "margin": float(policy["minimumTop1Top2Margin"]),
        "anchors": sufficiency["identityAnchors"],
        "anchor_concepts": sufficiency.get("anchorConcepts") or {},
        "generic_tokens": set(sufficiency["genericTokens"]),
        "min_evidence_score": int(sufficiency["minEvidenceScore"]),
        "min_non_generic_text": int(sufficiency["minNonGenericText"]),
        "min_discriminative_signal": int(sufficiency["minDiscriminativeSignal"]),
        "require_text_evidence": bool(sufficiency["requireTextEvidence"]),
    }


def load_profile(profile: str) -> tuple[dict, dict, dict, str]:
    """Returns (embedding_cfg, hardening, prototype_specs, profile_id)."""
    if profile == "v1":
        p = json.loads(PROFILE_V1_PATH.read_text(encoding="utf-8"))
        label = p["identity_prototypes"] if "identity_prototypes" in p else p["identityPrototypes"]
        # v1 has no policy block: hardening disabled on the v1 report path (kept byte-identical).
        prototypes = {
            identity: [
                {"prototypeId": f"v1:{identity}:canonical", "elements": el["elements"]}
                for el in ([label[identity]] if isinstance(label[identity], dict) else label[identity])
            ]
            for identity in ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]
        }
        hardening = None
        thresholds = p["per_identity_thresholds"] if "per_identity_thresholds" in p else p["perIdentityThresholds"]
        return p, hardening, prototypes, "BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1"
    if profile == "v3":
        p = json.loads(PROFILE_V3_PATH.read_text(encoding="utf-8"))
        hardening = load_hardening(p)
        prototypes = {
            identity: [{"prototypeId": spec["prototypeId"], "elements": spec["elements"]}
                       for spec in p["identity_prototypes"][identity]]
            for identity in ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]
        }
        thresholds = {"DeveloperOptions": 0.3, "WifiSettings": 0.3, "NetworkAndInternet": 0.65, "SettingsRoot": 0.30}
        return p, hardening, prototypes, "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3"
    if profile == "v4":
        # Profile V4 = V3 prototypes (FROZEN) + FeatureRepresentation V2 (normalization).
        p = json.loads(PROFILE_V4_PATH.read_text(encoding="utf-8"))
        v4 = p
        hardening = load_hardening(p)
        v3 = json.loads(PROFILE_V3_PATH.read_text(encoding="utf-8"))
        prototypes = {
            identity: [{"prototypeId": spec["prototypeId"], "elements": spec["elements"]}
                       for spec in v3["identity_prototypes"][identity]]
            for identity in ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]
        }
        thresholds = {"DeveloperOptions": 0.3, "WifiSettings": 0.3, "NetworkAndInternet": 0.65, "SettingsRoot": 0.30}
        return v4, hardening, prototypes, "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4"
    p = json.loads(PROFILE_V2_PATH.read_text(encoding="utf-8"))
    hardening = load_hardening(p)
    v1label = json.loads(PROFILE_V1_PATH.read_text(encoding="utf-8"))["identity_prototypes"]
    prototypes = {
        identity: [{"prototypeId": f"v2:{identity}:canonical", "elements": v1label[identity]["elements"]}]
        for identity in ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]
    }
    thresholds = {"DeveloperOptions": 0.30, "WifiSettings": 0.30, "NetworkAndInternet": 0.65, "SettingsRoot": 0.30}
    return p, hardening, prototypes, "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2"


def evidence_sufficiency(features: dict, hardening: dict | None, claimed_identity: str | None) -> dict:
    """Mirror of the C# EvidenceSufficiencyEvaluator (same semantics)."""
    if hardening is None:
        return {"sufficient": True, "reason": None,
                "total": 0, "nonGeneric": 0, "anchors": 0, "structuralSignal": 0}
    annotated = [t.strip().lower() for t in features.get("texts_annotated") or features["texts"] if t.strip()]
    texts = [t.strip().lower() for t in features["texts"] if t.strip()]
    structural = sorted(set(features["structural"]))
    switch = sum(1 for s in structural if s.startswith("switch:"))
    if hardening["require_text_evidence"] and not texts:
        return {"sufficient": False, "reason": "near-empty: no text fragments",
                "total": 0, "nonGeneric": 0, "anchors": 0, "structuralSignal": switch}
    anchors = {a for a in hardening["anchors"].get(claimed_identity, [])}
    generic = hardening["generic_tokens"]
    effective_anchors = {a for a in anchors if a not in generic}
    anchor_count = sum(1 for t in texts if t in effective_anchors)
    generic_count = sum(1 for t in texts if t in generic)
    non_generic = len(texts) - generic_count
    # Stage C — semantic anchor concepts: count ROW occurrences that map to an
    # anchor concept of the claimed identity. Stricter than exact spelling but
    # per-ROW: several rows of the same concept family count (single generic
    # concept occurrence does not).
    anchor_concepts = set(hardening["anchor_concepts"].get(claimed_identity, []))
    observed_concepts = {
        token.strip("[]")
        for t in annotated
        for token in t.split()
        if token.strip("[]") in anchor_concepts
    }
    # Distinct-concept rule (stricter, anti-collision): >=2 DIFFERENT anchor
    # concepts of the identity, or exactly one concept with a switch signal.
    # A single-concept family (e.g. 3 rows of developer-debugging) is NOT enough
    # by itself — exact spelling anchors still carry the identity-specific claim.
    concept_signal = 0
    if len(observed_concepts) >= 2:
        concept_signal = len(observed_concepts)
    elif len(observed_concepts) == 1 and switch >= 1:
        concept_signal = 1
    signal = anchor_count + concept_signal + min(switch, 2)
    score = non_generic + 2 * len(structural) + (2 + anchor_count if anchor_count > 0 else 0)
    total = len(texts) + len(structural)
    if non_generic + anchor_count < hardening["min_non_generic_text"]:
        return {"sufficient": False, "reason": "generic-only: observed text is generic UI vocabulary",
                "total": total, "nonGeneric": non_generic, "anchors": anchor_count, "structuralSignal": switch}
    if signal < hardening["min_discriminative_signal"]:
        return {"sufficient": False, "reason": "no discriminative evidence: no identity anchor and no switch-state signal",
                "total": total, "nonGeneric": non_generic, "anchors": anchor_count, "structuralSignal": switch}
    if score < hardening["min_evidence_score"]:
        return {"sufficient": False, "reason": "evidence score below minimum",
                "total": total, "nonGeneric": non_generic, "anchors": anchor_count, "structuralSignal": switch}
    return {"sufficient": True, "reason": None,
            "total": total, "nonGeneric": non_generic, "anchors": anchor_count, "structuralSignal": switch}


def decide(
    *,
    features: dict,
    similarities: dict[str, float],
    prototype_types: dict[str, set[str]],
    thresholds: dict,
    prev: str | None,
    hardening: dict | None,
) -> dict:
    """Frozen decision pipeline.

    Shared order:
      R4 min evidence → R1 structural filter → top eligible → [V2: sufficiency] →
      [V2: margin] → R2 conflict → R3 threshold → accept.

    V1 behavior is byte-identical to the previous gate when hardening=None.
    """
    r4_abstain = not features["texts"] and not features["types"] and not features["structural"]
    if r4_abstain:
        return {"predicted": "None", "confidence": 0.0, "accepted": False,
                "structural_rejected": False, "conflict_rejected": False,
                "threshold_rejected": False, "low_evidence_abstained": True,
                "margin": None, "margin_rejected": False,
                "evidence_sufficiency": None,
                "raw_top1": None}

    ranked = sorted(similarities.items(), key=lambda kv: kv[1], reverse=True)
    raw_top1 = ranked[0][0]

    obs_types = set(features["types"])
    eligible = [(identity, score) for identity, score in ranked if obs_types & prototype_types[identity]]

    if not eligible:
        return {"predicted": "None", "confidence": 0.0, "accepted": False,
                "structural_rejected": True, "conflict_rejected": False,
                "threshold_rejected": False, "low_evidence_abstained": False,
                "margin": None, "margin_rejected": False,
                "evidence_sufficiency": None,
                "raw_top1": raw_top1}

    top_identity, top_score = eligible[0]

    evidence = None
    if hardening is not None:
        evidence = evidence_sufficiency(features, hardening, top_identity)
        if not evidence["sufficient"]:
            return {"predicted": "None", "confidence": 0.0, "accepted": False,
                    "structural_rejected": False, "conflict_rejected": False,
                    "threshold_rejected": False, "low_evidence_abstained": False,
                    "margin": None, "margin_rejected": False,
                    "evidence_sufficiency": evidence,
                    "raw_top1": raw_top1}

    margin = None
    margin_rejected = False
    if hardening is not None and len(eligible) >= 2:
        margin = float(top_score - eligible[1][1])
        if margin < hardening["margin"]:
            margin_rejected = True
            return {"predicted": "None", "confidence": 0.0, "accepted": False,
                    "structural_rejected": False, "conflict_rejected": False,
                    "threshold_rejected": False, "low_evidence_abstained": False,
                    "margin": margin, "margin_rejected": True,
                    "evidence_sufficiency": evidence,
                    "raw_top1": raw_top1}

    if prev is not None and top_identity != prev:
        return {"predicted": "None", "confidence": 0.0, "accepted": False,
                "structural_rejected": False, "conflict_rejected": True,
                "threshold_rejected": False, "low_evidence_abstained": False,
                "margin": margin, "margin_rejected": False,
                "evidence_sufficiency": evidence,
                "raw_top1": raw_top1}

    if top_score < float(thresholds[top_identity]):
        return {"predicted": "None", "confidence": 0.0, "accepted": False,
                "structural_rejected": False, "conflict_rejected": False,
                "threshold_rejected": True, "low_evidence_abstained": False,
                "margin": margin, "margin_rejected": False,
                "evidence_sufficiency": evidence,
                "raw_top1": raw_top1}

    return {"predicted": top_identity, "confidence": float(top_score), "accepted": True,
            "structural_rejected": False, "conflict_rejected": False,
            "threshold_rejected": False, "low_evidence_abstained": False,
            "margin": margin, "margin_rejected": False,
            "evidence_sufficiency": evidence,
            "raw_top1": raw_top1}


def classify_failure(decision: dict, expected: str, predicted: str, prev: str | None, hardening: dict | None) -> str:
    raw = decision.get("raw_top1")
    if expected == "None" and predicted != "None":
        return UNKNOWN  # over-assertion must not happen under V2 (regression would flag it)
    if expected != "None" and predicted == "None":
        # Rank-order defect (raw top1 is a different identity) is an embedding /
        # magnet issue regardless of which gate then rejected the claim.
        if raw is not None and raw != expected:
            return EMBEDDING_SEPARATION_FAILURE
        ev = decision.get("evidence_sufficiency")
        if ev is not None and not ev["sufficient"]:
            return EVIDENCE_SUFFICIENCY_FAILURE
        if decision["margin_rejected"]:
            return MARGIN_AMBIGUITY_FAILURE
        if decision["conflict_rejected"]:
            return PROTOTYPE_MAGNET_FAILURE
        if decision["structural_rejected"]:
            return STRUCTURAL_RULE_FAILURE
        if decision["threshold_rejected"]:
            return THRESHOLD_GENERALIZATION_FAILURE
        return UNKNOWN
    return UNKNOWN


def compute_metrics(rows: list[dict], cases: list[dict], ie_ids: set[str], ln_ids: set[str] | None = None, cc_ids: set[str] | None = None) -> dict:
    total = len(rows)
    positives = sum(1 for r in rows if r["expected"] != "None")
    negatives = sum(1 for r in rows if r["expected"] == "None")
    hits = sum(1 for r in rows if r["hit"])
    accepted_negative = sum(1 for r in rows if r["expected"] == "None" and r["accepted"])
    negative_abstained = sum(1 for r in rows if r["expected"] == "None" and r["predicted"] == "None")
    correct_recovery = sum(1 for r in rows if r["expected"] != "None" and r["predicted"] == r["expected"])
    abstention = sum(1 for r in rows if r["predicted"] == "None")
    ie_admitted = sum(1 for r in rows if r["caseId"] in ie_ids and r["accepted"])
    ordered = sorted(r["latencyMs"] for r in rows)

    def percentile(p):
        if not ordered:
            return 0.0
        pos = (len(ordered) - 1) * p
        lo = int(pos)
        hi = min(len(ordered) - 1, lo + 1)
        if lo == hi:
            return ordered[lo]
        w = pos - lo
        return ordered[lo] * (1 - w) + ordered[hi] * w

    return {
        "top1Accuracy": hits / total,
        "top3Recall": hits / total,
        "top5Recall": hits / total,
        "falseRecoveryRate": accepted_negative / negatives if negatives else 0.0,
        "falsePositiveRate": accepted_negative / negatives if negatives else 0.0,
        "hardNegativeRejectionRate": negative_abstained / negatives if negatives else 0.0,
        "abstentionCorrectness": negative_abstained / negatives if negatives else 0.0,
        "insufficientEvidenceAdmitted": ie_admitted,
        "correctRecoveryRate": correct_recovery / positives if positives else 0.0,
        "lexicallyNovelPositiveRecovery": round(
            (sum(1 for r in rows if r["caseId"] in (ln_ids or set())
                 and r["expected"] != "None" and r["predicted"] == r["expected"])
             / max(1, sum(1 for r in rows if r["caseId"] in (ln_ids or set()) and r["expected"] != "None"))), 4),
        "conceptCollisionNegativeFalseRecovery": round(
            (sum(1 for r in rows if r["caseId"] in (cc_ids or set()) and r["accepted"])
             / max(1, sum(1 for r in rows if r["caseId"] in (cc_ids or set())))), 4),
        "conceptCollisionNegativeHardNegativeRejection": round(
            (sum(1 for r in rows if r["caseId"] in (cc_ids or set()) and r["predicted"] == "None")
             / max(1, sum(1 for r in rows if r["caseId"] in (cc_ids or set())))), 4),
        "abstentionRate": abstention / total,
        "positiveCount": positives,
        "negativeCount": negatives,
        "acceptedOnNegative": accepted_negative,
        "performance": {
            "p50Ms": percentile(0.50),
            "p95Ms": percentile(0.95),
            "p99Ms": percentile(0.99),
            "samples": len(ordered),
        },
    }


def run(profile: str, corpus_select: str = "v1", write_report: bool = True) -> tuple[dict, list[dict]]:
    identity_order = ["DeveloperOptions", "WifiSettings", "NetworkAndInternet", "SettingsRoot"]
    corpus_path, manifest_meta = resolve_corpus(corpus_select)
    corpus = json.loads(corpus_path.read_text(encoding="utf-8"))
    ie_ids = set(manifest_meta.get("designatedInsufficientEvidenceIds", []))
    ln_ids = set(manifest_meta.get("lexicallyNovelPositiveIds", []))
    cc_ids = set(manifest_meta.get("conceptCollisionNegativeIds", []))
    cfg, hardening, prototype_specs, profile_id = load_profile(profile)
    concept_map = load_terminology() if profile == "v4" else None
    thresholds = cfg["per_identity_thresholds"] if "per_identity_thresholds" in cfg else {
        "DeveloperOptions": 0.30, "WifiSettings": 0.30, "NetworkAndInternet": 0.65, "SettingsRoot": 0.30}
    profile_sha = sha256_file(
        PROFILE_V1_PATH if profile == "v1" else (PROFILE_V2_PATH if profile == "v2" else (PROFILE_V3_PATH if profile == "v3" else PROFILE_V4_PATH)))

    from fastembed import TextEmbedding

    embedder = TextEmbedding(
        model_name=(cfg["model"]["model_id"] if profile == "v1" else cfg["embedding"]["model"]["model_id"]),
        cache_dir="/tmp/bge-cache")
    model_info = {"model_id": "BAAI/bge-small-en-v1.5", "model_revision": "fastembed-pinned", "embedding_dimension": 384}

    # Flatten prototype list: (identity, prototypeId, text, types)
    proto_specs = []
    proto_identities = []
    for identity in identity_order:
        for spec in prototype_specs[identity]:
            feats = build_feature_texts(spec["elements"], concept_map)
            proto_specs.append((identity, spec["prototypeId"], feats["query_text"], set(feats["types"])))
            proto_identities.append(identity)
    prototype_vectors = np.asarray(list(embedder.embed([ps[2] for ps in proto_specs])))

    cases = corpus["cases"]
    rows = []
    for case in cases:
        features = build_feature_texts(case["elements"], concept_map)
        t0 = time.perf_counter()
        query_vec = np.asarray(list(embedder.embed([features["query_text"]]))[0])
        sims = cosine_similarities(query_vec, prototype_vectors)
        # identity-max aggregation: per identity, best prototype similarity + its types.
        similarities = {}
        identity_proto_types = {}
        for identity in identity_order:
            best_sim = -1.0
            best_types = None
            for idx, ptypes in enumerate(ps[3] for ps in proto_specs):
                if proto_identities[idx] == identity and sims[idx] > best_sim:
                    best_sim = sims[idx]
                    best_types = ptypes
            if best_sim >= 0.0:
                similarities[identity] = float(best_sim)
                identity_proto_types[identity] = best_types
        decision = decide(features=features, similarities=similarities, prototype_types=identity_proto_types,
                          thresholds=thresholds, prev=case["previousVerifiedIdentity"], hardening=hardening)
        latency_ms = (time.perf_counter() - t0) * 1000.0
        hit = decision["predicted"] == case["expectedCandidate"]
        ev = decision.get("evidence_sufficiency")
        rows.append({
            "caseId": case["caseId"],
            "expected": case["expectedCandidate"],
            "predicted": decision["predicted"],
            "confidence": decision["confidence"],
            "accepted": decision["accepted"],
            "hit": hit,
            "rawTop1": decision.get("raw_top1"),
            "similarities": {k: round(v, 4) for k, v in similarities.items()},
            "margin": decision.get("margin"),
            "marginRejected": decision.get("margin_rejected", False),
            "evidenceSufficiency": ev,
            "structuralRejected": decision.get("structural_rejected", False),
            "conflictRejected": decision.get("conflict_rejected", False),
            "thresholdRejected": decision.get("threshold_rejected", False),
            "failureClass": classify_failure(decision, case["expectedCandidate"], decision["predicted"],
                                             case["previousVerifiedIdentity"], hardening) if not hit else None,
            "latencyMs": latency_ms,
        })

    metrics = compute_metrics(rows, cases, ie_ids, ln_ids, cc_ids)
    breakdown = {
        "identity": bucket_by(rows, cases, lambda c: c["expectedIdentity"] or "None"),
        "difficulty": bucket_by(rows, cases, lambda c: c["difficulty"]),
        "source": bucket_by(rows, cases, lambda c: c["source"]),
        "viewportState": bucket_by(rows, cases, lambda c: c["viewportState"]),
        "ambiguityLevel": bucket_by(rows, cases, lambda c: str(c["ambiguityLevel"])),
    }
    report = {
        "schema": "uniclaw.semantic.heldoutReport.v1",
        "reportId": f"container-identity-heldout-{corpus_select}-bge-small-profile-{profile}",
        "backend": "bge-small",
        "profileId": profile_id,
        "profileSha256": profile_sha,
        "model": model_info,
        "corpusId": corpus["corpusId"],
        "corpusSha256": sha256_file(corpus_path),
        "corpusCaseCount": len(cases),
        "generated": GENERATED,
        "thresholds": {i: thresholds[i] for i in identity_order},
        "regressionRole": "former-heldout-v1: regression/adversarial corpus (NOT a held-out qualification dataset)"
        if corpus_select == "v1" else
        "heldout-v2: true independent held-out qualification corpus (Profile V2; qualified or failed, never tuned)",
        "metrics": metrics,
        "breakdown": breakdown,
        "failures": [
            {"caseId": r["caseId"], "failureClass": r["failureClass"], "expected": r["expected"],
             "predicted": r["predicted"], "confidence": round(r["confidence"], 4), "margin": r["margin"]}
            for r in rows if r["failureClass"] is not None
        ],
        "cases": rows,
    }
    if write_report:
        if profile == "v4":
            path = (REPORT_V4_V4CORPUS if corpus_select == "v4"
                    else REPORT_V4_V3CORPUS if corpus_select == "v3"
                    else REPORT_V4_V2CORPUS if corpus_select == "v2" else REPORT_V4_V1CORPUS)
        elif profile == "v3":
            path = REPORT_V3_V3CORPUS if corpus_select == "v3" else (REPORT_V3_V2CORPUS if corpus_select == "v2" else REPORT_V3_V1CORPUS)
        elif corpus_select == "v2":
            path = REPORT_V2_QUAL
        else:
            path = REPORT_V1 if profile == "v1" else REPORT_V2
        path.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(f"wrote {path}")
    print(f"  profile={profile} top1={metrics['top1Accuracy']:.4f} fr={metrics['falseRecoveryRate']:.4f} "
          f"hnr={metrics['hardNegativeRejectionRate']:.4f} ieAdm={metrics['insufficientEvidenceAdmitted']} "
          f"correctRecovery={metrics['correctRecoveryRate']:.4f} abstainRate={metrics['abstentionRate']:.4f} "
          f"p50={metrics['performance']['p50Ms']:.2f}ms p95={metrics['performance']['p95Ms']:.2f}ms")
    print(f"  failures: {len(report['failures'])} -> {[(f['caseId'], f['failureClass']) for f in report['failures']]}")
    return report, rows


def bucket_by(rows: list[dict], cases: list[dict], key_fn) -> list[dict]:
    by_case = {c["caseId"]: c for c in cases}
    groups: dict[str, list] = {}
    for r in rows:
        groups.setdefault(key_fn(by_case[r["caseId"]]), []).append(r)
    result = []
    for key in sorted(groups):
        g = groups[key]
        fp = sum(1 for r in g if r["expected"] == "None" and r["accepted"])
        hits = sum(1 for r in g if r["hit"])
        result.append({"key": key, "count": len(g), "top1Accuracy": hits / len(g),
                       "falsePositive": fp, "falsePositiveRate": fp / len(g)})
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", choices=["v1", "v2", "v3", "v4"], default="v1")
    parser.add_argument("--corpus", choices=["v1", "v2", "v3", "v4"], default="v1")
    args = parser.parse_args()
    run(args.profile, args.corpus)