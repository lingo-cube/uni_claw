#!/usr/bin/env python3
"""Finite reference models only. No UniClaw product/runtime/device execution."""
from collections import deque
from itertools import product
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent


def reachable_oracle(adj, root, depth):
    # Independent shortest-distance oracle, not the traversal implementation.
    distances = {root: 0}
    queue = deque([root])
    while queue:
        node = queue.popleft()
        for child in adj[node]:
            if child not in distances:
                distances[child] = distances[node] + 1
                queue.append(child)
    return {node for node, distance in distances.items() if distance <= depth}


def lifted_traversal(adj, root, depth):
    pending = [(root, depth)]
    seen_states, covered = set(), set()
    while pending:
        node, remaining = pending.pop()
        if (node, remaining) in seen_states:
            continue
        seen_states.add((node, remaining))
        covered.add(node)
        if remaining:
            pending.extend((child, remaining - 1) for child in reversed(adj[node]))
    return covered


def naive_global_visited(adj, root, depth):
    pending, seen = [(root, depth)], set()
    while pending:
        node, remaining = pending.pop()
        if node in seen:
            continue
        seen.add(node)
        if remaining:
            pending.extend((child, remaining - 1) for child in reversed(adj[node]))
    return seen


def select(values):
    for index, value in enumerate(values):
        if value == "U":
            return "pending"
        if value == "T":
            return index
    return "no-match"


def completion_outcomes(values):
    positions = [i for i, value in enumerate(values) if value == "U"]
    outcomes = set()
    for bits in product(["F", "T"], repeat=len(positions)):
        completed = list(values)
        for index, bit in zip(positions, bits):
            completed[index] = bit
        outcomes.add(next((i for i, value in enumerate(completed) if value == "T"), "no-match"))
    return outcomes


def main():
    graph_cases, graphs = 0, 0
    for size in range(1, 4):
        for mask in range(1 << (size * size)):
            graphs += 1
            adj = [[j for j in range(size) if mask & (1 << (i * size + j))]
                   for i in range(size)]
            for root in range(size):
                for depth in range(size + 1):
                    graph_cases += 1
                    assert lifted_traversal(adj, root, depth) == reachable_oracle(adj, root, depth), (adj, root, depth)

    counter_adj = [[1, 2], [2], [3], []]
    expected = reachable_oracle(counter_adj, 0, 2)
    naive = naive_global_visited(counter_adj, 0, 2)
    corrected = lifted_traversal(counter_adj, 0, 2)
    assert expected == corrected == {0, 1, 2, 3}
    assert naive == {0, 1, 2}

    selection_cases, decided, pending = 0, 0, 0
    for values in product(["T", "F", "U"], repeat=3):
        selection_cases += 1
        result = select(values)
        outcomes = completion_outcomes(values)
        if result == "pending":
            pending += 1
            assert len(outcomes) > 1
        else:
            decided += 1
            assert outcomes == {result}, (values, result, outcomes)

    unknown_witness = ["U", "T", "F"]
    naive_choice = next(i for i, value in enumerate(unknown_witness) if value == "T")
    assert naive_choice == 1 and completion_outcomes(unknown_witness) == {0, 1}
    result = dict(
        kind="BOUNDED_REFERENCE_MODELS_ONLY", result="PASS_REFERENCE_CHECKS_ONLY",
        graph_count=graphs, graph_root_depth_cases=graph_cases,
        graph_limits="1..3 vertices; every directed graph including loops; all roots; depth 0..size",
        naive_visited_counterexample=dict(adjacency=counter_adj, root=0, max_depth=2,
            oracle=sorted(expected), naive=sorted(naive), lifted=sorted(corrected), missed=[3]),
        three_valued_rule_cases=selection_cases, decided_cases=decided, pending_cases=pending,
        unknown_as_false_counterexample=dict(values=unknown_witness, naive_choice=naive_choice,
            possible_correct_choices=[0, 1], required="pending"),
        product_executions=0, device_runs=0, proves_real_world_completeness=False,
        limitations=["no real asynchronous kernel", "no identity/perception proof",
                     "no handler recovery implementation", "no end-to-end correctness proof"])
    (HERE / "model-results.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
