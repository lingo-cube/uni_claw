"""Bounded review calculations only. No product imports, providers, or runtime tests."""
import itertools
import json
from pathlib import Path


def closure(edges, seed):
    reached = set(seed)
    rounds = 0
    while True:
        following = reached | {b for a, b in edges if a in reached}
        if following == reached:
            return reached, rounds
        reached = following
        rounds += 1


def canonical_scope(scope):
    return json.dumps(sorted(set(scope)), ensure_ascii=False, separators=(",", ":"))


def main():
    nodes = tuple(range(3))
    possible_edges = tuple(itertools.product(nodes, repeat=2))
    graph_checks = 0
    max_rounds = 0
    for bits in itertools.product((False, True), repeat=len(possible_edges)):
        edges = tuple(edge for edge, active in zip(possible_edges, bits) if active)
        for seed_bits in itertools.product((False, True), repeat=3):
            seed = {n for n, active in zip(nodes, seed_bits) if active}
            reached, rounds = closure(edges, seed)
            assert seed <= reached
            assert all(b in reached for a, b in edges if a in reached)
            assert rounds <= len(nodes) - len(seed)
            # Minimality: every closed superset of the seed contains the result.
            for subset_bits in itertools.product((False, True), repeat=3):
                subset = {n for n, active in zip(nodes, subset_bits) if active}
                if seed <= subset and all(b in subset for a, b in edges if a in subset):
                    assert reached <= subset
            graph_checks += 1
            max_rounds = max(max_rounds, rounds)
    missing_edge_full, _ = closure(((0, 1), (1, 2)), {0})
    missing_edge_recorded, _ = closure(((0, 1),), {0})
    assert missing_edge_recorded < missing_edge_full

    scopes = [s for length in range(5) for s in itertools.product(("A", "B", "C"), repeat=length)]
    representations = {}
    for scope in scopes:
        key = canonical_scope(scope)
        meaning = frozenset(scope)
        assert key not in representations or representations[key] == meaning
        representations[key] = meaning
    for left, right in itertools.product(scopes, repeat=2):
        assert (canonical_scope(left) == canonical_scope(right)) == (set(left) == set(right))
    assert canonical_scope(("A+B",)) != canonical_scope(("A", "B"))
    legacy_id = lambda revision, root, scope: f"slice:{revision}:{root}"
    assert legacy_id("rev-1", "A", ("A",)) == legacy_id("rev-1", "A", ("A", "B"))

    # Actual attempt ids belong to the execution producer, not Evidence revisions.
    events = [("waiting", None), ("result", "a1"), ("duplicate", "a1"), ("reinterpreted", "a1"), ("result", "a2")]
    results = set()
    reminders = set()
    counts = []
    active_reminders = []
    for kind, attempt in events:
        new_result = kind == "result" and attempt not in results
        if new_result:
            results.add(attempt)
        key = ("bounded-gap", tuple(sorted(results)))
        if new_result and key not in reminders:
            reminders.add(key)
        counts.append(len(results))
        active_reminders.append(len(reminders))
    assert counts == [0, 1, 1, 1, 2]
    assert active_reminders == [0, 1, 1, 1, 2]

    # Exhaust only the Boolean applicability abstraction, not a World predicate.
    reuse_checks = 0
    for source_valid, correspondence, applicable, counterevidence, slow_done, dynamic_known in itertools.product((False, True), repeat=6):
        reference = source_valid
        current_meaning = source_valid and correspondence and applicable and not counterevidence
        current_dynamic = dynamic_known  # independent source dimension
        assert reference == source_valid
        if not source_valid or not correspondence or not applicable or counterevidence:
            assert not current_meaning
        assert current_dynamic == dynamic_known
        # Slow completion does not occur in either derivation.
        reuse_checks += 1

    # Countermodel: shared wrong role labels can pass a state check.
    actual_resource, inherited_label, observed_state, desired_state = "wifi", "bluetooth", "on", "on"
    local_label_check = inherited_label == "bluetooth" and observed_state == desired_state
    independent_goal_check = actual_resource == "bluetooth" and observed_state == desired_state
    assert local_label_check and not independent_goal_check

    result = {
        "schemaVersion": "world-finite-review.v1",
        "level": "DETERMINISTIC_ABSTRACT_ONLY",
        "productPass": False,
        "method": "python3 changes/WORLD-UNCERTAINTY-001/review/evidence/finite-derivation.py",
        "expected": "finite closure minimality, canonical scope equality, duplicate counting, and explicit counterexamples",
        "actual": "all finite assertions satisfied; counterexamples reproduced in abstract domains",
        "domains": {
            "dependency": {"nodes": 3, "directedGraphsIncludingLoops": 512, "seedSubsets": 8, "graphSeedChecks": graph_checks, "maxStrictGrowthRounds": max_rounds},
            "scope": {"symbols": ["A", "B", "C"], "sequenceLength": [0, 4], "sequences": len(scopes), "distinctSets": len(representations), "pairChecks": len(scopes)**2},
            "reuseBooleanAssignments": reuse_checks,
            "attemptEventCounts": counts,
            "reminderCounts": active_reminders,
        },
        "counterexamples": {
            "legacyScopeCollision": {"revision": "rev-1", "root": "A", "leftScope": ["A"], "rightScope": ["A", "B"], "id": "slice:rev-1:A"},
            "missingDependencyEdge": {"fullReachability": sorted(missing_edge_full), "recordedReachability": sorted(missing_edge_recorded), "omittedAffectedNode": 2},
            "sharedWrongLabel": {"actualResource": actual_resource, "inheritedLabel": inherited_label, "localCheck": local_label_check, "independentGoalCheck": independent_goal_check},
        },
        "assumptions": ["finite fixed dependency graph", "complete recorded edges required for full impact", "scope semantics are sets in this abstraction", "execution producer supplied ids are genuine", "Boolean reuse conditions are assumptions, not product evaluators"],
        "notProved": ["World implementation correctness", "producer evidence sufficiency", "temporal or spatial compatibility", "concurrent dispatch behavior", "actual Agent input or model behavior", "global termination", "external effect reversibility", "cryptographic hash injectivity"],
    }
    path = Path(__file__).with_name("finite-derivation.json")
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"abstractAssertions": "satisfied", "graphSeedChecks": graph_checks, "scopePairChecks": len(scopes)**2, "productPass": False}))


if __name__ == "__main__":
    main()
